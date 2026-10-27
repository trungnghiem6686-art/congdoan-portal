using System.Net;
using CongDoan.Application.Identity;
using CongDoan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Identity.Tests;

public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public AuthTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Candidate_Cookie_Cannot_Call_Admin_Endpoint_Returns_403()
    {
        await _f.CandidateAsync("ts-403");
        using var c = await _f.LoginCandidateAsync("ts-403");

        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/thi/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/admin/auth/me")).StatusCode);

        using var anon = _f.CreateTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/admin/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_Locks_After_5_Failures_For_15_Minutes()
    {
        await _f.AdminAsync("lock-me");
        using var c = _f.CreateTestClient();
        for (var i = 0; i < 5; i++)
        {
            var bad = await c.PostAsync("/api/admin/auth/login", new { userName = "lock-me", password = "wrong-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }

        var locked = await c.PostAsync("/api/admin/auth/login", new { userName = "lock-me", password = Seed.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal("account_locked_out", await TestClient.ErrorCodeAsync(locked));

        _f.Clock.Advance(TimeSpan.FromMinutes(14));
        var still = await c.PostAsync("/api/admin/auth/login", new { userName = "lock-me", password = Seed.Password });
        Assert.Equal("account_locked_out", await TestClient.ErrorCodeAsync(still));

        _f.Clock.Advance(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(1)));
        var ok = await c.PostAsync("/api/admin/auth/login", new { userName = "lock-me", password = Seed.Password });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Behind_Https_Proxy_Session_Cookie_Is_Secure()
    {
        // IIS terminates TLS and forwards plain HTTP to Kestrel on localhost with X-Forwarded-Proto.
        // Cookies are passed by hand: a cookie container would not send Secure cookies back over http://.
        await _f.AdminAsync("proxy-user");
        using var http = _f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        http.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        var csrf = await http.GetAsync("/api/antiforgery");
        var cookies = string.Join("; ", csrf.Headers.GetValues("Set-Cookie").Select(h => h.Split(';')[0]));
        using var doc = System.Text.Json.JsonDocument.Parse(await csrf.Content.ReadAsStringAsync());
        var login = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/login")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { userName = "proxy-user", password = Seed.Password }),
        };
        login.Headers.Add("Cookie", cookies);
        login.Headers.Add("X-XSRF-TOKEN", doc.RootElement.GetProperty("data").GetProperty("token").GetString());
        var ok = await http.SendAsync(login);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var session = ok.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith("cd_admin="));
        Assert.Contains("secure", session, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Wrong_Password_And_Unknown_User_Return_Same_Error()
    {
        await _f.AdminAsync("known-user");
        using var c = _f.CreateTestClient();
        var a = await c.PostAsync("/api/admin/auth/login", new { userName = "known-user", password = "nope-nope" });
        var b = await c.PostAsync("/api/admin/auth/login", new { userName = "ghost-user", password = "nope-nope" });
        Assert.Equal(await TestClient.ErrorCodeAsync(a), await TestClient.ErrorCodeAsync(b));
    }

    [Fact]
    public async Task FirstLogin_Forces_Password_Change()
    {
        await _f.CandidateAsync("ts-first", mustChange: true);
        using var c = _f.CreateTestClient();
        var login = await TestClient.DataAsync(await c.PostAsync("/api/thi/auth/login", new { userName = "ts-first", password = Seed.Password }));
        Assert.True(login.GetProperty("mustChangePassword").GetBoolean());
        await c.CsrfAsync(refresh: true);

        var blocked = await c.GetAsync("/api/thi/exams");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("password_change_required", await TestClient.ErrorCodeAsync(blocked));

        var change = await c.PostAsync("/api/thi/auth/change-password",
            new { currentPassword = Seed.Password, newPassword = "NewPassw0rd!" });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        var me = await TestClient.DataAsync(await c.GetAsync("/api/thi/auth/me"));
        Assert.False(me.GetProperty("mustChangePassword").GetBoolean());
        Assert.NotEqual("password_change_required", await TestClient.ErrorCodeAsync(await c.GetAsync("/api/thi/exams")));
    }

    [Fact]
    public async Task Locked_Account_Invalidates_Existing_Session()
    {
        var user = await _f.AdminAsync("to-lock");
        using var c = await _f.LoginAdminAsync("to-lock");
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/admin/auth/me")).StatusCode);

        await _f.ScopedAsync(async sp =>
        {
            var db = sp.GetRequiredService<IIdentityDbContext>();
            var u = await db.AdminUsers.SingleAsync(x => x.Id == user.Id);
            u.Lock();
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/admin/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_Revokes_Session()
    {
        await _f.AdminAsync("bye");
        using var c = await _f.LoginAdminAsync("bye");
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync("/api/admin/auth/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/admin/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Write_Without_Antiforgery_Token_Rejected()
    {
        await _f.AdminAsync("csrf-user");
        using var c = _f.CreateTestClient();
        var res = await c.SendAsync(HttpMethod.Post, "/api/admin/auth/login",
            new { userName = "csrf-user", password = Seed.Password }, csrf: false);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("csrf_invalid", await TestClient.ErrorCodeAsync(res));
    }
}

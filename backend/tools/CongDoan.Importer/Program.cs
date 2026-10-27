using CongDoan.Application.Cms.Import;
using CongDoan.Application.Shared;
using CongDoan.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Importer run --source <dir> [--dry-run] [--batch legacy] [--report report.csv]
// Settings come from appsettings.json next to the tool and environment variables
// (ConnectionStrings__Default, Media__RootPath, Frontend__RevalidateUrl...) — never from the command line.
const string Usage = "Cách dùng: Importer run --source <thư mục xuất> [--dry-run] [--batch legacy] [--report bao-cao.csv]";

if (args.Length == 0 || args[0] != "run")
{
    Console.Error.WriteLine(Usage);
    return 1;
}

string? Arg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

var source = Arg("--source");
if (source is null)
{
    Console.Error.WriteLine(Usage);
    return 1;
}
var dryRun = args.Contains("--dry-run");
var batch = Arg("--batch") ?? "legacy";
var reportPath = Arg("--report") ?? $"import-{DateTime.UtcNow:yyyyMMdd-HHmmss}{(dryRun ? "-dry" : "")}.csv";

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = Array.Empty<string>(), ContentRootPath = AppContext.BaseDirectory });
builder.Configuration.AddJsonFile("appsettings.json", optional: true).AddEnvironmentVariables();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ICurrentUser, ImportUser>();
using var host = builder.Build();

try
{
    await using var scope = host.Services.CreateAsyncScope();
    var report = await scope.ServiceProvider.GetRequiredService<LegacyImporter>().RunAsync(new ImportOptions(source, dryRun, batch));
    await File.WriteAllTextAsync(reportPath, "﻿" + report.ToCsv());
    Console.WriteLine($"{(dryRun ? "[CHẠY THỬ] " : "")}Bài nguồn: {report.SourcePosts} | {(dryRun ? "Sẽ tạo" : "Tạo mới")}: {report.Created} | Đã có: {report.Skipped} | Lỗi: {report.Failed} | Ảnh: {report.Images} | Redirect: {report.Redirects} | Cảnh báo: {report.Issues.Count}");
    Console.WriteLine($"Báo cáo đối soát: {Path.GetFullPath(reportPath)}");
    return report.Failed > 0 ? 2 : 0;
}
catch (AppException e)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}

/// <summary>Audit entries written by the importer are attributed to it, not to a person.</summary>
internal sealed class ImportUser : ICurrentUser
{
    public Guid? AdminId => null;
    public Guid? CandidateId => null;
    public string? UserName => "importer";
    public string? RequestId => "import-" + Environment.ProcessId;
    public string? IpAddress => null;
}

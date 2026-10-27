using System.Net;
using System.Text.Json;

namespace CongDoan.Testing;

public static class ExamHelpers
{
    public static object Single(string content, string topic = "Chung", int correct = 0, params string[] choices)
    {
        var list = choices.Length > 0 ? choices : new[] { "Phương án A", "Phương án B", "Phương án C", "Phương án D" };
        return new
        {
            type = "Single", topic, difficulty = "Medium", content, explanation = "Lời giải " + content, points = 1,
            choices = list.Select((c, i) => new { content = c, isCorrect = i == correct }).ToArray(),
        };
    }

    public static object Multiple(string content, string topic, params int[] correct) => new
    {
        type = "Multiple", topic, difficulty = "Hard", content, explanation = "Giải thích", points = 2,
        choices = new[] { "A1", "B1", "C1", "D1" }.Select((c, i) => new { content = c, isCorrect = correct.Contains(i) }).ToArray(),
    };

    public static async Task<JsonElement> CreateQuestionAsync(this TestClient c, object body, bool approve = true)
    {
        var q = await TestClient.DataAsync(await c.PostAsync("/api/admin/questions", body), HttpStatusCode.Created);
        if (approve) q = await TestClient.DataAsync(await c.PostAsync($"/api/admin/questions/{q.GetProperty("id").GetGuid()}/approve"));
        return q;
    }
}

public static class ExamSetup
{
    /// <summary>Creates approved questions under a unique topic and returns (topic, ids).</summary>
    public static async Task<(string Topic, List<Guid> Ids)> BankAsync(this TestClient c, int count, string difficulty = "Medium", string? topic = null)
    {
        topic ??= CmsHelpers.Unique("ChuDe");
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var body = new
            {
                type = "Single", topic, difficulty, content = $"Câu {i + 1} – {topic}", explanation = $"Giải thích {i + 1}", points = 1,
                choices = new[] { new { content = "Đúng " + i, isCorrect = true }, new { content = "Sai A " + i, isCorrect = false }, new { content = "Sai B " + i, isCorrect = false } },
            };
            ids.Add((await c.CreateQuestionAsync(body)).GetProperty("id").GetGuid());
        }
        return (topic, ids);
    }

    public static async Task<JsonElement> CreateExamAsync(this TestClient c, ApiFactory f, object? overrides = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["title"] = CmsHelpers.Unique("Kỳ thi"),
            ["opensAtUtc"] = f.Clock.UtcNow.AddMinutes(-5),
            ["closesAtUtc"] = f.Clock.UtcNow.AddDays(1),
            ["durationMinutes"] = 30,
            ["maxAttempts"] = 2,
            ["scoreRule"] = "Highest",
            ["passScore"] = 5,
            ["resultRelease"] = "Immediately",
            ["shuffleQuestions"] = false,
            ["shuffleChoices"] = false,
            ["questionSource"] = "Manual",
        };
        if (overrides is not null)
            foreach (var p in overrides.GetType().GetProperties()) body[p.Name] = p.GetValue(overrides);
        return await TestClient.DataAsync(await c.PostAsync("/api/admin/exams", body), HttpStatusCode.Created);
    }

    /// <summary>Exam with manual questions, assigned to the candidates, published.</summary>
    public static async Task<Guid> PublishedExamAsync(this TestClient c, ApiFactory f, IReadOnlyList<Guid> questionIds, IReadOnlyList<Guid> candidateIds, object? overrides = null)
    {
        var exam = await c.CreateExamAsync(f, overrides);
        var id = exam.GetProperty("id").GetGuid();
        await TestClient.DataAsync(await c.PutAsync($"/api/admin/exams/{id}/questions", new { questionIds }));
        await TestClient.DataAsync(await c.PutAsync($"/api/admin/exams/{id}/assignments",
            new { add = candidateIds.Select(x => new { targetType = "Candidate", targetId = x }).ToArray() }));
        await TestClient.DataAsync(await c.PostAsync($"/api/admin/exams/{id}/publish"));
        return id;
    }
}

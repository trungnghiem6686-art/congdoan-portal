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

namespace CongDoan.Domain.Exams;

public enum QuestionType
{
    Single = 1,
    /// <summary>Scores only when exactly the full set of correct choices is selected.</summary>
    Multiple = 2,
    TrueFalse = 3,
}

public enum Difficulty
{
    Easy = 1,
    Medium = 2,
    Hard = 3,
}

public enum QuestionStatus
{
    Draft = 0,
    Approved = 1,
    Archived = 2,
}

public sealed class Question
{
    private Question() { }

    public Question(QuestionType type, string topic, Difficulty difficulty, Guid? createdBy, DateTime createdUtc)
    {
        Id = Guid.NewGuid();
        Type = type;
        Topic = topic;
        Difficulty = difficulty;
        CreatedBy = createdBy;
        CreatedUtc = createdUtc;
        UpdatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }
    public string? Code { get; set; }
    public QuestionType Type { get; set; }
    public string Topic { get; set; } = "";
    public Difficulty Difficulty { get; set; }
    public Guid? OrgUnitId { get; set; }
    public QuestionStatus Status { get; private set; }
    public Guid? CurrentVersionId { get; private set; }
    public int CurrentVersionNo { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedUtc { get; private set; }
    public bool IsDeleted { get; set; }
    public string? SourceKey { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    /// <summary>Every edit adds a version and sends the question back for approval.</summary>
    public QuestionVersion AddVersion(string content, string? explanation, decimal points, IEnumerable<(string Content, bool IsCorrect)> choices, Guid? by, DateTime nowUtc)
    {
        var version = new QuestionVersion(Id, CurrentVersionNo + 1, content, explanation, points, by, nowUtc, choices);
        CurrentVersionId = version.Id;
        CurrentVersionNo = version.VersionNo;
        Status = QuestionStatus.Draft;
        ApprovedBy = null;
        ApprovedUtc = null;
        UpdatedUtc = nowUtc;
        return version;
    }

    public void Approve(Guid? by, DateTime nowUtc)
    {
        Status = QuestionStatus.Approved;
        ApprovedBy = by;
        ApprovedUtc = nowUtc;
    }

    public void Archive() => Status = QuestionStatus.Archived;
}

public sealed class QuestionVersion
{
    private readonly List<Choice> _choices = new();

    private QuestionVersion() { }

    public QuestionVersion(Guid questionId, int versionNo, string content, string? explanation, decimal points, Guid? createdBy, DateTime createdUtc,
        IEnumerable<(string Content, bool IsCorrect)> choices)
    {
        Id = Guid.NewGuid();
        QuestionId = questionId;
        VersionNo = versionNo;
        Content = content;
        Explanation = explanation;
        Points = points;
        CreatedBy = createdBy;
        CreatedUtc = createdUtc;
        var order = 0;
        foreach (var (text, correct) in choices) _choices.Add(new Choice(Id, text, correct, order++));
    }

    public Guid Id { get; private set; }
    public Guid QuestionId { get; private set; }
    public int VersionNo { get; private set; }
    public string Content { get; private set; } = "";
    public string? Explanation { get; private set; }
    public decimal Points { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public IReadOnlyList<Choice> Choices => _choices;
}

public sealed class Choice
{
    private Choice() { }

    public Choice(Guid versionId, string content, bool isCorrect, int sortOrder)
    {
        Id = Guid.NewGuid();
        QuestionVersionId = versionId;
        Content = content;
        IsCorrect = isCorrect;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }
    public Guid QuestionVersionId { get; private set; }
    public string Content { get; private set; } = "";
    public bool IsCorrect { get; private set; }
    public int SortOrder { get; private set; }
}

/// <summary>Parsed import kept server-side between preview and commit (commit writes all rows or none).</summary>
public sealed class QuestionImport
{
    private QuestionImport() { }

    public QuestionImport(Guid? createdBy, DateTime createdUtc, int rowCount, int errorCount, string payloadJson)
    {
        Id = Guid.NewGuid();
        CreatedBy = createdBy;
        CreatedUtc = createdUtc;
        RowCount = rowCount;
        ErrorCount = errorCount;
        PayloadJson = payloadJson;
    }

    public Guid Id { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public int RowCount { get; private set; }
    public int ErrorCount { get; private set; }
    public string PayloadJson { get; private set; } = "[]";
    public DateTime? CommittedUtc { get; set; }
}

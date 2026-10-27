namespace CongDoan.Domain.Exams;

public enum ScoreRule
{
    Highest = 1,
    Latest = 2,
    First = 3,
}

public enum ResultRelease
{
    Immediately = 1,
    AfterClose = 2,
    AfterApproval = 3,
}

public enum QuestionSource
{
    Manual = 1,
    Random = 2,
}

public enum ExamStatus
{
    Draft = 0,
    Published = 1,
    Closed = 2,
    Archived = 3,
}

public sealed class Exam
{
    private Exam() { }

    public Exam(string title, Guid? createdBy, DateTime createdUtc)
    {
        Id = Guid.NewGuid();
        Title = title;
        CreatedBy = createdBy;
        CreatedUtc = createdUtc;
        UpdatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }
    public string? Code { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public DateTime OpensAtUtc { get; set; }
    public DateTime ClosesAtUtc { get; set; }
    public int DurationMinutes { get; set; }
    public int MaxAttempts { get; set; } = 1;
    public ScoreRule ScoreRule { get; set; } = ScoreRule.Highest;
    /// <summary>Pass mark on the 10-point scale.</summary>
    public decimal PassScore { get; set; } = 5;
    public int RoundingDecimals { get; set; } = 2;
    public ResultRelease ResultRelease { get; set; } = ResultRelease.Immediately;
    public bool ShowAnswerDetail { get; set; }
    public bool ShowExplanation { get; set; }
    public bool ShuffleQuestions { get; set; }
    public bool ShuffleChoices { get; set; }
    public bool LogTabSwitches { get; set; }
    public QuestionSource QuestionSource { get; set; } = QuestionSource.Manual;
    public ExamStatus Status { get; private set; }
    public bool ResultsApproved { get; private set; }
    public DateTime? ResultsApprovedUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? PublishedUtc { get; private set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    /// <summary>Accepting new attempts right now.</summary>
    public bool IsOpen(DateTime nowUtc) => Status == ExamStatus.Published && !IsDeleted && OpensAtUtc <= nowUtc && nowUtc < ClosesAtUtc;

    /// <summary>The exam is over (closed manually, archived or past its closing time).</summary>
    public bool IsOver(DateTime nowUtc) => Status is ExamStatus.Closed or ExamStatus.Archived || (Status == ExamStatus.Published && nowUtc >= ClosesAtUtc);

    public string Phase(DateTime nowUtc) => Status switch
    {
        ExamStatus.Draft => "draft",
        ExamStatus.Archived => "archived",
        ExamStatus.Closed => "closed",
        _ when nowUtc < OpensAtUtc => "upcoming",
        _ when nowUtc >= ClosesAtUtc => "closed",
        _ => "open",
    };

    public void Publish(DateTime nowUtc)
    {
        Status = ExamStatus.Published;
        PublishedUtc ??= nowUtc;
    }

    public void Unpublish() => Status = ExamStatus.Draft;

    public void Close() => Status = ExamStatus.Closed;

    public void Archive() => Status = ExamStatus.Archived;

    public void ApproveResults(DateTime nowUtc)
    {
        ResultsApproved = true;
        ResultsApprovedUtc = nowUtc;
    }
}

public sealed class ExamQuestion
{
    private ExamQuestion() { }

    public ExamQuestion(Guid examId, Guid questionId, int sortOrder)
    {
        ExamId = examId;
        QuestionId = questionId;
        SortOrder = sortOrder;
    }

    public Guid ExamId { get; private set; }
    public Guid QuestionId { get; private set; }
    public int SortOrder { get; private set; }
}

/// <summary>Random-draw rule: take Count approved questions matching the filters.</summary>
public sealed class ExamMatrixRule
{
    private ExamMatrixRule() { }

    public ExamMatrixRule(Guid examId, string? topic, Difficulty? difficulty, QuestionType? type, int count, int sortOrder)
    {
        Id = Guid.NewGuid();
        ExamId = examId;
        Topic = topic;
        Difficulty = difficulty;
        Type = type;
        Count = count;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }
    public Guid ExamId { get; private set; }
    public string? Topic { get; private set; }
    public Difficulty? Difficulty { get; private set; }
    public QuestionType? Type { get; private set; }
    public int Count { get; private set; }
    public int SortOrder { get; private set; }
}

public enum AssignmentTarget
{
    Candidate = 1,
    Group = 2,
    OrgUnit = 3,
}

public sealed class ExamAssignment
{
    private ExamAssignment() { }

    public ExamAssignment(Guid examId, AssignmentTarget targetType, Guid targetId)
    {
        Id = Guid.NewGuid();
        ExamId = examId;
        TargetType = targetType;
        TargetId = targetId;
    }

    public Guid Id { get; private set; }
    public Guid ExamId { get; private set; }
    public AssignmentTarget TargetType { get; private set; }
    public Guid TargetId { get; private set; }
}

/// <summary>Reusable settings + matrix to create exams quickly.</summary>
public sealed class ExamTemplate
{
    private ExamTemplate() { }

    public ExamTemplate(string name)
    {
        Id = Guid.NewGuid();
        Name = name;
    }

    public Guid Id { get; private set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Serialized exam settings, manual question ids and matrix rules.</summary>
    public string SettingsJson { get; set; } = "{}";
    public bool IsDeleted { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

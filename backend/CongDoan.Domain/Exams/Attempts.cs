namespace CongDoan.Domain.Exams;

public enum AttemptStatus
{
    InProgress = 0,
    Submitted = 1,
    /// <summary>Closed by the server at the deadline (graded with saved answers).</summary>
    Expired = 2,
    Voided = 3,
}

/// <summary>
/// One candidate attempt. A filtered unique index allows only one InProgress attempt per (exam, candidate);
/// (exam, candidate, attempt no) is unique so the attempt limit cannot be exceeded by racing requests.
/// </summary>
public sealed class Attempt
{
    private Attempt() { }

    public Attempt(Guid examId, Guid candidateId, int attemptNo, DateTime startedUtc, DateTime deadlineUtc, decimal maxScore)
    {
        Id = Guid.NewGuid();
        ExamId = examId;
        CandidateId = candidateId;
        AttemptNo = attemptNo;
        StartedUtc = startedUtc;
        DeadlineUtc = deadlineUtc;
        MaxScore = maxScore;
        Status = AttemptStatus.InProgress;
    }

    public Guid Id { get; private set; }
    public Guid ExamId { get; private set; }
    public Guid CandidateId { get; private set; }
    public int AttemptNo { get; private set; }
    public AttemptStatus Status { get; private set; }
    public DateTime StartedUtc { get; private set; }
    public DateTime DeadlineUtc { get; private set; }
    public DateTime? FinishedUtc { get; private set; }
    /// <summary>Raw points earned.</summary>
    public decimal? Points { get; private set; }
    public decimal MaxScore { get; private set; }
    /// <summary>Score on the 10-point scale, rounded per exam settings.</summary>
    public decimal? Score { get; private set; }
    public bool? Passed { get; private set; }
    public int CorrectCount { get; private set; }
    /// <summary>Highest autosave sequence accepted (monotonic, rejects stale writes).</summary>
    public long LastSeq { get; set; }
    /// <summary>The browser tab currently allowed to write answers.</summary>
    public string? WriterToken { get; set; }
    public int TabSwitchCount { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public bool IsFinished => Status != AttemptStatus.InProgress;

    public void Finish(AttemptStatus status, DateTime finishedUtc, int correctCount, decimal points, bool passed, decimal? score = null)
    {
        Status = status;
        FinishedUtc = finishedUtc;
        CorrectCount = correctCount;
        Points = points;
        Score = score ?? (MaxScore == 0 ? 0 : Math.Round(points / MaxScore * 10, 2, MidpointRounding.AwayFromZero));
        Passed = passed;
    }

    public void Regrade(decimal points, decimal score, bool passed, int correctCount)
    {
        Points = points;
        Score = score;
        Passed = passed;
        CorrectCount = correctCount;
    }
}

/// <summary>Frozen copy of a question as served in one attempt (content, order, choices and key).</summary>
public sealed class AttemptQuestion
{
    private AttemptQuestion() { }

    public AttemptQuestion(Guid attemptId, int orderNo, Guid questionId, Guid questionVersionId, QuestionType type, string content,
        string? explanation, decimal points, string choicesJson)
    {
        Id = Guid.NewGuid();
        AttemptId = attemptId;
        OrderNo = orderNo;
        QuestionId = questionId;
        QuestionVersionId = questionVersionId;
        Type = type;
        Content = content;
        Explanation = explanation;
        Points = points;
        ChoicesJson = choicesJson;
    }

    public Guid Id { get; private set; }
    public Guid AttemptId { get; private set; }
    public int OrderNo { get; private set; }
    public Guid QuestionId { get; private set; }
    public Guid QuestionVersionId { get; private set; }
    public QuestionType Type { get; private set; }
    public string Content { get; private set; } = "";
    public string? Explanation { get; private set; }
    public decimal Points { get; private set; }
    /// <summary>[{id, content, isCorrect}] in display order. Server-side only.</summary>
    public string ChoicesJson { get; private set; } = "[]";
}

public sealed class Answer
{
    private Answer() { }

    public Answer(Guid attemptId, Guid attemptQuestionId)
    {
        AttemptId = attemptId;
        AttemptQuestionId = attemptQuestionId;
    }

    public Guid AttemptId { get; private set; }
    public Guid AttemptQuestionId { get; private set; }
    public string SelectedJson { get; set; } = "[]";
    public bool Flagged { get; set; }
    public long Seq { get; set; }
    public DateTime SavedUtc { get; set; }
    public decimal? AwardedPoints { get; set; }
}

/// <summary>Audit of a manual regrade: original and new score, reason, who and when.</summary>
public sealed class GradeRevision
{
    private GradeRevision() { }

    public GradeRevision(Guid attemptId, decimal? oldScore, decimal newScore, string reason, Guid? by, DateTime atUtc)
    {
        Id = Guid.NewGuid();
        AttemptId = attemptId;
        OldScore = oldScore;
        NewScore = newScore;
        Reason = reason;
        By = by;
        AtUtc = atUtc;
    }

    public Guid Id { get; private set; }
    public Guid AttemptId { get; private set; }
    public decimal? OldScore { get; private set; }
    public decimal NewScore { get; private set; }
    public string Reason { get; private set; } = "";
    public Guid? By { get; private set; }
    public DateTime AtUtc { get; private set; }
}

/// <summary>Business event during an attempt (start, save conflict, tab switch...). A signal only, never a verdict.</summary>
public sealed class AttemptEvent
{
    private AttemptEvent() { }

    public AttemptEvent(Guid attemptId, string kind, string? detail, DateTime atUtc)
    {
        AttemptId = attemptId;
        Kind = kind;
        Detail = detail;
        AtUtc = atUtc;
    }

    public long Id { get; private set; }
    public Guid AttemptId { get; private set; }
    public string Kind { get; private set; } = "";
    public string? Detail { get; private set; }
    public DateTime AtUtc { get; private set; }
}

using CongDoan.Application.Exams;
using CongDoan.Application.Shared;
using CongDoan.Domain.Exams;

namespace CongDoan.Infrastructure.Exams;

/// <summary>Placeholder until grading exists: an expired attempt cannot be closed yet.</summary>
internal sealed class PendingFinisher : IAttemptFinisher
{
    public Task FinishAsync(Guid attemptId, AttemptStatus status, CancellationToken ct = default) =>
        throw AppException.Conflict("previous_attempt_finishing", "Lượt làm bài trước đang được chấm. Vui lòng thử lại sau giây lát.");
}

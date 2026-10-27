namespace CongDoan.Application.Shared;

public sealed record ApiError(string Code, string Message, string RequestId);

public sealed record ApiEnvelope<T>(bool Success, T? Data, ApiError? Error)
{
    public static ApiEnvelope<T> Ok(T data) => new(true, data, null);
    public static ApiEnvelope<T> Fail(ApiError error) => new(false, default, error);
}

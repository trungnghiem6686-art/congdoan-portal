namespace CongDoan.Application.Shared;

/// <summary>Expected business failure; mapped to the error envelope with its HTTP status.</summary>
public class AppException : Exception
{
    public AppException(string code, string message, int status = 400) : base(message)
    {
        Code = code;
        Status = status;
    }

    public string Code { get; }
    public int Status { get; }

    public static AppException NotFound(string message = "Không tìm thấy dữ liệu.") => new("not_found", message, 404);
    public static AppException Forbidden(string message = "Bạn không có quyền thực hiện thao tác này.") => new("forbidden", message, 403);
    public static AppException Conflict(string code, string message) => new(code, message, 409);
    public static AppException Validation(string message) => new("validation_error", message, 400);
}

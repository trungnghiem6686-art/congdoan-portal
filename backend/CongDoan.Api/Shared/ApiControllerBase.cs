using CongDoan.Application.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CongDoan.Api.Shared;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : Controller
{
    protected IActionResult Envelope<T>(T data) => Ok(ApiEnvelope<T>.Ok(data));

    protected IActionResult NotFoundEnvelope(string message = "Không tìm thấy nội dung.") =>
        NotFound(ApiEnvelope<object>.Fail(new ApiError("not_found", message, HttpContext.TraceIdentifier)));

    protected IActionResult Created<T>(T data) => StatusCode(StatusCodes.Status201Created, ApiEnvelope<T>.Ok(data));
}

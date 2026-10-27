using CongDoan.Application.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CongDoan.Api.Shared;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult Envelope<T>(T data) => Ok(ApiEnvelope<T>.Ok(data));

    protected IActionResult Created<T>(T data) => StatusCode(StatusCodes.Status201Created, ApiEnvelope<T>.Ok(data));
}

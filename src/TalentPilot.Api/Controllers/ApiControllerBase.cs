using Microsoft.AspNetCore.Mvc;
using TalentPilot.Common.Results;

namespace TalentPilot.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult FromResult(Result result)
    {
        return result.Succeeded
            ? NoContent()
            : ToErrorResponse(result.Error);
    }

    protected ActionResult<T> FromResult<T>(Result<T> result)
    {
        return result.Succeeded
            ? Ok(result.Value)
            : ToErrorResponse(result.Error);
    }

    private ObjectResult ToErrorResponse(Error error)
    {
        var status = error.Code.Contains("not_found", StringComparison.OrdinalIgnoreCase)
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;

        return StatusCode(status, new
        {
            error = error.Code,
            message = error.Message
        });
    }
}

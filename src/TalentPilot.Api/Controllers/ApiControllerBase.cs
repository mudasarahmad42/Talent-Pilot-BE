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
        var status = StatusCodes.Status400BadRequest;
        if (error.Code.Contains("not_found", StringComparison.OrdinalIgnoreCase))
        {
            status = StatusCodes.Status404NotFound;
        }
        else if (error.Code.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
        {
            status = StatusCodes.Status403Forbidden;
        }

        return StatusCode(status, new
        {
            error = error.Code,
            message = error.Message
        });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TalentPilot.Application.Calendar;

namespace TalentPilot.Api.Controllers;

[Route("api/google-calendar")]
public sealed class GoogleCalendarController : ApiControllerBase
{
    private readonly IGoogleCalendarService _googleCalendarService;

    public GoogleCalendarController(IGoogleCalendarService googleCalendarService)
    {
        _googleCalendarService = googleCalendarService;
    }

    [HttpGet("status")]
    public async Task<ActionResult<GoogleCalendarConnectionStatus>> Status(CancellationToken cancellationToken)
    {
        return FromResult(await _googleCalendarService.GetConnectionStatusAsync(cancellationToken));
    }

    [HttpGet("connect-url")]
    public async Task<ActionResult<GoogleCalendarConnectResponse>> ConnectUrl(CancellationToken cancellationToken)
    {
        return FromResult(await _googleCalendarService.BuildConnectResponseAsync(cancellationToken));
    }

    [HttpGet("connect")]
    public async Task<IActionResult> Connect(CancellationToken cancellationToken)
    {
        var result = await _googleCalendarService.BuildConnectResponseAsync(cancellationToken);
        return result.Succeeded
            ? Redirect(result.Value.AuthorizationUrl)
            : BadRequest(new { error = result.Error.Code, message = result.Error.Message });
    }

    [AllowAnonymous]
    [HttpGet("oauth/callback")]
    public async Task<IActionResult> OAuthCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        var result = await _googleCalendarService.CompleteOAuthCallbackAsync(code, state, error, cancellationToken);
        return result.Succeeded
            ? Redirect(result.Value.RedirectUrl)
            : BadRequest(new { error = result.Error.Code, message = result.Error.Message });
    }

    [HttpPost("events")]
    public async Task<ActionResult<GoogleCalendarEventResponse>> CreateEvent(
        CreateGoogleCalendarEventInput input,
        CancellationToken cancellationToken)
    {
        return FromResult(await _googleCalendarService.CreateGoogleCalendarEventAsync(input, cancellationToken));
    }
}

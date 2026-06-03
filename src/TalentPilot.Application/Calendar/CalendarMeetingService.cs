using TalentPilot.Common.Results;

namespace TalentPilot.Application.Calendar;

public enum CalendarMeetingParticipantRole
{
    Candidate,
    HiringManager,
    Interviewer,
    Recruiter,
    Other
}

public sealed record CalendarMeetingParticipant(
    string Email,
    string DisplayName,
    CalendarMeetingParticipantRole Role);

public sealed record CalendarMeetingRequest(
    string Title,
    string Description,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string TimeZoneId,
    IReadOnlyList<CalendarMeetingParticipant> Participants,
    bool CreateOnlineMeeting,
    string RequestId);

public sealed record CalendarMeetingResult(
    bool Created,
    string Provider,
    string? EventId,
    string? EventHtmlLink,
    string? MeetingLink,
    string StatusMessage);

public interface ICalendarMeetingService
{
    Task<Result<CalendarMeetingResult>> CreateMeetingAsync(
        CalendarMeetingRequest request,
        CancellationToken cancellationToken);
}

public sealed class NoOpCalendarMeetingService : ICalendarMeetingService
{
    public Task<Result<CalendarMeetingResult>> CreateMeetingAsync(
        CalendarMeetingRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Result<CalendarMeetingResult>.Success(new CalendarMeetingResult(
            false,
            "None",
            null,
            null,
            null,
            "Calendar integration is disabled.")));
    }
}

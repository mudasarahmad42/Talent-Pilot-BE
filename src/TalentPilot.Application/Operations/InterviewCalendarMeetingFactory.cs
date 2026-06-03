using TalentPilot.Application.Calendar;

namespace TalentPilot.Application.Operations;

public static class InterviewCalendarMeetingFactory
{
    public static CalendarMeetingRequest Build(
        OperationsInterviewScheduleContext context,
        DateTimeOffset startsAtUtc,
        string? locationText,
        string requestId,
        IEnumerable<CalendarMeetingParticipant>? additionalParticipants = null,
        bool createOnlineMeeting = true,
        string? existingMeetingLink = null)
    {
        var endsAtUtc = startsAtUtc.AddMinutes(context.DurationMinutes);
        var participants = new[]
            {
                new CalendarMeetingParticipant(
                    context.CandidateEmail,
                    context.CandidateName,
                    CalendarMeetingParticipantRole.Candidate),
                new CalendarMeetingParticipant(
                    context.HiringManagerEmail,
                    context.HiringManagerName,
                    CalendarMeetingParticipantRole.HiringManager),
                new CalendarMeetingParticipant(
                    context.InterviewerEmail,
                    context.InterviewerName,
                    CalendarMeetingParticipantRole.Interviewer)
            }
            .Concat(additionalParticipants ?? [])
            .Where(participant => !string.IsNullOrWhiteSpace(participant.Email))
            .DistinctBy(participant => participant.Email.Trim().ToUpperInvariant())
            .ToArray();

        var title = $"{context.RoundName}: {context.CandidateName} - {context.JobTitle}";
        var descriptionLines = new List<string>
        {
            $"{context.CompanyName} interview for {context.JobTitle}.",
            string.Empty,
            $"Request: {context.RequestCode}",
            $"Candidate: {context.CandidateName}",
            $"Round: {context.RoundName}",
            $"Recruiter: {context.RecruiterName}"
        };

        if (!string.IsNullOrWhiteSpace(locationText))
        {
            descriptionLines.Add($"Location/notes: {locationText.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(existingMeetingLink))
        {
            descriptionLines.Add($"Meeting link: {existingMeetingLink.Trim()}");
        }

        return new CalendarMeetingRequest(
            title,
            string.Join(Environment.NewLine, descriptionLines),
            startsAtUtc,
            endsAtUtc,
            string.IsNullOrWhiteSpace(context.TimeZoneId) ? "UTC" : context.TimeZoneId,
            participants,
            createOnlineMeeting,
            requestId);
    }
}

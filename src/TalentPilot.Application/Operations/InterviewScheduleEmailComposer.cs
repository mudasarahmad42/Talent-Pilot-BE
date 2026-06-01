namespace TalentPilot.Application.Operations;

public static class InterviewScheduleEmailComposer
{
    public static IReadOnlyList<InterviewScheduleEmailMessage> Build(InterviewScheduleEmailContext context)
    {
        var messages = new List<InterviewScheduleEmailMessage>(3)
        {
            BuildCandidateMessage(context),
            BuildInterviewerMessage(context),
            BuildHiringManagerMessage(context)
        };

        return messages;
    }

    private static InterviewScheduleEmailMessage BuildCandidateMessage(InterviewScheduleEmailContext context)
    {
        return new InterviewScheduleEmailMessage(
            "Candidate",
            null,
            context.CandidateEmail,
            $"Interview scheduled for {context.JobTitle}",
            BuildBody(
                $"Hello {context.CandidateName},",
                $"{context.CompanyName} has scheduled your {context.RoundName} interview for {context.JobTitle}.",
                context));
    }

    private static InterviewScheduleEmailMessage BuildInterviewerMessage(InterviewScheduleEmailContext context)
    {
        return new InterviewScheduleEmailMessage(
            "Interviewer",
            context.InterviewerUserId,
            context.InterviewerEmail,
            $"Interview assigned: {context.CandidateName}",
            BuildBody(
                $"Hello {context.InterviewerName},",
                $"You have been assigned to conduct {context.RoundName} for {context.CandidateName} against {context.JobTitle}.",
                context));
    }

    private static InterviewScheduleEmailMessage BuildHiringManagerMessage(InterviewScheduleEmailContext context)
    {
        return new InterviewScheduleEmailMessage(
            "HiringManager",
            context.HiringManagerUserId,
            context.HiringManagerEmail,
            $"Interview scheduled for {context.JobTitle}",
            BuildBody(
                $"Hello {context.HiringManagerName},",
                $"{context.CandidateName} has been scheduled for {context.RoundName} against {context.JobTitle}.",
                context));
    }

    private static string BuildBody(string greeting, string intro, InterviewScheduleEmailContext context)
    {
        var lines = new List<string>
        {
            greeting,
            string.Empty,
            intro,
            string.Empty,
            $"Job: {context.JobTitle} ({context.RequestCode})",
            $"Candidate: {context.CandidateName}",
            $"Round: {context.RoundName}",
            $"Date/time: {context.StartsAtUtc:yyyy-MM-dd HH:mm} UTC",
            $"Duration: {context.DurationMinutes} minutes",
            $"Recruiter: {context.RecruiterName}"
        };

        if (!string.IsNullOrWhiteSpace(context.MeetingLink))
        {
            lines.Add($"Meeting link: {context.MeetingLink}");
        }

        if (!string.IsNullOrWhiteSpace(context.LocationText))
        {
            lines.Add($"Location/notes: {context.LocationText}");
        }

        lines.Add(string.Empty);
        lines.Add("Regards,");
        lines.Add(context.CompanyName);
        return string.Join(Environment.NewLine, lines);
    }
}

public sealed record InterviewScheduleEmailContext(
    string CompanyName,
    string RequestCode,
    string JobTitle,
    string CandidateName,
    string CandidateEmail,
    Guid InterviewerUserId,
    string InterviewerName,
    string InterviewerEmail,
    Guid HiringManagerUserId,
    string HiringManagerName,
    string HiringManagerEmail,
    string RecruiterName,
    string RoundName,
    DateTimeOffset StartsAtUtc,
    int DurationMinutes,
    string? MeetingLink,
    string? LocationText);

public sealed record InterviewScheduleEmailMessage(
    string RecipientType,
    Guid? RecipientUserId,
    string RecipientEmail,
    string Subject,
    string Body);

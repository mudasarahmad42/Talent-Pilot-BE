using TalentPilot.Application.Calendar;
using TalentPilot.Application.Operations;

namespace TalentPilot.Tests.Operations;

public sealed class InterviewCalendarMeetingFactoryTests
{
    [Fact]
    public void Build_CreatesMeetingRequestWithCoreInterviewParticipants()
    {
        var startsAt = new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero);
        var context = CreateContext();

        var request = InterviewCalendarMeetingFactory.Build(
            context,
            startsAt,
            "Google Meet",
            "calendar-request-1");

        Assert.Equal("Technical Interview: Nida Farooq - Senior React Developer", request.Title);
        Assert.Equal(startsAt, request.StartsAtUtc);
        Assert.Equal(startsAt.AddMinutes(60), request.EndsAtUtc);
        Assert.Equal("Asia/Karachi", request.TimeZoneId);
        Assert.True(request.CreateOnlineMeeting);
        Assert.Equal("calendar-request-1", request.RequestId);
        Assert.Contains("TP-REQ-004", request.Description);
        Assert.Contains("Sara Malik", request.Description);
        Assert.Contains("Google Meet", request.Description);
        Assert.Collection(
            request.Participants,
            candidate =>
            {
                Assert.Equal("candidate@example.com", candidate.Email);
                Assert.Equal("Nida Farooq", candidate.DisplayName);
                Assert.Equal(CalendarMeetingParticipantRole.Candidate, candidate.Role);
            },
            hiringManager =>
            {
                Assert.Equal("hm@example.com", hiringManager.Email);
                Assert.Equal("Fatima Noor", hiringManager.DisplayName);
                Assert.Equal(CalendarMeetingParticipantRole.HiringManager, hiringManager.Role);
            },
            interviewer =>
            {
                Assert.Equal("interviewer@example.com", interviewer.Email);
                Assert.Equal("Bilal Hussain", interviewer.DisplayName);
                Assert.Equal(CalendarMeetingParticipantRole.Interviewer, interviewer.Role);
            });
    }

    [Fact]
    public void Build_AcceptsAdditionalParticipantsAndDeduplicatesEmails()
    {
        var startsAt = new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero);
        var context = CreateContext();
        var additionalParticipants = new[]
        {
            new CalendarMeetingParticipant(
                "panel@example.com",
                "Panel Interviewer",
                CalendarMeetingParticipantRole.Interviewer),
            new CalendarMeetingParticipant(
                "INTERVIEWER@example.com",
                "Duplicate Interviewer",
                CalendarMeetingParticipantRole.Interviewer)
        };

        var request = InterviewCalendarMeetingFactory.Build(
            context,
            startsAt,
            null,
            "calendar-request-2",
            additionalParticipants);

        Assert.Equal(4, request.Participants.Count);
        Assert.Contains(
            request.Participants,
            participant => participant.Email == "panel@example.com" &&
                participant.Role == CalendarMeetingParticipantRole.Interviewer);
        Assert.Single(
            request.Participants,
            participant => string.Equals(participant.Email, "interviewer@example.com", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_IncludesManualMeetingLinkWithoutRequestingGoogleMeet()
    {
        var startsAt = new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero);
        var context = CreateContext();

        var request = InterviewCalendarMeetingFactory.Build(
            context,
            startsAt,
            "Client-provided video link",
            "calendar-request-3",
            createOnlineMeeting: false,
            existingMeetingLink: "https://meet.example/manual");

        Assert.False(request.CreateOnlineMeeting);
        Assert.Contains("Client-provided video link", request.Description);
        Assert.Contains("https://meet.example/manual", request.Description);
    }

    private static OperationsInterviewScheduleContext CreateContext()
    {
        return new OperationsInterviewScheduleContext(
            CompanyName: "Tkxel",
            RequestCode: "TP-REQ-004",
            JobTitle: "Senior React Developer",
            CandidateName: "Nida Farooq",
            CandidateEmail: "candidate@example.com",
            InterviewerUserId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            InterviewerName: "Bilal Hussain",
            InterviewerEmail: "interviewer@example.com",
            HiringManagerUserId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            HiringManagerName: "Fatima Noor",
            HiringManagerEmail: "hm@example.com",
            RecruiterName: "Sara Malik",
            RecruiterEmail: "recruiter@example.com",
            RoundName: "Technical Interview",
            DurationMinutes: 60,
            TimeZoneId: "Asia/Karachi");
    }
}

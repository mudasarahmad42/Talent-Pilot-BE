using TalentPilot.Application.Operations;
using TalentPilot.Domain.Notifications;

namespace TalentPilot.Tests.Operations;

public sealed class InterviewScheduleEmailComposerTests
{
    [Fact]
    public void Build_CreatesCandidateInterviewerAndHiringManagerMessages()
    {
        var interviewerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var hiringManagerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var context = new InterviewScheduleEmailContext(
            CompanyName: "Tkxel",
            RequestCode: "TP-REQ-004",
            JobTitle: "Senior React Developer",
            CandidateName: "Nida Farooq",
            CandidateEmail: "mudasar.ahmad@tkxel.com",
            InterviewerUserId: interviewerId,
            InterviewerName: "Bilal Hussain",
            InterviewerEmail: "mudasar.ahmad@tkxel.com",
            HiringManagerUserId: hiringManagerId,
            HiringManagerName: "Fatima Noor",
            HiringManagerEmail: "mudasar.ahmad@tkxel.com",
            RecruiterName: "Sara Malik",
            RoundName: "Technical Interview",
            StartsAtUtc: new DateTimeOffset(2026, 6, 1, 9, 30, 0, TimeSpan.Zero),
            DurationMinutes: 60,
            MeetingLink: "https://meet.example/interview",
            LocationText: "Google Meet");

        var messages = InterviewScheduleEmailComposer.Build(context);

        Assert.Equal(3, messages.Count);
        Assert.Collection(
            messages,
            candidate =>
            {
                Assert.Equal("Candidate", candidate.RecipientType);
                Assert.Null(candidate.RecipientUserId);
                Assert.Equal("mudasar.ahmad@tkxel.com", candidate.RecipientEmail);
                Assert.Contains("Senior React Developer", candidate.Subject);
            },
            interviewer =>
            {
                Assert.Equal("Interviewer", interviewer.RecipientType);
                Assert.Equal(interviewerId, interviewer.RecipientUserId);
                Assert.Equal("mudasar.ahmad@tkxel.com", interviewer.RecipientEmail);
                Assert.Contains("Nida Farooq", interviewer.Subject);
            },
            hiringManager =>
            {
                Assert.Equal("HiringManager", hiringManager.RecipientType);
                Assert.Equal(hiringManagerId, hiringManager.RecipientUserId);
                Assert.Equal("mudasar.ahmad@tkxel.com", hiringManager.RecipientEmail);
                Assert.Contains("Senior React Developer", hiringManager.Subject);
            });
    }

    [Fact]
    public void Build_IncludesSchedulingDetailsInEveryMessage()
    {
        var context = new InterviewScheduleEmailContext(
            CompanyName: "Tkxel",
            RequestCode: "TP-REQ-004",
            JobTitle: "Senior React Developer",
            CandidateName: "Nida Farooq",
            CandidateEmail: "candidate@example.com",
            InterviewerUserId: Guid.NewGuid(),
            InterviewerName: "Bilal Hussain",
            InterviewerEmail: "interviewer@example.com",
            HiringManagerUserId: Guid.NewGuid(),
            HiringManagerName: "Fatima Noor",
            HiringManagerEmail: "hm@example.com",
            RecruiterName: "Sara Malik",
            RoundName: "Technical Interview",
            StartsAtUtc: new DateTimeOffset(2026, 6, 1, 9, 30, 0, TimeSpan.Zero),
            DurationMinutes: 60,
            MeetingLink: "https://meet.example/interview",
            LocationText: "Google Meet");

        var messages = InterviewScheduleEmailComposer.Build(context);

        foreach (var message in messages)
        {
            Assert.Contains("Senior React Developer", message.Body);
            Assert.Contains("TP-REQ-004", message.Body);
            Assert.Contains("Nida Farooq", message.Body);
            Assert.Contains("Technical Interview", message.Body);
            Assert.Contains("2026-06-01 09:30 UTC", message.Body);
            Assert.Contains("60 minutes", message.Body);
            Assert.Contains("Sara Malik", message.Body);
            Assert.Contains("https://meet.example/interview", message.Body);
            Assert.Contains("Google Meet", message.Body);
        }
    }

    [Fact]
    public void NotificationEventCodes_IncludesInterviewScheduled()
    {
        Assert.Contains(NotificationEventCodes.InterviewScheduled, NotificationEventCodes.All);
    }
}

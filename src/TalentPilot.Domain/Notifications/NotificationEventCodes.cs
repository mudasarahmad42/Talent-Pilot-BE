namespace TalentPilot.Domain.Notifications;

public static class NotificationEventCodes
{
    public const string PresalesRequestSubmitted = "PRESALES_REQUEST_SUBMITTED";
    public const string PmoEmployeeReferred = "PMO_EMPLOYEE_REFERRED";
    public const string PmoForwardedToRecruiting = "PMO_FORWARDED_TO_RECRUITING";
    public const string PresalesEmployeeReferralAccepted = "PRESALES_EMPLOYEE_REFERRAL_ACCEPTED";
    public const string PresalesEmployeeReferralRejected = "PRESALES_EMPLOYEE_REFERRAL_REJECTED";
    public const string RecruiterAssignedInterviewers = "RECRUITER_ASSIGNED_INTERVIEWERS";
    public const string InterviewScheduled = "INTERVIEW_SCHEDULED";
    public const string InterviewFeedbackSubmitted = "INTERVIEW_FEEDBACK_SUBMITTED";
    public const string CandidateStageChanged = "CANDIDATE_STAGE_CHANGED";
    public const string CandidateInvitedToApply = "CANDIDATE_INVITED_TO_APPLY";
    public const string HiringManagerReviewReady = "HIRING_MANAGER_REVIEW_READY";
    public const string OfferPresentationMeetingScheduled = "OFFER_PRESENTATION_MEETING_SCHEDULED";
    public const string RealtimeNotification = "REALTIME_NOTIFICATION";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        PresalesRequestSubmitted,
        PmoEmployeeReferred,
        PmoForwardedToRecruiting,
        PresalesEmployeeReferralAccepted,
        PresalesEmployeeReferralRejected,
        RecruiterAssignedInterviewers,
        InterviewScheduled,
        InterviewFeedbackSubmitted,
        CandidateStageChanged,
        CandidateInvitedToApply,
        HiringManagerReviewReady,
        OfferPresentationMeetingScheduled,
        RealtimeNotification
    };
}

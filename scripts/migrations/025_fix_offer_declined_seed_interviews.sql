/*
    Repair demo seed consistency for Talent Rediscovery.
    OfferDeclined should only appear after the candidate has cleared every configured interview.
*/

DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
DECLARE @SanaHistApplicationId UNIQUEIDENTIFIER = '26000000-0000-0000-0000-000000000003';
DECLARE @SanaCandidateId UNIQUEIDENTIFIER = '25000000-0000-0000-0000-000000000003';

UPDATE dbo.JobApplications
SET
    FinalDecisionReason = N'Candidate cleared all interviews, received an offer, and then accepted a counter offer.',
    RecruiterNotes = N'Priority 3 seed: late-stage non-fit closure with positive interview evidence and offer declined for timing/compensation reasons.',
    UpdatedAtUtc = @Now
WHERE JobApplicationId = @SanaHistApplicationId
  AND CurrentStatus = N'OfferDeclined';

UPDATE dbo.InterviewFeedback
SET
    TechnicalScore = 4,
    CommunicationScore = 4,
    CultureScore = 4,
    Recommendation = N'Proceed',
    FeedbackText = CASE InterviewFeedbackId
        WHEN '26200000-0000-0000-0000-000000000008' THEN N'Cleared the technical interview for full-stack portal delivery; strongest evidence was React and TypeScript with acceptable backend collaboration depth.'
        WHEN '26200000-0000-0000-0000-000000000009' THEN N'Cleared department-head discussion and was approved for offer; candidate later accepted a counter offer.'
        ELSE FeedbackText
    END,
    IsSubmitted = CAST(1 AS BIT),
    UpdatedAtUtc = @Now
WHERE InterviewFeedbackId IN
(
    '26200000-0000-0000-0000-000000000008',
    '26200000-0000-0000-0000-000000000009'
);

UPDATE dbo.AiRecommendationLogs
SET
    Explanation = CASE
        WHEN AiAgentDefinitionId = N'talent-rediscovery'
            THEN N'Priority 3: late-stage candidate who cleared interviews but declined the offer for timing/compensation reasons.'
        ELSE Explanation
    END,
    PayloadJson = REPLACE(REPLACE(REPLACE(PayloadJson,
        N'"interviewPassSummary":"1/3 passed"', N'"interviewPassSummary":"3/3 passed"'),
        N'"InterviewPassSummary":"1/3 passed"', N'"InterviewPassSummary":"3/3 passed"'),
        N'"InterviewsPassed":1', N'"InterviewsPassed":3'),
    UpdatedAtUtc = @Now
WHERE RecommendedEntityType = N'Candidate'
  AND RecommendedEntityId = @SanaCandidateId
  AND PayloadJson IS NOT NULL;

UPDATE dbo.AiRecommendationLogs
SET
    PayloadJson = REPLACE(PayloadJson, N'"interviewsPassed":1', N'"interviewsPassed":3'),
    UpdatedAtUtc = @Now
WHERE RecommendedEntityType = N'Candidate'
  AND RecommendedEntityId = @SanaCandidateId
  AND PayloadJson IS NOT NULL;

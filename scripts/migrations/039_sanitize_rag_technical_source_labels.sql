/*
    Removes internal RAG source tuple labels from saved assistant answers.
    Citation chips and evidence previews still carry structured metadata; natural
    language assistant text should not say things like "(BenchMatch, BenchMatchLog)".
*/

UPDATE dbo.AiAssistantMessages
SET Content = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(Content,
        N'(BenchMatch, BenchMatchLog)', N''),
        N'(BenchEmployee, BenchEmployeeProfile)', N''),
        N'(JobRequest, JobRequest)', N''),
        N'(CandidateApplication, CandidateApplication)', N''),
        N'(ApplicantRanking, ApplicantRankingLog)', N''),
        N'(TalentRediscovery, TalentRediscoveryLog)', N''),
        N'(EmployeeReferral, EmployeeReferral)', N''),
        N'  ', N' ')
WHERE Role = N'Assistant'
  AND (
        Content LIKE N'%(BenchMatch, BenchMatchLog)%'
        OR Content LIKE N'%(BenchEmployee, BenchEmployeeProfile)%'
        OR Content LIKE N'%(JobRequest, JobRequest)%'
        OR Content LIKE N'%(CandidateApplication, CandidateApplication)%'
        OR Content LIKE N'%(ApplicantRanking, ApplicantRankingLog)%'
        OR Content LIKE N'%(TalentRediscovery, TalentRediscoveryLog)%'
        OR Content LIKE N'%(EmployeeReferral, EmployeeReferral)%'
      );

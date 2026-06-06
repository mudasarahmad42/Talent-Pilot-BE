/*
    Sanitizes stale bench-match explanations generated before the rationale guard
    removed invalid claims that 6.8 years is below a 3+ years requirement.
*/

DECLARE @OldSnippet NVARCHAR(MAX) = N'Zain Javaid has 6.8 years of experience as a Senior Java Engineer, which is less than the required 3+ years for this role. Despite his experience, he lacks skills in AWS, Design Patterns, and Python, which are essential for this position.';
DECLARE @OldSnippetAlt NVARCHAR(MAX) = N'Zain Javaid has 6.8 years of experience as a Senior Java Engineer, which is less than the required 3+ years for this position. He has SQL evidence but lacks AWS, Design Patterns, and Python. The ranking is based on limited experience and skill gaps.';
DECLARE @Corrected NVARCHAR(MAX) = N'Zain Javaid''s profile is primarily Java (Senior Java Engineer); while they have backend/project experience and 6.8 years overall, this request is centered on Python, AWS, SQL, and Design Patterns, and current tenant evidence only supports SQL. They are not preferred until missing Python, AWS, and Design Patterns evidence is validated.';

UPDATE dbo.AiRecommendationLogs
SET Explanation = CASE
        WHEN Explanation LIKE N'%6.8 years of experience as a Senior Java Engineer%less than the required 3+ years%'
            THEN @Corrected
        ELSE Explanation
    END,
    PayloadJson = CASE
        WHEN ISJSON(PayloadJson) = 1
             AND JSON_VALUE(PayloadJson, '$.explanation') LIKE N'%6.8 years of experience as a Senior Java Engineer%less than the required 3+ years%'
            THEN JSON_MODIFY(PayloadJson, '$.explanation', @Corrected)
        ELSE PayloadJson
    END,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE AiAgentDefinitionId = N'bench-matching'
  AND (
        Explanation LIKE N'%6.8 years of experience as a Senior Java Engineer%less than the required 3+ years%'
        OR (ISJSON(PayloadJson) = 1 AND JSON_VALUE(PayloadJson, '$.explanation') LIKE N'%6.8 years of experience as a Senior Java Engineer%less than the required 3+ years%')
      );

UPDATE dbo.KnowledgeChunks
SET ChunkText = REPLACE(REPLACE(ChunkText, @OldSnippet, @Corrected), @OldSnippetAlt, @Corrected),
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE SourceEntityType = N'BenchMatch'
  AND ChunkText LIKE N'%6.8 years of experience as a Senior Java Engineer%less than the required 3+ years%';

UPDATE dbo.AiAssistantMessageCitations
SET Excerpt = CASE
        WHEN Excerpt LIKE N'%6.8 years of experience as a Senior Java Engineer%less than the required 3+ years%'
            THEN CONCAT(N'Bench match rationale: ', @Corrected)
        ELSE REPLACE(REPLACE(Excerpt, @OldSnippet, @Corrected), @OldSnippetAlt, @Corrected)
    END
WHERE SourceType = N'BenchMatch'
  AND Excerpt LIKE N'%6.8 years of experience as a Senior Java Engineer%less than the required 3+ years%';

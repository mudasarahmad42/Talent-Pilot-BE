/*
    Clarifies saved PMO assistant answers that started with "Refer" while the
    cited evidence said the employee lacked essential required skills.
*/

DECLARE @OldAnswer NVARCHAR(MAX) = N'Refer Zain Javaid to pre sales based on his internal ranking as a candidate for PMO review. [C1] Zain Javaid match rationale states he lacks skills in AWS, Design Patterns, and Python, which are essential for the Senior Python Developer role. [C2] Zain Javaid also lists these missing skills.';
DECLARE @ClearAnswer NVARCHAR(MAX) = N'Do not refer Zain Javaid to Presales yet based on the current evidence. [C1] Zain Javaid match rationale states he lacks skills in AWS, Design Patterns, and Python, which are essential for the Senior Python Developer role. [C2] Zain Javaid also lists these missing skills.';

UPDATE dbo.AiAssistantMessages
SET Content = REPLACE(Content, @OldAnswer, @ClearAnswer)
WHERE Role = N'Assistant'
  AND Content LIKE N'Refer Zain Javaid to pre sales based on his internal ranking%'
  AND Content LIKE N'%lacks skills in AWS, Design Patterns, and Python%';

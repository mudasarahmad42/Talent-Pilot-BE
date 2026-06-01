/*
    Updates existing tenant AI runtime rows to the locally installed Ollama LLM model.
    This keeps the Job Description Drafting Agent aligned with the model expected by
    the current development environment.
*/
UPDATE dbo.TenantAiSettings
SET LlmModel = N'llama3.2',
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE LlmModel = N'llama3.1:8b';

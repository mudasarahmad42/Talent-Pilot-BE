using TalentPilot.Application.Abstractions;
using TalentPilot.Common.Results;

namespace TalentPilot.Application.Admin.AiSettings;

public interface IAdminAiSettingsService
{
    Result<AdminAiRuntimeResponse> GetRuntime();

    Result<AdminAiAgentListResponse> GetAgents();

    Result<AdminAiGuardrailsResponse> GetGuardrails();
}

public sealed class AdminAiSettingsService : IAdminAiSettingsService
{
    private readonly IAdminRuntimeSettings _runtimeSettings;

    public AdminAiSettingsService(IAdminRuntimeSettings runtimeSettings)
    {
        _runtimeSettings = runtimeSettings;
    }

    public Result<AdminAiRuntimeResponse> GetRuntime()
    {
        return Result<AdminAiRuntimeResponse>.Success(new AdminAiRuntimeResponse(
            _runtimeSettings.Provider,
            _runtimeSettings.LlmModel,
            _runtimeSettings.EmbeddingModel,
            _runtimeSettings.EmbeddingDimensions,
            "SQL Server",
            RuntimeEditable: false));
    }

    public Result<AdminAiAgentListResponse> GetAgents()
    {
        var agents = new[]
        {
            new AdminAiAgentDefinition("requirement-parser", "Requirement Parser", "Extracts structured hiring requirements from resource requests and job descriptions.", "Job request title, description, skills, seniority, location, and hiring context.", "Structured requirement profile.", "AI is advisory and does not approve or reject requests.", true),
            new AdminAiAgentDefinition("cv-parser", "CV Parser", "Parses DOCX resumes into candidate profile and matching evidence.", "DOCX text extracted server-side.", "Structured candidate profile and skill evidence.", "DOCX only for MVP; recruiters review extracted data.", true),
            new AdminAiAgentDefinition("bench-matching", "Bench Matching", "Recommends currently benched employees to PMO.", "Job requirement profile and active benched employee profiles.", "Ranked employee matches with fit evidence.", "PMO decides whether to refer an employee.", true),
            new AdminAiAgentDefinition("talent-rediscovery", "Talent Rediscovery", "Prioritizes previous similar-job candidates before external sourcing.", "Historical applications, interview outcomes, and requirement profile.", "Ranked warm candidates.", "Recruiters decide who to contact.", true),
            new AdminAiAgentDefinition("fit-explanation", "Fit Explanation", "Explains why an employee or candidate was recommended.", "Recommendation evidence, skills, experience, and gaps.", "Readable strengths, gaps, and confidence notes.", "Explanation supports human review only.", true),
            new AdminAiAgentDefinition("hiring-manager-decision-brief", "Hiring Manager Decision Brief", "Summarizes interview feedback and candidate context for final human review.", "Interview feedback, application history, and candidate profile.", "Decision brief for Hiring Manager.", "Hiring Manager owns the final decision.", true)
        };

        return Result<AdminAiAgentListResponse>.Success(new AdminAiAgentListResponse(agents.Length, agents));
    }

    public Result<AdminAiGuardrailsResponse> GetGuardrails()
    {
        return Result<AdminAiGuardrailsResponse>.Success(new AdminAiGuardrailsResponse(
            HumanReviewRequired: true,
            AutoRejectEnabled: false,
            "AI recommendations never auto-reject candidates or make final hiring decisions."));
    }
}

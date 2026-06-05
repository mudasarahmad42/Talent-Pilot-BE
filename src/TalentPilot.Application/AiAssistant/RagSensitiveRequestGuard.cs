namespace TalentPilot.Application.AiAssistant;

public static class RagSensitiveRequestGuard
{
    private static readonly string[] SecretTerms =
    [
        "api key",
        "app secret",
        "azure secret",
        "client secret",
        "connection string",
        "credential",
        "credentials",
        "entra secret",
        "microsoft graph secret",
        "password",
        "private key",
        "refresh token",
        "secret key",
        "token"
    ];

    private static readonly string[] DisclosureTerms =
    [
        "find",
        "forgot",
        "forgotten",
        "give",
        "get",
        "recover",
        "retrieve",
        "reveal",
        "send",
        "share",
        "show",
        "tell me",
        "what is",
        "where is"
    ];

    public static bool IsCredentialDisclosureRequest(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.ToLowerInvariant();
        return SecretTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal))
            && DisclosureTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
    }

    public static string BuildCredentialRefusal(string contextType)
    {
        var examples = RagAssistantContextTypes.Normalize(contextType) switch
        {
            RagAssistantContextTypes.PmoRequest => "bench fit, missing skills, request status, referral context, or presales handoff evidence",
            RagAssistantContextTypes.RecruiterCandidateFit => "candidate fit, CV evidence, ranking reasons, missing skills, or application status",
            RagAssistantContextTypes.HiringDecisionBrief => "candidate comparison, interview feedback, decision risks, strengths, or ranking evidence",
            _ => "Talent Pilot hiring workflow evidence, candidate context, job requests, rankings, or configuration summaries"
        };

        return $"That sounds like an admin vault job, not a chat-panel job. I cannot find, reveal, recover, or display client secrets, credentials, tokens, API keys, passwords, private keys, or connection strings. Ask a Tenant Admin to rotate or regenerate the credential in the authorized identity portal or secrets store, then update the approved configuration path. I can still help with {examples}.";
    }
}

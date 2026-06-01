namespace TalentPilot.Application.Abstractions;

public interface IAdminRuntimeSettings
{
    string Provider { get; }

    string LlmModel { get; }

    string EmbeddingModel { get; }

    int EmbeddingDimensions { get; }

    string VectorStore { get; }

    string OllamaBaseUrl { get; }
}

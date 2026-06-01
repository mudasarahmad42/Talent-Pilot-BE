using Microsoft.Extensions.Configuration;
using TalentPilot.Application.Abstractions;

namespace TalentPilot.Infrastructure.Runtime;

public sealed class AdminRuntimeSettings : IAdminRuntimeSettings
{
    public AdminRuntimeSettings(IConfiguration configuration)
    {
        var section = configuration.GetSection("TalentPilotRuntime");
        Provider = section["Provider"] ?? "Mock/Ollama";
        LlmModel = section["LlmModel"] ?? "llama3.2";
        EmbeddingModel = section["EmbeddingModel"] ?? "nomic-embed-text";
        EmbeddingDimensions = int.TryParse(section["EmbeddingDimensions"], out var dimensions)
            ? dimensions
            : 768;
        VectorStore = section["VectorStore"] ?? "SqlServerVector";
        OllamaBaseUrl = section["OllamaBaseUrl"] ?? "http://localhost:11434";
    }

    public string Provider { get; }

    public string LlmModel { get; }

    public string EmbeddingModel { get; }

    public int EmbeddingDimensions { get; }

    public string VectorStore { get; }

    public string OllamaBaseUrl { get; }
}

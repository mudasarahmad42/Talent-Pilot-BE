using System.IO.Compression;
using System.Text;
using TalentPilot.Application.Abstractions;
using TalentPilot.Application.Ai;

namespace TalentPilot.Tests.Ai;

public sealed class CvParserAgentTests
{
    [Fact]
    public async Task ParseAsync_ExtractsCandidateProfileFromDocx()
    {
        var modelProvider = new CapturingModelProvider("""
            {
              "displayName": "Sara Khan",
              "email": "sara.khan@example.com",
              "phone": "+92 300 111 2222",
              "currentDesignation": "Senior React Developer",
              "currentCompany": "Product Studio",
              "experienceYears": 6.5,
              "skills": ["React", "TypeScript", "Azure", "SQL Server"],
              "universityName": "University of Lahore",
              "degreeName": "BS Computer Science",
              "graduationYear": 2018,
              "summary": "Sara Khan is a Senior React Developer with 6.5 years of experience and React, TypeScript, Azure, and SQL Server evidence. Recruiter review is required."
            }
            """);
        var logger = new CapturingRunLogger();
        var agent = new CvParserAgent(modelProvider, new StaticRuntimeSettingsResolver(), logger);
        var content = CreateDocx(
            "Sara Khan",
            "Senior React Developer",
            "sara.khan@example.com",
            "+92 300 111 2222",
            "Current Company: Product Studio",
            "6.5 years experience",
            "Skills: React, TypeScript, Azure, SQL Server",
            "University of Lahore",
            "BS Computer Science 2018");

        var result = await agent.ParseAsync(
            StaticRuntimeSettingsResolver.TenantId,
            new CvParseRequest("sara-khan.docx", content),
                CancellationToken.None);

        Assert.Equal("cv-parser", logger.AgentId);
        Assert.Equal(CvParserAgent.AgentId, modelProvider.LastRequest?.AgentId);
        Assert.Contains("Return strict JSON only", modelProvider.LastRequest?.Prompt);
        Assert.Equal("Sara Khan", result.DisplayName);
        Assert.Equal("sara.khan@example.com", result.Email);
        Assert.Equal("Senior React Developer", result.CurrentDesignation);
        Assert.Equal("Product Studio", result.CurrentCompany);
        Assert.Equal(6.5m, result.ExperienceYears);
        Assert.Contains("React", result.Skills);
        Assert.Contains("TypeScript", result.Skills);
        Assert.Contains("Azure", result.Skills);
        Assert.Contains("University of Lahore", result.UniversityName);
        Assert.Contains("BS Computer Science", result.DegreeName);
        Assert.Equal(2018, result.GraduationYear);
        Assert.True(logger.Succeeded);
        Assert.False(logger.Failed);
    }

    [Fact]
    public async Task ParseAsync_WhenDocxCannotBeRead_LogsFailure()
    {
        var logger = new CapturingRunLogger();
        var agent = new CvParserAgent(new CapturingModelProvider("{}"), new StaticRuntimeSettingsResolver(), logger);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            agent.ParseAsync(
                StaticRuntimeSettingsResolver.TenantId,
                new CvParseRequest("broken.docx", Encoding.UTF8.GetBytes("not a docx")),
                CancellationToken.None));

        Assert.True(logger.Failed);
        Assert.Contains("Directory", logger.OutputSummary);
    }

    [Fact]
    public async Task ParseAsync_WhenModelFails_LogsFailure()
    {
        var logger = new CapturingRunLogger();
        var agent = new CvParserAgent(new ThrowingModelProvider(), new StaticRuntimeSettingsResolver(), logger);
        var content = CreateDocx("Sara Khan", "Senior React Developer", "sara.khan@example.com");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.ParseAsync(
                StaticRuntimeSettingsResolver.TenantId,
                new CvParseRequest("sara-khan.docx", content),
                CancellationToken.None));

        Assert.True(logger.Failed);
        Assert.Contains("model offline", logger.OutputSummary);
    }

    private static byte[] CreateDocx(params string[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write("""
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body>
                """);
            foreach (var paragraph in paragraphs)
            {
                writer.Write("<w:p><w:r><w:t>");
                writer.Write(paragraph);
                writer.Write("</w:t></w:r></w:p>");
            }

            writer.Write("""
                  </w:body>
                </w:document>
                """);
        }

        return stream.ToArray();
    }

    private sealed class StaticRuntimeSettingsResolver : IAiRuntimeSettingsResolver
    {
        public static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        public Task<AiRuntimeSettingsSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new AiRuntimeSettingsSnapshot(
                TenantId,
                "Mock/Ollama",
                "llama3.2",
                "nomic-embed-text",
                768,
                "SqlServerVector",
                "http://localhost:11434"));
        }
    }

    private sealed class CapturingModelProvider : IAiModelProvider
    {
        private readonly string _response;

        public CapturingModelProvider(string response)
        {
            _response = response;
        }

        public AiPromptRequest? LastRequest { get; private set; }

        public Task<string> GenerateAsync(AiPromptRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_response);
        }
    }

    private sealed class ThrowingModelProvider : IAiModelProvider
    {
        public Task<string> GenerateAsync(AiPromptRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("model offline");
        }
    }

    private sealed class CapturingRunLogger : IAiAgentRunLogger
    {
        public bool Succeeded { get; private set; }

        public bool Failed { get; private set; }

        public string AgentId { get; private set; } = string.Empty;

        public string OutputSummary { get; private set; } = string.Empty;

        public Task<Guid> StartAsync(AiAgentRunStart run, CancellationToken cancellationToken)
        {
            AgentId = run.AgentId;
            return Task.FromResult(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        }

        public Task SucceedAsync(
            Guid tenantId,
            Guid runId,
            string outputSummary,
            IReadOnlyDictionary<string, string> metadata,
            CancellationToken cancellationToken)
        {
            Succeeded = true;
            OutputSummary = outputSummary;
            return Task.CompletedTask;
        }

        public Task FailAsync(
            Guid tenantId,
            Guid runId,
            string outputSummary,
            IReadOnlyDictionary<string, string> metadata,
            CancellationToken cancellationToken)
        {
            Failed = true;
            OutputSummary = outputSummary;
            return Task.CompletedTask;
        }
    }
}

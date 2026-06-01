using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TalentPilot.Application.Abstractions;

namespace TalentPilot.Application.Ai;

public sealed class CvParserAgent : ICvParserAgent
{
    public const string AgentId = "cv-parser";

    private static readonly string[] KnownSkills =
    [
        ".NET",
        "React",
        "Angular",
        "Azure",
        "SQL Server",
        "Python",
        "DevOps",
        "Node.js",
        "TypeScript",
        "JavaScript",
        "AWS",
        "Docker",
        "Kubernetes",
        "Terraform"
    ];

    private readonly IAiRuntimeSettingsResolver _settingsResolver;
    private readonly IAiAgentRunLogger _runLogger;

    public CvParserAgent(
        IAiRuntimeSettingsResolver settingsResolver,
        IAiAgentRunLogger runLogger)
    {
        _settingsResolver = settingsResolver;
        _runLogger = runLogger;
    }

    public async Task<CvParseResult> ParseAsync(
        Guid tenantId,
        CvParseRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Content.Length == 0)
        {
            throw new InvalidOperationException("CV file is empty.");
        }

        var settings = await _settingsResolver.GetCurrentAsync(cancellationToken);
        var generatedAt = DateTimeOffset.UtcNow;
        var inputHash = AiTextHasher.HashText($"{request.FileName}:{Convert.ToHexString(request.Content[..Math.Min(request.Content.Length, 128)])}:{request.Content.Length}");
        var runId = await _runLogger.StartAsync(
            new AiAgentRunStart(
                tenantId,
                AgentId,
                "CandidateCv",
                Guid.NewGuid(),
                settings.LlmModel,
                settings.EmbeddingModel,
                inputHash,
                new Dictionary<string, string>
                {
                    ["purpose"] = "candidate-cv-parse",
                    ["fileName"] = request.FileName,
                    ["humanReviewRequired"] = "true"
                }),
            cancellationToken);

        try
        {
            var text = ExtractDocxText(request.Content);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("CV parser could not extract text from the DOCX file.");
            }

            var lines = text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => line.Length > 1)
                .ToArray();

            var email = MatchValue(text, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase);
            var phone = MatchValue(text, @"(?:\+?\d[\d\s().-]{7,}\d)");
            var displayName = ExtractDisplayName(lines, email, phone);
            var designation = ExtractDesignation(lines);
            var company = ExtractLabeledValue(lines, "current company", "company", "employer");
            var experience = ExtractExperience(text);
            var skills = KnownSkills
                .Where(skill => Regex.IsMatch(text, $@"(?<![A-Za-z0-9]){Regex.Escape(skill)}(?![A-Za-z0-9])", RegexOptions.IgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(skill => skill)
                .ToArray();
            var university = lines.FirstOrDefault(line => line.Contains("university", StringComparison.OrdinalIgnoreCase));
            var degree = lines.FirstOrDefault(line =>
                Regex.IsMatch(line, @"\b(BS|B\.S\.|BSc|Bachelor|MS|M\.S\.|MSc|Master|MBA)\b", RegexOptions.IgnoreCase));
            var graduationYear = ExtractYear(text);
            var summary = BuildSummary(displayName, designation, experience, skills);

            await _runLogger.SucceedAsync(
                tenantId,
                runId,
                summary,
                new Dictionary<string, string>
                {
                    ["extractedSkills"] = string.Join(", ", skills),
                    ["hasEmail"] = (!string.IsNullOrWhiteSpace(email)).ToString(CultureInfo.InvariantCulture),
                    ["humanReviewRequired"] = "true"
                },
                cancellationToken);

            return new CvParseResult(
                runId,
                settings.LlmModel,
                generatedAt,
                text,
                displayName,
                email,
                phone,
                designation,
                company,
                experience,
                skills,
                university,
                degree,
                graduationYear,
                summary);
        }
        catch (Exception ex)
        {
            await _runLogger.FailAsync(
                tenantId,
                runId,
                ex.Message.Length <= 900 ? ex.Message : ex.Message[..900],
                new Dictionary<string, string>
                {
                    ["humanReviewRequired"] = "true"
                },
                cancellationToken);
            throw;
        }
    }

    private static string ExtractDocxText(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.GetEntry("word/document.xml")
            ?? throw new InvalidOperationException("Only DOCX files are supported by the CV Parser Agent.");

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        var xml = XDocument.Parse(reader.ReadToEnd());
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paragraphs = xml.Descendants(w + "p")
            .Select(paragraph => string.Concat(paragraph.Descendants(w + "t").Select(text => text.Value)).Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join('\n', paragraphs);
    }

    private static string? MatchValue(string text, string pattern, RegexOptions options = RegexOptions.None)
    {
        var match = Regex.Match(text, pattern, options);
        return match.Success ? match.Value.Trim() : null;
    }

    private static string? ExtractDisplayName(IReadOnlyList<string> lines, string? email, string? phone)
    {
        return lines.FirstOrDefault(line =>
        {
            if (!string.IsNullOrWhiteSpace(email) && line.Contains(email, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(phone) && line.Contains(phone, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return Regex.IsMatch(line, @"^[A-Za-z][A-Za-z\s.'-]{2,60}$")
                && line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 5;
        });
    }

    private static string? ExtractDesignation(IReadOnlyList<string> lines)
    {
        return lines.FirstOrDefault(line =>
            Regex.IsMatch(line, @"\b(Engineer|Developer|Architect|Consultant|Manager|Designer|Analyst|Specialist)\b", RegexOptions.IgnoreCase));
    }

    private static string? ExtractLabeledValue(IReadOnlyList<string> lines, params string[] labels)
    {
        foreach (var line in lines)
        {
            foreach (var label in labels)
            {
                var match = Regex.Match(line, $@"^{Regex.Escape(label)}\s*[:|-]\s*(.+)$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
        }

        return null;
    }

    private static decimal? ExtractExperience(string text)
    {
        var match = Regex.Match(text, @"(\d+(?:\.\d+)?)\+?\s*(?:years|yrs)\b", RegexOptions.IgnoreCase);
        return match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var years)
            ? years
            : null;
    }

    private static int? ExtractYear(string text)
    {
        var matches = Regex.Matches(text, @"\b(19[7-9]\d|20[0-4]\d)\b");
        return matches
            .Select(match => int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) ? year : (int?)null)
            .Where(year => year.HasValue)
            .OrderByDescending(year => year)
            .FirstOrDefault();
    }

    private static string BuildSummary(string? name, string? designation, decimal? experience, IReadOnlyList<string> skills)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(name))
        {
            parts.Add(name);
        }

        if (!string.IsNullOrWhiteSpace(designation))
        {
            parts.Add(designation);
        }

        if (experience.HasValue)
        {
            parts.Add($"{experience.Value:0.#} years experience");
        }

        if (skills.Count > 0)
        {
            parts.Add($"skills: {string.Join(", ", skills.Take(8))}");
        }

        return parts.Count == 0
            ? "CV parsed. Recruiter review is required before using extracted profile data."
            : $"CV parsed: {string.Join("; ", parts)}. Recruiter review is required.";
    }
}

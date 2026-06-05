using System.Text.RegularExpressions;

namespace TalentPilot.Application.AiAssistant;

public static class RagCitationUsage
{
    private static readonly Regex CitationLabelPattern = new(
        @"(?<![A-Za-z0-9])\[?(C\d+)\]?(?![A-Za-z0-9])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<RagCitationDraft> FilterReferenced(
        IReadOnlyList<RagCitationDraft> citations,
        string answer)
    {
        if (citations.Count == 0 || string.IsNullOrWhiteSpace(answer))
        {
            return Array.Empty<RagCitationDraft>();
        }

        var referencedLabels = CitationLabelPattern
            .Matches(answer)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (referencedLabels.Count == 0)
        {
            return Array.Empty<RagCitationDraft>();
        }

        return citations
            .Where(citation => referencedLabels.Contains(citation.Label))
            .ToArray();
    }
}

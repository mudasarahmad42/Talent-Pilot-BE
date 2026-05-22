namespace TalentPilot.Application.Operations;

public static class OperationsBenchMatchScoring
{
    public static int CalculateScore(IReadOnlyCollection<string> requiredSkills, IReadOnlyCollection<string> employeeSkills)
    {
        if (requiredSkills.Count == 0)
        {
            return 70;
        }

        var matchedCount = employeeSkills
            .Intersect(requiredSkills, StringComparer.OrdinalIgnoreCase)
            .Count();

        var skillFit = (int)Math.Round(40m * matchedCount / requiredSkills.Count, MidpointRounding.AwayFromZero);
        var completeMatchBonus = matchedCount == requiredSkills.Count ? 5 : 0;
        return Math.Clamp(55 + skillFit + completeMatchBonus, 0, 100);
    }

    public static string BuildExplanation(
        IReadOnlyCollection<string> requiredSkills,
        IReadOnlyCollection<string> employeeSkills,
        int currentAllocationPercent)
    {
        var matchedSkills = employeeSkills
            .Intersect(requiredSkills, StringComparer.OrdinalIgnoreCase)
            .OrderBy(skill => skill, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var skillSummary = requiredSkills.Count == 0
            ? "No required skills were configured on the job request."
            : $"Matched {matchedSkills.Length} of {requiredSkills.Count} requested skills: {FormatSkills(matchedSkills)}.";

        return $"{skillSummary} Available and benched with {currentAllocationPercent}% active allocation.";
    }

    private static string FormatSkills(IReadOnlyList<string> skills)
    {
        return skills.Count == 0 ? "none" : string.Join(", ", skills);
    }
}

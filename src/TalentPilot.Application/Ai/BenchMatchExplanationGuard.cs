using TalentPilot.Application.Operations;

namespace TalentPilot.Application.Ai;

internal static class BenchMatchExplanationGuard
{
    public static string Apply(
        OperationsBenchEmployee employee,
        OperationsBenchMatchingContext context,
        string explanation)
    {
        return Apply(employee, context.JobRequest, context.ExperienceMinYears, explanation);
    }

    public static string Apply(
        OperationsBenchEmployee employee,
        OperationsJobRequest request,
        string explanation)
    {
        return Apply(employee, request, InferMinimumExperienceYears(request.Experience), explanation);
    }

    private static string Apply(
        OperationsBenchEmployee employee,
        OperationsJobRequest request,
        decimal? experienceMinYears,
        string explanation)
    {
        var sanitized = RemoveInvalidExperienceShortfallClaims(employee, experienceMinYears, explanation);
        var mismatchPreface = BuildSkillMismatchPreface(employee, request);
        if (string.IsNullOrWhiteSpace(mismatchPreface) ||
            sanitized.StartsWith(mismatchPreface, StringComparison.OrdinalIgnoreCase))
        {
            return sanitized;
        }

        return $"{mismatchPreface} {sanitized}";
    }

    private static decimal? InferMinimumExperienceYears(string? experience)
    {
        if (string.IsNullOrWhiteSpace(experience))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            experience,
            @"\d+(?:\.\d+)?");
        return match.Success && decimal.TryParse(match.Value, out var value) ? value : null;
    }

    private static string RemoveInvalidExperienceShortfallClaims(
        OperationsBenchEmployee employee,
        decimal? experienceMinYears,
        string explanation)
    {
        var trimmed = explanation.Trim();
        if (!employee.ExperienceYears.HasValue ||
            !experienceMinYears.HasValue ||
            employee.ExperienceYears.Value < experienceMinYears.Value)
        {
            return trimmed;
        }

        var normalized = trimmed.ToLowerInvariant();
        if (!ContainsAny(normalized, "less than", "below", "under", "short of"))
        {
            return ReplaceInvalidLimitedExperienceWording(trimmed);
        }

        const string decimalPlaceholder = "__decimal__";
        var protectedText = System.Text.RegularExpressions.Regex.Replace(
            trimmed,
            @"(\d)\.(\d)",
            $"$1{decimalPlaceholder}$2");
        var sentences = protectedText
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(sentence => sentence.Replace(decimalPlaceholder, ".", StringComparison.Ordinal))
            .Where(sentence => !ContainsInvalidExperienceShortfall(sentence))
            .Select(ReplaceInvalidLimitedExperienceWording)
            .Select(sentence => sentence.EndsWith(".", StringComparison.Ordinal) ? sentence : $"{sentence}.")
            .ToArray();

        return sentences.Length == 0 ? trimmed : string.Join(' ', sentences);
    }

    private static bool ContainsInvalidExperienceShortfall(string sentence)
    {
        var text = sentence.ToLowerInvariant();
        return ContainsAny(text, "less than", "below", "under", "short of") &&
               ContainsAny(text, "experience", "year", "years", "required", "requirement");
    }

    private static string ReplaceInvalidLimitedExperienceWording(string sentence)
    {
        return sentence
            .Replace("limited experience and skill gaps", "limited required-skill evidence and skill gaps", StringComparison.OrdinalIgnoreCase)
            .Replace("limited experience", "limited required-skill evidence", StringComparison.OrdinalIgnoreCase);
    }

    private static string? BuildSkillMismatchPreface(
        OperationsBenchEmployee employee,
        OperationsJobRequest request)
    {
        if (employee.MissingSkills.Count == 0)
        {
            return null;
        }

        var primaryFocus = InferPrimaryProfileFocus(employee);
        if (string.IsNullOrWhiteSpace(primaryFocus) ||
            request.Skills.Any(skill => IsSameSkill(skill, primaryFocus)))
        {
            return null;
        }

        var requestedSkills = JoinReadableList(request.Skills);
        var matchedSkills = employee.MatchedSkills.Count == 0
            ? "no direct requested skills"
            : JoinReadableList(employee.MatchedSkills);
        var missingSkills = JoinReadableList(employee.MissingSkills);
        var experiencePhrase = employee.ExperienceYears.HasValue
            ? $" and {FormatYears(employee.ExperienceYears)} years overall"
            : string.Empty;
        var rolePhrase = string.IsNullOrWhiteSpace(employee.Designation)
            ? string.Empty
            : $" ({employee.Designation})";

        return $"{employee.DisplayName}'s profile is primarily {primaryFocus}{rolePhrase}; while they have backend/project experience{experiencePhrase}, this request is centered on {requestedSkills}, and current tenant evidence only supports {matchedSkills}. They are not preferred until missing {missingSkills} evidence is validated.";
    }

    private static string? InferPrimaryProfileFocus(OperationsBenchEmployee employee)
    {
        string[] knownFocusAreas =
        [
            "Java",
            ".NET",
            "Python",
            "React",
            "Angular",
            "Node.js",
            "PHP",
            "Ruby",
            "Go",
            "DevOps",
            "QA",
            "Data"
        ];

        var designation = employee.Designation ?? string.Empty;
        var focusFromDesignation = knownFocusAreas.FirstOrDefault(focus => ContainsSkillToken(designation, focus));
        if (!string.IsNullOrWhiteSpace(focusFromDesignation))
        {
            return focusFromDesignation;
        }

        return employee.Skills
            .GroupBy(skill => knownFocusAreas.FirstOrDefault(focus => IsSameSkill(skill, focus)) ?? string.Empty)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();
    }

    private static string JoinReadableList(IReadOnlyList<string> values)
    {
        var cleaned = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return cleaned.Length switch
        {
            0 => "the requested skills",
            1 => cleaned[0],
            2 => $"{cleaned[0]} and {cleaned[1]}",
            _ => $"{string.Join(", ", cleaned.Take(cleaned.Length - 1))}, and {cleaned[^1]}"
        };
    }

    private static bool IsSameSkill(string value, string skill)
    {
        return ContainsSkillToken(value, skill);
    }

    private static bool ContainsSkillToken(string value, string skill)
    {
        var normalizedValue = NormalizeSkillToken(value);
        var normalizedSkill = NormalizeSkillToken(skill);
        return normalizedValue == normalizedSkill ||
               normalizedValue.StartsWith($"{normalizedSkill} ", StringComparison.Ordinal) ||
               normalizedValue.EndsWith($" {normalizedSkill}", StringComparison.Ordinal) ||
               normalizedValue.Contains($" {normalizedSkill} ", StringComparison.Ordinal);
    }

    private static string NormalizeSkillToken(string? value)
    {
        return new string((value ?? string.Empty)
                .ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
                .ToArray())
            .Trim();
    }

    private static string FormatYears(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.#") : "Not recorded";
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }
}

using System.Globalization;
using E3A.Domain.Publishing;

namespace E3A.Application.Publishing.Shared;

public static class SemanticVersionCalculator
{
    // Three dot-separated non-negative integers; anything else is not a semantic version we produced.
    private const int ComponentCount = 3;
    private const string InitialSemanticVersion = "1.0.0";

    public static string Next(string? previousSemanticVersion, VersionIncrement increment)
    {
        if (string.IsNullOrWhiteSpace(previousSemanticVersion))
        {
            return InitialSemanticVersion;
        }

        var components = previousSemanticVersion.Split('.');

        if (components.Length != ComponentCount)
        {
            return InitialSemanticVersion;
        }

        if (!TryParse(components[0], out var major) || !TryParse(components[1], out var minor) || !TryParse(components[2], out var patch))
        {
            return InitialSemanticVersion;
        }

        return increment switch
        {
            VersionIncrement.Minor => Format(major, minor + 1, 0),
            VersionIncrement.Major => Format(major + 1, 0, 0),
            _ => Format(major, minor, patch + 1),
        };
    }

    private static bool TryParse(string component, out int value)
    {
        return int.TryParse(component, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static string Format(int major, int minor, int patch)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}.{patch}");
    }
}

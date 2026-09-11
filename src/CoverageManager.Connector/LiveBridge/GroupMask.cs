using System.Text.RegularExpressions;

namespace CoverageManager.Connector.LiveBridge;

/// <summary>
/// MT5-style group masks for <see cref="IMT5Api.GetUserLogins"/>: patterns separated by commas or semicolons, <c>*</c> and
/// <c>?</c> wildcards, a leading <c>!</c> excludes. A group is selected when it matches at least one include pattern (or
/// there is none) and no exclude pattern. Blank or <c>*</c> selects everything. Case-insensitive.
/// </summary>
public static class GroupMask
{
    public static bool Matches(string? mask, string? group)
    {
        if (string.IsNullOrWhiteSpace(mask)) return true;
        var value = group ?? "";
        var includes = new List<Regex>();
        var excludes = new List<Regex>();
        foreach (var raw in mask.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var pattern = raw.Trim();
            if (pattern.Length == 0) continue;
            var exclude = pattern[0] == '!';
            if (exclude) pattern = pattern[1..].Trim();
            if (pattern.Length == 0) continue;
            (exclude ? excludes : includes).Add(ToRegex(pattern));
        }
        if (excludes.Any(r => r.IsMatch(value))) return false;
        return includes.Count == 0 || includes.Any(r => r.IsMatch(value));
    }

    private static Regex ToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".");
        return new Regex("^" + escaped + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}

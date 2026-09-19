using System.Text.RegularExpressions;

namespace TrainingLoadAnalyzer.Web.Tests.Theme;

/// <summary>
///   FR-005 and SC-009: colour lives in the theme, and nowhere else.
/// </summary>
/// <remarks>
///   <para>
///     This is the rule that erodes. One hard-coded <c>#666</c> at a call site and the dark scheme
///     has a permanent light-grey artifact that nobody notices until someone with a dark device
///     opens the page — the failure is invisible to whoever introduced it. The scan costs a few
///     lines and makes it impossible to land unnoticed (007 research R7).
///   </para>
///   <para>
///     It reads the repository, so it also covers files no test renders.
///   </para>
/// </remarks>
public class ColourDisciplineTests
{
    /// <summary>Hex literals, the functional notations, and the CSS named colours worth catching.</summary>
    private static readonly Regex ColourLiteral = new(
        @"#[0-9a-fA-F]{3,8}\b|\brgba?\s*\(|\bhsla?\s*\(|\b(?:red|blue|green|yellow|orange|purple|pink|brown|grey|gray|black|white|lightyellow|lightgrey|lightgray|darkgrey|darkgray|silver|gold|teal|navy|olive|maroon|lime|aqua|fuchsia)\b",
        RegexOptions.Compiled);

    /// <summary>
    ///   The one directory allowed to name a colour. Everything else consumes the theme.
    /// </summary>
    private const string ThemeDirectory = "Theme";

    [Fact]
    public void No_stylesheet_or_component_names_a_colour_outside_the_theme()
    {
        var offenders = new List<string>();

        foreach (var file in WebProjectFiles())
        {
            var relative = Path.GetRelativePath(WebProjectRoot(), file);

            if (relative.Split(Path.DirectorySeparatorChar).Contains(ThemeDirectory))
            {
                continue;
            }

            foreach (var (line, number) in CodeLines(File.ReadAllLines(file)))
            {
                var match = ColourLiteral.Match(line);

                if (match.Success)
                {
                    offenders.Add($"{relative}:{number}  {match.Value}  |  {line.Trim()}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Colour must be defined in Theme/ and nowhere else (FR-005, SC-009). Found:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    ///   The lines that actually declare something, with comments stripped out.
    /// </summary>
    /// <remarks>
    ///   Block state has to be tracked rather than testing each line for a leading marker: several
    ///   comments in this project explain across multiple lines why a colour is the value it is,
    ///   and a continuation line looks like ordinary code. Flagging prose would only teach everyone
    ///   to stop writing the reasoning down, which is the opposite of what this codebase wants.
    /// </remarks>
    private static IEnumerable<(string Line, int Number)> CodeLines(string[] lines)
    {
        var inBlock = false;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.TrimStart();

            if (inBlock)
            {
                if (trimmed.Contains("*/", StringComparison.Ordinal) || trimmed.Contains("*@", StringComparison.Ordinal))
                {
                    inBlock = false;
                }

                continue;
            }

            if (trimmed.StartsWith("/*", StringComparison.Ordinal) || trimmed.StartsWith("@*", StringComparison.Ordinal))
            {
                if (!trimmed.Contains("*/", StringComparison.Ordinal) && !trimmed.Contains("*@", StringComparison.Ordinal))
                {
                    inBlock = true;
                }

                continue;
            }

            if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("*", StringComparison.Ordinal))
            {
                continue;
            }

            yield return (line, index + 1);
        }
    }

    private static IEnumerable<string> WebProjectFiles() =>
        new[] { "*.css", "*.razor" }
            .SelectMany(pattern => Directory.EnumerateFiles(WebProjectRoot(), pattern, SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order();

    /// <summary>
    ///   Walks up from the test assembly to the repository root. The alternative — a path relative
    ///   to the working directory — breaks the moment the suite is run from somewhere else.
    /// </summary>
    private static string WebProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return Path.Combine(directory.FullName, "src", "TrainingLoadAnalyzer.Web");
    }
}

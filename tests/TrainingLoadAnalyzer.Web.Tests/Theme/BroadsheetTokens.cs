using System.Globalization;
using System.Text.RegularExpressions;

namespace TrainingLoadAnalyzer.Web.Tests.Theme;

/// <summary>
///   The design tokens, read out of the stylesheet the application actually ships.
/// </summary>
/// <remarks>
///   <para>
///     Feature 008 replaced the MudBlazor theme object with a vendored stylesheet, so the palette
///     tests lost the C# type they used to reflect over (008 research R7). Parsing the stylesheet
///     keeps what those tests were actually for: the contrast audit has a RED state, and a nudged
///     colour cannot pass unnoticed.
///   </para>
///   <para>
///     Reading the file rather than a copy of its values is the point. A table of expected hex
///     codes maintained beside the sheet would agree with itself forever while disagreeing with
///     what the browser paints.
///   </para>
/// </remarks>
internal static class BroadsheetTokens
{
    /// <summary>
    ///   The path segment that exempts this sheet from <see cref="ColourDisciplineTests"/>. Spelled
    ///   exactly so — that test's check is an ordinal <c>Contains</c> (008 research R2).
    /// </summary>
    public static string StylesheetPath => Path.Combine(
        RepositoryRoot(), "src", "TrainingLoadAnalyzer.Web", "wwwroot", "Theme", "broadsheet.css");

    public static bool StylesheetExists => File.Exists(StylesheetPath);

    /// <summary>The tokens under <c>:root</c>.</summary>
    public static TokenSet Light => Read("light");

    /// <summary>
    ///   The tokens a device set to a dark appearance resolves — the light set with the
    ///   <c>prefers-color-scheme: dark</c> block applied over it, which is what the browser does.
    /// </summary>
    public static TokenSet Dark => Read("dark");

    /// <summary>Only what the dark media query itself redefines, with nothing inherited.</summary>
    public static IReadOnlyDictionary<string, string> DarkOverrides =>
        DeclarationsIn(DarkBlock(File.ReadAllText(StylesheetPath)));

    private static TokenSet Read(string scheme)
    {
        var css = File.ReadAllText(StylesheetPath);
        var declarations = new Dictionary<string, string>(DeclarationsIn(RootBlock(css)), StringComparer.Ordinal);

        if (scheme == "dark")
        {
            foreach (var (token, value) in DeclarationsIn(DarkBlock(css)))
            {
                declarations[token] = value;
            }
        }

        return new TokenSet(scheme, declarations);
    }

    /// <summary>The first top-level <c>:root { … }</c>, which is the light token block.</summary>
    private static string RootBlock(string css)
    {
        var start = css.IndexOf(":root", StringComparison.Ordinal);

        return start < 0 ? string.Empty : BlockAt(css, css.IndexOf('{', start));
    }

    /// <summary>The <c>:root</c> inside the dark media query, or empty when there is none.</summary>
    private static string DarkBlock(string css)
    {
        var query = Regex.Match(css, @"@media[^{]*prefers-color-scheme\s*:\s*dark[^{]*");

        if (!query.Success)
        {
            return string.Empty;
        }

        var media = BlockAt(css, css.IndexOf('{', query.Index + query.Length - 1));
        var root = media.IndexOf(":root", StringComparison.Ordinal);

        return root < 0 ? string.Empty : BlockAt(media, media.IndexOf('{', root));
    }

    /// <summary>The body of the brace-delimited block opening at <paramref name="open"/>.</summary>
    private static string BlockAt(string css, int open)
    {
        if (open < 0)
        {
            return string.Empty;
        }

        var depth = 0;

        for (var i = open; i < css.Length; i++)
        {
            if (css[i] == '{')
            {
                depth++;
            }
            else if (css[i] == '}' && --depth == 0)
            {
                return css[(open + 1)..i];
            }
        }

        return string.Empty;
    }

    private static IReadOnlyDictionary<string, string> DeclarationsIn(string block)
    {
        var declarations = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match match in Regex.Matches(WithoutComments(block), @"(--[A-Za-z0-9-]+)\s*:\s*([^;]+);"))
        {
            declarations[match.Groups[1].Value] = match.Groups[2].Value.Trim();
        }

        return declarations;
    }

    /// <summary>
    ///   Comments are stripped before declarations are read. Several token comments in this sheet
    ///   explain across multiple lines why a value is what it is, and one of them names a colour.
    /// </summary>
    private static string WithoutComments(string css) =>
        Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("The repository root is not above the test assembly.");
    }
}

/// <summary>One scheme's resolved custom properties.</summary>
internal sealed class TokenSet(string scheme, IReadOnlyDictionary<string, string> declarations)
{
    public string Scheme => scheme;

    public IEnumerable<string> Names => declarations.Keys;

    public bool Declares(string token) => declarations.ContainsKey(token);

    public string Raw(string token) =>
        declarations.TryGetValue(token, out var value)
            ? value
            : throw new KeyNotFoundException($"'{token}' is not declared in the {scheme} scheme.");

    /// <summary>
    ///   A CSS colour expression as the opaque <c>#rrggbb</c> a reader would actually see, with any
    ///   transparency composited over the scheme's own page background.
    /// </summary>
    /// <remarks>
    ///   Compositing is not a convenience. The muted greys in this sheet are written as
    ///   <c>color-mix(… 70%, transparent)</c>, so their measured contrast depends on what they sit
    ///   on; treating the unblended ink as the foreground would report a ratio nobody sees.
    /// </remarks>
    public string Resolve(string expression) => Resolve(expression, Raw("--color-bg"));

    /// <summary>The same, composited over an explicit background rather than the page.</summary>
    public string Resolve(string expression, string over)
    {
        var colour = Parse(expression);

        if (colour.Alpha >= 0.999)
        {
            return Hex(colour);
        }

        var ground = Parse(over);

        return Hex(new Rgba(
            (colour.Red * colour.Alpha) + (ground.Red * (1 - colour.Alpha)),
            (colour.Green * colour.Alpha) + (ground.Green * (1 - colour.Alpha)),
            (colour.Blue * colour.Alpha) + (ground.Blue * (1 - colour.Alpha)),
            1));
    }

    /// <summary>Ink at a stated opacity, the form this sheet's muted text takes.</summary>
    public string Ink(int percent) =>
        Resolve($"color-mix(in srgb, var(--color-text) {percent}%, transparent)");

    private readonly record struct Rgba(double Red, double Green, double Blue, double Alpha);

    private Rgba Parse(string expression)
    {
        var value = expression.Trim();

        if (value.StartsWith("var(", StringComparison.Ordinal))
        {
            return Parse(Raw(value[4..value.LastIndexOf(')')].Trim()));
        }

        if (value.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            return new Rgba(0, 0, 0, 0);
        }

        if (value.StartsWith("color-mix(", StringComparison.Ordinal))
        {
            return Mix(value[10..value.LastIndexOf(')')]);
        }

        if (value.StartsWith('#'))
        {
            return FromHex(value);
        }

        throw new FormatException($"'{expression}' is not a colour this test can read.");
    }

    /// <summary>
    ///   <c>color-mix(in srgb, …)</c>, mixed in premultiplied alpha as the specification says — so
    ///   mixing a colour with <c>transparent</c> yields that colour at the stated opacity.
    /// </summary>
    private Rgba Mix(string arguments)
    {
        var parts = SplitTopLevel(arguments);

        if (parts.Count != 3 || !parts[0].Trim().Equals("in srgb", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"color-mix({arguments}) is not a form this test can read.");
        }

        var (first, firstShare) = Component(parts[1]);
        var (second, secondShare) = Component(parts[2]);

        firstShare ??= 1 - (secondShare ?? 0.5);
        secondShare ??= 1 - firstShare.Value;

        var alpha = (first.Alpha * firstShare.Value) + (second.Alpha * secondShare.Value);

        if (alpha == 0)
        {
            return new Rgba(0, 0, 0, 0);
        }

        double Channel(Func<Rgba, double> of) =>
            ((of(first) * first.Alpha * firstShare.Value) + (of(second) * second.Alpha * secondShare.Value)) / alpha;

        return new Rgba(Channel(c => c.Red), Channel(c => c.Green), Channel(c => c.Blue), alpha);
    }

    private (Rgba Colour, double? Share) Component(string argument)
    {
        var text = argument.Trim();
        var percent = Regex.Match(text, @"\s(\d+(?:\.\d+)?)%$");

        return percent.Success
            ? (Parse(text[..percent.Index]), double.Parse(percent.Groups[1].Value, CultureInfo.InvariantCulture) / 100)
            : (Parse(text), null);
    }

    /// <summary>Splits on commas that are not inside a nested function call.</summary>
    private static List<string> SplitTopLevel(string arguments)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < arguments.Length; i++)
        {
            switch (arguments[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ',' when depth == 0:
                    parts.Add(arguments[start..i]);
                    start = i + 1;
                    break;
                default:
                    break;
            }
        }

        parts.Add(arguments[start..]);

        return parts;
    }

    private static Rgba FromHex(string hex)
    {
        var value = hex.TrimStart('#');

        if (value.Length is 3 or 4)
        {
            value = string.Concat(value.Select(c => new string(c, 2)));
        }

        if (value.Length is not (6 or 8))
        {
            throw new FormatException($"'{hex}' is not a colour this test can read.");
        }

        double Channel(int offset) =>
            int.Parse(value.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;

        return new Rgba(Channel(0), Channel(2), Channel(4), value.Length == 8 ? Channel(6) : 1);
    }

    private static string Hex(Rgba colour) => string.Create(
        CultureInfo.InvariantCulture,
        $"#{Byte(colour.Red):x2}{Byte(colour.Green):x2}{Byte(colour.Blue):x2}");

    private static int Byte(double channel) => (int)Math.Round(Math.Clamp(channel, 0, 1) * 255);
}

/// <summary>
///   Declared property values, read out of a stylesheet the application ships.
/// </summary>
/// <remarks>
///   <para>
///     The contrast audit has to measure what a rule <em>draws with</em>, not merely what the token
///     block <em>offers</em>. Asserting that <c>--color-accent-700</c> clears 4.5:1 proves nothing
///     about a button that is painted <c>--color-accent</c>; the pairing that matters is the one
///     the stylesheet actually writes down (008 research R4).
///   </para>
///   <para>
///     A deliberately small reader. It resolves no cascade and no specificity — it finds a rule by
///     its exact selector and reads one property out of it, which is all these assertions need.
///   </para>
/// </remarks>
internal static class Stylesheet
{
    public static string Broadsheet => BroadsheetTokens.StylesheetPath;

    public static string MetricsChart => UnderWeb("Components", "Dashboard", "MetricsChartView.razor.css");

    public static string Dashboard => UnderWeb("Components", "Pages", "Dashboard.razor.css");

    public static string UnderWeb(params string[] segments) => Path.Combine(
        [WebProjectRoot(), .. segments]);

    /// <summary>
    ///   The value of <paramref name="property"/> in the rule whose selector is exactly
    ///   <paramref name="selector"/>, or null when there is no such rule or no such property.
    /// </summary>
    public static string? Declaration(string stylesheet, string selector, string property) =>
        File.Exists(stylesheet)
            ? DeclarationIn(File.ReadAllText(stylesheet), selector, property)
            : null;

    /// <summary>
    ///   The body of the <c>@media</c> rule whose condition contains <paramref name="condition"/>,
    ///   or null when the stylesheet declares no such query.
    /// </summary>
    /// <remarks>
    ///   A responsive rule cannot be asserted by rendering: a scoped <c>.razor.css</c> compiles
    ///   into a separate bundle and never appears in the markup, and bUnit has no viewport to
    ///   narrow. Reading the rule off disk proves it exists; that it produces the right reflow is
    ///   human-verified (008 T050, T053).
    /// </remarks>
    public static string? MediaQuery(string stylesheet, string condition)
    {
        if (!File.Exists(stylesheet))
        {
            return null;
        }

        var css = WithoutComments(File.ReadAllText(stylesheet));
        var query = Regex.Match(css, @"@media[^{]*" + Regex.Escape(condition) + @"[^{]*\{");

        if (!query.Success)
        {
            return null;
        }

        var open = query.Index + query.Length - 1;
        var depth = 0;

        for (var i = open; i < css.Length; i++)
        {
            if (css[i] == '{')
            {
                depth++;
            }
            else if (css[i] == '}' && --depth == 0)
            {
                return css[(open + 1)..i];
            }
        }

        return null;
    }

    /// <summary>The same read, over CSS already in hand rather than a file.</summary>
    public static string? DeclarationIn(string stylesheet, string selector, string property)
    {
        var css = WithoutComments(stylesheet);

        foreach (Match rule in Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}"))
        {
            if (!Normalised(rule.Groups[1].Value).Equals(Normalised(selector), StringComparison.Ordinal))
            {
                continue;
            }

            var declaration = Regex.Match(
                rule.Groups[2].Value,
                $@"(?:^|;)\s*{Regex.Escape(property)}\s*:\s*([^;]+)");

            if (declaration.Success)
            {
                return declaration.Groups[1].Value.Trim();
            }
        }

        return null;
    }

    private static string WithoutComments(string css) =>
        Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

    private static string Normalised(string selector) =>
        Regex.Replace(selector.Trim(), @"\s*,\s*", ",").Replace("\n", " ", StringComparison.Ordinal) is var collapsed
            ? Regex.Replace(collapsed, @"\s+", " ")
            : selector;

    private static string WebProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new DirectoryNotFoundException("No repository root above the test assembly."),
            "src",
            "TrainingLoadAnalyzer.Web");
    }
}

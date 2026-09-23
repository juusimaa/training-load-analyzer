using System.Net;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ParityGenerator;

/// <summary>
///   Renders the reference's components to HTML with ASP.NET Core's own <see cref="HtmlRenderer"/>,
///   and reduces that HTML to the text a reader sees.
/// </summary>
internal static partial class Rendering
{
    public static async Task<string> RenderAsync<TComponent>(
        IServiceProvider services,
        IDictionary<string, object?>? parameters = null)
        where TComponent : IComponent
    {
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(
                ParameterView.FromDictionary(parameters ?? new Dictionary<string, object?>()));

            return output.ToHtmlString();
        });
    }

    /// <summary>
    ///   Renders <c>Pages/Dashboard.razor</c> with bUnit, as the reference's DashboardRenderContext
    ///   does: the page declares an interactive render mode, which <see cref="HtmlRenderer"/>
    ///   refuses. <paramref name="register"/> supplies the page's dependencies. When
    ///   <paramref name="settled"/> is given, the markup is taken once the page has left its
    ///   loading state; otherwise it is the first render — the loading state itself.
    /// </summary>
    public static string RenderPage<TComponent>(Action<IServiceCollection> register, bool settled)
        where TComponent : IComponent
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        register(context.Services);

        var page = context.Render<TComponent>();

        if (settled)
        {
            page.WaitForState(
                () => !page.Markup.Contains("Reading your training history", StringComparison.Ordinal),
                TimeSpan.FromSeconds(30));
        }

        return page.Markup;
    }

    public static ServiceProvider Plain()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        return services.BuildServiceProvider();
    }

    /// <summary>
    ///   Whitespace-normalised text, the equivalent of the DOM's <c>textContent</c> with every run of
    ///   whitespace collapsed to one space and the ends trimmed. The React tests reduce their DOM the
    ///   same way, so the two sides compare what a reader reads and not how the markup is indented.
    /// </summary>
    public static string Text(string html)
    {
        var withoutComments = Comment().Replace(html, string.Empty);
        var withoutTags = Tag().Replace(withoutComments, string.Empty);

        return Whitespace().Replace(WebUtility.HtmlDecode(withoutTags), " ").Trim();
    }

    /// <summary>The markup between an element's opening tag and the given closing marker.</summary>
    public static string Between(string html, string open, string close)
    {
        var start = html.IndexOf(open, StringComparison.Ordinal);
        var end = html.IndexOf(close, start + open.Length, StringComparison.Ordinal);

        if (start < 0 || end < 0)
        {
            throw new InvalidOperationException($"'{open}' … '{close}' not found in rendered markup.");
        }

        return html[start..(end + close.Length)];
    }

    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

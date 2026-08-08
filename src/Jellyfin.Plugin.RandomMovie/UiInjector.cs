using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RandomMovie;

public sealed class UiInjector
{
    private const string MarkerStart =
        "<!-- RandomMovie:BEGIN -->";

    private const string MarkerEnd =
        "<!-- RandomMovie:END -->";

    private static readonly string ScriptTag =
        "<!-- RandomMovie:BEGIN -->\n" +
        "<script src=\"/RandomMovie/inject.js\" defer></script>\n" +
        "<!-- RandomMovie:END -->";

    private readonly ILogger<UiInjector> _logger;

    public UiInjector(ILogger<UiInjector> logger)
    {
        _logger = logger;
    }

    public void Inject(string webDirectory)
    {
        var indexFile = FindIndexFile(webDirectory);

        if (indexFile is null)
        {
            _logger.LogWarning(
                "RandomMovie: could not locate index.html under {Path}.",
                webDirectory);

            return;
        }

        string html;

        try
        {
            html = File.ReadAllText(
                indexFile,
                Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "RandomMovie: could not read {Path}.",
                indexFile);

            return;
        }

        /*
         * Remove our previous injection first.
         * This makes repeated server/plugin upgrades safe.
         */
        html = Regex.Replace(
            html,
            Regex.Escape(MarkerStart) +
            ".*?" +
            Regex.Escape(MarkerEnd),
            string.Empty,
            RegexOptions.Singleline);

        var closingHead =
            html.IndexOf(
                "</head>",
                StringComparison.OrdinalIgnoreCase);

        var closingHtml =
            html.IndexOf(
                "</html>",
                StringComparison.OrdinalIgnoreCase);

        var insertAt =
            closingHead >= 0
                ? closingHead
                : closingHtml >= 0
                    ? closingHtml
                    : html.Length;

        html = html.Insert(
            insertAt,
            ScriptTag + "\n");

        try
        {
            File.WriteAllText(
                indexFile,
                html,
                new UTF8Encoding(false));

            _logger.LogInformation(
                "RandomMovie: injected UI script into {Path}.",
                indexFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "RandomMovie: could not write {Path}.",
                indexFile);
        }
    }

    private static string? FindIndexFile(
        string webDirectory)
    {
        if (string.IsNullOrWhiteSpace(webDirectory))
        {
            return null;
        }

        var candidates = new[]
        {
            Path.Combine(webDirectory, "index.html"),
            Path.Combine(webDirectory, "index.htm")
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}

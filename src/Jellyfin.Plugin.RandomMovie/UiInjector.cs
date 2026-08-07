using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RandomMovie;

public class UiInjector
{
    private const string MarkerStart = "<!-- RandomMovie:BEGIN -->";
    private const string MarkerEnd = "<!-- RandomMovie:END -->";

    private static readonly string ScriptTag =
        "<!-- RandomMovie:BEGIN -->\n<script src=\"RandomMovie/inject.js\" defer></script>\n<!-- RandomMovie:END -->";

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
            _logger.LogWarning("RandomMovie: could not locate index.html under {Path}.", webDirectory);
            return;
        }

        string html;
        try
        {
            html = File.ReadAllText(indexFile, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RandomMovie: could not read index.html at {Path}.", indexFile);
            return;
        }

        html = Regex.Replace(html, MarkerStart + ".*?" + MarkerEnd, string.Empty, RegexOptions.Singleline);

        if (html.Contains(MarkerStart, StringComparison.Ordinal))
        {
            return;
        }

        var closingHead = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        var injectBefore = closingHead >= 0 ? closingHead : html.IndexOf("</html>", StringComparison.OrdinalIgnoreCase);
        var insertAt = injectBefore >= 0 ? injectBefore : html.Length;

        if (insertAt >= html.Length)
        {
            _logger.LogWarning("RandomMovie: index.html has no </head> or </html> marker; appending script.");
        }

        html = html.Insert(insertAt, ScriptTag + "\n");

        try
        {
            File.WriteAllText(indexFile, html, new UTF8Encoding(false));
            _logger.LogInformation("RandomMovie: injected script tag into {Path}.", indexFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RandomMovie: could not write injected script to {Path}.", indexFile);
        }
    }

    private static string? FindIndexFile(string webDirectory)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(webDirectory))
        {
            candidates.Add(Path.Combine(webDirectory, "index.html"));
            candidates.Add(Path.Combine(webDirectory, "index.htm"));
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
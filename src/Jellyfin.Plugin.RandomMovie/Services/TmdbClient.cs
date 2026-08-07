using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RandomMovie.Services;

public class MovieSuggestion
{
    public string Title { get; set; } = string.Empty;

    public int? Year { get; set; }

    public double? Rating { get; set; }

    public string Overview { get; set; } = string.Empty;

    public string? ImdbId { get; set; }

    public string? PosterUrl { get; set; }

    public string? BackdropUrl { get; set; }
}

internal sealed class TmdbDiscoverResponse
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("total_results")]
    public int TotalResults { get; set; }

    [JsonPropertyName("results")]
    public List<TmdbMovie> Results { get; set; } = new();
}

internal sealed class TmdbMovie
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("overview")]
    public string Overview { get; set; } = string.Empty;

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("vote_average")]
    public double VoterAverage { get; set; }

    [JsonPropertyName("imdb_id")]
    public string? ImdbId { get; set; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    [JsonPropertyName("backdrop_path")]
    public string? BackdropPath { get; set; }
}

public class TmdbClient
{
    private const string DiscoverUrl = "https://api.themoviedb.org/3/discover/movie";
    private const string ImageBase = "https://image.tmdb.org/t/p/w500";
    private const string BackdropImageBase = "https://image.tmdb.org/t/p/w780";

    private readonly HttpClient _http;
    private readonly Random _random = new();

    public TmdbClient()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
        _http.DefaultRequestHeaders.Add("User-Agent", "Jellyfin.RandomMovie/1.0");
    }

    public async Task<MovieSuggestion?> FindRandomMovieAsync(
        string apiKey,
        int? genreId,
        int? year,
        double? minRating,
        int maxPages,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        int page = _random.Next(1, Math.Max(1, maxPages));
        var url = BuildDiscoverUrl(apiKey, genreId, year, minRating, page);

        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var discover = await JsonSerializer.DeserializeAsync<TmdbDiscoverResponse>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (discover is null || discover.TotalResults <= 0 || discover.Results.Count == 0)
        {
            return null;
        }

        var movie = discover.Results[_random.Next(discover.Results.Count)];
        return ToSuggestion(movie);
    }

    private static string BuildDiscoverUrl(string apiKey, int? genreId, int? year, double? minRating, int page)
    {
        var builder = new UriBuilder(DiscoverUrl);
        var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
        query["api_key"] = apiKey;
        query["page"] = page.ToString();
        query["language"] = "en-US";
        query["include_adult"] = "false";
        query["vote_count.gte"] = "50";

        if (genreId.HasValue && genreId.Value > 0)
        {
            query["with_genres"] = genreId.ToString();
        }

        if (year.HasValue && year.Value > 0)
        {
            query["primary_release_year"] = year.ToString();
        }

        if (minRating.HasValue)
        {
            query["vote_average.gte"] = minRating.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        builder.Query = query.ToString();
        return builder.ToString();
    }

    private static MovieSuggestion? ToSuggestion(TmdbMovie movie)
    {
        if (string.IsNullOrWhiteSpace(movie.Title))
        {
            return null;
        }

        var year = ParseYear(movie.ReleaseDate);

        return new MovieSuggestion
        {
            Title = movie.Title,
            Year = year,
            Rating = movie.VoterAverage,
            Overview = movie.Overview ?? string.Empty,
            ImdbId = movie.ImdbId,
            PosterUrl = string.IsNullOrEmpty(movie.PosterPath) ? null : ImageBase + movie.PosterPath,
            BackdropUrl = string.IsNullOrEmpty(movie.BackdropPath) ? null : BackdropImageBase + movie.BackdropPath
        };
    }

    private static int? ParseYear(string? releaseDate)
    {
        if (int.TryParse(releaseDate?[..4], out var year))
        {
            return year;
        }

        return null;
    }
}
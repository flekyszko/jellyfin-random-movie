using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RandomMovie.Services;

public sealed class MovieSuggestion
{
    public string Title { get; set; } = string.Empty;
    public int? Year { get; set; }
    public double? Rating { get; set; }
    public string Overview { get; set; } = string.Empty;
    public string? ImdbId { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public long TmdbId { get; set; }
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
    public double VoteAverage { get; set; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    [JsonPropertyName("backdrop_path")]
    public string? BackdropPath { get; set; }
}

internal sealed class TmdbMovieDetails
{
    [JsonPropertyName("imdb_id")]
    public string? ImdbId { get; set; }
}

public sealed class TmdbClient
{
    private const string DiscoverUrl =
        "https://api.themoviedb.org/3/discover/movie";

    private const string DetailsUrl =
        "https://api.themoviedb.org/3/movie";

    private const string ImageBase =
        "https://image.tmdb.org/t/p/w500";

    private const string BackdropImageBase =
        "https://image.tmdb.org/t/p/w780";

    private static readonly HttpClient Http = CreateHttpClient();

    private static readonly Random Random = new();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        client.DefaultRequestHeaders.Add(
            "Accept",
            "application/json");

        client.DefaultRequestHeaders.Add(
            "User-Agent",
            "Jellyfin.RandomMovie/1.1");

        return client;
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
            throw new InvalidOperationException(
                "TMDB API key is not configured.");
        }

        maxPages = Math.Clamp(maxPages, 1, 20);

        ValidateFilters(genreId, year, minRating);

        /*
         * First request page 1.
         * This tells us how many pages actually exist.
         */
        var firstPageUrl = BuildDiscoverUrl(
            apiKey,
            genreId,
            year,
            minRating,
            1);

        using var firstResponse =
            await Http.GetAsync(
                firstPageUrl,
                cancellationToken).ConfigureAwait(false);

        if (!firstResponse.IsSuccessStatusCode)
        {
            var body = await firstResponse.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            throw new HttpRequestException(
                $"TMDB Discover returned {(int)firstResponse.StatusCode}: {body}");
        }

        var firstPage =
            await DeserializeAsync<TmdbDiscoverResponse>(
                firstResponse,
                cancellationToken).ConfigureAwait(false);

        if (firstPage is null ||
            firstPage.TotalResults <= 0 ||
            firstPage.Results.Count == 0)
        {
            return null;
        }

        /*
         * Never select a page that does not exist.
         */
        var availablePages = Math.Min(
            Math.Max(firstPage.TotalPages, 1),
            maxPages);

        var selectedPage =
            Random.Next(1, availablePages + 1);

        TmdbDiscoverResponse discover = firstPage;

        /*
         * If page 1 was selected, reuse it.
         * Otherwise request the randomly selected page.
         */
        if (selectedPage != 1)
        {
            var url = BuildDiscoverUrl(
                apiKey,
                genreId,
                year,
                minRating,
                selectedPage);

            using var response =
                await Http.GetAsync(
                    url,
                    cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            discover =
                await DeserializeAsync<TmdbDiscoverResponse>(
                    response,
                    cancellationToken).ConfigureAwait(false)
                ?? new TmdbDiscoverResponse();
        }

        if (discover.Results.Count == 0)
        {
            return null;
        }

        var movie =
            discover.Results[
                Random.Next(discover.Results.Count)];

        /*
         * Get the real IMDb ID from TMDB's movie details endpoint.
         */
        string? imdbId = null;

        try
        {
            imdbId = await GetImdbIdAsync(
                apiKey,
                movie.Id,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            /*
             * IMDb is optional.
             * The movie itself should still be returned
             * if this secondary request fails.
             */
        }

        return ToSuggestion(movie, imdbId);
    }

    private static async Task<string?> GetImdbIdAsync(
        string apiKey,
        long tmdbId,
        CancellationToken cancellationToken)
    {
        var url =
            $"{DetailsUrl}/{tmdbId}" +
            "?api_key=" +
            Uri.EscapeDataString(apiKey);

        using var response =
            await Http.GetAsync(
                url,
                cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var details =
            await DeserializeAsync<TmdbMovieDetails>(
                response,
                cancellationToken).ConfigureAwait(false);

        return details?.ImdbId;
    }

    private static async Task<T?> DeserializeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken).ConfigureAwait(false);

        return await JsonSerializer.DeserializeAsync<T>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateFilters(
        int? genreId,
        int? year,
        double? minRating)
    {
        if (genreId is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(genreId));
        }

        var currentYear = DateTime.UtcNow.Year;

        if (year is < 1888 or > currentYear)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }

        if (minRating is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(minRating));
        }
    }

    private static string BuildDiscoverUrl(
        string apiKey,
        int? genreId,
        int? year,
        double? minRating,
        int page)
    {
        var builder = new UriBuilder(DiscoverUrl);

        var query =
            System.Web.HttpUtility.ParseQueryString(string.Empty);

        query["api_key"] = apiKey;
        query["page"] = page.ToString(
            CultureInfo.InvariantCulture);

        query["language"] = "en-US";
        query["include_adult"] = "false";

        /*
         * Avoid extremely obscure movies with 1-2 votes.
         * This can be removed if you want literally everything.
         */
        query["vote_count.gte"] = "50";

        if (genreId.HasValue)
        {
            query["with_genres"] =
                genreId.Value.ToString(
                    CultureInfo.InvariantCulture);
        }

        if (year.HasValue)
        {
            query["primary_release_year"] =
                year.Value.ToString(
                    CultureInfo.InvariantCulture);
        }

        if (minRating.HasValue)
        {
            query["vote_average.gte"] =
                minRating.Value.ToString(
                    CultureInfo.InvariantCulture);
        }

        builder.Query = query.ToString();

        return builder.ToString();
    }

    private static MovieSuggestion? ToSuggestion(
        TmdbMovie movie,
        string? imdbId)
    {
        if (string.IsNullOrWhiteSpace(movie.Title))
        {
            return null;
        }

        return new MovieSuggestion
        {
            TmdbId = movie.Id,
            Title = movie.Title,
            Year = ParseYear(movie.ReleaseDate),
            Rating = movie.VoteAverage,
            Overview = movie.Overview ?? string.Empty,
            ImdbId = imdbId,
            PosterUrl = string.IsNullOrWhiteSpace(movie.PosterPath)
                ? null
                : ImageBase + movie.PosterPath,
            BackdropUrl = string.IsNullOrWhiteSpace(movie.BackdropPath)
                ? null
                : BackdropImageBase + movie.BackdropPath
        };
    }

    private static int? ParseYear(string? releaseDate)
    {
        if (string.IsNullOrWhiteSpace(releaseDate) ||
            releaseDate.Length < 4)
        {
            return null;
        }

        return int.TryParse(
            releaseDate[..4],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var year)
            ? year
            : null;
    }
}

using Jellyfin.Plugin.RandomMovie.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RandomMovie.Api;

[ApiController]
[Authorize]
[Route("RandomMovie")]
public class RandomMovieController : ControllerBase
{
    private readonly ILogger<RandomMovieController> _logger;

    public RandomMovieController(ILogger<RandomMovieController> logger)
    {
        _logger = logger;
    }

    [HttpGet("random")]
    [ProducesResponseType(typeof(MovieSuggestion), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MovieSuggestion>> GetRandom(
        [FromQuery] int? genreId,
        [FromQuery] int? year,
        [FromQuery] double? minRating,
        CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        var config = plugin?.Configuration;

        if (config is null || string.IsNullOrWhiteSpace(config.TmdbApiKey))
        {
            _logger.LogWarning("RandomMovie: TMDB API key is not configured.");
            return BadRequest("TMDB API key is not configured. Set it in the plugin settings.");
        }

        var tmdb = new TmdbClient();
        var suggestion = await tmdb.FindRandomMovieAsync(
            config.TmdbApiKey,
            genreId,
            year,
            minRating,
            config.MaxRandomPages,
            cancellationToken).ConfigureAwait(false);

        if (suggestion is null)
        {
            return NotFound("No movie matched the given filters.");
        }

        return Ok(suggestion);
    }

    [AllowAnonymous]
    [HttpGet("inject.js")]
    [Produces("application/javascript")]
    public async Task GetInjectJs(CancellationToken cancellationToken)
    {
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("Jellyfin.Plugin.RandomMovie.Web.randommovie.js");
        if (stream is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        Response.ContentType = "application/javascript";
        await stream.CopyToAsync(Response.Body, cancellationToken).ConfigureAwait(false);
    }
}
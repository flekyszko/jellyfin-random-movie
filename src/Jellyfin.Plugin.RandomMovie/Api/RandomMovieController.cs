using Jellyfin.Plugin.RandomMovie.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RandomMovie.Api;

[ApiController]
[Authorize]
[Route("RandomMovie")]
public sealed class RandomMovieController : ControllerBase
{
    private readonly ILogger<RandomMovieController> _logger;

    public RandomMovieController(
        ILogger<RandomMovieController> logger)
    {
        _logger = logger;
    }

    [HttpGet("random")]
    [ProducesResponseType(
        typeof(MovieSuggestion),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<MovieSuggestion>> GetRandom(
        [FromQuery] int? genreId,
        [FromQuery] int? year,
        [FromQuery] double? minRating,
        CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        var config = plugin?.Configuration;

        if (config is null ||
            string.IsNullOrWhiteSpace(config.TmdbApiKey))
        {
            _logger.LogWarning(
                "RandomMovie: TMDB API key is not configured.");

            return BadRequest(
                "TMDB API key is not configured. " +
                "Configure it in Dashboard → Plugins → Random Movie.");
        }

        try
        {
            var tmdb = new TmdbClient();

            var suggestion =
                await tmdb.FindRandomMovieAsync(
                    config.TmdbApiKey,
                    genreId,
                    year,
                    minRating,
                    config.MaxRandomPages,
                    cancellationToken)
                .ConfigureAwait(false);

            if (suggestion is null)
            {
                return NotFound(
                    "No movie matched the selected filters.");
            }

            return Ok(suggestion);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning(
                ex,
                "RandomMovie: invalid filter values.");

            return BadRequest(
                "One or more filter values are invalid.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "RandomMovie: TMDB request failed.");

            return StatusCode(
                StatusCodes.Status502BadGateway,
                "TMDB could not be reached.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "RandomMovie: unexpected error.");

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "Unexpected Random Movie plugin error.");
        }
    }

    [AllowAnonymous]
    [HttpGet("inject.js")]
    [Produces("application/javascript")]
    public async Task GetInjectJs(
        CancellationToken cancellationToken)
    {
        await using var stream =
            typeof(Plugin).Assembly
                .GetManifestResourceStream(
                    "Jellyfin.Plugin.RandomMovie.Web.randommovie.js");

        if (stream is null)
        {
            _logger.LogError(
                "RandomMovie: embedded randommovie.js was not found.");

            Response.StatusCode =
                StatusCodes.Status404NotFound;

            return;
        }

        Response.ContentType =
            "application/javascript; charset=utf-8";

        await stream.CopyToAsync(
            Response.Body,
            cancellationToken).ConfigureAwait(false);
    }
}

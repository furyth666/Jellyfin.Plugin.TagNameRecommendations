using System;
using System.Net.Mime;
using Jellyfin.Plugin.TagNameRecommendations.Models;
using Jellyfin.Plugin.TagNameRecommendations.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.TagNameRecommendations.Api;

/// <summary>
/// Recommendation endpoints.
/// </summary>
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[Authorize]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _recommendationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationsController"/> class.
    /// </summary>
    /// <param name="recommendationService">The recommendation service.</param>
    public RecommendationsController(IRecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
    }

    /// <summary>
    /// Gets recommendations for a seed item.
    /// </summary>
    /// <param name="itemId">The seed Jellyfin item id.</param>
    /// <param name="userId">Optional user id for access filtering.</param>
    /// <param name="limit">Optional result limit.</param>
    /// <returns>Recommended items.</returns>
    [HttpGet("JellyfinRecommendations/Recommendations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<RecommendationResponse> GetRecommendations(
        [FromQuery] Guid itemId,
        [FromQuery] Guid? userId,
        [FromQuery] int? limit)
    {
        var response = _recommendationService.GetRecommendations(itemId, userId, limit);

        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }

    /// <summary>
    /// Gets recommendations from a user's recently watched videos.
    /// </summary>
    /// <param name="userId">The Jellyfin user id.</param>
    /// <param name="limit">Optional result limit.</param>
    /// <param name="seedLimit">Optional number of recent watched items to use.</param>
    /// <returns>Recommended items.</returns>
    [HttpGet("JellyfinRecommendations/RecentlyWatched")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<RecommendationResponse> GetRecentlyWatchedRecommendations(
        [FromQuery] Guid userId,
        [FromQuery] int? limit,
        [FromQuery] int? seedLimit)
    {
        var response = _recommendationService.GetRecommendationsForRecentlyWatched(userId, limit, seedLimit);

        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }
}

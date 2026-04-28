using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.TagNameRecommendations.Services;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace Jellyfin.Plugin.TagNameRecommendations.HomeScreen;

/// <summary>
/// Result provider invoked by the Home Screen Sections plugin.
/// </summary>
public class HomeScreenRecommendationResults
{
    private readonly IDtoService _dtoService;
    private readonly ILibraryManager _libraryManager;
    private readonly IRecommendationService _recommendationService;
    private readonly IUserManager _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="HomeScreenRecommendationResults"/> class.
    /// </summary>
    /// <param name="dtoService">The dto service.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="recommendationService">The recommendation service.</param>
    /// <param name="userManager">The user manager.</param>
    public HomeScreenRecommendationResults(
        IDtoService dtoService,
        ILibraryManager libraryManager,
        IRecommendationService recommendationService,
        IUserManager userManager)
    {
        _dtoService = dtoService;
        _libraryManager = libraryManager;
        _recommendationService = recommendationService;
        _userManager = userManager;
    }

    /// <summary>
    /// Gets homepage recommendations for the requesting user.
    /// </summary>
    /// <param name="payload">The Home Screen Sections payload.</param>
    /// <returns>Recommended items as Jellyfin DTOs.</returns>
    public QueryResult<BaseItemDto> GetRecentlyWatchedRecommendations(HomeScreenRecommendationPayload payload)
    {
        var userObject = GetUserById(payload.UserId);
        if (userObject is null || !_recommendationService.IsPlaybackReportingAvailable)
        {
            return new QueryResult<BaseItemDto>();
        }

        var response = _recommendationService.GetRecommendationsForRecentlyWatched(payload.UserId, 16, null);
        if (response is null || response.Items.Count == 0)
        {
            return new QueryResult<BaseItemDto>();
        }

        var items = response.Items
            .Select(candidate => _libraryManager.GetItemById(candidate.Id))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();

        var dtoOptions = new DtoOptions
        {
            Fields =
            [
                ItemFields.PrimaryImageAspectRatio,
                ItemFields.MediaSourceCount
            ],
            ImageTypes =
            [
                ImageType.Primary,
                ImageType.Backdrop,
                ImageType.Banner,
                ImageType.Thumb
            ],
            ImageTypeLimit = 1
        };

        var dtos = InvokeBaseItemDtos(items, dtoOptions, userObject);
        return new QueryResult<BaseItemDto>(dtos?.ToArray() ?? []);
    }

    private object? GetUserById(Guid userId)
    {
        return _userManager
            .GetType()
            .GetMethod(nameof(IUserManager.GetUserById), [typeof(Guid)])
            ?.Invoke(_userManager, [userId]);
    }

    private IEnumerable<BaseItemDto>? InvokeBaseItemDtos(List<BaseItem> items, DtoOptions dtoOptions, object userObject)
    {
        foreach (var method in _dtoService.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == nameof(IDtoService.GetBaseItemDtos))
            .OrderBy(method => method.GetParameters().Length))
        {
            var args = BuildDtoMethodArguments(method, items, dtoOptions, userObject);
            if (args is null)
            {
                continue;
            }

            try
            {
                if (method.Invoke(_dtoService, args) is IEnumerable<BaseItemDto> dtos)
                {
                    return dtos;
                }
            }
            catch (TargetInvocationException)
            {
            }
            catch (ArgumentException)
            {
            }
        }

        return null;
    }

    private static object?[]? BuildDtoMethodArguments(MethodInfo method, List<BaseItem> items, DtoOptions dtoOptions, object userObject)
    {
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];

        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];
            var parameterType = parameter.ParameterType;

            if (parameterType.IsAssignableFrom(items.GetType()))
            {
                args[index] = items;
            }
            else if (parameterType.IsAssignableFrom(dtoOptions.GetType()))
            {
                args[index] = dtoOptions;
            }
            else if (parameterType.IsAssignableFrom(userObject.GetType()))
            {
                args[index] = userObject;
            }
            else if (parameter.HasDefaultValue)
            {
                args[index] = parameter.DefaultValue;
            }
            else if (!parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) is not null)
            {
                args[index] = null;
            }
            else if (parameterType == typeof(bool))
            {
                args[index] = false;
            }
            else if (parameterType == typeof(int))
            {
                args[index] = 0;
            }
            else
            {
                return null;
            }
        }

        return args;
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.TagNameRecommendations.Configuration;
using Jellyfin.Plugin.TagNameRecommendations.Models;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using SQLitePCL.pretty;

namespace Jellyfin.Plugin.TagNameRecommendations.Services;

/// <inheritdoc />
public class RecommendationService : IRecommendationService
{
    private static readonly Guid PlaybackReportingPluginId = Guid.Parse("5c534381-91a3-43cb-907a-35aa02eb9d2c");
    private static readonly Guid ReportsPluginId = Guid.Parse("d4312cd9-5c90-4f38-82e8-51da566790e8");
    private static readonly BaseItemKind[] SupportedSeedKinds =
    [
        BaseItemKind.Movie,
        BaseItemKind.Series,
        BaseItemKind.Episode,
        BaseItemKind.MusicVideo
    ];

    private readonly IServerApplicationPaths _applicationPaths;
    private readonly ILibraryManager _libraryManager;
    private readonly IPluginManager _pluginManager;
    private readonly IUserManager _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationService"/> class.
    /// </summary>
    /// <param name="applicationPaths">The server application paths.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="pluginManager">The plugin manager.</param>
    /// <param name="userManager">The user manager.</param>
    public RecommendationService(
        IServerApplicationPaths applicationPaths,
        ILibraryManager libraryManager,
        IPluginManager pluginManager,
        IUserManager userManager)
    {
        _applicationPaths = applicationPaths;
        _libraryManager = libraryManager;
        _pluginManager = pluginManager;
        _userManager = userManager;
    }

    /// <inheritdoc />
    public bool IsPlaybackReportingAvailable =>
        _pluginManager.GetPlugin(PlaybackReportingPluginId)?.IsEnabledAndSupported == true
        || _pluginManager.GetPlugin(ReportsPluginId)?.IsEnabledAndSupported == true;

    /// <inheritdoc />
    public RecommendationResponse? GetRecommendations(Guid itemId, Guid? userId, int? limit)
    {
        var seed = _libraryManager.GetItemById(itemId);

        if (seed is null)
        {
            return null;
        }

        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var resultLimit = Math.Clamp(limit ?? config.DefaultLimit, 1, 100);
        var maxCandidates = Math.Clamp(config.MaxCandidates, resultLimit, 5000);
        var seedProfile = new SeedProfile(seed, 1);

        var candidates = _libraryManager.GetItemList(new InternalItemsQuery
        {
            Recursive = true,
            Limit = maxCandidates,
            ExcludeItemIds = [itemId],
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series, BaseItemKind.Episode, BaseItemKind.MusicVideo],
            MediaTypes = [MediaType.Video],
            IsVirtualItem = false,
            EnableTotalRecordCount = false
        });

        var scoredItems = candidates
            .Select(item => ScoreItem([seedProfile], item, config))
            .Where(item => item.Score >= config.MinimumScore)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(resultLimit)
            .ToArray();

        return new RecommendationResponse
        {
            SeedItemId = seed.Id,
            SeedItemName = seed.Name,
            Seeds = [new RecommendationSeed { Id = seed.Id, Name = seed.Name }],
            Items = scoredItems
        };
    }

    /// <inheritdoc />
    public RecommendationResponse? GetRecommendationsForRecentlyWatched(Guid userId, int? limit, int? seedLimit)
    {
        if (!IsPlaybackReportingAvailable)
        {
            return null;
        }

        if (GetUserById(userId) is null)
        {
            return null;
        }

        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var resultLimit = Math.Clamp(limit ?? config.DefaultLimit, 1, 100);
        var maxCandidates = Math.Clamp(config.MaxCandidates, resultLimit, 5000);
        var recentLimit = Math.Clamp(seedLimit ?? config.RecentWatchedCount, 1, 50);
        var minimumPlaybackSeconds = Math.Clamp(config.MinimumSeedPlaybackSeconds, 0, 86400);

        var recentItems = GetRecentPlaybackReportSeeds(userId, minimumPlaybackSeconds, Math.Clamp(recentLimit * 4, recentLimit, 200))
            .Select(CreateSeed)
            .Where(seed => seed is not null)
            .Select(seed => seed!)
            .Take(recentLimit)
            .ToArray();

        if (recentItems.Length == 0)
        {
            return new RecommendationResponse();
        }

        var seedProfiles = recentItems
            .Select((seed, index) => new SeedProfile(seed.Item, GetSeedWeight(seed, index)))
            .ToArray();
        var seedIds = recentItems.Select(seed => seed.Item.Id).ToArray();

        var candidateQuery = new InternalItemsQuery
        {
            Recursive = true,
            Limit = maxCandidates,
            ExcludeItemIds = seedIds,
            IncludeItemTypes = SupportedSeedKinds,
            MediaTypes = [MediaType.Video],
            IsVirtualItem = false,
            EnableTotalRecordCount = false
        };

        var candidates = _libraryManager.GetItemList(candidateQuery);
        var scoredItems = candidates
            .Select(item => ScoreItem(seedProfiles, item, config))
            .Where(item => item.Score >= config.MinimumScore)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(resultLimit)
            .ToArray();

        return new RecommendationResponse
        {
            SeedItemId = recentItems[0].Item.Id,
            SeedItemName = recentItems[0].Item.Name,
            Seeds = recentItems.Select(seed => seed.ToRecommendationSeed()).ToArray(),
            Items = scoredItems
        };
    }

    private object? GetUserById(Guid userId)
    {
        return _userManager
            .GetType()
            .GetMethod(nameof(IUserManager.GetUserById), [typeof(Guid)])
            ?.Invoke(_userManager, [userId]);
    }

    private SeedPlayback? CreateSeed(PlaybackActivity activity)
    {
        if (!Guid.TryParse(activity.ItemId, out var itemId))
        {
            return null;
        }

        var item = _libraryManager.GetItemById(itemId);
        if (item is null || item.MediaType != MediaType.Video || !SupportedSeedKinds.Contains(item.GetBaseItemKind()))
        {
            return null;
        }

        var playbackPositionTicks = 0L;
        var playbackPositionSeconds = TimeSpan.FromTicks(playbackPositionTicks).TotalSeconds;
        var playedPercentage = item.RunTimeTicks is > 0
            ? Math.Round(Math.Clamp((double)playbackPositionTicks / item.RunTimeTicks.Value * 100, 0, 100), 2)
            : (double?)null;

        return new SeedPlayback(
            item,
            null,
            activity.LastPlaybackActivityDate,
            activity.ActualPlaybackSeconds,
            activity.PlaybackReportPlayCount,
            playbackPositionTicks,
            playbackPositionSeconds,
            playedPercentage,
            activity.ActualPlaybackSeconds >= item.RunTimeTicks / TimeSpan.TicksPerSecond * 0.9);
    }

    private static double GetSeedWeight(SeedPlayback seed, int index)
    {
        var recencyWeight = 1 / (1 + index * 0.35);
        var durationWeight = Math.Clamp(seed.ActualPlaybackSeconds / 1800.0, 0.2, 1.5);
        return recencyWeight * durationWeight;
    }

    private List<PlaybackActivity> GetRecentPlaybackReportSeeds(Guid userId, int minimumPlaybackSeconds, int limit)
    {
        var databasePath = Path.Combine(_applicationPaths.DataPath, "playback_reporting.db");
        if (!File.Exists(databasePath))
        {
            return [];
        }

        var itemTypes = string.Join(",", SupportedSeedKinds.Select(kind => $"'{kind}'"));
        var sql = $@"
SELECT
    ItemId,
    MAX(DateCreated) AS LastPlaybackActivityDate,
    SUM(CASE WHEN PlayDuration > 0 THEN PlayDuration ELSE 0 END) AS ActualPlaybackSeconds,
    COUNT(1) AS PlaybackReportPlayCount
FROM PlaybackActivity
WHERE (UserId = @UserIdN OR UserId = @UserIdD)
    AND ItemType IN ({itemTypes})
    AND PlayDuration > 0
GROUP BY ItemId
HAVING ActualPlaybackSeconds >= @MinimumPlaybackSeconds
ORDER BY LastPlaybackActivityDate DESC
LIMIT @Limit";

        var activities = new List<PlaybackActivity>();
        using var connection = SQLite3.Open(databasePath, ConnectionFlags.ReadOnly, null);
        using var statement = connection.PrepareStatement(sql);
        statement.BindParameters["@UserIdN"].Bind(userId.ToString("N", CultureInfo.InvariantCulture));
        statement.BindParameters["@UserIdD"].Bind(userId.ToString("D", CultureInfo.InvariantCulture));
        statement.BindParameters["@MinimumPlaybackSeconds"].Bind(minimumPlaybackSeconds);
        statement.BindParameters["@Limit"].Bind(limit);

        foreach (var row in statement.Query())
        {
            activities.Add(new PlaybackActivity(
                row[0].ToString(),
                ParseDateTime(row[1].ToString()),
                row[2].ToInt(),
                row[3].ToInt()));
        }

        return activities;
    }

    private static DateTime ParseDateTime(string? value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date)
            ? date
            : DateTime.MinValue;
    }

    private static RecommendationCandidate ScoreItem(
        IReadOnlyList<SeedProfile> seeds,
        BaseItem candidate,
        PluginConfiguration config)
    {
        var candidateTags = NormalizeSet(candidate.Tags);
        var candidateNameTokens = TokenizeName(candidate.Name);
        var matchedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedNameTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedSeedNames = new List<string>();
        var totalWeight = 0.0;
        var totalScore = 0.0;

        foreach (var seed in seeds)
        {
            var tagSimilarity = Jaccard(seed.Tags, candidateTags);
            var nameSimilarity = Jaccard(seed.NameTokens, candidateNameTokens);
            var containsBonus = HasNameContainment(seed.Item.Name, candidate.Name) ? config.NameContainsBonus : 0;
            var seedScore = (config.TagWeight * tagSimilarity) + (config.NameWeight * nameSimilarity) + containsBonus;

            if (seedScore > 0)
            {
                matchedSeedNames.Add(seed.Item.Name);

                foreach (var tag in seed.Tags.Intersect(candidateTags, StringComparer.OrdinalIgnoreCase))
                {
                    matchedTags.Add(tag);
                }

                foreach (var token in seed.NameTokens.Intersect(candidateNameTokens, StringComparer.OrdinalIgnoreCase))
                {
                    matchedNameTokens.Add(token);
                }
            }

            totalScore += seedScore * seed.Weight;
            totalWeight += seed.Weight;
        }

        var weightedScore = totalWeight == 0 ? 0 : totalScore / totalWeight;
        var score = Math.Round(Math.Clamp(weightedScore * 100, 0, 100), 2);

        return new RecommendationCandidate
        {
            Id = candidate.Id,
            Name = candidate.Name,
            Type = candidate.GetType().Name,
            Score = score,
            Tags = candidateTags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase).ToArray(),
            MatchedTags = matchedTags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase).ToArray(),
            MatchedNameTokens = matchedNameTokens.OrderBy(token => token, StringComparer.OrdinalIgnoreCase).ToArray(),
            MatchedSeedNames = matchedSeedNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static HashSet<string> NormalizeSet(IEnumerable<string>? values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLower(CultureInfo.InvariantCulture))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
    }

    private static HashSet<string> TokenizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return [];
        }

        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var token = new List<char>();

        foreach (var character in name)
        {
            if (char.IsLetterOrDigit(character))
            {
                token.Add(char.ToLower(character, CultureInfo.InvariantCulture));
                continue;
            }

            AddToken(tokens, token);
        }

        AddToken(tokens, token);
        return tokens;
    }

    private static void AddToken(HashSet<string> tokens, List<char> token)
    {
        if (token.Count >= 2)
        {
            tokens.Add(new string(token.ToArray()));
        }

        token.Clear();
    }

    private static double Jaccard(IReadOnlySet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var intersection = left.Intersect(right, StringComparer.OrdinalIgnoreCase).Count();
        var union = left.Union(right, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static bool HasNameContainment(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return left.Contains(right, StringComparison.OrdinalIgnoreCase)
            || right.Contains(left, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SeedProfile
    {
        public SeedProfile(BaseItem item, double weight)
        {
            Item = item;
            Weight = weight;
            Tags = NormalizeSet(item.Tags);
            NameTokens = TokenizeName(item.Name);
        }

        public BaseItem Item { get; }

        public double Weight { get; }

        public IReadOnlySet<string> Tags { get; }

        public IReadOnlySet<string> NameTokens { get; }
    }

    private sealed record SeedPlayback(
        BaseItem Item,
        DateTime? LastPlayedDate,
        DateTime? LastPlaybackActivityDate,
        int ActualPlaybackSeconds,
        int PlaybackReportPlayCount,
        long PlaybackPositionTicks,
        double PlaybackPositionSeconds,
        double? PlayedPercentage,
        bool Played)
    {
        public RecommendationSeed ToRecommendationSeed()
        {
            return new RecommendationSeed
            {
                Id = Item.Id,
                Name = Item.Name,
                LastPlayedDate = LastPlayedDate,
                LastPlaybackActivityDate = LastPlaybackActivityDate,
                ActualPlaybackSeconds = ActualPlaybackSeconds,
                PlaybackReportPlayCount = PlaybackReportPlayCount,
                PlaybackPositionTicks = PlaybackPositionTicks,
                PlaybackPositionSeconds = Math.Round(PlaybackPositionSeconds, 2),
                RunTimeTicks = Item.RunTimeTicks,
                PlayedPercentage = PlayedPercentage,
                Played = Played
            };
        }
    }

    private sealed record PlaybackActivity(
        string ItemId,
        DateTime LastPlaybackActivityDate,
        int ActualPlaybackSeconds,
        int PlaybackReportPlayCount);
}

# Jellyfin Tag/Name Recommendations

一个简易 Jellyfin 插件：根据用户最近观看过的影片/剧集/单集生成推荐。插件会读取 Jellyfin 媒体库里的视频项目，根据 tag 和名称 token 计算相似度，然后返回推荐列表。

## 前置依赖

`RecentlyWatched` 推荐需要先安装并启用 Jellyfin 官方 `Playback Reporting` 插件。插件 GUID：

```text
5c534381-91a3-43cb-907a-35aa02eb9d2c
```

如果没有安装或未启用，`RecentlyWatched` 接口会返回 `424 Failed Dependency`。

## API

```text
GET /JellyfinRecommendations/RecentlyWatched?userId={userId}&limit=20&seedLimit=8
GET /JellyfinRecommendations/Recommendations?itemId={itemId}&userId={userId}&limit=20
```

推荐使用 `RecentlyWatched`。`seedLimit` 表示使用最近观看的多少个项目作为推荐依据。

`Recommendations` 是调试用的单影片 seed 接口；其中 `userId` 可选。

## Scoring

- Tag 相似度：seed item 与候选 item 的 tag Jaccard 相似度。
- 名称相似度：标题分词后的 Jaccard 相似度。
- 名称包含 bonus：一个标题包含另一个标题时，给小额加分。
- 最近观看推荐：需要 Playback Reporting 已安装启用；seed 使用 Playback Reporting 的真实播放秒数，太短的跳转或误触不会作为推荐依据。
- 最终分数：按插件配置页里的权重换算到 0-100。

## Build

需要 .NET 8 SDK：

```bash
dotnet build Jellyfin.Plugin.TagNameRecommendations.sln -c Release
```

构建后把 DLL 复制到 Jellyfin 插件目录中，例如：

```text
config/plugins/TagNameRecommendations/Jellyfin.Plugin.TagNameRecommendations.dll
```

然后重启 Jellyfin。

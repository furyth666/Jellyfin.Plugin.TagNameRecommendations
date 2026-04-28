# Jellyfin Tag/Name Recommendations

一个简易 Jellyfin 插件：根据用户最近观看过的影片/剧集/单集生成推荐。插件会读取 Jellyfin 媒体库里的视频项目，根据 tag 和名称 token 计算相似度，然后返回推荐列表。

## 前置依赖

`RecentlyWatched` 推荐需要先安装并启用 Jellyfin 官方 `Playback Reporting` 插件。插件 GUID：

```text
5c534381-91a3-43cb-907a-35aa02eb9d2c
```

如果没有安装或未启用，`RecentlyWatched` 接口会返回 `424 Failed Dependency`。

如果希望 Jellyfin Web 首页出现推荐栏，还需要安装并启用 `Home Screen Sections / Modular Home` 插件。没有安装它时，本插件仍然可以通过 API 使用。

## Install

在 Jellyfin 后台的插件仓库中添加这个 URL：

```text
https://raw.githubusercontent.com/furyth666/Jellyfin.Plugin.TagNameRecommendations/main/manifest.json
```

然后在 Catalog 里安装 `Tag Name Recommendations`。

## Home Screen

首页推荐栏依赖 `Home Screen Sections / Modular Home`：

1. 安装并启用 `Playback Reporting`。
2. 安装并启用 `Home Screen Sections / Modular Home` 以及它要求的依赖。
3. 安装或更新本插件到 `0.3.1.0` 后重启 Jellyfin。
4. 打开 Jellyfin Web 首页侧边栏里的 `Modular Home` 设置。
5. 启用 `Recommended for You` section，并把它放到你想要的位置。

这个推荐栏使用 Playback Reporting 的真实播放时长作为推荐种子，不会把单纯拖动进度条当成真实观看。

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

## License

GPL-3.0-or-later

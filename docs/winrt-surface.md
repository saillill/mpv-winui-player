# WinRT 组件的公开接口使用情况

审查范围扩大后，第一次覆盖 C++/WinRT 层。这份记录的是**测量结果**，不是清理结论。

## 为什么没有直接改

`mpv-winrt-projection` 是一个**发布为 NuGet 包的可复用组件**：

```xml
<PackageVersion>0.3.0-prerelease</PackageVersion>
<PackageAuthors>ikas</PackageAuthors>
<GeneratePackageOnBuild>true</GeneratePackageOnBuild>
```

IDL 是与下游消费者的契约。删除事件或类型是**对带版本号的包做破坏性变更**，
需要先知道 `0.3.0-prerelease` 是否已有外部使用者——这一点本仓库无法回答。

## 测量：29 个事件，19 个被 C# 订阅

| 事件 | C# 是否订阅 | C++ 是否触发 |
|---|---|---|
| `FileStarted` | 是 | 是 |
| `FileLoaded` | 是 | 是 |
| `FileFailed` | **否** | 是（4 处） |
| `FileEnded` | **否** | 是（4 处） |
| `FileStopped` | **否** | 是（4 处） |
| `SeekStarted` | 是 | 是 |
| `PlaybackRestarted` | 是 | 是 |
| `TrackChanged` | **否** | 是（4 处） |
| `BufferingChanged` | **否** | 是（4 处） |
| `PlaybackStateChanged` | 是 | 是 |
| `VolumeChanged` | 是 | 是 |
| `PositionChanged` | 是 | 是 |
| `SpeedChanged` | 是 | 是 |
| `MediaInfoChanged` | 是 | 是 |
| `NetworkInfoChanged` | **否** | 是（4 处） |
| `TrackListChanged` | **否** | 是（4 处） |
| `TrackListCountChanged` | **否** | 是（4 处） |
| `SwapChainChanged` | **否** | 是（4 处） |
| `WindowChanged` | 是 | 是 |
| `DiscMenuActiveChanged` | 是 | 是 |
| `LoopFileChanged` | 是 | 是 |
| `LoopPlaylistChanged` | 是 | 是 |
| `ShuffleChanged` | 是 | 是 |
| `PlaylistChanged` | 是 | 是 |
| `MediaLoaded` | **否** | **从不触发** |
| `PlaybackFailed` | 是 | 是 |
| `Seeked` | 是 | 是 |
| `VoConfigured` | 是 | 是 |
| `LogMessage` | **否** | **从不触发** |

**9 个事件在每次条件发生时都会触发一次，进入一个没有处理函数的事件。**

## 9 个类型未被 C# 引用

```
FileFailedEventArgs            MpvChapter        TrackType
MpvLogEventArgs                MpvEdition
NetworkInfoChangedEventArgs    MpvProfile
TrackListChangedEventArgs
TrackListCountChangedEventArgs
```

其中 5 个是上面 9 个未订阅事件的参数类型，4 个是枚举。

## 两种处理方式

**A. 保守（我倾向）**：不动 IDL。这 9 个空触发是极小的开销（一次 `winrt::event<>::operator()`
对空 handler 集合的检查），而保留它们让组件对未来的消费者保持完整。

**B. 激进**：删除 10 个未订阅事件 + 9 个未引用类型。需要同时改
`MpvPlayer.idl`、`MpvPlayer.h`（含 `m_*Event` 成员）、`MpvPlayer.cpp`（10 处触发）、
以及 5 个 `Events/*.idl`、4 个 `Types/*.idl` 文件。包版本应升到 `0.4.0`。

这需要你判断：`mpv-winrt-projection` 是否对外发布过。

## 顺带确认：C++ 内部没有独立死代码

- 4 个 `m_*Event` 成员被 `MPVWINRT_EVENT` 宏通过 `m_##field##Event` 拼接使用，不是死代码
- 无未调用的文件内函数
- 无未使用的私有成员（除上述与公开事件绑定的）

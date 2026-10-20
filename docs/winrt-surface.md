# WinRT 组件的公开接口使用情况

审查范围扩大后对 C++/WinRT 层的测量。**这份文档第一版有四处是错的**，
下面第二节记录错在哪 —— 因为错的方式比结论更值得留下。

## 一、实测（2026-10-21，逐文件、跨全部 `.cpp`）

方法：从 `MpvPlayer.idl` 读出 29 个事件的声明；跨**所有** `mpv-winrt/*.cpp`
搜 `m_*Event(` 得到真实触发点；事件名到成员名的映射同时读 `MPVWINRT_EVENT`
宏的参数与 `winrt::event<...> m_*Event;` 的声明列表（宏用 `m_##field##Event`
拼名字，任何按名搜索都看不见）；C# 侧的订阅按 `.事件名 +=` 在全部 `.cs` 里搜。

| 事件 | C++ 触发 | 触发点数 | C# 订阅 |
|---|---|---:|---|
| `FileStarted` | 是 | 1 | 是 |
| `FileLoaded` | 是 | 1 | 是 |
| `FileFailed` | 是 | 1 | 是 |
| `FileEnded` | 是 | 1 | **否** |
| `FileStopped` | 是 | 1 | **否** |
| `SeekStarted` | 是 | 1 | 是 |
| `PlaybackRestarted` | 是 | 1 | 是 |
| `TrackChanged` | **从不** | 0 | 否 |
| `BufferingChanged` | 是 | 1 | **否** |
| `PlaybackStateChanged` | 是 | 2 | 是 |
| `VolumeChanged` | 是 | 1 | 是 |
| `PositionChanged` | 是 | 1 | 是 |
| `SpeedChanged` | 是 | 1 | 是 |
| `MediaInfoChanged` | 是 | 2 | 是 |
| `NetworkInfoChanged` | **从不** | 0 | 否 |
| `TrackListChanged` | **从不** | 0 | 否 |
| `TrackListCountChanged` | **从不** | 0 | 否 |
| `SwapChainChanged` | 是 | 1 | **否** |
| `WindowChanged` | 是 | 1 | 是 |
| `DiscMenuActiveChanged` | 是 | 1 | 是 |
| `LoopFileChanged` | 是 | 1 | 是 |
| `LoopPlaylistChanged` | 是 | 1 | 是 |
| `ShuffleChanged` | 是 | 1 | 是 |
| `PlaylistChanged` | 是 | 1 | 是 |
| `MediaLoaded` | **从不** | 0 | 否 |
| `PlaybackFailed` | **从不** | 0 | 否 |
| `Seeked` | **从不** | 0 | **是** |
| `VoConfigured` | **从不** | 0 | 否 |
| `LogMessage` | 是 | 2 | **否** |

**20 个被订阅、5 个触发但无人接收、8 个声明了从不触发。**

## 二、第一版错在哪（四个错，同一个病）

| 第一版的说法 | 实测 | 错因 |
|---|---|---|
| 19 个被订阅 | **20** | `FileFailed` 当时没接上，我在后续提交里修了它 |
| 9 个触发但无人接收 | **5** | 把 `TrackChanged` / `NetworkInfo*` / `TrackList*` 算成"在触发"，实际它们是**从不触发** |
| `PlaybackFailed`、`Seeked`、`VoConfigured` "C++ 是" | **从不触发** | 只看了 IDL 声明和访问器，没查 `m_*Event(` 的调用点 |
| `LogMessage` "从不触发" | **触发 2 次** | 同上，方向反了 |

病根是同一句话：**声明存在 ≠ 访问器存在 ≠ 有人触发**。这三件事在
C++/WinRT 里分别由 IDL、`.cpp` 的访问器实现、`.cpp` 里对 `m_*Event` 的
调用决定，任何只查其中一项的测量都会给出自信的错误答案。

## 三、这轮因此暴露并修掉的真实缺陷

`c49b461`（"refactor mpv-winrt event"）把三个触发点改了名，C# 侧没跟上：

```
m_playbackFailedEvent  ->  m_fileFailedEvent
m_voConfiguredEvent    ->  m_swapChainChangedEvent
m_seekedEvent          ->  m_seekStartedEvent
```

旧事件留在 IDL 里、访问器照旧，所以**订阅旧名字的代码照常编译、照常类型检查、
永远收不到**：

- `PlayerControl` 订阅 `PlaybackFailed` → `MediaPlayer_MediaFailed` 从未运行，
  `PlayerControl.xaml` 里那个 `Error` 视觉状态**不可达**。任何加载失败的文件
  都不会显示 mpv 的错误文本。
- `MpvPlayerPage` 订阅 `VoConfigured` → swap chain 重挂接从未运行。该项职责已在
  `222be73` 内化为 C++ 的 `CompletePendingSwapChainAttach`，所以订阅被**删除**
  而不是重接 —— 重接会让每次 reconfig 挂两次。
- `PlayerControl` 订阅 `Seeked` → `MediaPlayer_Seeked` 是死代码。它的活由
  `SeekStarted` / `PlaybackRestarted` 那一对承担。处理体保留并注明不可达。

## 四、仍然无人接收的 5 个（测量，未处理）

`FileEnded`、`FileStopped`、`BufferingChanged`、`SwapChainChanged`、`LogMessage`。

`FileEnded` 与 `FileStopped` 在 `MPV_EVENT_END_FILE` 里按 reason 分流触发，
播放结束时进入空 handler 集合 —— 开销是一次 `winrt::event<>::operator()`
对空集合的检查，可以忽略。`LogMessage` 的代价不同：它每次日志消息都会构造一个
`MpvLogEventArgs`（两次 `winrt::to_hstring`），而 `msg-level = all=info` 在
默认配置里就是开的。**这是这五个里唯一有实际成本的**，但接上它需要先决定
这些日志往哪写。

## 五、为什么不删 IDL

`mpv-winrt-projection` 带 `<PackageVersion>0.3.0-prerelease</PackageVersion>`、
`<GeneratePackageOnBuild>true</GeneratePackageOnBuild>`、作者 `ikas`，是发布为
NuGet 包的可复用组件。IDL 是契约，删除事件是**对带版本号的包做破坏性变更**。

本仓库内**没有任何 `nuget push`**：CI 不发布、无 `NuGetApiKey`、无
`nuget.config` 指向 feed。也就是说这个包只在本地生成过 —— 但"仓库里没有发布路径"
不等于"从未发布"，两者不是同一个论断。要动 IDL 需要这一点的外部确认。

## 六、9 个类型未被 C# 引用

```
FileFailedEventArgs            MpvChapter        TrackType
MpvLogEventArgs                MpvEdition
NetworkInfoChangedEventArgs    MpvProfile
TrackListChangedEventArgs
TrackListCountChangedEventArgs
```

`FileFailedEventArgs` 在本轮接入 `FileFailed` 后**已在用**，所以这一组现在应重新
测量；其余 8 个仍然只是 IDL 的公开面。

## 七、C++ 内部没有独立死代码（复核过）

- `m_*Event` 成员由 `MPVWINRT_EVENT` 宏用 `m_##field##Event` 令牌拼接使用，
  按名搜索看不见 —— 第一版在这里报了 4 个假"未使用成员"
- 无未调用的文件内函数
- 无未使用的私有成员（除上述与公开事件绑定的）

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

## 四、曾经无人接收的 5 个

`FileEnded`、`FileStopped`、`BufferingChanged`、`SwapChainChanged`、`LogMessage`。

**`LogMessage` 已接入**，见下面第五节。它接上后第一次启动就报出了一条
此前无人可见的错误，因此独立成节。

其余四个（`FileEnded`、`FileStopped`、`BufferingChanged`、`SwapChainChanged`）
仍然无人接收，且**刻意不接**：它们每个触发点只做一次
`winrt::event<>::operator()` 对空集合的检查，没有参数构造，没有字符串转换。
`FileEnded`/`FileStopped` 的语义由 `PlaybackStateChanged` 的 idle 边沿覆盖，
`SwapChainChanged` 的职责已在 `222be73` 内化进 C++。接上它们只会增加一条
没有消费者的代码路径。

## 五、`LogMessage` 接上后立刻报出的东西

`LogMessage` 是这五个里唯一有实际成本的：mpv 从 initialize 起就以 `warn`
请求日志，每条都构造 `MpvLogEventArgs`（三次 `winrt::to_hstring`）并投进空集合。
接上之后第一批输出：

```
[mpv/select]  script-opts: unknown key populate_menu_data, ignoring        x8
[mpv/vo/gpu-next/d3d11] Failed to query swap chain's output information    x8
[mpv/main]    Command 'no-osd set volume-max 100' not found.               x3
[mpv/main]    Command 'no-osd set alang auto' not found.                   x1
```

### 5.1 `volume-max`（已修）

`ToCommand` 为它发出运行时 `set`，而 mpv 每次拒绝。原因是 `volume-max` 是
**仅当混音器活跃时**才存在的属性，`ao=wasapi` 下混音器归系统所有 —— mpv 自己的
interface-changes 就写着 "volume-max is 100 while the mixer is not active"。

没有任何东西依赖这条命令成功：音量滑块上限直接读
`AppContext.AppSetting.VolumeMax`，引擎侧的值由 `mpv.conf` 的 `volume-max=100`
在启动时给出。所以它的唯一效果是每次启动一条失败命令和一行错误。

修法：`ToCommand` 对 `VolumeMax` 返回 `null`。选项仍留在 `MpvOptionNames` 里，
所以设置行照常写进 mpv.conf、读回也照常解析；`ApplyMpv` 本来就把 `null` 命令
当作"只持久化、不下发"。

**实测验证**：修前三次启动三条错误，修后一次启动零条，而 mpv 日志转发仍在工作
（日志里累计 20 行 `[mpv/`）—— 证明被消掉的是那条命令，不是转发本身。

### 5.2 `alang auto`（**只记录，未改**）

`MpvSettings.ToCommand` 发 `set alang auto`，mpv 回答 not found。链路是：
`mpv.conf` 里 `alang = auto` → `LoadFromConfig` 读进
`AppSettings.AudioLanguage` → `ToCommand` 原样发回 mpv。

**为什么没有改**：我无法验证到底是哪里不对。三种可能各自都能解释这一条错误——

1. `auto` 不是合法的语言代码（`alang` 是 `<lang>,<lang>` 列表），配置文件宽容、
   命令接口严格；
2. `alang` 的运行时写入形式不是 `set`；
3. 该次启动的时序让 `alang` 落在一个尚未就绪的路径上。

本机**没有独立的 `mpv.exe`**，唯一能跑 mpv 的是 `mpvw.exe`，而项目规则禁止
驱动它（IPC、点击、UIA 都不行）。所以我不能做那个区分实验。凭其中一种猜测去改
`ToCommand`，可能把 `slang`（同一形态、却**零报错**）一起改坏 —— 两者共用
`IsNullOrWhiteSpace ? null : $"set ..."` 的写法，这个差异本身就是未解释的证据。

**下一步需要的外部条件**：一个能跑 `--input-ipc-server` 的独立 mpv（或允许在
本轮之外手工验证一次 `set alang auto` 与 `set alang eng`）。两者都不成立之前，
把它留在文档里比改坏它更有价值。

`FileEnded` 与 `FileStopped` 在 `MPV_EVENT_END_FILE` 里按 reason 分流触发，
播放结束时进入空 handler 集合 —— 开销是一次 `winrt::event<>::operator()`
对空集合的检查，可以忽略。`LogMessage` 的代价不同：它每次日志消息都会构造一个
`MpvLogEventArgs`（两次 `winrt::to_hstring`），而 `msg-level = all=info` 在
默认配置里就是开的。**这是这五个里唯一有实际成本的**，但接上它需要先决定
这些日志往哪写。

## 六、为什么不删 IDL

`mpv-winrt-projection` 带 `<PackageVersion>0.3.0-prerelease</PackageVersion>`、
`<GeneratePackageOnBuild>true</GeneratePackageOnBuild>`、作者 `ikas`，是发布为
NuGet 包的可复用组件。IDL 是契约，删除事件是**对带版本号的包做破坏性变更**。

本仓库内**没有任何 `nuget push`**：CI 不发布、无 `NuGetApiKey`、无
`nuget.config` 指向 feed。也就是说这个包只在本地生成过 —— 但"仓库里没有发布路径"
不等于"从未发布"，两者不是同一个论断。要动 IDL 需要这一点的外部确认。

## 七、9 个类型未被 C# 引用

```
FileFailedEventArgs            MpvChapter        TrackType
MpvLogEventArgs                MpvEdition
NetworkInfoChangedEventArgs    MpvProfile
TrackListChangedEventArgs
TrackListCountChangedEventArgs
```

`FileFailedEventArgs` 在本轮接入 `FileFailed` 后**已在用**，所以这一组现在应重新
测量；其余 8 个仍然只是 IDL 的公开面。

## 八、C++ 内部没有独立死代码（复核过）

- `m_*Event` 成员由 `MPVWINRT_EVENT` 宏用 `m_##field##Event` 令牌拼接使用，
  按名搜索看不见 —— 第一版在这里报了 4 个假"未使用成员"
- 无未调用的文件内函数
- 无未使用的私有成员（除上述与公开事件绑定的）

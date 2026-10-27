# 动效体系

`Infrastructure/Motion/` 是全应用唯一的动效来源。数值集中在
`MotionTokens.cs`，曲线在 `MotionEasing.cs`，闸门在 `MotionGate.cs`，
具名行为在 `MotionBehaviors.cs`。数字可重算（`§8`）。

---

## 1. 三条曲线、三个时长

### 1.1 时长（`MotionTokens.cs:35,38,41`）

| 令牌 | 值 | 用途（代码里的说法） |
|---|---:|---|
| `ShortMs` | **83** | 淡入淡出、退出、收起的半程 |
| `MediumMs` | **167** | 入场与位移——主力 |
| `LongMs` | **250** | 页面级表面 |

这三个值与 WinUI 自己的主题资源同值，XAML 侧应引用
`ControlFasterAnimationDuration` / `ControlFastAnimationDuration` /
`ControlNormalAnimationDuration`（常量名见 `MotionTokens.cs:68,70,72`），
不要写毫秒字面量。

### 1.2 曲线（`MotionEasing.cs`，控制点定义在 `MotionTokens.cs`）

| 名称 | cubic-bezier | 用途 |
|---|---|---|
| `Enter` | `(0,0) (0,1)` | 元素此前不存在，现在到达 |
| `Move` | `(0.55,0.55) (0,1)` | 已在屏元素改变位置 |
| `Exit` | `(1,0) (1,1)` | 离开。**必须与淡出配对**，单独用读作"突然消失" |
| `Linear` | 线性 | 只动透明度（曲线对 opacity 没有意义），83 ms |

缓动函数**按 `Compositor` 缓存**（`ConditionalWeakTable<Compositor, CurveSet>`）。
理由写在 `MotionEasing.cs:7-16`：`EasingFunction` 归属于创建它的 compositor，
不能共享；这个应用有多个 compositor（主窗口 + 每次开 PiP 一个新的），
用强引用字典会把每个 PiP 的 compositor 钉在内存里。

### 1.3 距离与缩放（`MotionTokens.cs:79-127`）

| 令牌 | 值 | 用途 |
|---|---:|---|
| `DefaultEnterOffset` | **24** | 通用入场 |
| `ContentEnterOffset` | **48** | 成片内容（库网格、设置列表） |
| `RowRevealOffset` | **16** | 行内展开的详情面板 |
| `PlayerPanelOffset` | **48** | 播放控制条整块面板 |
| `DropdownFromScale` | **0.94** | 下拉展开的起始缩放 |
| `PressFromScale` | **0.94** | **未被使用**（见 §7） |

**距离必须大到看得见。** `ContentEnterOffset` 原本是 8，8 px 配 167 ms 被反馈成
"动画缺失"——肉眼读到的只是一个纯淡入。官方指南把内容入场写作
"slide up 150 px"，150 是整页量级，卡片按比例缩到 48（`MotionTokens.cs:82-92`
的注释记录了这段推理）。

---

## 2. 为什么用 Translation，不用 Offset

这是整套实现里最容易做错的一处。规则写在 `MotionBehaviors.cs:17-42` 的类注释里：

**不要动 `Visual.Offset`。** Offset 由布局拥有：一段 Offset 动画结束后仍会持续填充
数值，元素永久停在被移走的位置；布局一旦给出新 Offset，动画里那个过期值反而会赢。
`PlayerControl.Animation.cs:12-20` 记录了后果——控制条在一次缩放后"搁浅"在错误位置。

**正确通道是 `"Translation"` 属性集成员**，由
`ElementCompositionPreview.SetIsTranslationEnabled` 发布（`MotionBehaviors.cs:81`）。
Windows App SDK 对该方法的定义是 "a render-time **post-layout** translate transform"。
post-layout 是关键：这个值叠加在布局结果之上，而不是取代它，所以不需要读取基准坐标，
也不会被后续布局重置。

两个直接好处：

1. 位移量是相对量，天然适配"从下方 48 px 滑到原位"。
2. 列表项刚被实现、还没测量时也能安全启动——`ItemsRepeater.ElementPrepared`
   就是在那个时刻回调的。

实现细节：`VisualOf()`（`:77-88`）每次调用都做三件事——取 visual、打开
Translation 开关、若属性集里还没有该成员则插入零值。两步都幂等，因为调用来自点击
处理器，没有单一的"初始化一次"时机。注意 `TryGetVector3` 返回
`CompositionGetValueStatus`，不是 `bool`。

### 2.1 第二条规则：合成不透明度与 XAML 不透明度相乘

这些行为只动合成侧的值；元素的 XAML `Opacity` 是另一个独立因子，调用方保持为 1。
唯一例外是播放器控制条的渐变层——它停泊在 0.01 以保持可命中，那是调用方的事
（`MotionBehaviors.cs:44-48`）。

---

## 3. 减弱动画的闸门

`MotionGate.Resolve()`（`MotionGate.cs:67-87`）：

```csharp
bool systemEnabled;
try { systemEnabled = UiSettings.AnimationsEnabled; }
catch { systemEnabled = true; }        // 读不到时按"允许动效"处理
var enabled = systemEnabled && _appEnabled;
if (enabled != _lastResolved) { _lastResolved = enabled; Changed?.Invoke(); }
return enabled;
```

四条设计要点，都来自代码注释：

1. **每次现读，不缓存**（`:18-24`）。代价是每次动效一次属性读，换来的是
   Windows 设置一翻就生效，且没有过期缓存要失效。
2. **读失败按允许处理**（`:60-64`）。失败的方向应是"没有动画"，不是"应用起不来"。
3. **`AppEnabled` 是运行期覆盖，不持久化**（`:41-58`）。持久化就得让它穿过
   设置 schema、漂移检查器和九个语言文件，而真正重要的只有系统设置。
4. **`Changed` 用于处理已经在飞的动画**（`:24-26`）。入口检查停不掉一个已经开始的
   动画，所以翻转时要知道去把面板强制推到终态。

**每个行为都有无动画直通分支**，调用方不需要自己判断。直通分支的共同点是：
设终值、**立刻执行完成回调**、返回。最后一点尤其重要——回调不执行，调用方基于
回调的状态机（比如切 `Visibility`）会永远卡住。

---

## 4. 具名行为清单

调用方只看到名字，看不到曲线和时长。

| 行为 | 曲线 | 时长 | 外部调用点数 |
|---|---|---:|---:|
| `FadeIn` / `FadeOut` | Linear | 83 | 4 / 2 |
| `ContentEnter` | Enter | 167 | **0** |
| `PosterEnter` | Enter | 250 | 2 |
| `ContentExit` | Exit | 83 | **0** |
| `MoveTo` | Move | 167 | **0** |
| `RowReveal` / `RowCollapse` | Enter / Exit | 250 / 83 | 2 / 2 |
| `SectionSwitch` | Enter | 167 | 2 |
| `SettleFrom` | Move | 167 | 1 |
| `ControlStateChange` | Move | 167 | **0** |
| `SpringPop` | Move（无回弹） | 280 | 2 |
| `PanelReveal` / `PanelConceal` | Move / Exit | 167 / 83 | 1 / 1 |
| `SoftenDropdown` | — | — | 1 |
| `DropdownEnter` | Enter | 250 | **0**（仅被 `SoftenDropdown` 内部调用） |
| `Reset` | — | — | 9 |

`PosterEnter` 是**一个名字而不是两处调相同参数**：库网格与设置列表都调它，
谁都不传距离、时长、曲线。这样"两个界面动效一致"由构造保证，而不是靠两处代码
碰巧写得一样（`MotionBehaviors.cs:247-252`）。

### 4.1 错峰：步长 20 ms、封顶 8

两处实现，数值相同，**都是字面量，没有具名常量**：

| 处 | 代码 |
|---|---|
| 库网格 | `MpvPlayerPage.Library.Cards.cs:814` `Math.Min(args.Index, 8) * 20` |
| 设置列表 | `OptionListControl.xaml.cs:93` `Math.Min(i, 8) * 20` |

`git grep StaggerStep\|StaggerCap` 无命中——旧文档写的
`StaggerStepMs = 20` / `StaggerCap = 8` 这两个常量**在代码里不存在**。
`OptionListControl.xaml.cs:92-93` 的注释明说两个界面用"同一个 20 ms 步长、同一个
上限八"，但它是靠两处各写一遍字面量实现的。

封顶的理由（旧文档记载，代码只留下"cap of eight"的说法）：不封顶时第 40 张卡片要等
800 ms 才开始动，期间用户视线早已落在网格上，看起来像卡顿而不是波浪。

### 4.2 触发钩子：两种容器，两种正确写法

| 容器 | 正确钩子 | 错误钩子及后果 |
|---|---|---|
| `ItemsRepeater` | `ElementPrepared` | — |
| `ListView` | 一次性标志 + `LayoutUpdated` | `ContainerContentChanging` 在容器回收复用时再次触发，滚动即重播动画 |

设置列表用的是后者，理由写在 `OptionListControl.xaml.cs:46-52`。

---

## 5. 已完成的一次退让：SpringPop 去掉了回弹

`SpringPop` 现在 **1.0 → peak → 1.0，只用 `Move` 曲线，单调，不回弹**
（`MotionBehaviors.cs:722-750`）。注释写明：

> 这曾是弹簧曲线上的 24 个采样帧，会越过峰值再回摆；项目风格不接受
> "在已有官方曲线能表达意图时发明一条曲线"。

时长仍是 280 ms（`SpringSettleMs`），所以节奏没变，只是回弹没了。峰值：
默认 **1.14**（按钮尺寸），满宽行调用方传 **1.02**（`MpvPlayerPage_Music.cs:399`）
——注释说满宽行用 1.14 会让文字看起来在跳。

**这解释了为什么 `SpringMath` 在运行期没有消费者**（见 §7）。
## 6. 已踩过的坑

| # | 现象 | 原因 | 处理 |
|---|---|---|---|
| 1 | 只有第 1 个元素动画正常，其余先实心闪现再跳成透明 | `StopAnimation(Opacity)` 把不透明度恢复成 XAML 默认的 1，而带 `DelayTime` 的动画在延迟期内不写属性 | 停放阶段显式 `visual.Opacity = 0f`（`MotionBehaviors.cs:294-300` 的注释记录了这个坑） |
| 2 | 淡入与位移差一帧，看起来"先亮后动" | 两个独立动画落在不同合成器 tick | `CreateScopedBatch` 圈成一批 |
| 3 | 元素永久停在偏移位置 | 用了 `Visual.Offset`（布局拥有） | 改用 `Translation` 属性集通道 |
| 4 | 滚动列表时动画反复重播 | 用了 `ContainerContentChanging` | 一次性标志 + `LayoutUpdated` |
| 5 | 位移肉眼看不见，被当成"没有动画" | 距离只有 8 px | `ContentEnterOffset` 改为 48 |
| 6 | 元素未布局时按原点缩放，向右下飞出 | 没有 `CenterPoint` | 缩放前检查 `visual.Size`，为 0 则直接显示并调回调（`ControlStateChange`、`SpringPop`、`DropdownEnter` 都有这个分支） |
| 7 | 动画结束后留下 1.0001 的缩放，边框出现发丝缝 | 只停动画、没有精确复位 | 完成回调里 `Scale = Vector3.One` 且 `CenterPoint = Vector3.Zero`（`MotionBehaviors.cs:742-748`） |
| 8 | 同一 offset 上两次 `InsertKeyFrame` 不是叠加而是覆盖 | Composition 的语义 | 一个峰值帧，不是两个（`MotionBehaviors.cs:734-736` 的注释） |

`PlayerControl.Animation.cs:12-30` 另记了一条：这个面板曾是两套实现——
主窗口用 XAML Storyboard、PiP 用 16 ms `DispatcherTimer` 补间。两个理由
（PiP 是第二个顶层窗口，Storyboard 会崩合成器；两者都不能动 Offset 否则被布局搁浅）
如今都不成立，所以合成一条路径。**这条路径里还有一段仍是 XAML 侧动画**：
控制条宿主的高度（高度是布局属性，不能走合成器），它因此只能跑在滑动之后，
并且只能用近似缓动——这个项目的 WinUI 投影没有在 XAML 侧写 cubic bezier 的入口
（`CubicEase` 在这里不带控制点成员）。
## 7. 现存的不一致（实测，未修）

### 7.1 `SpringMath` 在运行期是死代码

- `MotionEasing.SpringValue`（`MotionEasing.cs:75-76`）是唯一的转发器，
  而 `git grep SpringValue` 只命中它自己的定义行——**没有调用者**。
- `SpringPop` 已改用 `Move` 曲线（§5）。
- `SpringMath.Value` 的实际消费者只有 `tests/conf/SpringCurveTests.cs`。

`SpringCurveTests` 有 7 个 `[Test]`，断言的是弹簧曲线的数学性质（端点、过冲量、
衰减、临界阻尼、阻尼比序关系）。**它测的是一个运行期不再使用的函数。**
`MotionTokens.SpringDamping` / `SpringFrequency` / `SpringOvershoot` 因此也没有运行期读取者
（只在 `SpringSettleMs` 上还活着，被 `SpringPop` 的时长使用）。

### 7.2 三个 token 无人使用

| 令牌 | 状态 |
|---|---|
| `PressFromScale`（0.94） | 定义处之外 **0 次**引用。`ControlStateChange` 用的是自己的 `0.98` 字面量（`MotionBehaviors.cs:664`） |
| `DefaultEnterOffset`（24） | 只作为 `ContentEnter` 的默认参数；而 `ContentEnter` 外部调用点 **0 个** |
| `MotionGate.Changed` | 事件有定义、有 `Invoke`，但**没有订阅者**（`git grep MotionGate.Changed` 只命中注释） |

`MotionGate.IsEnabled` 同样无调用者。

### 7.3 注释与常量不一致处

`MotionBehaviors.cs:686` 的注释说 SpringPop 是
"1.0 -> 1.14 -> 1.0，at `MotionTokens.SpringDamping`"，但实现里没有任何地方读
`SpringDamping`——峰值是 `peak` 参数，曲线是 `Move`。这段注释是 §5 那次退让之前
留下的。
## 8. 重算本文每个数字

```powershell
Select-String -Path src\mpvw-app\Infrastructure\Motion\MotionTokens.cs -Pattern 'const (double|float|string) \w+ ='
Select-String -Path src\mpvw-app\Infrastructure\Motion\SpringMath.cs   -Pattern 'const (double|float) \w+ ='

# 每个具名行为的外部调用点数
foreach ($n in @('FadeIn','FadeOut','ContentEnter','PosterEnter','ContentExit','MoveTo',
                 'RowReveal','RowCollapse','SectionSwitch','SettleFrom','ControlStateChange',
                 'SpringPop','PanelReveal','PanelConceal','SoftenDropdown','DropdownEnter','Reset')) {
  "{0,-20} {1}" -f $n, (git grep -n "MotionBehaviors\.$n\b" -- '*.cs' |
                        Where-Object { $_ -notmatch 'MotionBehaviors.cs' } | Measure-Object).Count
}

git grep -n 'Math.Min' -- '*.cs'                  # 两处错峰：各写一遍 20 / 8
git grep -n 'StaggerStep\|StaggerCap' -- '*.cs'   # 无输出 → 常量不存在

git grep -n 'SpringValue'  -- '*.cs'              # 只有定义（SpringMath 运行期无消费者）
git grep -n 'PressFromScale' -- '*.cs'            # 只有定义
git grep -n 'MotionGate.Changed' -- '*.cs'        # 只有注释
git grep -n 'MotionGate.IsEnabled' -- '*.cs'      # 无输出

Select-String -Path src\mpvw-app\Infrastructure\Motion\MotionBehaviors.cs -Pattern '0\.\d+f'
(Select-String -Path tests\conf\SpringCurveTests.cs -Pattern '\[Test\]').Count   # 7
```

## 9. 原因不明

- **`PressFromScale` 为什么被留下**：`ControlStateChange` 用 0.98 而不是 0.94，
  两处都是 0.94/0.98 级别的按压反馈。是无意重复还是刻意区分，代码无说明。
- **错峰步长为何不做成具名常量**：同一组 `20`/`8` 在两处各写一遍，注释强调
  "同一个步长、同一个上限"，但没有任何机制保证它们同步。为什么没抽到
  `MotionTokens`，无记录。

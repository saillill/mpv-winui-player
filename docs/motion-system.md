# WinUI 3 动效体系设计说明

一份可直接移植到其他 WinUI 3 / Windows App SDK 项目的动效实现说明。全部内容取自
`mpv-winui` 的实际实现，但本文不依赖该项目的任何代码——所需的代码在文中完整给出。

---

## 1. 设计前提

这套方案解决的是「同一产品里每个界面各自写一套动画」的问题。目标是三条：

1. **一致性**：一个产品只有三条曲线、三个时长。任何新动效都从这三对里选，不新增。
2. **可关闭**：系统「减弱动画」开启时，全部动效跳过并直接落到终态，不留半途状态。
3. **可复用**：调用方说的是「什么在出现」，不是「它怎么动」。调用方不传曲线、不传时长。

技术栈假设：WinUI 3 / Windows App SDK，C#，`Microsoft.UI.Composition`。
本文所有位移与透明度都跑在合成器线程上，不占用 UI 线程的帧预算。

---

## 2. 为什么不用 XAML Storyboard

Storyboard 可以工作，但这套方案不用它，理由有三条，都是实际会出问题的：

| 问题 | 说明 |
|------|------|
| 跑在 UI 线程 | Storyboard 在 UI 线程逐帧插值。列表入场这种「几十个元素同时动」的场景会把帧预算吃光，正是最需要合成器的时候。 |
| 第二个顶层窗口会崩 | 在第二个 `Window`（弹幕窗、PiP 窗）上启动 Storyboard 会以 `0x8000FFFF` 崩溃合成器。 |
| 与布局耦合 | Storyboard 只能动 `RenderTransform`，而 `RenderTransform` 与布局测量在同一棵树上；元素被折叠再展开时数值会残留。 |

结论：**全部改用 `Microsoft.UI.Composition`**，通过 `ElementCompositionPreview` 拿到元素的 Visual。

---

## 3. 三条曲线、三个时长

### 3.1 时长

| 令牌 | 值 | 用途 |
|------|-----|------|
| `Short` | 83 ms | 淡入淡出、退出、收起 |
| `Medium` | 167 ms | 已在屏元素的位置移动、分区切换 |
| `Long` | 250 ms | 内容入场（错峰淡入）、抽屉展开、下拉展开 |

### 3.2 曲线

对应 `Compositor.CreateCubicBezierEasingFunction(控制点1, 控制点2)`：

| 名称 | 控制点 | 用途 |
|------|--------|------|
| `Entrance` | (0, 0) → (0, 1) | 元素出现、展开、滑入 |
| `Move` | (0.55, 0.55) → (0, 1) | 元素已在屏、改变位置 |
| `Exit` | (1, 0) → (1, 1) | 元素离开 |
| `Linear` | (0, 0) → (1, 1) | 只动透明度时（曲线对透明度没有意义） |

**曲线缓动函数必须按 `Compositor` 缓存。** 每次动效都新建一个 `CubicBezierEasingFunction`
是纯粹的分配浪费，而 `Compositor` 在一个窗口里是稳定的：

```csharp
private static readonly ConditionalWeakTable<Compositor, EasingSet> Cache = new();

private sealed class EasingSet
{
    public CompositionEasingFunction Entrance = null!;
    public CompositionEasingFunction Move = null!;
    public CompositionEasingFunction Exit = null!;
    public CompositionEasingFunction Linear = null!;
}

private static EasingSet For(Compositor compositor)
{
    if (Cache.TryGetValue(compositor, out var cached))
    {
        return cached;
    }

    var set = new EasingSet
    {
        Entrance = compositor.CreateCubicBezierEasingFunction(new Vector2(0f, 0f), new Vector2(0f, 1f)),
        Move     = compositor.CreateCubicBezierEasingFunction(new Vector2(0.55f, 0.55f), new Vector2(0f, 1f)),
        Exit     = compositor.CreateCubicBezierEasingFunction(new Vector2(1f, 0f), new Vector2(1f, 1f)),
        Linear   = compositor.CreateCubicBezierEasingFunction(new Vector2(0f, 0f), new Vector2(1f, 1f)),
    };

    Cache.Add(compositor, set);
    return set;
}
```

用 `ConditionalWeakTable` 而不是 `Dictionary`：`Compositor` 随窗口生灭，弱表不会把它钉在内存里。

### 3.3 距离

位移是**绝对量，不是坐标**。每个用途一个常量，集中定义，不要散落在调用点：

| 令牌 | 值 | 用途 |
|------|-----|------|
| `DefaultEnterOffset` | 24 | 通用入场 |
| `ContentEnterOffset` | 48 | 卡片网格、设置行等成片内容 |
| `RowRevealOffset` | 16 | 行内展开的详情面板（面板本身只有几行高） |
| `PanelOffset` | 48 | 底部控制条这类整块面板 |

**距离必须大到看得见。** 这一点值得单独强调：这套实现最初把入场距离定成 8 px、时长
167 ms，结果被反馈为「动画缺失」。8 px 配上淡入，肉眼读到的是一个纯淡入。官方动效规范
把内容入场写作「slide up 150 px」，150 是整页转场的量级，卡片网格按比例缩到 48 才既能
看见又不过头。

---

## 4. 位移通道：Translation，不是 Offset

这是整套实现里最容易做错的一处。

**不要动 `Visual.Offset`。** Offset 由布局拥有：一段 Offset 动画结束后仍会持续填充数值，
元素就永久停在被移走的位置；而布局一旦重新给出新的 Offset，动画里那个过期值会赢。这个坑
在这套实现的前身里真实发生过——控制条在一次缩放后「搁浅」在错误位置。

**正确通道是 `"Translation"` 属性集成员**，由 `ElementCompositionPreview.SetIsTranslationEnabled`
发布。Windows App SDK 对该方法的定义原文是：

> Toggles the ability to **set, animate, and use a render-time post-layout translate transform**
> to position XAML elements on screen using Composition APIs.

「post-layout」是关键：这个值叠加在布局结果之上，而不是取代它。因此它**不需要读取基准坐标**，
也不会被后续的布局pass重置。这带来两个直接好处：

1. 位移量是相对量，天然适配「从下方 48 px 滑到原位」这种表达。
2. 在列表项刚被实现、还没测量时也能安全启动——`ItemsRepeater.ElementPrepared` 就是在那个时刻回调的。

```csharp
private const string Translation = "Translation";
private const string Opacity = "Opacity";

/// <summary>取元素 Visual，并确保 Translation 通道已发布。</summary>
private static Visual VisualOf(UIElement element)
{
    var visual = ElementCompositionPreview.GetElementVisual(element);
    ElementCompositionPreview.SetIsTranslationEnabled(element, true);

    // 属性集成员必须先存在才能被动画指向；写入一次即可，重复调用无害。
    if (visual.Properties.TryGetVector3(Translation, out _) != CompositionGetValueStatus.Succeeded)
    {
        visual.Properties.InsertVector3(Translation, Vector3.Zero);
    }

    return visual;
}

private static Vector3 TranslationOf(Visual visual) =>
    visual.Properties.TryGetVector3(Translation, out var value) == CompositionGetValueStatus.Succeeded
        ? value
        : Vector3.Zero;
```

> 注意 `TryGetVector3` 的返回类型是 `CompositionGetValueStatus`，**不是 `bool`**。写成
> `if (visual.Properties.TryGetVector3(...))` 不能编译。

---

## 5. 错峰淡入（核心）

这是本文重点，也是「动效演示」里最常被要求复刻的效果。

### 5.1 期望效果

一批内容（卡片网格、设置行）出现时，不是整块闪现，而是自下而上滑入 + 淡入，
并且**逐个**入场，形成一道波浪。

### 5.2 单个元素的入场

```csharp
/// <summary>
/// 所有入场与横向归位的唯一实现：从「不可见 + 偏移」滑到原位。
/// 两个轴用同一个动画，避免斜向移动被拆成两条时间线而在不同帧起步。
/// </summary>
private static void SlideCore(
    UIElement element,
    float fromX,
    float fromY,
    TimeSpan duration,
    TimeSpan? delay,
    Action? completed)
{
    var visual = VisualOf(element);
    var compositor = visual.Compositor;
    var ease = For(compositor).Entrance;
    var d = delay ?? TimeSpan.Zero;

    if (!MotionGate.Resolve())
    {
        visual.StopAnimation(Opacity);
        visual.Opacity = 1f;
        ParkTranslated(visual, 0f, 0f);
        completed?.Invoke();
        return;
    }

    // 释放上一次可能残留的动画，再从干净状态起步。
    visual.StopAnimation(Opacity);
    visual.StopAnimation(Translation);
    ParkTranslated(visual, fromX, fromY);

    // ★ 关键：把「延迟期内」的不透明度显式停在 0。
    //   StopAnimation 会把 XAML 默认值（1）恢复回来，而带 DelayTime 的动画
    //   在延迟结束前根本不写这个属性。少了这一行，第 2 个之后的元素会先以
    //   实心出现在偏移位置，然后猛地跳成透明再淡入——正是错峰想要避免的闪烁。
    visual.Opacity = 0f;

    Batch(compositor, () =>
    {
        var fade = compositor.CreateScalarKeyFrameAnimation();
        fade.Duration = duration;
        fade.InsertKeyFrame(0f, 0f);
        fade.InsertKeyFrame(1f, 1f, ease);
        if (d > TimeSpan.Zero) { fade.DelayTime = d; }
        visual.StartAnimation(Opacity, fade);

        var slide = compositor.CreateVector3KeyFrameAnimation();
        slide.Duration = duration;
        slide.InsertKeyFrame(0f, new Vector3(fromX, fromY, 0f));
        slide.InsertKeyFrame(1f, new Vector3(0f, 0f, 0f), ease);
        if (d > TimeSpan.Zero) { slide.DelayTime = d; }
        visual.StartAnimation(Translation, slide);
    }, completed);
}

private static void ParkTranslated(Visual visual, float x, float y) =>
    visual.Properties.InsertVector3(Translation, new Vector3(x, y, 0f));
```

### 5.3 为什么要 `Batch`

两个 `StartAnimation` 分属两个属性。不加作用域批处理，它们可能落在不同的合成器 tick 上，
于是淡入和位移错开一帧——肉眼表现为轻微的「先亮后动」。用 `Compositor.CreateScopedBatch`
把两个动画圈成一批，并只在这一批结束时回调一次：

```csharp
private static void Batch(Compositor compositor, Action start, Action? completed)
{
    var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

    try
    {
        start();
    }
    finally
    {
        // End 必须在 finally 里：中途抛异常而不 End，这一批会一直挂着。
        batch.End();
    }

    if (completed is not null)
    {
        batch.Completed += (_, _) => completed();
    }
}
```

### 5.4 错峰：步长与封顶

```csharp
private const double StaggerStepMs = 20;
private const int StaggerCap = 8;

var delay = TimeSpan.FromMilliseconds(Math.Min(index, StaggerCap) * StaggerStepMs);
SlideCore(element, 0f, ContentEnterOffset, Long, delay, null);
```

两个参数的选择依据：

- **步长 20 ms**：再小就看不出「逐个」，再大就变成排队。Fluent 自己的列表入场大约在
  20–50 ms 区间。
- **必须封顶**。不封顶的话，第 40 张卡片要等 800 ms 才开始动，而此时用户的视线早已落在
  网格上——看上去不像波浪，像卡顿。封顶 8 之后，后续元素与第 8 个同时入场，
  总时长稳定在 160 ms + 250 ms 以内。

### 5.5 触发时机：两种内容，两种正确写法

错峰要「在元素出现的那一刻」触发，而这两类容器的正确钩子不同：

**`ItemsRepeater`：用 `ElementPrepared`。**

```csharp
grid.ElementPrepared += (_, args) =>
{
    var delay = TimeSpan.FromMilliseconds(Math.Min(args.Index, StaggerCap) * StaggerStepMs);
    SlideCore(args.Element, 0f, ContentEnterOffset, Long, delay, null);
};
```

注意 `args.Element` 的静态类型是 `UIElement`，**不是** `FrameworkElement`。上文的辅助函数
因此全部以 `UIElement` 为形参，否则这里无法直接传入。

**`ListView`：不要用 `ContainerContentChanging`。**

`ContainerContentChanging` 在每次容器被回收复用并重新实现时都会再次触发，也就是**滚动时会
重播动画**——向上滚一格，卡片就再入场一次。正确做法是一次性标志 + 下一次布局完成：

```csharp
private bool _entrancePending;

// 列表数据源被整体替换时置位
private void ApplyItemsSource()
{
    ListView.ItemsSource = items;
    _entrancePending = true;
}

// 构造函数里订阅一次；空闲时只做一次布尔判断，开销可忽略
public MyControl() => LayoutUpdated += OnLayoutUpdated;

private void OnLayoutUpdated(object? sender, object e)
{
    if (!_entrancePending) { return; }
    if (PlayRowEntrance()) { _entrancePending = false; }
}

private bool PlayRowEntrance()
{
    var count = ListView.Items.Count;
    if (count == 0) { return true; }   // 空列表也要清标志，否则永久重试

    var animated = false;
    for (var i = 0; i < count; i++)
    {
        if (ListView.ContainerFromIndex(i) is not FrameworkElement container)
        {
            continue;   // 尚未实现，留到下一次布局
        }

        var delay = TimeSpan.FromMilliseconds(Math.Min(i, StaggerCap) * StaggerStepMs);
        SlideCore(container, 0f, ContentEnterOffset, Long, delay, null);
        animated = true;
    }

    return animated;
}
```

`PlayRowEntrance` 返回「是否已经处理过」，有两个作用：容器还没实现时不消费标志，下一次布局
再试；列表为空时清掉标志，避免每次布局都重试。

### 5.6 一致性靠名字，不靠参数

如果两个界面分别调用「同样的参数」，它们迟早会不一致。做法是把这条动效做成**一个具名行为**，
两个界面都只调用这个名字，调用方**不传**距离、时长、曲线：

```csharp
/// <summary>卡片网格与设置列表共用的入场。调用方不传任何参数。</summary>
public static void PosterEnter(UIElement element, TimeSpan? delay = null, Action? completed = null) =>
    SlideCore(element, 0f, ContentEnterOffset, Long, delay, completed);
```

这样「时长、曲线、触发时机一致」是由构造保证的，而不是靠两处代码碰巧写得一样。
改的时候也只有一个地方要改。

### 5.7 完成回调与所有权释放

涉及 `Visibility` 的动效**必须在完成回调里再判一次期望状态**——83 ms 足够让状态变两次：

```csharp
public static void FadeOut(UIElement element, Action? completed = null)
{
    // 切换为 Collapsed 只能发生在淡出结束之后，否则元素会在动画中途消失。
    Batch(compositor, () => { /* 淡出动画 */ }, completed);
}

// 调用方
MotionBehaviors.FadeOut(panel, () =>
{
    // 回调里重新读取「现在到底该不该隐藏」，而不是假设
    if (!_shouldBeVisible)
    {
        panel.Visibility = Visibility.Collapsed;
        MotionBehaviors.Reset(panel);   // 复位，否则下次显示时透明度还停在 0
    }
});
```

`Reset` 是必要的：动画结束后合成值仍处于终值，下一次显示如果不复位，元素会以「已经是 0
透明度」的状态开始淡入——或者干脆不出现。

---

## 6. 减弱动画闸门

系统设置里的「减弱动画」必须被尊重。闸门要**每次现读**，不能在设计期缓存：
用户切完系统开关后，下一次动效就该立刻生效，而不是要重启应用。

```csharp
public static class MotionGate
{
    /// <summary>本会话的强制开关。不持久化——它是个调试/演示开关，不是设置项。</summary>
    public static bool AppEnabled { get; set; } = true;

    public static bool Resolve() => AppEnabled && SystemAllowsAnimations();

    private static bool SystemAllowsAnimations()
    {
        try
        {
            return new UISettings().AnimationsEnabled;
        }
        catch
        {
            // 某些受限上下文里构造 UISettings 会抛。此时的选择是「按允许动效处理」——
            // 动效失败的方向应该是「没有动画」，而不是「应用启动不了」。
            return true;
        }
    }
}
```

**每个行为都要有无动画分支**，而不是让调用方去判断：

```csharp
if (!MotionGate.Resolve())
{
    visual.StopAnimation(Opacity);
    visual.Opacity = 1f;                 // 直接落到终态
    ParkTranslated(visual, 0f, 0f);      // 位移归零
    completed?.Invoke();                 // 回调立刻执行，调用方的状态机不卡住
    return;
}
```

最后一行尤其重要：回调必须立刻执行，否则调用方基于完成回调的后续逻辑（比如切换
`Visibility`）会一直不执行。

---

## 7. 具名行为清单

调用方只应该看到这些名字，看不到曲线和时长：

| 行为 | 曲线 | 时长 | 说明 |
|------|------|------|------|
| `FadeIn` / `FadeOut` | Linear | 83 ms | 纯透明度，用于覆盖层 |
| `ContentEnter` | Entrance | 167 ms | 通用入场 |
| `PosterEnter` | Entrance | 250 ms | 卡片网格 / 列表行的错峰入场 |
| `ContentExit` | Exit | 83 ms | 离开，位移与淡出同时进行 |
| `MoveTo` | Move | 167 ms | 已在屏元素改变位置 |
| `RowReveal` / `RowCollapse` | Entrance / Exit | 250 / 83 ms | 行内详情面板展开与收起 |
| `SectionSwitch` | Entrance | 167 ms | 内容区域被整体替换 |
| `SettleFrom` | Move | 167 ms | 布局已经跳变，把元素从旧位置补滑回新位置 |
| `ControlStateChange` | Move | 83 ms | 行级状态反馈（缩放脉冲） |
| `PanelReveal` / `PanelConceal` | Entrance / Exit | 167 / 83 ms | 整块浮动面板进出 |
| `DropdownEnter` | Entrance | 250 ms | 下拉内容展开 |
| `Reset` | — | — | 清掉合成值，把元素交还给 XAML |

### 7.1 `SettleFrom`：无法补间的布局变化

有些布局变化本身不可动画——比如 `NavigationView.PaneDisplayMode` 在「展开」与「图标栏」
之间切换，宽度是控件私有的布局属性，框架不给补间。这时可以先量出元素实际移动了多远，
再把它「推回」旧位置并滑到新位置：

```csharp
// 变化前：量出元素当前 X
var before = element.TransformToVisual(reference).TransformPoint(new Point(0, 0)).X;

// ... 触发那次不可动画的布局变化 ...

// 下一次布局完成后：量出新位置，把差值作为起点滑回 0
var now = element.TransformToVisual(reference).TransformPoint(new Point(0, 0)).X;
MotionBehaviors.SettleFrom(element, (float)(before - now));
```

这样容器本身仍是硬切，但用户视线所及的内容是平滑移动的。

### 7.2 `DropdownEnter`：接管框架自带动画

`ComboBox` 的展开动画由 `PopupThemeTransition` 驱动，**不暴露时长**。要改速度只能接管：
在模板树里找到那个 `Popup`，清掉它自己的 `ChildTransitions`，改由 `Popup.Opened` 驱动自己的
缩放 + 淡入。**不要为了改一个过渡而去复制整个控件模板**——那意味着接管几百行需要随框架
更新反复核对的 XAML。

```csharp
popup.ChildTransitions = null;   // 关掉框架的展开动画
popup.Opened += (_, _) =>
{
    if (popup.Child is UIElement content)
    {
        DropdownEnter(content);   // 用自己的 250 ms 缩放 + 淡入
    }
};
```

缩放要以**顶边中心**为原点，让面板向下长出来；用元素默认原点（左上角）会向右下飞出：

```csharp
var size = visual.Size;
if (size.X <= 0f || size.Y <= 0f)
{
    // 尚未布局，没有有意义的中心点：直接显示，不要以原点缩放
    visual.Scale = Vector3.One;
    completed?.Invoke();
    return;
}

visual.CenterPoint = new Vector3(size.X / 2f, 0f, 0f);
```

---

## 8. 移植清单

1. 新建一个动效模块目录，放入四件东西：令牌、曲线缓存、闸门、具名行为。
2. 全项目搜索 `Storyboard`、`DoubleAnimation`、`TranslateTransform`，逐个替换为具名行为。
3. 给每个需要入场的列表定一个触发钩子：`ItemsRepeater` 用 `ElementPrepared`，
   `ListView` 用一次性标志 + `LayoutUpdated`。
4. 把「同一类内容」的入场收敛到**同一个具名行为**，调用方不传参数。
5. 检查每一处涉及 `Visibility` 的动效：隐藏是否只发生在完成回调里，回调里是否重新判定了状态。
6. 检查每一处 `Collapsed` 之后再次显示：是否有 `Reset`。
7. 在无障碍/减弱动画开启的情况下过一遍全部界面：应当没有任何元素停在半途或不可见。

---

## 9. 已踩过的坑（按出现频率排序）

| # | 现象 | 原因 | 处理 |
|---|------|------|------|
| 1 | 只有第 1 个元素动画正常，其余先实心闪现再跳成透明 | `StopAnimation(Opacity)` 把不透明度恢复成 XAML 默认的 1，而带 `DelayTime` 的动画在延迟期内不写属性 | 停放阶段显式 `visual.Opacity = 0f` |
| 2 | 淡入与位移差一帧，看起来「先亮后动」 | 两个独立动画落在不同合成器 tick | `CreateScopedBatch` 圈成一批 |
| 3 | 元素永久停在偏移位置 | 用了 `Visual.Offset`（布局拥有），动画结束后仍持续填充 | 改用 `"Translation"` 属性集通道 |
| 4 | 滚动列表时动画反复重播 | 用了 `ContainerContentChanging`，容器复用时再次触发 | 一次性标志 + `LayoutUpdated` |
| 5 | 展开箭头方向不反转 | `AnimatedIcon` 的动画源在子命名空间，状态名写错 | `xmlns:x="using:Microsoft.UI.Xaml.Controls.AnimatedVisuals"`，状态用 `NormalOff` / `NormalOn` |
| 6 | 位移肉眼看不见，被当成「没有动画」 | 距离只有 8 px | 距离加大到 24–48 px，并按用途分开成常量 |
| 7 | 编译报「`IsEnabled` 不存在」 | `IsEnabled` 在 `Control` 上，不在 `FrameworkElement` 上 | 逐类型设置，或改用 `Control` 形参 |
| 8 | AOT 构建出现 trimming 警告/行为异常 | 集合表达式指向 `IReadOnlyList<T>`，没有确定的具象类型 | 改用具体 `List<T>` |
| 9 | `Double` 类型的资源填不进 `Padding` | `Padding` / `Margin` 是 `Thickness`，XAML 不做数值到 Thickness 的隐式转换 | 单独定义 `Thickness` 资源 |
| 10 | `<Thickness x:Key="A">{StaticResource B}</Thickness>` 编译失败 | 结构体类型资源的值不能写标记扩展 | 用 `<StaticResource x:Key="A" ResourceKey="B" />` 别名写法 |

---

## 10. 一句话总结

**曲线和时长收敛成常量并在一个模块里集中实现；调用方只调用「什么在出现」这样的具名行为；
位移一律走 Translation 属性集；错峰的距离要看得见、步长要封顶；每个动效都要有一条
「无动画」的直通分支；涉及 Visibility 的收尾一律放在完成回调里并重新判定状态。**

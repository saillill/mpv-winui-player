# 分区 I1 · 基础设施 · 动效（Motion）

## 职责

全应用的动效**令牌与曲线**，以及给 XAML 用的附加属性行为。

这里只有数学和附加属性，没有业务逻辑，也没有用户可见的语义。

## 入口文件

| 文件 | 作用 |
|---|---|
| `MotionTokens.cs` | 时长 / 位移 / 缓动令牌。**所有动画数值从这里取**，不要在 XAML 里写字面量 |
| `MotionEasing.cs` | 缓动函数（无过冲） |
| `SpringMath.cs` | 弹簧数学（纯函数，链接进 `mpv-conf-test`，有 `SpringCurveTests`） |
| `MotionGate.cs` | 系统「减少动画」开关的判定 |
| `MotionBehaviors.cs` | 附加属性：`FadeIn` / `Reset` 等（34 KB，最大的一个） |

## 对外接口

| 类型 | 成员 | 谁在用 |
|---|---|---|
| `MotionBehaviors` | `FadeIn(element)` / `Reset(element)` | 库切换、页面过渡 |
| `MotionGate` | `AnimationsEnabled` | 所有行为内部判定 |
| `MotionTokens` | 静态字段 | XAML 通过 `{StaticResource}` 或代码读取 |

## 依赖

- 只依赖 WinUI。**禁止**依赖任何 `Domains/*` 功能域与 `Shell`。

## 关键约定

- **没有过冲**：`21ce531` 把 `SpringPop` 改成 `Move` 曲线。若要做回弹，先确认
  产品是否接受（历史决定是不接受）。
- **`MotionGate` 必须被尊重**：用户开了「减少动画」时行为要降级为瞬时，不是缩短时长。
- **`SpringMath` 是纯函数**，不要在里面读 `AppContext` 或注册表——它要被单元测试链接。

## 上下文裁剪提示

加一个动画 → 只读 `MotionTokens.cs`（6 KB）+ `MotionBehaviors.cs` 里同名行为，
再跑 `dotnet test mpv-conf-test`（`SpringCurveTests` 会验证曲线性质）。

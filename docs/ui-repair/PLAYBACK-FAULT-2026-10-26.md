# 播放故障：根因与修复

**日期**：2026-10-26
**状态**：**已修复**（commit `2f3247a`）。本文取代之前所有"根因未找到"的记录。

---

## 1. 根因

`no-osd set idle no`

启动批次（`MpvSettings.BuildApplyAllCommands()`）有 **515 条命令**，每次启动都发，
且发生在用户第一次打开文件之前。第 **152** 条是它。

`AppSettings.Mpv_idle` 是 schema 生成的设置项，默认值照抄 mpv 自己的默认值 `"no"`，
于是批次把它发了出去，**覆盖了** `MpvPlayer::Initialize` 里刻意设置的 `idle=yes`。

内嵌播放器在启动时**永远**处于"没有文件"的状态。`idle=no` 下 mpv 会在这个状态下
退出播放循环。之后 `mpv_command` 对 `loadfile` 仍然返回 **0**，而 mpv 什么都不做——
没有 `START_FILE`、没有 `FILE_LOADED`、没有下一行日志。

这正是最初报告的症状。

## 2. 怎么找到的

不是读代码，是二分。

**第一步：在真实应用进程里做最小加载。** 在 `CreateAsync` 里（`InitializeAsync`
返回后）直接调 `LoadFile`，绕过所有应用逻辑：

```
plain Command(["osd-auto","loadfile",clip,"replace+play"])   -> FileLoaded #1
plain Command(...) from Task.Run                             -> FileLoaded #1  （线程排除）
startup batch 之后再 Command(...)                            -> 什么都没有，且之后永远没有
```

**第二步：二分那个批次。** 切点从一个临时文件读，一次构建跑完整个搜索：

| 前缀长度 | 结果 |
|---|---|
| 0 | 能加载 |
| 144 | 能加载 |
| 152 | 能加载 |
| **153** | **卡死** |

第 152 条 = `no-osd set idle no`。

## 3. 为什么之前的排查全都找不到

之前每一轮都是"改一个 mpv 选项再重测"，而改的都是 `mpv_initialize` **之前**的选项，
或者 `mpv.conf`。**没有一条改动会影响批次在 init 之后发送什么。**

批次有 515 条，所以"改一处再测"永远碰不到这一行。`vo=null`、
`d3d11-output-mode=window`、清空 `mpv.conf`、延迟 3 秒加载——全都仍然失败，
因为**没有一条能阻止 `idle=no` 被发送**。

这是一个方法论教训，不是运气问题：当症状是"命令被接受但什么也没发生"，
要二分的是**应用发给 mpv 的命令流**，而不是 mpv 的配置。

## 4. 修复

新增一个类别 `EmbeddingOwnedKeys`（`MpvSettings.cs`）：设置窗口可以显示、可以存值，
但**批次不得发送**，因为发送它会替换掉 `Initialize` 为内嵌场景做出的决定。

```csharp
nameof(AppSettings.Mpv_idle),                 // 没有 idle=yes，播放循环在打开文件前就退出了
nameof(AppSettings.Mpv_force_window),         // 默认 "no"
nameof(AppSettings.Mpv_d3d11_output_mode),    // 默认 "auto"，会破坏 composition 输出
```

后两项有同样的形状：默认值会撤销内嵌所需的配置。

## 5. 修复前后的测量

同一台机器、同一个剪辑（640x360、60 秒）：

| | 修复前 | 修复后 |
|---|---|---|
| `file-loaded` | 0 | **1** |
| `surface sized` | 960x540（空状态值） | **1280x720** |
| `time-pos` | 不可得 | **12.26 → 13.45** |
| `pause` / `idle` | — | **false / false** |
| `verify_playback` | FAIL | **PASS** |

## 6. 验证器的两处诚实修正

**第四项判据必须改。** 它原本抓四帧比对像素。修复后那一项**仍然是 0%**，但 mpv 确实
在播——`composition` 输出 + GDI 截图会把视频区域抓成平的。所以：

- 像素**有变化** → 直接接受为证明
- 像素**没变化**，但 `time-pos` 在推进且 `pause=false` → 记为 **INCONCLUSIVE**，
  判定回退到 mpv 自己的时钟（不解码就不会推进）
- 两者都不成立 → 真的失败

`--self-test` 仍然能变红。

---

## 7. 现在能重跑的证据

```bash
cd C:/Users/z2637/Documents/DeepSeek/mpv

python tools/mvpw-mcp/verify_playback.py            # 期望 VERDICT: PASS
python tools/mvpw-mcp/verify_playback.py --self-test # 期望 SELF-TEST PASSED
python tools/ui-repair/gate.py                      # 期望 9/9
```

二分用的探针保留在 `_work/probe/bisect_batch.py`（`_work` 不入库）。
它依赖应用里已经删掉的临时探针，所以现在不能直接重跑；重新需要时，
把 `_work/msg/09-idle-root-cause.txt` 里描述的那段 `IsolatedLoadProbe` 加回
`MpvPlayerPage.xaml.cs` 的 `CreateAsync` 即可。

## 8. 教训（写给下一个人）

1. **"命令被接受但无反应"要二分命令流，不是二分配置。** 515 条命令的批次里，
   一条错误的默认值就足以让整个功能失效，而且日志上完全沉默。
2. **默认值会覆盖刻意的设置。** `AppSettings` 里任何镜像 mpv 默认值的项，
   如果 `Initialize` 为内嵌场景设了别的值，它就是一个定时炸弹。
   新增 schema 生成项时值得问一句：这个选项内嵌播放器自己有主张吗？
3. **gate 全绿不等于产品能用。** 旧的 8/8 之所以是假绿，是因为判据是
   "最后一行含 OK"，且九个阶段里没有一个走过应用自己的打开路径。

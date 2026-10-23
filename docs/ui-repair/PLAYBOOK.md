# PLAYBOOK：每一轮的固定动作

`README.md` 说做什么，这里说**怎么做**。目标是让任意一个 agent 在信息不全时也能按步骤
推进，而不是凭感觉改代码。

---

## 第 0 步：取项

```bash
python tools/ui-repair/next.py
```

输出包含：缺陷 id、分区、当前状态、复现命令、该读的分区 README。

台账在 `QUEUE.md`。**只做 `next.py` 给出的那一项**，不要并行开多个。

---

## 第 1 步：复现（这一步不能跳）

### 1.1 先看它是否已经被复现过

台账里标 `repro: confirmed` 的项有已验证的复现脚本，直接跑：

```bash
python tools/ui-repair/repro.py <id>
```

标 `repro: unverified` 的项是**假设**，可能是探针问题（见 README §3.1 的误判表）。

### 1.2 新缺陷：两条独立路径

写复现时至少用两种不通用的测量，例如：

| 路径 | 工具 | 适合证明 |
|---|---|---|
| UI Automation | `get_ui_snapshot` / `assert_element_state` | 控件存在、可见、启用、位置 |
| mpv IPC | `_ipc(["get_property", ...])` | 播放状态、属性值 |
| 像素 | `scan-skins-pixels.py` 的 `PrintWindow` | 颜色、圆角、渲染差异 |
| 日志 | `%LOCALAPPDATA%\mpv-winui\logs\` | 异常、拒绝的命令 |

**选对工具比跑两次更重要。** 实例：三套皮肤的控件 rect 完全一致，据此判定"皮肤没
生效"；改测窗口像素后得到三个不同 hash，皮肤正常。`Template` 改的是圆角与画笔，控件
外框本来就不动。**几何相同 ≠ 视觉相同。**

两条路径给出**冲突**结论时，缺陷不存在或探针有问题——继续查探针，不要改产品。

### 1.3 IPC 的硬约束

一个进程**只在启动后约 1 秒内**接受一个连接，之后拒绝，空闲连接会死。所以：

- 需要 IPC 的探针必须先 `open_launch_window()`，然后**立刻**发请求；
- 连接后 `sleep` 会让会话死掉；
- 一次会话只够少数几个请求，超了用 232/109 拒绝；
- 想读两个东西，就开两次进程。

这不是产品缺陷，是本仓库嵌入 libmpv 的方式。探针必须适应。

---

## 第 2 步：定位

### 2.1 按分区加载

```bash
cat mpv-winui/mpv-winui/Modules/<Domain>/README.md
```

README 给出：职责、入口文件、公开接口、关键状态、依赖方向、验证命令。

### 2.2 找对齐基线

按 `BASELINE.md` 的顺序：

1. **官方**：`winapp find-api` 查真实 API；`winapp find-ui` 查官方控件示例
2. **上游**：`git fetch https://github.com/ikas-mc/mpv-winui-player.git main --depth=1`
   然后 `git show FETCH_HEAD:<path>` 看它怎么写
3. **本地令牌**：`Styles.xaml` 的 `Mpv*` 键、`MotionTokens.cs`

三者都查不到，先在 `BASELINE.md` 补依据再动手。

### 2.3 判断：移植、对齐、保留、清理

| 情况 | 动作 |
|---|---|
| 上游有、本地无、且是通用能力 | **移植**，保留版权头，记录到 `VERSIONS.md`（配置层）或台账 |
| 上游有、本地有、行为不同 | **对齐**到上游，除非本地差异有明确理由（写进台账） |
| 上游无、本地自研且有用 | **保留**，标注"本地扩展"+ 理由 |
| 上游删除、本地还留着 | **清理**，先查零引用 |

清理前必须 `git grep` 确认零引用，且不能是反射/x:Name 使用。

---

## 第 3 步：修

### 3.1 改 UI 的硬约束

- **先跑 `python tools/check-xaml.py`**。XAML 注释里不能有 `--`（`WMC9997`），
  这个仓库为此浪费过五次完整构建
- 间距/字号/圆角/动画**只用令牌**。新增令牌时同时更新 `Styles.xaml` 和此处说明
- 主题色用 `ThemeResource`，不要字面色
- 需要 `Vector3`（如 `Translation`）时，XAML 没有资源形式，用字面量 + 注释说明原因
- 不要在 `.Gen.cs` 文件里改代码，改生成器或 `mpv-options.json`

### 3.2 改行为的硬约束

- 设置项：读 `docs/settings-design-contract.md`，然后跑
  `check-sections.py` / `check-localization.py` / `check-settings-drift.py` /
  `check-empty-sections.py`
- 本地化：所有用户可见字符串走 `AppLang` × `Languages/*.json`（9 种语言）
- 跨语言契约（C# ↔ C++ 事件、Lua script-opts）：改了必须跑对应的 `check-*.py`

### 3.3 BOM 与行尾

本仓库 BOM 和行尾**不统一**，两种方向都出过事。改文件后确认：

```bash
git diff --stat            # 不能出现整文件重写
git diff --check           # 空白字符
```

`edit` 类工具会丢 BOM；Python 脚本可能加 BOM。判据是"与 HEAD 一致"，不是"与邻文件一致"。

---

## 第 4 步：验证（三层，全绿才算）

### 4.1 构建与静态检查

```bash
python tools/check-xaml.py
pwsh build.ps1 -Configuration Release -Platform x64     # 0 错误
```

全部 19 个 `tools/check-*.py` 必须 0 失败。

### 4.2 行为验证

至少跑与改动域相关的那几个：

```bash
python tools/mvpw-mcp/verify_e2e.py            # 19/19
python tools/mvpw-mcp/verify_settings.py       # 0 failure
python tools/mvpw-mcp/verify_settings_rows.py  # PASS
python tools/mvpw-mcp/verify_screenshot.py     # PASS
```

### 4.3 该缺陷自己的复现必须翻转

```bash
python tools/ui-repair/repro.py <id>           # 现在应当 PASS
```

**跑两次**。一次通过不算——本仓库的间歇性失败率高到一次结果没有意义。

### 4.4 一键闸门

```bash
python tools/ui-repair/gate.py
```

它按顺序跑上面全部内容，并在最后检查部署一致性（`differing: 0`）。

---

## 第 5 步：记录

```bash
python tools/ui-repair/done.py <id> --evidence "verify_e2e 19/19 x2; repro PASS x2"
```

它做三件事：

1. 台账该项标 `done`，写入证据与时间
2. 若改了产品代码，提示一起提交（`_work/msg/` 下写提交信息）
3. 打印下一项

**记录必须包含**：缺陷是什么、根因、怎么修的、用什么证明。不要写"已修复"三个字。

---

## 第 6 步：提交

一个缺陷一个提交。提交信息写清根因，不写"修复若干问题"。

```bash
git add -A <相关文件>
git commit -F _work/msg/<nn>-<slug>.txt
git push cloud main
```

---

## 遇到这些情况怎么办

| 情况 | 做法 |
|---|---|
| 复现不出 | 台账标 `cannot-reproduce`，写明试了什么，取下一项 |
| 修完验证仍失败 | 回滚该提交，台账标 `blocked` 并写清卡在哪一步 |
| 发现是探针问题 | 修探针，台账标 `probe-bug`，**不要改产品** |
| 需要改 XAML 但 `check-xaml` 报错 | 先修注释里的 `--`，这是已知坑 |
| 两个方案都合理 | 选改动小的那个，写进台账"决策记录" |
| 影响面大且不可逆 | 这是允许停下的第一种情况，问用户 |
| 不断出现新缺陷 | 正常。把它们加进台账，继续循环 |

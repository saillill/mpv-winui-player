# mpv-winui-player（自用分支）

基于 [ikas-mc/mpv-winui-player](https://github.com/ikas-mc/mpv-winui-player) 的 mpv WinUI 3 前端。

**这是我自用的分支**：按自己的使用习惯改，不追求通用性，也不保证与上游同步。
播放内核就是 mpv 本体，界面用 WinUI 3 重写，不需要命令行，常用操作都在界面上。

## 已实现功能

**播放**

- 自研控制栏：自适应布局引擎、进度条标记、面板动画
- 快速控制面板（音频 / 字幕 / 视频）、轨道选择、音量飞出
- 画中画（PiP），可拖动调整大小
- 宽高比 / 裁剪，按面板实际尺寸锁定
- 内置缩略图预览，兼容 `osc-preview-api` 插件提供的预览

**菜单**

- 菜单栏 + 菜单编辑器：树形编辑、拖动排序，改动写回 `menus.conf`
- 播放列表右键菜单、快捷键提示

**设置**

- 选项以卡片 + 二级文件夹组织，可拖动排序、新建文件夹、隐藏/显示、重命名
- 主题、界面字体、窗口背景材质
- mpv.conf 编辑器（按 schema 分类，支持 profile）
- 媒体信息（MediaInfo）、播放历史 / 稍后观看
- 文件关联与协议注册
- 9 种界面语言，1225 条文案（另有自定义模式的一批键，来自 JSON 而非 AppLang）

**配置层**

- 自带一份 mpv 配置（`mpv-winui-lazy/`）：`mpv.conf` / `input.conf` / `profiles.conf`、
  一组脚本（HDR / VSR 自动切换、封面、最近打开、stats、console、select …）、shader 与工具
- 启动时同步到 `%LOCALAPPDATA%\mpv-winui\mpv`：属于我们的文件会被更新；
  用户改过的文件只备份、不覆盖；我们不再提供的文件会被清理

## 已知限制

播放使用 `d3d11-output-mode=composition`，mpv 拿不到显示信息，靠自定义属性补偿：

```
user-data/mpvw/color-kind    : SDR / WCG / HDR
user-data/mpvw/refresh-rate  : 60
```

部分 mpv 命令不支持（退出、窗口相关等）。只维护 Windows x64。

## 与上游的关系

差异清单见 [`docs/compare-upstream.md`](docs/compare-upstream.md)。简单说：

- **比上游多**：设置界面的整套改造、本地化（上游基本没有）、自研控制栏与快速面板、
  菜单编辑器、配置层与部署清理，以及一套自检脚本（`tools/`）
- **已按上游对齐**：原先为了兼容而保留的一层 wrapper 垫片已经拆掉，调用点全部改为直连原生
  `MpvPlayer` API；上游三个未合并的提交（轨道选择器绑定、菜单编辑器、README）也已并入

目前与上游的关系是"功能超集 + 架构同向"：原生层是严格超集（`MpvPlayer.idl` 101 条声明
对上游 87 条，上游没有任何我们没有的 API），改动方向也与上游一致。

## 开发

```bash
# 只编 C# 层（约 30 秒，不编 C++）
dotnet build mpv-winui/mpv-winui/mpv-winui.csproj -c Release -p:Platform=x64 \
    -p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false

# 完整构建（含 C++ 原生层）
./build.ps1
```

自检脚本：

```bash
python tools/check-localization.py     # 各语言键一致性
python tools/check-settings-drift.py   # 设置项与映射漂移
python tools/check-ui-tooltips.py      # 界面提示文案
```

## 许可

LGPL-2.1，与上游一致。第三方组件与来源见
[`mpv-winui-lazy/licenses/THIRD_PARTY_NOTICES.md`](mpv-winui-lazy/licenses/THIRD_PARTY_NOTICES.md)。

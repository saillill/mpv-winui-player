# 筛选日志

本文件记录 `mpvw` 仓库历史重建的**每一处处置**。没有这份日志，筛选就是不可复核的，等同于随意删改历史。

## 一、对象与结果

| 项 | 值 |
|---|---|
| 源仓库 | `mpv-clean`（751 提交，HEAD `d4bda5e`） |
| 筛选范围 | 改造前 `14ecb30` 的 750 条提交 |
| 第一父线 | **710** 条（67 `ikas` + 643 `saillill`） |
| 侧枝 | **40** 条（`cc98273^2` 并入的 `ikas`） |
| 折叠上游基线 | 107 条 `ikas` → **1** 条 |
| `saillill` 处置 | 643 → **103** 保留 + 527 合并 + 13 剔除 |
| 结构重建 | **9** 条（M1–M5b + 文档清理，保持原样） |
| **最终提交数** | **113**（区间要求 80–120） |

## 二、筛选手法

按**阶段分段**而非按日历天。段边界是一条"开启新阶段"的提交，两边界之间的所有提交属于同一阶段。段内规则：

1. **5 个验收锚点缺陷无条件保留**，保证"某个缺陷是哪个提交修的"始终可答：

   | 缺陷 | 提交 |
   |---|---|
   | 启动崩溃：App.xaml 指向旧路径 | `64e4bfa` |
   | 设置窗口崩溃：Preset/CheckForUpdates 未映射 | `ad1128a` |
   | 专辑封面丢失 | `8cb9f27` |
   | 二级设置页空白 | `2edf6a7` |
   | 控制栏拖拽门控 | `04a8f43` |

2. 结构性提交（≥10 文件）按大小取前 6
3. 修复类按"命中的区域"分组，每区域留最大一条
4. 检查器/门禁类建立者全留
5. 每段至多 1 条文档提交
6. **每段硬上限 10 条**

## 三、上游基线的依据

107 条 `ikas` 提交折叠为 1 条，前提是**两类作者在时间上零交错**：

| | 最早 | 最晚 |
|---|---|---|
| `ikas`（第一父线 67 条） | 2026-07-15 `8ad7118` | 2026-08-05 `ae57d2f` |
| `saillill`（643 条） | 2026-08-07 `082aded` | 2026-10-27 `14ecb30` |

`ikas` 出现在首个 `saillill` 之后 = **0 条**。因此折叠不丢先后关系，符合 `02-TASK.md` 要求 3 第 1 条。

`ikas` 侧枝 40 条来自 `cc98273^2`（末条 `2b74e2a`，2026-09-12），是上游原始历史的延续，与第一父线的 67 条同属一个上游序列，一并折叠。

## 四、逐条处置

| 旧提交 | 日期 | 文件数 | 处置 | 理由 | 并入到哪一条 |
|---|---|---|---|---|---|
| `082aded` | 2026-08-07 | 95 | 保留 | 结构性提交（95 文件） | - |
| `169e7e5` | 2026-08-07 | 1 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `1a225bd` | 2026-08-07 | 2 | 保留 | 修复「ci」族的代表提交 | - |
| `9d3a5ef` | 2026-08-07 | 2 | 保留 | 修复「file」族的代表提交 | - |
| `e681b83` | 2026-08-07 | 1 | 保留 | 修复「workflow」族的代表提交 | - |
| `eded741` | 2026-08-07 | 4 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `174d8d9` | 2026-08-08 | 1 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `1fec6c6` | 2026-08-08 | 6 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `2292e74` | 2026-08-08 | 3 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `28510aa` | 2026-08-08 | 2 | 保留 | 修复「settings」族的代表提交 | - |
| `2ca2265` | 2026-08-08 | 21 | 保留 | 结构性提交（21 文件） | - |
| `3693eb8` | 2026-08-08 | 20 | 保留 | 结构性提交（20 文件） | - |
| `36be296` | 2026-08-08 | 1 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `38e79b8` | 2026-08-08 | 19 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `3f1d167` | 2026-08-08 | 1 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `462aa6d` | 2026-08-08 | 1 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `462cedc` | 2026-08-08 | 29 | 保留 | 结构性提交（29 文件） | - |
| `515e71b` | 2026-08-08 | 6 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `695f8fb` | 2026-08-08 | 29 | 保留 | 结构性提交（29 文件） | - |
| `6a580ff` | 2026-08-08 | 13 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `6a993d8` | 2026-08-08 | 16 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `7cf86b4` | 2026-08-08 | 15 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `80667a1` | 2026-08-08 | 17 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `83c1c55` | 2026-08-08 | 20 | 保留 | 结构性提交（20 文件） | - |
| `8f15c30` | 2026-08-08 | 1 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `9ba3e48` | 2026-08-08 | 2 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `a745a12` | 2026-08-08 | 18 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `b0aeb0c` | 2026-08-08 | 13 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `b36f4a1` | 2026-08-08 | 17 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `b948a11` | 2026-08-08 | 1 | 合并 | same phase (segment 1); folded into that phase's representative commit | `082aded` |
| `b9a666f` | 2026-08-08 | 21 | 保留 | 结构性提交（21 文件） | - |
| `bb1f598` | 2026-08-08 | 22 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `bb23c01` | 2026-08-08 | 25 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `c0046e8` | 2026-08-08 | 29 | 保留 | 结构性提交（29 文件） | - |
| `c2c1a5d` | 2026-08-08 | 13 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `c393440` | 2026-08-08 | 12 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `c94e33f` | 2026-08-08 | 14 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `cc54d4a` | 2026-08-08 | 16 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `cd3e753` | 2026-08-08 | 2 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `d17e267` | 2026-08-08 | 24 | 保留 | 结构性提交（24 文件） | - |
| `d8e3a43` | 2026-08-08 | 2 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `ed6e0b9` | 2026-08-08 | 16 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `f021bb8` | 2026-08-08 | 17 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `f5ed2c3` | 2026-08-08 | 1 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `f717f1b` | 2026-08-08 | 19 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `fc6ab7f` | 2026-08-08 | 1 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `0e2ad9d` | 2026-08-09 | 8 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `16abab9` | 2026-08-09 | 17 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `29af27e` | 2026-08-09 | 25 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `2f988ab` | 2026-08-09 | 18 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `3588d3a` | 2026-08-09 | 27 | 保留 | 结构性提交（27 文件） | - |
| `3e75172` | 2026-08-09 | 2 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `55abc0e` | 2026-08-09 | 22 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `7cf294c` | 2026-08-09 | 39 | 保留 | 结构性提交（39 文件） | - |
| `919548d` | 2026-08-09 | 1 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `9388313` | 2026-08-09 | 15 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `a5a9db0` | 2026-08-09 | 3 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `a64311e` | 2026-08-09 | 3 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `ac7a8f1` | 2026-08-09 | 15 | 保留 | 结构性提交（15 文件） | - |
| `b355895` | 2026-08-09 | 30 | 保留 | 结构性提交（30 文件） | - |
| `b3d2c5d` | 2026-08-09 | 2 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `d328c97` | 2026-08-09 | 20 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `d76ffe3` | 2026-08-09 | 20 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `d81d074` | 2026-08-09 | 1 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `dd070b5` | 2026-08-09 | 1 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `df9dc4a` | 2026-08-09 | 14 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `e06c7ba` | 2026-08-09 | 24 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `e2cbaa9` | 2026-08-09 | 23 | 保留 | 结构性提交（23 文件） | - |
| `e3ccfed` | 2026-08-09 | 7 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `e6a73ee` | 2026-08-09 | 16 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `ecf062c` | 2026-08-09 | 1 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `044ac9c` | 2026-08-10 | 3 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `174378b` | 2026-08-10 | 2 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `336ac67` | 2026-08-10 | 12 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `36198ca` | 2026-08-10 | 6 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `4aa1aff` | 2026-08-10 | 1 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `60f4b3e` | 2026-08-10 | 3 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `6c887a8` | 2026-08-10 | 3 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `7d97e23` | 2026-08-10 | 3 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `87163eb` | 2026-08-10 | 6 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `976cf3e` | 2026-08-10 | 23 | 保留 | 结构性提交（23 文件） | - |
| `af70f45` | 2026-08-10 | 4 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `b88e7bc` | 2026-08-10 | 6 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `c1caa08` | 2026-08-10 | 35 | 保留 | 结构性提交（35 文件） | - |
| `c290908` | 2026-08-10 | 6 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `cf57627` | 2026-08-10 | 1 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `d0de09a` | 2026-08-10 | 5 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `dc312aa` | 2026-08-10 | 8 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `ddc393b` | 2026-08-10 | 3 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `e1159e5` | 2026-08-10 | 6 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `edc11b2` | 2026-08-10 | 1 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `f1bd7d4` | 2026-08-10 | 20 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `0f150d8` | 2026-08-11 | 4 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `21532f1` | 2026-08-11 | 8 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `24d16bd` | 2026-08-11 | 2 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `2cde5e5` | 2026-08-11 | 1 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `2d489e5` | 2026-08-11 | 3 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `2e5ec4c` | 2026-08-11 | 12 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `49bec5b` | 2026-08-11 | 2 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `4e2555f` | 2026-08-11 | 39 | 保留 | 结构性提交（39 文件） | - |
| `6a91fff` | 2026-08-11 | 2 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `6dbd43a` | 2026-08-11 | 3 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `7316649` | 2026-08-11 | 2 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `79d6b1a` | 2026-08-11 | 2 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `810f47b` | 2026-08-11 | 2 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `815fe77` | 2026-08-11 | 8 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `8739d6a` | 2026-08-11 | 5 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `8ea6654` | 2026-08-11 | 2 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `95d1394` | 2026-08-11 | 5 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `989ff8f` | 2026-08-11 | 3 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `98da6ca` | 2026-08-11 | 4 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `9a250a9` | 2026-08-11 | 7 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `9c4c0b0` | 2026-08-11 | 9 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `a94ef89` | 2026-08-11 | 4 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `aac6f00` | 2026-08-11 | 5 | 合并 | same phase (segment 3); folded into that phase's representative commit | `4e2555f` |
| `c109ca6` | 2026-08-11 | 2 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `c5f118e` | 2026-08-11 | 2 | 保留 | 本阶段代表文档提交 | - |
| `cab728e` | 2026-08-11 | 1 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `cee0ab6` | 2026-08-11 | 1 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `d195a4e` | 2026-08-11 | 12 | 保留 | 结构性提交（12 文件） | - |
| `dce022c` | 2026-08-11 | 2 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `ec19d0a` | 2026-08-11 | 5 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `edcd1af` | 2026-08-11 | 2 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `f421145` | 2026-08-11 | 2 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `fd8c5da` | 2026-08-11 | 5 | 保留 | 修复「pip」族的代表提交 | - |
| `033c1a0` | 2026-08-12 | 3 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `04a8f43` | 2026-08-12 | 1 | 保留 | 验收锚点缺陷，必须可追溯 | - |
| `0a06e2b` | 2026-08-12 | 16 | 保留 | 结构性提交（16 文件） | - |
| `0b4e553` | 2026-08-12 | 2 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `13069c6` | 2026-08-12 | 1 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `1549b99` | 2026-08-12 | 17 | 保留 | 结构性提交（17 文件） | - |
| `158935b` | 2026-08-12 | 1 | 保留 | 修复「multi-file」族的代表提交 | - |
| `1f67cb0` | 2026-08-12 | 2 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `27058ed` | 2026-08-12 | 4 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `2a66cdd` | 2026-08-12 | 1 | 保留 | 建立检查器/门禁 | - |
| `2b136ec` | 2026-08-12 | 2 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `2b897f0` | 2026-08-12 | 12 | 保留 | 结构性提交（12 文件） | - |
| `3358f4c` | 2026-08-12 | 6 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `3688448` | 2026-08-12 | 1 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `372e24e` | 2026-08-12 | 1 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `3aca28d` | 2026-08-12 | 5 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `3cd30ff` | 2026-08-12 | 1 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `3ce4e9d` | 2026-08-12 | 5 | 合并 | same phase (segment 4); folded into that phase's representative commit | `1549b99` |
| `3dc83a2` | 2026-08-12 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `3fd62e6` | 2026-08-12 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `4453e25` | 2026-08-12 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `4b0079d` | 2026-08-12 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `4b78c86` | 2026-08-12 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `4bb5595` | 2026-08-12 | 3 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `4d2060f` | 2026-08-12 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `5d386ff` | 2026-08-12 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `62112e3` | 2026-08-12 | 12 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `69161e7` | 2026-08-12 | 14 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `6a01612` | 2026-08-12 | 3 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `6ae0526` | 2026-08-12 | 22 | 保留 | 结构性提交（22 文件） | - |
| `6c3101f` | 2026-08-12 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `7088828` | 2026-08-12 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `70edbcf` | 2026-08-12 | 13 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `75d6934` | 2026-08-12 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `7688d51` | 2026-08-12 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `7d22d10` | 2026-08-12 | 3 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `854e8bf` | 2026-08-12 | 13 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `8fb5558` | 2026-08-12 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `94952e6` | 2026-08-12 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `98740dd` | 2026-08-12 | 3 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `9a72d57` | 2026-08-12 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `9c0ba4c` | 2026-08-12 | 3 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `9c4a0f9` | 2026-08-12 | 14 | 保留 | 结构性提交（14 文件） | - |
| `9d95e6b` | 2026-08-12 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `a17716a` | 2026-08-12 | 4 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `a195e7e` | 2026-08-12 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `a304540` | 2026-08-12 | 12 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `abf95cb` | 2026-08-12 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `ad1128a` | 2026-08-12 | 1 | 保留 | 验收锚点缺陷，必须可追溯 | - |
| `aef1fe3` | 2026-08-12 | 3 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `bdb03c5` | 2026-08-12 | 10 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `c13e123` | 2026-08-12 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `c1c90bd` | 2026-08-12 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `c1cfc2f` | 2026-08-12 | 7 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `c469885` | 2026-08-12 | 6 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `c91b604` | 2026-08-12 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `d1a2be3` | 2026-08-12 | 11 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `df83442` | 2026-08-12 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `e6c428d` | 2026-08-12 | 12 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `f069d8e` | 2026-08-12 | 4 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `f252ddd` | 2026-08-12 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `f6561db` | 2026-08-12 | 12 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `f93514c` | 2026-08-12 | 11 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `fb6d291` | 2026-08-12 | 15 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `fe25b4d` | 2026-08-12 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `ff32a0f` | 2026-08-12 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `021efda` | 2026-08-13 | 11 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `07e9004` | 2026-08-13 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `112b6cc` | 2026-08-13 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `12863d7` | 2026-08-13 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `14d60f9` | 2026-08-13 | 16 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `1719bc7` | 2026-08-13 | 9 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `1fd41e5` | 2026-08-13 | 18 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `2ac44ab` | 2026-08-13 | 3 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `3606a2f` | 2026-08-13 | 10 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `58d64c7` | 2026-08-13 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `7283704` | 2026-08-13 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `7dbb7b5` | 2026-08-13 | 10 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `86ab0fe` | 2026-08-13 | 15 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `a1c4930` | 2026-08-13 | 20 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `a5c30e6` | 2026-08-13 | 27 | 保留 | 结构性提交（27 文件） | - |
| `b357e57` | 2026-08-13 | 14 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `bd8b4f6` | 2026-08-13 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `bdce2bf` | 2026-08-13 | 19 | 保留 | 结构性提交（19 文件） | - |
| `bf1e127` | 2026-08-13 | 4 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `d457121` | 2026-08-13 | 15 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `d96b195` | 2026-08-13 | 11 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `dc50604` | 2026-08-13 | 16 | 保留 | 结构性提交（16 文件） | - |
| `df7d0bd` | 2026-08-13 | 18 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `fb27b75` | 2026-08-13 | 7 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `fb78ce0` | 2026-08-13 | 12 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `a944749` | 2026-08-14 | 64 | 保留 | 结构性提交（64 文件） | - |
| `119580c` | 2026-08-16 | 4 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `143f742` | 2026-08-16 | 3 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `19abc69` | 2026-08-16 | 24 | 保留 | 结构性提交（24 文件） | - |
| `23edd2c` | 2026-08-16 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `3d29f72` | 2026-08-16 | 30 | 保留 | 结构性提交（30 文件） | - |
| `3e36071` | 2026-08-16 | 3 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `4c6c213` | 2026-08-16 | 5 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `4e9582a` | 2026-08-16 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `5f11c41` | 2026-08-16 | 3 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `6019d14` | 2026-08-16 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `7961511` | 2026-08-16 | 21 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `7df8dfe` | 2026-08-16 | 3 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `92b81e8` | 2026-08-16 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `c89c735` | 2026-08-16 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `cd72384` | 2026-08-16 | 5 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `d363640` | 2026-08-16 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `d61113a` | 2026-08-16 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `e36be46` | 2026-08-16 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `e38e24a` | 2026-08-16 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `e4a9946` | 2026-08-16 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `eb94dab` | 2026-08-16 | 13 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `ef5d382` | 2026-08-16 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `f1d3df1` | 2026-08-16 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `f5764e6` | 2026-08-16 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `29c8a5a` | 2026-08-17 | 32 | 保留 | 结构性提交（32 文件） | - |
| `2c482eb` | 2026-08-17 | 3 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `3f9918b` | 2026-08-17 | 13 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `426e032` | 2026-08-17 | 13 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `5a04458` | 2026-08-17 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `8e1548a` | 2026-08-17 | 4 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `be287e0` | 2026-08-17 | 8 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `c1eee48` | 2026-08-17 | 5 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `cc0b526` | 2026-08-17 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `d1201c1` | 2026-08-17 | 17 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `06e1eee` | 2026-08-22 | 8 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `6c418cf` | 2026-08-22 | 4 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `a426ba9` | 2026-08-22 | 14 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `0fe8339` | 2026-08-23 | 10 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `2579f5c` | 2026-08-23 | 11 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `27f3f97` | 2026-08-23 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `3c65b92` | 2026-08-23 | 4 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `4a598df` | 2026-08-23 | 2 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `5b7b556` | 2026-08-23 | 16 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `601736d` | 2026-08-23 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `671d68e` | 2026-08-23 | 11 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `7ca770b` | 2026-08-23 | 1 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `8d9d611` | 2026-08-23 | 3 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `9b11e99` | 2026-08-23 | 3 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `cc799ad` | 2026-08-23 | 15 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `e77b2e3` | 2026-08-23 | 11 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `eacc0ee` | 2026-08-23 | 11 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `f2a963e` | 2026-08-23 | 11 | 合并 | same phase (segment 5); folded into that phase's representative commit | `a944749` |
| `04e44cf` | 2026-08-24 | 1 | 合并 | same phase (segment 6); folded into that phase's representative commit | `66c2670` |
| `083543c` | 2026-08-24 | 2 | 合并 | same phase (segment 6); folded into that phase's representative commit | `66c2670` |
| `2b43497` | 2026-08-24 | 1 | 合并 | same phase (segment 6); folded into that phase's representative commit | `66c2670` |
| `4d8a7da` | 2026-08-24 | 8 | 合并 | same phase (segment 6); folded into that phase's representative commit | `66c2670` |
| `50c5215` | 2026-08-24 | 17 | 保留 | 结构性提交（17 文件） | - |
| `66c2670` | 2026-08-24 | 20 | 保留 | 结构性提交（20 文件） | - |
| `6c32031` | 2026-08-24 | 1 | 合并 | same phase (segment 6); folded into that phase's representative commit | `66c2670` |
| `777202d` | 2026-08-24 | 1 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `8e82f85` | 2026-08-24 | 11 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `9f33719` | 2026-08-24 | 1 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `a01244d` | 2026-08-24 | 1 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `a3d20d4` | 2026-08-24 | 11 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `a3db2ad` | 2026-08-24 | 17 | 保留 | 结构性提交（17 文件） | - |
| `aa94923` | 2026-08-24 | 13 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `befa769` | 2026-08-24 | 1 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `bfa4875` | 2026-08-24 | 1 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `c3251c9` | 2026-08-24 | 1 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `c44ff5a` | 2026-08-24 | 1 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `c5c49b0` | 2026-08-24 | 1 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `c5eafb1` | 2026-08-24 | 20 | 保留 | 结构性提交（20 文件） | - |
| `d016e9e` | 2026-08-24 | 2 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `d99a9c6` | 2026-08-24 | 2 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `dabcf47` | 2026-08-24 | 1 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `f081b3e` | 2026-08-24 | 1 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `5574dc4` | 2026-08-26 | 1 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `ee419ad` | 2026-08-26 | 1 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `0b36b43` | 2026-08-28 | 7 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `1693af3` | 2026-08-28 | 5 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `4101787` | 2026-08-28 | 23 | 保留 | 结构性提交（23 文件） | - |
| `9402a3d` | 2026-08-28 | 16 | 保留 | 结构性提交（16 文件） | - |
| `b20b667` | 2026-08-28 | 7 | 保留 | 本阶段代表文档提交 | - |
| `b4be889` | 2026-08-28 | 8 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `00522e9` | 2026-08-29 | 5 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `01e026f` | 2026-08-29 | 2 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `0725de0` | 2026-08-29 | 3 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `1a433ad` | 2026-08-29 | 2 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `2c18e40` | 2026-08-29 | 17 | 保留 | 结构性提交（17 文件） | - |
| `2dcba70` | 2026-08-29 | 14 | 保留 | 结构性提交（14 文件） | - |
| `4bc00d1` | 2026-08-29 | 2 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `4fa4aa3` | 2026-08-29 | 2 | 保留 | 修复「quit」族的代表提交 | - |
| `6ec8e48` | 2026-08-29 | 2 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `728186d` | 2026-08-29 | 17 | 保留 | 结构性提交（17 文件） | - |
| `9429cf1` | 2026-08-29 | 1 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `a0c5b4d` | 2026-08-29 | 3 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `ad24584` | 2026-08-29 | 11 | 保留 | 结构性提交（11 文件） | - |
| `bf0f924` | 2026-08-29 | 7 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `d6f5759` | 2026-08-29 | 1 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `d9ae7ef` | 2026-08-29 | 12 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `db13bc1` | 2026-08-29 | 14 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `e04a4ef` | 2026-08-29 | 9 | 合并 | same phase (segment 7); folded into that phase's representative commit | `4101787` |
| `222be73` | 2026-08-30 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `05c689e` | 2026-08-31 | 4 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `149eef9` | 2026-08-31 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `1a82990` | 2026-08-31 | 3 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `3436314` | 2026-08-31 | 4 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `392ec9a` | 2026-08-31 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `3bd945f` | 2026-08-31 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `4327ff6` | 2026-08-31 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `47edafc` | 2026-08-31 | 13 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `4995b2a` | 2026-08-31 | 3 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `56a5964` | 2026-08-31 | 4 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `6006d6d` | 2026-08-31 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `6aaf723` | 2026-08-31 | 14 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `6d1e337` | 2026-08-31 | 6 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `7e34557` | 2026-08-31 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `83f6dca` | 2026-08-31 | 11 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `8da16c9` | 2026-08-31 | 3 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `9582a9d` | 2026-08-31 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `a00d832` | 2026-08-31 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `a5453e6` | 2026-08-31 | 10 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `b4cd3b5` | 2026-08-31 | 3 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `c7a7140` | 2026-08-31 | 4 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `d0fb5aa` | 2026-08-31 | 15 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `d700454` | 2026-08-31 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `dbbf1ac` | 2026-08-31 | 3 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `f12f4a9` | 2026-08-31 | 13 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `fc96822` | 2026-08-31 | 3 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `93488e0` | 2026-09-01 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `638c515` | 2026-09-02 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `7ea194e` | 2026-09-02 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `be60bf1` | 2026-09-02 | 9 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `56feb65` | 2026-09-11 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `61d2d45` | 2026-09-11 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `e08d3df` | 2026-09-11 | 5 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `e757590` | 2026-09-11 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `0be77ab` | 2026-09-13 | 6 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `3682f0e` | 2026-09-13 | 14 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `482ef5a` | 2026-09-13 | 37 | 保留 | 结构性提交（37 文件） | - |
| `4ddd2f1` | 2026-09-13 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `64e4bfa` | 2026-09-13 | 1 | 保留 | 验收锚点缺陷，必须可追溯 | - |
| `65860bd` | 2026-09-13 | 33 | 保留 | 结构性提交（33 文件） | - |
| `700bdea` | 2026-09-13 | 5 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `908d6c0` | 2026-09-13 | 26 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `9ad3ca8` | 2026-09-13 | 16 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `abe8b62` | 2026-09-13 | 3 | 保留 | 修复「settings」族的代表提交 | - |
| `cc98273` | 2026-09-13 | 162 | 保留 | 结构性提交（162 文件） | - |
| `f2723ae` | 2026-09-13 | 16 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `09ea310` | 2026-09-14 | 3 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `302157d` | 2026-09-14 | 13 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `38565f7` | 2026-09-14 | 34 | 保留 | 结构性提交（34 文件） | - |
| `816e68d` | 2026-09-14 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `95c5899` | 2026-09-14 | 13 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `97a0e93` | 2026-09-14 | 21 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `ae929f3` | 2026-09-14 | 11 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `b04f438` | 2026-09-14 | 20 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `bb4f905` | 2026-09-14 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `bc326ce` | 2026-09-14 | 15 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `c6ae98a` | 2026-09-14 | 20 | 保留 | 结构性提交（20 文件） | - |
| `74851c9` | 2026-09-15 | 5 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `c67c047` | 2026-09-15 | 16 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `bee1f0c` | 2026-09-16 | 25 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `ec72d7d` | 2026-09-16 | 18 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `144650a` | 2026-09-17 | 68 | 保留 | 结构性提交（68 文件） | - |
| `3048aeb` | 2026-09-17 | 11 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `47f30f4` | 2026-09-17 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `7b502ad` | 2026-09-17 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `7f4db58` | 2026-09-17 | 12 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `1d7dfbd` | 2026-09-18 | 12 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `1f536f0` | 2026-09-18 | 5 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `37d8f4c` | 2026-09-18 | 27 | 保留 | 结构性提交（27 文件） | - |
| `40d37bc` | 2026-09-18 | 7 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `4e0235f` | 2026-09-18 | 12 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `546b0dc` | 2026-09-18 | 14 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `5ba793b` | 2026-09-18 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `6dddd9d` | 2026-09-18 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `74e62c3` | 2026-09-18 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `86bb2fb` | 2026-09-18 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `93bf0b4` | 2026-09-18 | 8 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `ad45f8e` | 2026-09-18 | 16 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `aef9c09` | 2026-09-18 | 12 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `d80fbbc` | 2026-09-18 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `e1f40dd` | 2026-09-18 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `edcb3f1` | 2026-09-18 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `f299569` | 2026-09-18 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `f341226` | 2026-09-18 | 17 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `3053352` | 2026-09-19 | 17 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `338c7be` | 2026-09-19 | 14 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `36b9c96` | 2026-09-19 | 1 | 保留 | 建立检查器/门禁 | - |
| `5033bee` | 2026-09-19 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `5e0cbcb` | 2026-09-19 | 17 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `61d6526` | 2026-09-19 | 14 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `75d0bf8` | 2026-09-19 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `7d64455` | 2026-09-19 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `7ebec6d` | 2026-09-19 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `80807f1` | 2026-09-19 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `8363550` | 2026-09-19 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `9119d7a` | 2026-09-19 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `98d8e9c` | 2026-09-19 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `9fc370a` | 2026-09-19 | 9 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `a16c04f` | 2026-09-19 | 19 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `a335c19` | 2026-09-19 | 24 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `a5bad38` | 2026-09-19 | 25 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `c0c1472` | 2026-09-19 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `c5a083d` | 2026-09-19 | 16 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `ca81817` | 2026-09-19 | 15 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `de4496c` | 2026-09-19 | 7 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `e206564` | 2026-09-19 | 22 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `e541f19` | 2026-09-19 | 5 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `e77cae4` | 2026-09-19 | 17 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `162d307` | 2026-09-20 | 3 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `5858255` | 2026-09-20 | 3 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `66bb430` | 2026-09-20 | 7 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `68bddad` | 2026-09-20 | 7 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `75ae472` | 2026-09-20 | 5 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `7bdbd8a` | 2026-09-20 | 19 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `895b0cd` | 2026-09-20 | 6 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `8f38ac5` | 2026-09-20 | 1 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `90d2e43` | 2026-09-20 | 8 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `9447ab1` | 2026-09-20 | 2 | 合并 | same phase (segment 8); folded into that phase's representative commit | `cc98273` |
| `994532f` | 2026-09-20 | 3 | 保留 | 构建或合并契约 | - |
| `9a4f6ec` | 2026-09-20 | 3 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `9aa3a11` | 2026-09-20 | 14 | 保留 | 结构性提交（14 文件） | - |
| `9f5e0ec` | 2026-09-20 | 8 | 保留 | 修复「build」族的代表提交 | - |
| `c6aa412` | 2026-09-20 | 3 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `cb97982` | 2026-09-20 | 1 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `cc06416` | 2026-09-20 | 7 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `ccc6fc3` | 2026-09-20 | 27 | 保留 | 结构性提交（27 文件） | - |
| `d0a06d9` | 2026-09-20 | 2 | 保留 | 修复「stop」族的代表提交 | - |
| `d4fe320` | 2026-09-20 | 5 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `d633a5d` | 2026-09-20 | 1 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `d6f8650` | 2026-09-20 | 10 | 保留 | 结构性提交（10 文件） | - |
| `dc0f848` | 2026-09-20 | 5 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `e771748` | 2026-09-20 | 2 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `ea6df9c` | 2026-09-20 | 1 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `f7ebbc4` | 2026-09-20 | 3 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `00cd9f5` | 2026-09-21 | 3 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `03d0263` | 2026-09-21 | 6 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `19325c7` | 2026-09-21 | 3 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `2edf6a7` | 2026-09-21 | 2 | 保留 | 验收锚点缺陷，必须可追溯 | - |
| `5f12112` | 2026-09-21 | 3 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `77de6f4` | 2026-09-21 | 1 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `9efefe0` | 2026-09-21 | 2 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `a4473a5` | 2026-09-21 | 1 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `d6cd84a` | 2026-09-21 | 2 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `d78045d` | 2026-09-21 | 3 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `e5d87ca` | 2026-09-21 | 5 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `e846619` | 2026-09-21 | 4 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `edd8574` | 2026-09-21 | 1 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `6858490` | 2026-09-22 | 4 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `b7fa7b8` | 2026-09-22 | 3 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `f4b69aa` | 2026-09-22 | 4 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `0b17cc0` | 2026-10-19 | 44 | 保留 | 结构性提交（44 文件） | - |
| `16db0be` | 2026-10-19 | 7 | 合并 | same phase (segment 9); folded into that phase's representative commit | `0b17cc0` |
| `2a1c100` | 2026-10-19 | 6 | 保留 | 本阶段代表文档提交 | - |
| `3017c3c` | 2026-10-19 | 1 | 合并 | same phase (segment 10); folded into that phase's representative commit | `95ee55c` |
| `37a649c` | 2026-10-19 | 8 | 合并 | same phase (segment 10); folded into that phase's representative commit | `95ee55c` |
| `6af4d3f` | 2026-10-19 | 4 | 合并 | same phase (segment 10); folded into that phase's representative commit | `95ee55c` |
| `72210a2` | 2026-10-19 | 3 | 合并 | same phase (segment 10); folded into that phase's representative commit | `95ee55c` |
| `7d249e8` | 2026-10-19 | 3 | 合并 | same phase (segment 10); folded into that phase's representative commit | `95ee55c` |
| `87485e3` | 2026-10-19 | 14 | 保留 | 结构性提交（14 文件） | - |
| `95ee55c` | 2026-10-19 | 15 | 保留 | 结构性提交（15 文件） | - |
| `a285d19` | 2026-10-19 | 12 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `c7412d8` | 2026-10-19 | 2 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `d9fc05a` | 2026-10-19 | 58 | 保留 | 结构性提交（58 文件） | - |
| `00f0b52` | 2026-10-20 | 2 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `04f7bb7` | 2026-10-20 | 7 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `05befbf` | 2026-10-20 | 1 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `0f6408e` | 2026-10-20 | 14 | 保留 | 结构性提交（14 文件） | - |
| `1066ffe` | 2026-10-20 | 12 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `19cfeb5` | 2026-10-20 | 4 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `2c5a882` | 2026-10-20 | 1 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `5af639f` | 2026-10-20 | 2 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `6145e1d` | 2026-10-20 | 3 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `76ffd54` | 2026-10-20 | 16 | 保留 | 结构性提交（16 文件） | - |
| `7cab636` | 2026-10-20 | 2 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `8b1be9e` | 2026-10-20 | 8 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `8cb9f27` | 2026-10-20 | 1 | 保留 | 验收锚点缺陷，必须可追溯 | - |
| `9355d70` | 2026-10-20 | 2 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `9aa31f2` | 2026-10-20 | 1 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `a5643ab` | 2026-10-20 | 8 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `a57ea75` | 2026-10-20 | 3 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `aab1a9f` | 2026-10-20 | 1 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `b0c6c31` | 2026-10-20 | 8 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `b485adf` | 2026-10-20 | 2 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `d1fb2f5` | 2026-10-20 | 14 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `dc63a7d` | 2026-10-20 | 1 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `0129738` | 2026-10-21 | 11 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `069f8c6` | 2026-10-21 | 1 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `128605a` | 2026-10-21 | 3 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `16906bf` | 2026-10-21 | 16 | 保留 | 结构性提交（16 文件） | - |
| `21ce531` | 2026-10-21 | 2 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `2a13f32` | 2026-10-21 | 2 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `39d7929` | 2026-10-21 | 11 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `39e6f88` | 2026-10-21 | 1 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `44ab945` | 2026-10-21 | 1 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `50291fb` | 2026-10-21 | 13 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `7b42fd5` | 2026-10-21 | 18 | 保留 | 结构性提交（18 文件） | - |
| `7e0dfcc` | 2026-10-21 | 2 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `8540cf9` | 2026-10-21 | 4 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `a4b4494` | 2026-10-21 | 1 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `a590c01` | 2026-10-21 | 3 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `ab10ffc` | 2026-10-21 | 1 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `ad57d68` | 2026-10-21 | 1 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `bf67dde` | 2026-10-21 | 3 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `c025148` | 2026-10-21 | 2 | 保留 | 本阶段代表文档提交 | - |
| `c08891a` | 2026-10-21 | 6 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `c4b596a` | 2026-10-21 | 3 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `c6006b5` | 2026-10-21 | 1 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `c6eb89f` | 2026-10-21 | 12 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `d330e41` | 2026-10-21 | 1 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `d82dc25` | 2026-10-21 | 3 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `dec1814` | 2026-10-21 | 4 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `e4bbe1c` | 2026-10-21 | 2 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `372a48c` | 2026-10-22 | 1 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `5050152` | 2026-10-22 | 6 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `53e8d0d` | 2026-10-22 | 2 | 合并 | same phase (segment 11); folded into that phase's representative commit | `d9fc05a` |
| `5f43522` | 2026-10-22 | 1 | 保留 | 建立检查器/门禁 | - |
| `7a3bc6d` | 2026-10-22 | 23 | 保留 | 结构性提交（23 文件） | - |
| `91b8ac8` | 2026-10-22 | 5 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `973131f` | 2026-10-22 | 7 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `993f2c7` | 2026-10-22 | 3 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `9e6162e` | 2026-10-22 | 10 | 保留 | 结构性提交（10 文件） | - |
| `9f27024` | 2026-10-22 | 1 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `a3158da` | 2026-10-22 | 6 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `a3da87d` | 2026-10-22 | 3 | 保留 | 建立检查器/门禁 | - |
| `bad0d40` | 2026-10-22 | 15 | 保留 | 结构性提交（15 文件） | - |
| `d01cd95` | 2026-10-22 | 9 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `d2330fd` | 2026-10-22 | 5 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `ec5b324` | 2026-10-22 | 5 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `f68b46a` | 2026-10-22 | 2 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `3b662a8` | 2026-10-23 | 30 | 保留 | 结构性提交（30 文件） | - |
| `49acbdf` | 2026-10-23 | 2 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `5282858` | 2026-10-23 | 8 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `931c9f6` | 2026-10-23 | 1 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `9eac404` | 2026-10-23 | 1 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `b0fefa8` | 2026-10-23 | 2 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `c428039` | 2026-10-23 | 6 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `e0b3c63` | 2026-10-23 | 4 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `067b06b` | 2026-10-24 | 1 | 剔除 | QUEUE commit: records task-queue state, not code | - |
| `071d5d9` | 2026-10-24 | 3 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `105a661` | 2026-10-24 | 2 | 保留 | 构建或合并契约 | - |
| `25ceb53` | 2026-10-24 | 2 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `344e56d` | 2026-10-24 | 1 | 剔除 | QUEUE commit: records task-queue state, not code | - |
| `3f18bf5` | 2026-10-24 | 2 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `68b432b` | 2026-10-24 | 1 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `70385a3` | 2026-10-24 | 3 | 保留 | 建立检查器/门禁 | - |
| `7b61658` | 2026-10-24 | 4 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `7c81005` | 2026-10-24 | 2 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `836bb39` | 2026-10-24 | 2 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `9da0469` | 2026-10-24 | 1 | 剔除 | QUEUE commit: records task-queue state, not code | - |
| `a9b5c71` | 2026-10-24 | 1 | 保留 | 建立检查器/门禁 | - |
| `b7b1fcf` | 2026-10-24 | 1 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `bf8e302` | 2026-10-24 | 2 | 剔除 | QUEUE commit: records task-queue state, not code | - |
| `c9371fc` | 2026-10-24 | 2 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `ce2958c` | 2026-10-24 | 2 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `cfb43d3` | 2026-10-24 | 8 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `e7d3440` | 2026-10-24 | 4 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `e8b1adb` | 2026-10-24 | 5 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `eea88aa` | 2026-10-24 | 1 | 剔除 | QUEUE commit: records task-queue state, not code | - |
| `fb130ca` | 2026-10-24 | 13 | 保留 | 结构性提交（13 文件） | - |
| `049efb3` | 2026-10-25 | 1 | 剔除 | QUEUE commit: records task-queue state, not code | - |
| `1a78e17` | 2026-10-25 | 1 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `1b345fb` | 2026-10-25 | 8 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `328a102` | 2026-10-25 | 1 | 剔除 | QUEUE commit: records task-queue state, not code | - |
| `47130a7` | 2026-10-25 | 4 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `6bc68fa` | 2026-10-25 | 1 | 保留 | 建立检查器/门禁 | - |
| `6decadb` | 2026-10-25 | 1 | 剔除 | QUEUE commit: records task-queue state, not code | - |
| `7ac7487` | 2026-10-25 | 3 | 合并 | same phase (segment 12); folded into that phase's representative commit | `3b662a8` |
| `832ff00` | 2026-10-25 | 1 | 剔除 | QUEUE commit: records task-queue state, not code | - |
| `9398df1` | 2026-10-25 | 21 | 保留 | 结构性提交（21 文件） | - |
| `9743869` | 2026-10-25 | 1 | 剔除 | QUEUE commit: records task-queue state, not code | - |
| `9c7cb14` | 2026-10-25 | 1 | 剔除 | QUEUE commit: records task-queue state, not code | - |
| `da44e16` | 2026-10-25 | 4 | 合并 | same phase (segment 13); folded into that phase's representative commit | `9398df1` |
| `efd843e` | 2026-10-25 | 1 | 剔除 | QUEUE commit: records task-queue state, not code | - |
| `feb60cd` | 2026-10-25 | 1 | 剔除 | QUEUE commit: records task-queue state, not code | - |
| `079e69d` | 2026-10-26 | 5 | 合并 | same phase (segment 13); folded into that phase's representative commit | `9398df1` |
| `0d0e889` | 2026-10-26 | 1 | 合并 | same phase (segment 13); folded into that phase's representative commit | `9398df1` |
| `0e25871` | 2026-10-26 | 8 | 合并 | same phase (segment 13); folded into that phase's representative commit | `9398df1` |
| `0f70f74` | 2026-10-26 | 4 | 合并 | same phase (segment 13); folded into that phase's representative commit | `9398df1` |
| `1b49ab9` | 2026-10-26 | 3 | 合并 | same phase (segment 13); folded into that phase's representative commit | `9398df1` |
| `1e41582` | 2026-10-26 | 4 | 保留 | 本阶段代表文档提交 | - |
| `2a4d996` | 2026-10-26 | 1 | 合并 | same phase (segment 13); folded into that phase's representative commit | `9398df1` |
| `2bdda2f` | 2026-10-26 | 2 | 合并 | same phase (segment 13); folded into that phase's representative commit | `9398df1` |
| `2ca1003` | 2026-10-26 | 1 | 保留 | 建立检查器/门禁 | - |
| `2f3247a` | 2026-10-26 | 2 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `37ea923` | 2026-10-26 | 1 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `3e93f2b` | 2026-10-26 | 2 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `466f11e` | 2026-10-26 | 2 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `4c6e0ad` | 2026-10-26 | 1 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `52631b3` | 2026-10-26 | 6 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `5e5afe9` | 2026-10-26 | 1 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `6bbfc7f` | 2026-10-26 | 5 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `741d9b7` | 2026-10-26 | 7 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `74ad169` | 2026-10-26 | 16 | 保留 | 结构性提交（16 文件） | - |
| `78aa016` | 2026-10-26 | 3 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `91e205a` | 2026-10-26 | 1 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `9a6096d` | 2026-10-26 | 6 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `9aaae43` | 2026-10-26 | 6 | 保留 | 建立检查器/门禁 | - |
| `b809274` | 2026-10-26 | 3 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `bd934df` | 2026-10-26 | 3 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `be32ec3` | 2026-10-26 | 4 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `c243b34` | 2026-10-26 | 6 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `c798c6e` | 2026-10-26 | 4 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `d81cec6` | 2026-10-26 | 1 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `db34c78` | 2026-10-26 | 2 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `e0687c7` | 2026-10-26 | 2 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `eae6822` | 2026-10-26 | 2 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `14ecb30` | 2026-10-27 | 26 | 保留 | 结构性提交（26 文件） | - |
| `2495be9` | 2026-10-27 | 1 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `42803e0` | 2026-10-27 | 2 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `5e7d160` | 2026-10-27 | 1 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `5e8bf85` | 2026-10-27 | 1 | 保留 | 建立检查器/门禁 | - |
| `636d423` | 2026-10-27 | 1 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `66ab595` | 2026-10-27 | 3 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `8d897b2` | 2026-10-27 | 1 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `92de5bb` | 2026-10-27 | 1 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `9b04563` | 2026-10-27 | 1 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `9c5813e` | 2026-10-27 | 2 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `a9d1d84` | 2026-10-27 | 2 | 保留 | 修复「windbg's」族的代表提交 | - |
| `b06f116` | 2026-10-27 | 2 | 保留 | 建立检查器/门禁 | - |
| `c90ca0e` | 2026-10-27 | 64 | 保留 | 结构性提交（64 文件） | - |
| `cc031ae` | 2026-10-27 | 21 | 保留 | 结构性提交（21 文件） | - |
| `e22ab76` | 2026-10-27 | 1 | 合并 | same phase (segment 14); folded into that phase's representative commit | `f3c89f0` |
| `f3c89f0` | 2026-10-27 | 67 | 保留 | 结构性提交（67 文件） | - |

## 五、复核方法

```powershell
# 总数为 643（应与本表行数一致）
(Import-Clixml evidence\plan4.xml).Count

# 处置计数
$p = Import-Clixml evidence\plan4.xml
@($p | Where-Object Disp -eq 'KEEP').Count   # 103
@($p | Where-Object Disp -eq 'FOLD').Count   # 527
@($p | Where-Object Disp -eq 'DROP').Count   # 13

# 5 个锚点缺陷全部保留
@('64e4bfa','ad1128a','8cb9f27','2edf6a7','04a8f43') |
  ForEach-Object { $s=$_; ($p | Where-Object { $_.Short -eq $s }).Disp }
```

## 六、已知的口径取舍

- **8 月占比偏高**（103 条中 52 条）。8 月是功能建立期，每次提交都在扩大代码面；9–10 月转入修复与验证，单次改动更小、更频繁，因此在"每段上限 10 条"的约束下被压缩得更多。
- **"仅在文档"提交每段只留 1 条**。被合并的 527 条里有 70 条是纯 `.md` 变更，它们的内容全部保留在树里，只是不各占一条历史。
- **`f3c89f0`（67 文件）保留**。它的标题是 `docs:`，但内含 15 个 `.ps1` + 7 个 `.cs`，是把旧工作区整批归档进 `docs/legacy-agent-workspaces/`。这不是"只改文档措辞"，属于资产入库，因此按结构性提交保留。
- **13 条 QUEUE 全部剔除**，符合 `02-TASK.md` 第 175 行。匹配不区分大小写：其中 `bf8e302` 是小写 `queue:`。
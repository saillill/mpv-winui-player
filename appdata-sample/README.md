# appdata-sample

默认配置**样例**，供手动复制到应用的 AppData 目录。**构建不引用这里**，删掉或改动都不影响编译。

上游把这个目录叫 `data/`，README 里写作 `mpvw-app-data-sample`。本分支改名为
`appdata-sample/`，因为 `data` 这个名字完全看不出它是"要手动拷进 AppData 的样例"，
而且它和 `mpv-winui-lazy/`（真正被部署的配置层）内容相似、极易混淆。

## 该拷到哪里

应用在**未打包（unpackaged）**模式下的数据根目录：

```
%LOCALAPPDATA%\mpv-winui\
```

| 这里的子目录 | 对应 AppData 位置 | 用途 | 本分支是否还需要手动拷 |
| :--- | :--- | :--- | :--- |
| `mpv-conf-editor/` | `%LOCALAPPDATA%\mpv-winui\mpv-conf-editor\` | mpv.conf 编辑器的选项定义（名称、类型、说明、取值范围） | **需要** —— 见下 |
| `mpv/` | `%LOCALAPPDATA%\mpv-winui\mpv\` | 一份精简的 mpv 配置样例 | **不需要**，已被 `mpv-winui-lazy/` 取代 |
| `menu.json`、`mpvw-menu.conf` | `%LOCALAPPDATA%\mpv-winui\` | 菜单样例 | 视需要 |

## 关于 `mpv-conf-editor/`（唯一仍然有用的部分）

`MpvConfEditorPage` 从 AppData 读 schema：

```csharp
var definitionDirectory = AppData.Current.ResolveLocalData(
    MpvConfSchemaService.DefinitionDirectoryName);   // "mpv-conf-editor"
return MpvConfSchemaService.LoadFromDirectory(definitionDirectory);
```

而 `LoadFromDirectory` 在目录不存在时返回 **空 schema**，应用不会自动创建它、也不会内置一份。
也就是说：**不手动拷过去，mpv.conf 编辑器就没有任何选项定义**（没有名称、类型和说明）。
用 `mpv-conf-editor/` 里的 json 即可补上；两份文件按文件名字典序合并，靠前的优先。

> 这两个 json 是本目录里**唯一**不可替代的东西 —— 别把整个 `appdata-sample/` 当垃圾清掉。

## 和 `mpv-winui-lazy/` 的区别

| | `mpv-winui-lazy/` | `appdata-sample/` |
| :--- | :--- | :--- |
| 谁在用 | 构建会打包，`ConfigDeployer` 启动时自动同步到 AppData | 无人引用，纯手动样例 |
| 内容 | 完整的 mpv 配置 + 脚本 + shader + 工具 | 上游早期的精简样例 |
| 改了会怎样 | 影响所有安装（自动部署并清理旧文件） | 什么都不影响 |

`mpv/` 子目录属于上游遗留：本分支的 `ConfigDeployer` 已经会用 `mpv-winui-lazy/`
自动部署并维护 `%LOCALAPPDATA%\mpv-winui\mpv`，所以这部分样例不必再手工拷贝。

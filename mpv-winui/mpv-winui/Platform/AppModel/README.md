# 分区 10 · 应用模型 / AppModel

## 职责

判断应用是**打包**（MSIX）还是**非打包**运行——这一个问题决定了设置存哪、
配置部署走哪条路、更新怎么查。

## 入口文件

| 文件 | 作用 |
|---|---|
| `PackageHelper.cs` | 打包判定与包标识读取 |

## 对外接口

| 类型 | 成员 | 谁在用 |
|---|---|---|
| `PackageHelper` | `IsPackaged` 等 | `AppSettings`（决定注册表根）、`UnpackageAppDataSetting`、`ConfigDeployer` |

## 依赖

- 只依赖 WinRT。**不依赖任何 功能域。**
- 被 `Settings/UnpackageAppDataSetting.cs` 与 `Settings/AppDataSetting.cs` 依赖。

## 上下文裁剪提示

这一个文件 2.6 KB，直接读完全部即可。改它之前确认两条路径（打包/非打包）
都能启动——目前发布的是**非打包自包含**部署。

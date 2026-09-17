# HS2 FemaleForMale — Studio

用于 Honey Select 2 工作室（StudioNEOV2）的 BepInEx 插件：将场景中的男角色完整替换为女角色，并迁移可兼容的 FK／IK、绑骨与 Timeline 轨道数据。

本仓库仅包含工作室版，不包含主游戏或 VR 插件。当前版本为 **1.2.0**，建议作为预发布版使用；复杂场景的兼容性仍需更多实际测试。

## 功能

- 使用女性身体和骨架重建目标角色，保留对象 ID、位置、旋转、缩放及层级。
- 支持 Character Loader 1.4.2 的卡片“替换”按钮，同时保留 Studio 原生替换入口。
- 迁移可匹配的 FK／IK，并处理可选骨骼、约束和时间轴插件的重绑。
- 转换前保存恢复快照，失败时尝试自动回滚。
- 骨骼等待采用可续期超时，加载提示随屏幕分辨率缩放。

## 依赖与兼容范围

开发适配环境为 HS2 BetterRepack R16。其他整合包和插件组合尚未全面验证。

| 依赖 | 要求 |
| --- | --- |
| BepInEx | HS2 使用的 5.x 环境 |
| ExtendedSave | 21.1.2 或更高 |
| HS2API | 1.45.1 或更高 |

可选兼容目标包括 Character Loader 1.4.2、NodesConstraints 1.6.2.1、Timeline 1.5.5.1、BonesFramework、AdditionalFKNodes、HS2_FKIK、AdvIK、HS2ABMX 和 HS2PE。兼容处理依赖这些插件的具体接口，不代表所有版本组合均已实测。

使用 Timeline 时，必须将其 **Autoplay 设置为 Ignore**，否则插件会提示并拒绝开始转换。插件不修改该设置，也不调用或拦截 Timeline 的播放、暂停、停止、跳转或插值控制。

## 安装与使用

1. 从本仓库 Releases 下载工作室版插件包。
2. 保存并关闭游戏和 Studio，将包中的 `BepInEx` 文件夹合并到游戏目录。
3. 旧版若安装在其他插件子目录，请先移除旧 DLL，避免重复加载。默认位置是 `BepInEx/plugins/Codex/HS2_FemaleForMale.Studio.dll`。
4. 启动 Studio，载入场景并另存一份备份。
5. 在工作区选中一个或多个男角色，在 Character Loader 的女卡上点击“替换”。女换女、男换男仍交给原插件处理。
6. 未安装 Character Loader 时，可使用“添加 → 女性角色 → 替换 / Change”。
7. 等待场景重载完成，检查动作和约束，再另存转换后的场景。

卸载时，关闭 Studio 后移除本插件 DLL 即可。卸载不会自动撤销已保存进场景的角色替换。

## 限制与恢复

- 替换会完整重载场景，并清空原有撤销历史；大型场景可能加载较慢，内存占用也可能增加。
- 男女骨架并不完全一致。男体专属 FK 骨 ID `67/68/69` 不迁移；不兼容的普通 FK 数据可能被重置或剪除。
- 涉及目标角色的 NodesConstraints 路径若无法安全映射，转换会取消并尝试回滚，不会静默删除该约束。
- 自动恢复可能因第三方插件异常而失败，请始终保留单独的场景备份。

默认快照位置相对于游戏目录：

```text
BepInEx/cache/HS2_FemaleForMale/LastRecovery.png
BepInEx/cache/HS2_FemaleForMale/LastConverted.png
```

`LastRecovery.png` 是转换前的恢复场景，`LastConverted.png` 是待载入的转换场景，不应仅凭文件存在判断转换成功。两者会在后续替换时覆盖；必要时先复制到其他位置，再手动载入恢复场景。

## 配置

安装 BepInEx ConfigurationManager 后，可按 F1 调整配置；也可在关闭 Studio 后编辑 `BepInEx/config/com.codex.hs2.femaleformale.studio.cfg`。

| 配置 | 默认行为 |
| --- | --- |
| 启用Studio跨性别替换 | 开启 |
| 扩展骨严格匹配 | 开启，建议保留 |
| 扩展骨无进展超时秒数 | 45 秒；检测到进展时续期，总上限为该值三倍 |
| 加载提示额外缩放倍率 | 1；4K 分辨率下另有自动 2 倍缩放 |

## 从源码构建

需要 Windows、PowerShell、.NET SDK，以及 .NET Framework 4.6 目标框架引用程序集。项目目标为 `net46`、C# 7.3。

依赖程序集从自己的游戏安装目录读取，包括 `BepInEx/core` 和 `StudioNEOV2_Data/Managed` 下的相关 DLL。仓库及发布包不提供游戏程序集或第三方插件二进制。

在仓库目录运行，按提示输入游戏根目录：

```powershell
$gameDirectory = Read-Host '游戏根目录'
.\build.ps1 -GameRoot $gameDirectory
```

默认只编译，输出为 `bin/Release/Studio/HS2_FemaleForMale.Studio.dll`。脚本不会默认安装插件。

需要同时安装时，先保存并关闭游戏和 Studio，再显式指定：

```powershell
.\build.ps1 -GameRoot $gameDirectory -Install
```

安装前会备份同一默认位置的旧 DLL。若游戏或 Studio 正在运行，脚本会拒绝安装。

## 问题反馈

请附上插件版本、相关依赖版本、操作步骤、错误提示及出错前后的日志片段。说明异常发生在加载阶段、替换完成后还是 Timeline 播放时。分享日志前，请去掉不希望公开的个人路径和角色信息。

## 许可证与依赖

本项目采用 MIT 许可证，见 [LICENSE](LICENSE)。游戏、Unity、BepInEx、Harmony 及其他第三方插件保留各自的权利和许可；本项目的 MIT 许可证不替代它们的许可。

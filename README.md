# CastleCraft｜失落城堡 × Minecraft

在失落城堡里挖矿、搭方块，带着 Minecraft 的装备打城堡里的怪。

CastleCraft 把两个正在运行的游戏连在一起：前台玩失落城堡，后台的 Minecraft 负责方块、物品和生物。城堡原有的关卡、NPC、剧情和 Boss 流程仍然保留。

Windows 单人完整版 · 2026 年 10 月 14 日

## 下载与安装

去 [Releases 下载完整版](https://github.com/YoucraftGame/CastleCraft/releases/latest)，选择 **CastleCraft-Full-2026.10.14-r2.zip**。包内有编译好的城堡端 DLL、Minecraft 端 JAR、城堡加载器、必要前置、安装检测程序和中文教程，不用自己编译。

第一次装请看 [安装教程](docs/安装教程.md)。想改代码请看 [编译说明](docs/编译说明.md)。每个下载包都附有 SHA-256 校验值。

## 可以玩什么

- **随时切换角色。** F8 切换城堡角色和 Minecraft 角色，F5 切换第一、第三人称。
- **在城堡里建造。** 使用 Minecraft 原生物品栏、热栏、采矿和放置方块，保留生物和掉落物。
- **两边一起打。** 同步角色位置、碰撞和双向受击；独立按键切换城堡武器虚拟槽（具体键位以屏幕提示为准），城堡交互和 Minecraft 操作可以配合使用。
- **保留城堡流程。** 换房、NPC、拾取、剧情和 Boss 继续交给失落城堡处理，联动世界自动打开。
- **第一人称联动。** 同步 HUD、菜单与手持画面，城堡角色和场景按已有素材映射；不是把原游戏所有角色都重新做成完整三维模型。

## 这次主要修了什么

- 部分修复最后隐藏关卡怪物无法被 Minecraft 正确判定和攻击的问题：补上原生 FallenHunter 的战斗目标映射。该怪物继承 HeroBase，之前只扫描 Monster 时会遗漏；保留原游戏的敌我、伤害和死亡处理。
- 切到后台再回来时，已完成的入场状态能够正确恢复，减少重复等待。
- 修补 Sodium 环境下挖掘、放置方块后画面没有及时刷新的问题。
- 分摊实体扫描，缓存战斗目标的类型与成员查找，减少靠近 Minecraft 生物时的同步开销。
- 修正共享画面倒置和 Vulkan 跨进程同步的问题。
- 修补角色切换时的生命值交接，避免初始化的零生命被误当成死亡状态重发。
- 修补史莱姆王房间误用无限相机边界、生成大量碰撞区域而卡在等待的问题；同样处理哥布林国王的无界侧。
- 恢复等待接管期间 Boss 开场的原生输入监听，让 ESC 松开事件能够交给原游戏处理。

完整说明及验证范围见 [更新记录](CHANGELOG.md)。

## 需要准备

Windows 64 位、Steam 版失落城堡第一代，以及自己拥有的 Minecraft Java Edition。当前构建对应 **Minecraft 26.3 / Fabric Loader 0.19.5 / Fabric API 0.161.0+26.3 / Java 25**，城堡端使用 **BepInEx 5.4.23.5 x64 Mono**。

两个游戏需要同时运行。游戏本体、账号、存档、Java 和启动器由玩家自行准备；包内已提供联动所需的编译产物和上述 Fabric API。额外玩法、画面模组不是桥接的必需部分，参考 [可选模组清单](docs/可选模组清单.md)自行安装。

## 当前范围

这是当前功能的完整版发布。已保留玩家确认流畅的修补基线；最新隐藏关卡目标映射修补通过 112 项宿主检查及游戏元数据/编译接线核对。此前 Boss 房间边界已完成 53 个场景离线核对。最近一次构建没有额外启动游戏做完整流程回测，因此不把自动检查写成全部关卡实测通过。

目前按单人使用。猎人完整骨骼、法术与部分音效仍有待核对项；流体、红石、TNT、投射物及特殊剧情组合没有逐项完成兼容回归。遇到问题请在 Issues 写上房间名、操作步骤、版本及相关日志，上传前删去账号和私人路径。

## 署名与许可

原创代码采用 [MIT 许可](LICENSE)。桥接基于 [chasmlol/SkyCraft](https://github.com/chasmlol/SkyCraft) 和 [aeironnsarmiento/PeakCraft](https://github.com/aeironnsarmiento/PeakCraft) 的工作，上游许可和来源记录随源码保留。

本项目由 AI（OpenAI Codex）协助开发与整理文档，参考了下列开源项目；没有新增生成美术或音频。

开发流程与发布检查使用了 [rehan-remade/Universal Modder](https://github.com/rehan-remade/universal-modder) 的技能与工具（包括 publish-mod 和 um publish check）。感谢该项目提供的模组开发方法与工具支持。BepInEx、Doorstop、Harmony、Fabric 等组件遵循各自许可，见 [第三方说明](THIRD-PARTY.md)。

非官方免费模组，与两个游戏的开发商无隶属关系。游戏素材在玩家本机读取，游戏本体与其素材权利仍归原权利人。

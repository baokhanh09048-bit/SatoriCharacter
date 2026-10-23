# 古明地觉 / Komeiji Satori

《杀戮尖塔 2》独立角色模组，当前源码版本 **0.4.34**。包含92张永久角色牌、4种衍生记忆牌、10件角色遗物，以及可选第四幕。

[Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3816248863) · [BaseLib 前置](https://steamcommunity.com/sharedfiles/filedetails/?id=3737335127)

## 本次变化

- 内讧不足5层时，每层降低对施加者的攻击伤害10%；5层转移完整攻击行动，转移攻击保留原伤害。
- 延展手臂8/11伤与3/4暗示；思维勒索单次造成4＋自己暗示层数×3/4伤；旧地狱的来信2费固有，升级1费。
- 支持多人第四幕。标准三幕路线中，队内任意觉领取遗落的心结，击败第三幕全部首领后全队进入庭院。多人路线不受各自本地第四幕开关影响，火堆预览入口仅限单人。
- 无实体破除进度由全队共享，目标为每名参战玩家4次，每人获得6张战斗专用心结。队友倒下时免去该玩家4次需求（每人一次），保留已有进度。
- 同行选择、珍贵的回忆、支援牌、能量和个人计数分别归属各玩家。暗示与内讧按施加者隔离，精神过载仍为敌人身上的共享资源。

联机时所有玩家须安装相同版本的模组及兼容的 BaseLib。

## 源码构建

需要 .NET 9 SDK、自己安装的游戏程序集及 BaseLib 3.4.7+。游戏、BaseLib 二进制和制作环境不包含在仓库内。

```powershell
pwsh -File tools/build-code.ps1 -Sts2Dir '<游戏安装目录>' -BaseLibDir '<含 BaseLib.dll 的目录>'
```

结果位于 `dist/`。这是 C# 代码构建，运行所需美术、动画、音频及导入资源请使用创意工坊**同版本**的 `SatoriCharacter.pck`；可通过 `-RuntimePackPath '<资源包路径>'` 一起复制。关闭游戏后，将同版本 DLL、JSON、PCK 放入 `mods/SatoriCharacter/`。请勿同时加载工坊版和本地版的重复副本。

`Scripts/` 包含全部 C# 运行逻辑，`SatoriCharacter/` 包含场景脚本、场景定义、运行清单和四语言文本，`tools/` 包含卡池、对白及翻译的作者文件。生成卡牌时运行 `make_card_catalog.py`，再运行 `make-localization.py` 与 `make-act4-localization.py`；生成结果应和作者文件一同提交。修改美术、音频或资源包内文本需要另行准备资源并通过 Godot 4.5.1 导入/导出；代码构建脚本不会重建 PCK。

卡表见 [完整卡池](docs/CARD_POOL_V0314.md)，本轮规则和验证范围见 [联机适配说明](docs/MULTIPLAYER_V0434.md)。历史文件名中的版本用于保持作者工具路径稳定。

## 许可与素材

本仓库的原创程序代码使用 [MIT License](LICENSE)。第三方游戏、依赖、东方 Project 角色设定、音乐及其他第三方素材不属于 MIT 授权范围。仓库不包含游戏程序集、音乐文件、制作原图、模型、账号凭据或本地操作记录。素材说明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

## English

Source code for the standalone Komeiji Satori character mod for Slay the Spire 2. Requires .NET 9, your own game installation and BaseLib 3.4.7+. Build with `tools/build-code.ps1` and use the matching Workshop PCK for runtime assets. Original program code is MIT-licensed; third-party assets and dependencies retain their own rights.

Multiplayer Act 4 uses shared Intangible-breaking progress (4 per participant) and personal companion choices/resources. Everyone must use the same mod version. See the validation notes for the exact tested scope; automated native-runtime checks do not replace a complete session on separate computers.

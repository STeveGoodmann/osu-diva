# osu! DIVA

[English](README.md) | [简体中文](README.zh-CN.md)

[osu!lazer](https://github.com/ppy/osu) 的自定义 ruleset。**玩法与 osu!standard 完全一致**：物件、判定、计分、mod、难度都不变。唯一的新增是在上面叠一层 Project DIVA 风格的飞行音符，帮助读谱。音符从屏幕边缘飞来，在物件的击打时间落到物件上。

![icon](docs/icon-preview.png)

## 玩法来源

飞行音符模仿 SEGA《初音未来 Project DIVA》系列的音符飞行。轨迹算法最早是给 tosu 写的 Python 叠加层原型，后来移植成 C#。这是非官方的同人项目，与 ppy 和 SEGA 无关，也未获得他们的认可。

做法：`OsuDivaRuleset` 内部持有一个 `OsuRuleset` 并把玩法全部委托给它，没有重写玩法。游玩区只多一个只读的飞行层，不碰判定和输入。规划器为每个音符选飞入角度，让音符在画面里停留足够久、不穿过其他物件、飞行线互不相交，同一个连击内的音符向外散开。

## 安装

只在 **Linux 上的 osu!lazer 2026.1005.0-lazer**（写作时的正式版）测试过。DLL 是对着这个版本编译的，其他版本可能无法加载；无法加载时 lazer 会弹出 "An issue with ruleset" 通知，详情在 `logs/runtime.log`。

1. 从[最新 release](../../releases/latest) 下载 `osu.Game.Rulesets.DivaFlight.dll`。
2. 关闭 lazer，把 DLL 复制到 lazer 数据目录的 `rulesets` 文件夹：
   - Linux：`~/.local/share/osu/rulesets/`
   - Windows：`%AppData%\osu\rulesets\`
   - macOS：`~/Library/Application Support/osu/rulesets/`
3. 启动 lazer，模式选择里会出现「osu! DIVA」，选项在设置里 osu! DIVA 对应的分组中。

只有 Linux 的路径在真实安装上核对过。卸载时删除 DLL 即可。

设置界面有简体中文和英文，跟随 lazer 的语言。默认值：飞行速度 400、音符大小 55%、接近圈不透明度 25%、扇形角度 25°。

## 公平游玩声明

osu! [官方规则](https://osu.ppy.sh/wiki/en/Rules)写明：使用任何第三方工具来获得任何形式的优势都是不允许的，并举例了瞄准辅助程序、改变时间速率、宏，以及物理操作与游戏内操作不一一对应的按键绑定或硬件。规则还说明，作弊可能导致账号被永久限制。

本项目是视觉上的读谱辅助。它做什么、不做什么：

- 只利用 lazer 已经提供的谱面和游戏时钟，在游玩区上方画飞行音符。
- 不读写游戏内存，不注入或改动输入，不移动光标、不按键，不改变判定窗口、判定、计分、mod 和谱面。全部玩法代码就是 lazer 自带的、未经修改的 osu!standard ruleset。
- 没有任何网络代码，不向任何地方发送数据。
- 不包含、也不会加入瞄准辅助、自动游玩、时机校正或任何替你操作的功能。

这类视觉提示在 osu! 规则里算不算「优势」，该由 ppy 和 osu! 管理员判断，不由我判断。我没有向他们询问过，也没有核实 lazer 如何处理自定义 ruleset 的成绩（是否上传、是否排名、比赛是否接受）。因此：

- 请用于练习和读谱训练。不要在比赛中使用，也不要在组织者禁止的场合使用；拿不准时先看规则并询问组织者。
- 使用方式由你自己负责。作者不保证使用本项目一定被允许，也不对你的账号受到的处罚负责。
- 请不要用本项目或其中的代码开发作弊工具。

## 注意

- 在这个模式里打出的成绩存在它自己的短名 `osudiva` 下。我没有核实 lazer 是否上传或排名自定义 ruleset 的成绩。
- 发布后不要单独改类名或短名，先读 `docs/DEVELOPMENT.zh-CN.md`：短名是 lazer 数据库里的主键。

## 编译

需要 .NET 10 SDK，要对着你正在用的 lazer 的程序集编译：

```
dotnet build osu.Game.Rulesets.DivaFlight/osu.Game.Rulesets.DivaFlight.csproj -c Release -p:LazerBin=/path/to/lazer/usr/bin
dotnet test osu.Game.Rulesets.DivaFlight.UnitTests -p:LazerBin=/path/to/lazer/usr/bin
```

开发记录：[docs/DEVELOPMENT.zh-CN.md](docs/DEVELOPMENT.zh-CN.md)。

## 许可证

MIT，见 [LICENSE](LICENSE)。`Resources/Textures` 里的纹理为 CC0。osu! 与 osu!lazer 属于 ppy，使用 MIT 许可证。

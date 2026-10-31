# osu! DIVA

[English](README.md) | [简体中文](README.zh-CN.md)

A custom ruleset for [osu!lazer](https://github.com/ppy/osu). **The gameplay is exactly osu!standard**: same hit objects, judgement, scoring, mods and difficulty. The only addition is a layer of Project DIVA style flying notes drawn on top as a reading aid. Each note flies in from the edge of the screen and lands on its hit object at the object's hit time.

![icon](docs/icon-preview.png)

## Where the idea comes from

The flying notes imitate the note flights of SEGA's *Hatsune Miku: Project DIVA* series. The trajectory maths started as a Python overlay prototype for tosu and was then ported to C#. This is an unofficial fan project and is not affiliated with or endorsed by ppy or SEGA.

How it works: `OsuDivaRuleset` holds an `OsuRuleset` and delegates everything to it, so gameplay is not reimplemented. A read-only layer is added to the playfield. It never touches judgement or input. A planner picks an entry angle for every note so that notes stay on screen long enough to read, avoid flying through other objects and crossing each other, and fan out from each other inside a combo.

## Install

Tested only with **osu!lazer 2026.1005.0-lazer on Linux** (the stable build at the time of writing). The DLL is compiled against that version. A different lazer version may refuse to load it; if so, lazer shows an "An issue with ruleset" notification and the details are in `logs/runtime.log`.

1. Download `osu.Game.Rulesets.OsuDiva.dll` from the [latest release](../../releases/latest).
2. Close lazer and copy the DLL into the `rulesets` folder of your lazer data directory:
   - Linux: `~/.local/share/osu/rulesets/`
   - Windows: `%AppData%\osu\rulesets\`
   - macOS: `~/Library/Application Support/osu/rulesets/`
3. Start lazer. "osu! DIVA" appears in the ruleset selector. Its options are in the settings, in the osu! DIVA section.

Only the Linux path has been checked on a real install. To uninstall, delete the DLL.

The settings are available in English and Simplified Chinese and follow lazer's language. Defaults: flight speed 400, note size 55%, approach circle opacity 25%, fan direction at 25 degrees.

## Fair play statement

osu!'s [official rules](https://osu.ppy.sh/wiki/en/Rules) say: "Using third-party utilities of any kind to get any sort of advantage is not okay", and give aim-assist programs, timescale modifications and macros or hardware that does not map one physical action to one in-game action as examples. They also say that cheating can lead to a permanent account restriction.

This project is a visual reading aid:

- It only draws flying notes on top of the playfield, using the beatmap and the game clock that lazer already provides.
- It does not read or write game memory, does not inject or alter input, does not move the cursor or press keys, and does not change hit windows, judgement, scoring, mods or the beatmap. All gameplay code is the unmodified osu!standard ruleset that ships with lazer.
- It has no network code and sends no data anywhere.
- It does not and will not contain aim assist, auto-play, timing correction or anything else that plays for you.

Whether a visual aid like this counts as "an advantage" under osu!'s rules is for ppy and the osu! moderators to decide. 

So:

- Use it for practice and reading training and fun. 
- You are responsible for how you use it. The author gives no guarantee that using it is allowed and accepts no responsibility for sanctions on your account.
- Please do not use this project, or code taken from it, to build cheats.

## Build

Needs the .NET 10 SDK. Build against the assemblies of the lazer you run:

```
dotnet build osu.Game.Rulesets.OsuDiva/osu.Game.Rulesets.OsuDiva.csproj -c Release -p:LazerBin=/path/to/lazer/usr/bin
dotnet test osu.Game.Rulesets.OsuDiva.UnitTests -p:LazerBin=/path/to/lazer/usr/bin
```

Development notes (Chinese): [docs/DEVELOPMENT.zh-CN.md](docs/DEVELOPMENT.zh-CN.md).

## License

MIT, see [LICENSE](LICENSE). The textures in `Resources/Textures` are CC0. osu! and osu!lazer are by ppy and licensed under MIT.

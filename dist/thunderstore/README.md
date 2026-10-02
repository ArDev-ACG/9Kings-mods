# Extra Game Speeds

Adds **4x, 6x and 8x** speed buttons right next to the vanilla 1x / 2x / 3x.
Your chosen speed carries over to the next wave, the same way the vanilla speeds do.
Pause and menus keep working normally.

## Use

Start a battle. Three new buttons appear to the right of 3x:
yellow = 4x, orange = 6x, red = 8x.

Click one, or press **4** / **5** / **6**. Click the same button again to drop back to 3x.
Picking 1x / 2x / 3x also turns it off.

## Config

`BepInEx/config/com.mods.ninekings.ultraspeed.cfg`, created on first run.

| Setting | Default | What it does |
|---|---|---|
| `Speeds` | `4,6,8` | One button per value. Add more, e.g. `4,6,8,12` |
| `Hotkeys` | `Digit4,Digit5,Digit6` | Same order as Speeds |
| `IconTints` | `#FFD24A,#FF8A3D,#FF3D3D` | Button colours, same order |
| `IncludeInCycle` | `true` | Include them when cycling speed |
| `Persist` | `true` | Keep your speed between waves |
| `AutoButtonNudgeX` | `0` | Extra nudge for the auto-shoot button |

The auto-shoot button is moved left automatically to make room, by one button width
per extra speed. `AutoButtonNudgeX` is only there if you want to fine-tune it.

## Bugs

Please report them on the mod page, and attach `BepInEx/LogOutput.log` if you can.

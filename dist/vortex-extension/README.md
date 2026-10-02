# 9 Kings — Vortex extension

Adds 9 Kings to Vortex so its BepInEx mods can be installed and deployed from there.
Vortex has no built-in support for this game.

## Install

Copy the `game-9kings` folder into:

```
%APPDATA%\Vortex\plugins\game-9kings
```

Restart Vortex. 9 Kings shows up under Games → Unmanaged; click Manage.

## What it does

- Finds the game through Steam (app id 2784470).
- Deploys mods from the game root, so archives that already contain
  `BepInEx/plugins/...` land in the right place.
- Archives that are just loose `.dll` files get placed into
  `BepInEx/plugins/<mod name>/` instead.
- On setup, creates `BepInEx/plugins` and, if `winhttp.dll` is missing, points you at
  the right BepInEx build.

## Why BepInEx is not auto-installed

9 Kings runs on Unity 6 with IL2CPP (metadata v39). Only BepInEx 6 bleeding edge
build **755 or newer** can read it; earlier builds fail while generating the interop
assemblies. Vortex's generic BepInEx installer serves older builds, so hooking it up
would install something that breaks the game. The extension asks you to install it
yourself instead:

https://thunderstore.io/c/9-kings/p/BepInEx/BepInExPack_IL2CPP/

Install BepInEx, launch the game once and let it finish (the first launch is slow
because BepInEx is generating files), then deploy mods from Vortex.

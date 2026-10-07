# ARcade Space

Source code for **ARcade Space**, an iPhone app that plays arcade games inside
3D cabinets placed in your room with AR. Each game's graphics layers
(backgrounds, sprites, HUD) are split out by the emulator and shown at
different depths on the cabinet screen.

Published by Inner Space XR Studio under the **GNU General Public License v2**
(see `LICENSE`), because the app links the MAME emulator core.

No ROMs are included. Users load their own arcade ROM files.

## Repositories

| Repo | What it is |
|---|---|
| this repo | The Unity app (C#, shaders, iOS native plugins, editor tools, project settings) |
| [arcade-space-mame](https://github.com/innerspace300-cyber/arcade-space-mame) | Fork of libretro's MAME core (branch `spatial-emulator`) with per-layer graphics export |

## What's not included

Art, audio, fonts and 3D models are not in this repo, including third-party
Asset Store packs (Pixel UI & HUD 4, Ultimate Mobile Controls Kit, Feel,
Retro/Pixel Arsenal, DevDunk Shadow Receiver, Dead Revolver fonts) and the
cabinet models. Scenes and prefabs still reference them, so a fresh clone opens
with missing art until you supply your own.

## Building

**Requirements:** Unity 6000.6.2f1 with iOS support, Xcode, an iPhone with ARKit.

### 1. Emulator core

```bash
git clone -b spatial-emulator https://github.com/innerspace300-cyber/arcade-space-mame.git
cd arcade-space-mame
make -f Makefile.libretro platform=ios-arm64 SUBTARGET=spatial -j8 \
  SOURCES=src/mame/atlus/cave.cpp,src/mame/capcom/cps1.cpp,src/mame/capcom/cps2.cpp,src/mame/cave/cv1k.cpp,src/mame/igs/pgm.cpp,src/mame/irem/m92.cpp,src/mame/konami/asterix.cpp,src/mame/konami/simpsons.cpp,src/mame/konami/tmnt.cpp,src/mame/konami/tmnt2.cpp,src/mame/konami/vendetta.cpp,src/mame/konami/xmen.cpp,src/mame/namco/galaga.cpp,src/mame/pacman/pacman.cpp,src/mame/sega/segas16a.cpp,src/mame/sega/segas16b.cpp,src/mame/sega/segas18.cpp,src/mame/snk/neogeo.cpp,src/mame/taito/taito_f3.cpp,src/mame/technos/ddragon.cpp,src/mame/technos/ddragon3.cpp,src/mame/toaplan/batsugun.cpp,src/mame/toaplan/truxton2.cpp,src/mame/williams/midtunit.cpp,src/mame/williams/midwunit.cpp,src/mame/williams/midyunit.cpp
```

This produces `spatial_libretro_ios.dylib`. Wrap it as a framework for Unity:

```bash
Tools/wrap_ios_framework.sh path/to/spatial_libretro_ios.dylib path/to/this-repo/Assets/Plugins/iOS
```

### 2. App

Open the project in Unity, switch the platform to iOS, open
`Assets/Scenes/SPATIAL EMULATOR.unity`, and build. `IosBuildPostprocessor.cs`
applies the Xcode project changes automatically.

## License

GPL-2.0. MAME is copyright the MAME team and contributors.
MAME is a registered trademark of Gregory Ember.

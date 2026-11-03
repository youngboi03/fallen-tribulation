# Van Kiep Tu Tien (Ten Thousand Tribulations)

A 2D wave-survival roguelite about cultivation (xianxia), made in Unity 6. One run takes about 25-30 minutes.
Wave loop and shop from *Brotato*, joker-style build synergies from *Balatro*, risky events from *Slay the Spire*, realms of cultivation from *Tale of Immortal*.

![Menu](docs/screens/menu.png)
![Combat](docs/screens/combat.png)

## Play
Download the latest zip from the **Releases** page, unzip, run `VanKiepTuTien.exe` (Windows 64-bit). The game starts in a 1600x900 window; F11 toggles fullscreen.
It speaks **English, Vietnamese and Chinese** (button at the top right of the menu; it follows your system language on first launch). A short tutorial appears the first time you play.

| Key | Action |
|---|---|
| WASD / arrows | Move (weapons aim and attack by themselves) |
| Space / Shift / right click | Dash (invulnerable while dashing) |
| E / J / left click | Divine Art: each character's signature skill |
| 1-4 | Pick an upgrade or event option |
| R | Reroll the shop |
| Esc / P | Pause |

Gamepad (Xbox layout) and touch controls are supported too.

![Characters](docs/screens/characters.png)

## Game loop
1. **Survive a wave** (20-60 s). Enemies are telegraphed with red circles. Kills drop Qi, which is both experience and the money you spend between waves.
2. **Between waves:** open chests, take a risky event, level up (choose 1 of 4 techniques), shop for weapons and treasures, then choose a safe or a perilous route.
3. **Boss every 5 waves**, with heavenly lightning that also hits enemies. Beat wave 20 to ascend.
4. Win or lose, you earn Dao Merit to buy permanent upgrades.

![Shop](docs/screens/shop.png)

## Build
- Every weapon belongs to one or more Paths (Sword, Body, Fire, Thunder, Talisman, Poison); 2, 4 or 6 of the same Path unlock set bonuses.
- Two weapons of the same kind and grade merge into a higher grade.
- Joker-style treasures create combos, for example one that grows with the Spirit Stones you hold, or one that multiplies damage when you run five different Paths.
- 6 characters, 6 difficulty levels (Tribulations) each, 20 achievements, a codex, and a permanent upgrade tree. All of it feeds the completion % shown on the menu.

## Project notes
- Unity **6000.3.27f1**. There are no hand-built scenes or prefabs: the game builds itself from code at runtime (`Assets/Scripts`, with `Core/`, `Play/`, `UI/`, `Data/`, `Auto/`).
- Game content and balance live in `Assets/Scripts/Data/Content.cs` and `Events.cs`.
- Music and sound effects are synthesized in code (`Synth`), mixed with Kenney sound packs.
- A self-playing bot (`-autotest`) and scenario tests (`-skilltest`) drive balance and regression checks: `bash tools/build_and_test.sh 3 -skilltest -only ""`.
- Store-readiness notes (input, saves, achievements abstraction): `docs/PORTING.md`. Design notes: `docs/DESIGN.md`.
- Vietnamese README: [README.vi.md](README.vi.md).

## Credits and licenses
- **Source code:** MIT, see `LICENSE`. Third-party assets keep their own licenses.
- **Character, enemy, boss, icon and background art:** generated with PixelLab (AI pixel-art tool) and cleaned up by hand in Aseprite; the tool and prompt for each image are logged in `Assets/ThirdParty/PixelLab-Assets.md`.
- **Tiles, weapon and treasure sprites, sound packs:** [Kenney](https://kenney.nl) (CC0): Tiny Dungeon, Impact Sounds, Interface Sounds, RPG Audio, Music Jingles. Licenses in `Assets/ThirdParty/`.
- **Font:** Baloo 2 by Ek Type, SIL Open Font License 1.1 (`Assets/ThirdParty/Baloo2-OFL.txt`). The Chinese UI uses an installed system font; none is bundled.

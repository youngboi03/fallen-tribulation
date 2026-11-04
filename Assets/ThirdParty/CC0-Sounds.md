# Sound sources

Rebuild: `tools/audio_gen.py design` reads the recordings below and writes the clips; recipes are the `DESIGN` table in that file.

## Sound effects: CC0 1.0

Recordings by Joseph Sardin and contributors, [BigSoundBank](https://bigsoundbank.com/licenses.html), each marked "CC0 (public domain): Free and royalty-free" on its own page. Attribution is not required; credited here for traceability.

| Game sound | BigSoundBank sound numbers |
|---|---|
| `hit` | 2068, 2459, 2461, 2463, 2465 |
| `dash`, `swing` | 0127, 0129, 1795, 1796, 1798 |
| `shoot`, `zap`, `heavy` | 0549, 1798, 3063, 1836, 2463 |
| `sk_dan` | 0987, 1340 |
| `sk_loi` | 2949, 3063, 3115 |
| `sk_the` | 0576, 1634, 1808, 1836 |
| `sk_phu` | 1340, 2961 |
| `sk_ma` | 0486, 1796, 1881 |
| `ult_b1` | 3061, 3063 |
| `ult_b1b` | 0127, 2892 |
| `ult_b2` | 1023, 1634, 1796, 1807 |
| `ult_b2b` | 2961, 2962 |
| `ult_b3` | 1519, 1521 |
| `ult_b4` | 1808, 2949, 3063, 3115 |
| `bell`, `sk_chime` | 0127, 2894 |
| `breakthrough` | 0129, plus a Stable Audio 3 flute run (`r_breakthrough`, cfg 2, seed 1011) |
| `coin`, `pickup`, `tick`, `click` | 0483, 0648, 1074 |
| `confirm`, `error`, `open`, `pluck` | 0127, 0211, 0576, 1074, 1531 |

`ult_b2b` also uses `slime_13` from [40 CC0 water / splash / slime SFX](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) (OpenGameArt, CC0).

Clips were cut, pitch-shifted, reversed, layered and levelled for this game.

## Music: AI-generated

`Assets/Resources/Music/*.ogg` were generated with Stable Audio 3 (`stable-audio-3-small-music`) through audio-studio-mcp; prompts and seeds are in `tools/audio_gen.py`. Terms in `stability-NOTICE.txt`.

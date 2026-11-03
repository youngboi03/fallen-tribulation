# Fallen Tribulation: a devlog

Fallen Tribulation is a 2D wave-survival roguelite about cultivation (xianxia) made in Unity 6. Its working title was "Vạn Kiếp Tu Tiên", roughly "Ten Thousand Tribulations". The Vietnamese title is now "Lục Đạo Đọa Kiếp" (Six Paths, Fallen Tribulation), because the number six runs through the whole game: six Paths, six characters, six bosses, six Tribulations. Today it has 6 characters, 6 bosses, 6 difficulty levels called Tribulations (Kiếp 0 to Kiếp 5), 6 Paths for building synergies, and you can play it from the first wave all the way to Ascension. I'm putting it on hold, so this is my look back: what worked, what hurt, and what is still rough.

## Where it came from

I started by listing the games I had played to 100%. They fell into four groups: build-and-synergy roguelikes (Balatro, Slay the Spire, Brotato), skill-heavy action (Dead Cells, Elden Ring, Wukong), cultivation stories (Tale of Immortal, Nobody), and anything with a collection to complete.

I considered a Balatro-style deckbuilder, but it had none of the action I enjoy. A Dead Cells-style platformer would have needed frame animation and level generation, more than I could do well. So I settled on a mix: Brotato's wave loop and shop, Balatro's joker-style items that multiply each other, Slay the Spire's risky events, and Tale of Immortal's cultivation realms as the spine of progression.

One technical decision shaped everything after it: no hand-built scenes or prefabs. The whole game is assembled from code at runtime inside a single empty scene, collision is a simple circle-distance check instead of the physics engine, and every balance number lives in one data file. It meant I could build, run a bot and compare results without ever worrying about an asset misplaced in the Editor.

## The core loop and builds

A run takes about 25 to 30 minutes, with waves of 20 to 60 seconds. Weapons fire on their own; you move, dash and cast your Divine Art. Kills drop Qi, both experience and money. The part I love most happens between waves: open chests, meet a random event, level up and pick one of four techniques, shop, then choose a safe or a perilous route for the next wave. A boss shows up every five waves, and clearing wave 20 means you ascend.

Builds revolve around six Paths: Sword, Body, Fire, Thunder, Talisman and Poison. Two, four or six weapons of one Path unlock set bonuses, and matching weapons merge into a higher grade. The game ended up with 24 weapons, 36 treasures and 16 events. Joker-style treasures are where builds explode: one grows with the Spirit Stones you hold, another multiplies damage when you run five different Paths. Realms follow your level, from Qi Refining up through Nascent Soul, and every breakthrough is a big choice.

Outside a run there is Dao Merit to spend on permanent upgrades, 20 achievements, an in-game encyclopedia, and a completion percentage on the main menu. I'm exactly the kind of player that number keeps hooked.

## Divine Arts

The first version only had movement and dash, with nothing active to do. So every character got a signature skill on E or left click that finds its own target. The Sword cultivator rains flying swords, the Alchemist detonates fire and heals, the Body cultivator turns invulnerable and stomps out a shockwave, the Talisman master freezes an area in place, the Demon cultivator trades blood for power, and the Thunder cultivator calls stunning lightning.

The first formula scaled with level and stats and failed as you'd expect: weak early, a screen-wipe late. Now base power is tied to the health of regular enemies on the current wave, with a factor that shrinks over 20 waves, so skills hit hard early and only stay strong late if you invest. Each boss kill raises your skill a stage, from I to IV, changing its form and making it look bigger. Realm breakthroughs open an Insight screen where you pick one of four cards, including character-specific ones. It didn't hit every target; more on that below.

## Bosses and Tribulations

This is the part I'm proudest of. Originally all six bosses shared one routine: charge, bullet ring, summon, spiral. "Phase two" just meant getting faster at half health. Higher Tribulations only multiplied stats, so every boss felt the same.

In the rebuild, each boss has a base kit plus two extension kits, each with its own ultimate. The kits stack: you learn a boss on Tribulation 0, and higher Tribulations add moves on top of the ones you know. From Tribulation 3, the boss enters with a mutation cutscene. On Tribulation 5, a second cutscene at half health unlocks the last kit. Every boss has two phases, and phase two brings new moves and its own climax track. Ultimates trigger at health thresholds, always telegraph for at least a second, and always leave somewhere safe to stand. The first time you meet a boss, a Borderlands-style card slides in with its portrait and a huge name.

The guiding rule: higher difficulty should come from moves more than stats. I also redesigned the bosses around Eastern myth: a one-eyed mountain demon, an opera general with four banners on his back, a spider spirit, a demon emperor in dragon robes.

## Art and sound

To be clear: the characters, enemies, bosses, icons, portraits and backgrounds were generated with PixelLab, an AI pixel-art tool, and then cleaned up and edited. None of them were drawn by hand from scratch. The tool and prompt behind every image are logged.

Generated art rarely works straight out of the tool: it comes out large, with too many colors and soft shading, while the game needs small sprites with chunky color blocks. So I built a pipeline to wrestle it into shape: downscale, quantize to about 20 colors without dithering, add a one-pixel black outline. Character sprites were redone at 48 pixels. Where something broke, like the Sword cultivator's legs, I regenerated just that region with inpainting. Every sprite then goes through a pixel clean-up script running Aseprite in batch mode, and an asset QA check. Walk strips are generated by script from each base sprite, and an import tool locks frames to its palette and pins the feet. My first boss portraits were far too smooth and detailed for the rest of the game, so I went simpler.

Everything else comes from Kenney (CC0): the Tiny Dungeon tiles, the original weapon and treasure sprites, and the impact, interface, RPG and jingle sound packs. The music is synthesized entirely in code, on a pentatonic scale, with plucked tones that hint at a guzheng. The phase-two boss track is the one I still enjoy listening to.

## Three languages

The game runs in Vietnamese, English and Chinese, switchable in-game, and on first launch it follows the system language. I wrote the game in Vietnamese and translate phrase by phrase at the point of display, from three-column text tables. A checker flags anything left untranslated. The limits: no native speaker has reviewed the translations yet, and the Chinese UI relies on a system font instead of a bundled one.

## Balancing with a bot that plays itself

I don't have a QA team, so I built a bot that plays the game. It finishes runs, takes screenshots and writes logs; a "smart" mode shops sensibly, another goes all-in on the Divine Art. It later learned to dodge boss hazards (hits taken in one test fell from 34 to 7). Scenario tests set up a situation and check the outcome. The last full run passed 1673 out of 1673 checks.

The bot showed me things I would never have seen by feel. An economy simulation found that about 43% of late-game items came from elite chests. At one point the skill-focused bot held 89 to 163 treasures by wave 19, distorting every measurement. I had to fix the economy before I could tune skills: elite chest odds now drop with every chest that falls in a wave, and each treasure has a copy limit by grade. Even the measuring had to be fixed: overkill damage inflated the skill's share, and the old bot waited for six enemies nearby before casting, so many waves it never cast at all.

The most painful lesson was the knock-on effect. Once I tightened the economy, Tribulation 3 fell from 3 wins in 6 to 1 in 6, and Tribulation 5 dropped to zero. The difficulty curve had been tuned on top of a surplus of chests. After two rounds, the smart bot won 6/6 on Tribulation 0, 3/6 on Tribulation 3 and 2/6 on Tribulation 5, right on target. Each cell was a single run, though, so the numbers are noisy.

## The UI

Every screen is uGUI built in code, with gamepad and touch support. The first version worked but looked cold and patchy. The last restyle swapped outlines for framed, hand-cut panels, a warm ink palette and the rounded Baloo 2 font, and made icons keep their aspect ratio. Tutorial hints now only appear in your first run, each one once, at the moment it matters. Small changes, but the game finally looked cared for.

## The hardest problems

- **Draw order.** Sorting by y let low-on-screen enemies climb above bullets and effects, so shots vanished behind monsters. Everything now sits in explicit layer bands.
- **Screen shake.** Every crit and heavy hit shook the camera, and late-game it never stopped. I removed crit shake, spaced out small shakes, and added options for shake and damage numbers.
- **The boss brain.** Changing phase mid-move broke its state, so phase changes now wait for the move to finish, and the brain must stop before the boss is destroyed.
- **Colors carry meaning.** I wanted the final boss in gold, but gold means "safe" in this game. An attack in the same color as a safe zone is a serious bug, so the boss stayed purple.

## What this version does well

The game is complete and stable, with no exceptions in the last full test run. The between-wave loop is full of real decisions. Each boss is recognizable within seconds, and higher Tribulations bring new moves, not just more health. Hits feel weighty, the tests catch regressions, and a platform layer for saves, achievements and input shortens the road to stores.

## What is unfinished or rough

- **Weapons are hard to tell apart.** At real icon size, the 24 weapons fall into five lookalike groups (eight read as the same diagonal blade). Projectiles come in only three shapes. A redesign plan in a light chibi style gives each weapon its own silhouette, and a few trial icons are approved, but the redraw isn't done.
- **Animation.** The frame-animation engine works and I tried walk strips for all six characters, but they did not look good enough, so this build turns them off: heroes and monsters still just hop as they move. Proper walk cycles, attack and idle clips, and all boss animation are still missing.
- **Some sprites don't match.** The Demon cultivator hasn't been redone like the other five. The final boss's crown has broken white patches. One boss's deep red is too dark for players with red color blindness.
- **Early-game Divine Arts.** In waves 1 to 5, the Alchemist, Body and Talisman skills deal only about 5 to 10% of total damage, against a target of 20 to 35%. Too few enemies are in range and you cast only one to three times per wave, so it needs a stage I redesign, not new numbers. The Demon cultivator's stage I deals no damage at all, and the Body cultivator barely changes when you invest in the skill.
- **Late-game economy.** Players still end up with roughly 500 to 900 spare Spirit Stones per wave late in a run, and it all pours into the shop.
- **A planned event** that would turn into a small fight was never built.
- **Human playtesting.** Most recent balance work has only been checked by the bot; the game needs more hands-on play.
- **Release work.** There's no Steam or Google Play integration yet. Settings lack resolution options, gamepad button icons are missing, and PixelLab's commercial terms for my plan need checking before any sale.

## Why I'm pausing

I'm starting a new project built around real-time action combat. Working on Fallen Tribulation taught me that what I love most is facing a boss: reading the telegraph, dodging, punishing. A game where weapons fire on their own has a ceiling right there. I'm carrying over the art, the asset pipeline and the lessons, above all the habit of letting the bot and the tests tell me the truth before I trust my gut.

Pausing isn't abandoning. The game is complete enough to play from start to finish, with all 6 characters, 6 bosses and 6 Tribulations. It just isn't fully polished.

## If I pick it back up

If I come back, or if you're curious, this is the order I'd work in:

1. Redraw the 24 weapons to the silhouette plan, with projectiles that carry each weapon's look.
2. Finish animation: redo the walk strips, add attack and idle clips for the characters, then the bosses, starting with each boss's base ultimate.
3. Redesign stage I of the Divine Arts (shorter early cooldowns or a wider reach), and give the Demon cultivator damage from the start.
4. Add late-game Spirit Stone sinks, such as treasures with a Brotato-style drawback, or chests where you pick one of two.
5. Turn that event into a small fight.
6. Get more people playing it, run more balance passes on Tribulations 3 and 5, and have native speakers review the translations.
7. Only then, release work: Steam, Steam Deck, then Android.

Thanks for reading.

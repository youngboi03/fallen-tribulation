using System.Collections;
using System.IO;
using UnityEngine;

namespace VK
{
    /// <summary>
    /// Launch with -autotest [-shots dir] [-waves N] [-god] [-char i] [-kiep k] [-speed s]:
    /// a bot plays the game, takes screenshots and logs a balance summary per wave.
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        string shotDir;
        int maxWaves = 6, charIdx, kiep, errors;
        bool god, smart;
        int metaLv;
        float speed = 2f;

        void Start()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                switch (args[i])
                {
                    case "-shots": shotDir = args[i + 1]; break;
                    case "-waves": int.TryParse(args[i + 1], out maxWaves); break;
                    case "-char": int.TryParse(args[i + 1], out charIdx); break;
                    case "-kiep": int.TryParse(args[i + 1], out kiep); break;
                    case "-meta": int.TryParse(args[i + 1], out metaLv); break;
                    case "-speed": float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out speed); break;
                }
            }
            god = System.Array.IndexOf(args, "-god") >= 0;
            smart = System.Array.IndexOf(args, "-smart") >= 0;
            if (string.IsNullOrEmpty(shotDir)) shotDir = Path.Combine(Application.persistentDataPath, "shots");
            Directory.CreateDirectory(shotDir);
            Application.logMessageReceived += (msg, st, type) =>
            {
                if (type == LogType.Exception || type == LogType.Error) { errors++; Debug.Log("[AUTOTEST] ERROR CAPTURED: " + msg + "\n" + st); }
            };
            // unlock everything in the test save so all screens can be checked
            Game.I.save.bestWave = 20; Game.I.save.totalKills = 2000; Game.I.save.bestLevel = 25; Game.I.save.wins = 1;
            Game.I.save.maxKiepUnlocked = 5;
            for (int m = 0; m < Game.I.save.meta.Length; m++) Game.I.save.meta[m] = Mathf.Clamp(metaLv, 0, Content.MetaMax);
            Game.I.player.god = god;
            StartCoroutine(Run());
        }

        static WaitForSecondsRealtime Real(float t) => new WaitForSecondsRealtime(t);

        IEnumerator Run()
        {
            var g = Game.I;
            yield return Real(2f);
            Shot("01_menu"); yield return Real(0.3f);
            g.ui.ShowCharSelect(); yield return Real(0.6f); Shot("02_charselect"); yield return Real(0.3f);
            g.ui.ShowMeta(); yield return Real(0.6f); Shot("03_meta"); yield return Real(0.3f);
            g.ui.ShowAchievements(); yield return Real(0.6f); Shot("04_achievements"); yield return Real(0.3f);
            g.ui.ShowCodex(); yield return Real(0.6f); Shot("05_codex"); yield return Real(0.3f);
            g.ui.ShowMenu(); yield return Real(0.3f);

            Time.timeScale = speed;
            g.StartRun(charIdx, kiep);
            g.player.god = god;
            int seenWave = 0;
            float waveStart = Time.realtimeSinceStartup;
            int peak = 0;
            float lowHp = 1f;
            while (true)
            {
                if (g.state == GState.Playing)
                {
                    peak = Mathf.Max(peak, Enemy.All.Count);
                    lowHp = Mathf.Min(lowHp, g.run.hp / Mathf.Max(1f, g.run.MaxHP));
                    if (g.run.wave != seenWave)
                    {
                        seenWave = g.run.wave;
                        waveStart = Time.realtimeSinceStartup;
                        peak = 0; lowHp = 1f;
                        StartCoroutine(MidShot(seenWave));
                    }
                    if (g.dir.bossWave && Time.realtimeSinceStartup - waveStart > 240f)
                    {
                        Debug.Log("[AUTOTEST] boss wave timeout");
                        Finish(); yield break;
                    }
                }
                else if (g.state == GState.Between)
                {
                    yield return Real(0.5f);
                    if (g.state != GState.Between) continue;
                    if (g.ui.screen == UIScreen.Relic)
                    {
                        Shot("w" + g.run.wave.ToString("00") + "_relic"); yield return Real(0.3f);
                        g.TakeRelic(Random.Range(0, g.run.relicChoices.Length));
                        yield return Real(0.15f);
                        continue;
                    }
                    if (g.ui.screen == UIScreen.Chest)
                    {
                        Shot("w" + g.run.wave.ToString("00") + "_chest"); yield return Real(0.3f);
                        bool full = g.run.chestItem.max > 0 && g.run.Count(g.run.chestItem.id) >= g.run.chestItem.max;
                        g.TakeChest(full);
                        yield return Real(0.15f);
                        continue;
                    }
                    if (g.ui.screen == UIScreen.Event)
                    {
                        Shot("w" + g.run.wave.ToString("00") + "_event"); yield return Real(0.3f);
                        int pick = 0; while (pick < g.run.currentEvent.options.Length - 1 && !g.run.currentEvent.options[pick].can(g.run)) pick++;
                        g.ChooseEvent(pick);
                        yield return Real(0.15f);
                        continue;
                    }
                    if (g.ui.screen == UIScreen.LevelUp)
                    {
                        if (g.run.ChoosingLevel <= 3 || g.run.IsBreakthrough) { Shot("w" + g.run.wave.ToString("00") + "_levelup_" + g.run.ChoosingLevel); yield return Real(0.3f); }
                        g.ChooseLevel(PickLevel());
                        yield return Real(0.15f);
                        continue;
                    }
                    if (g.ui.screen == UIScreen.Shop)
                    {
                        var r = g.run;
                        Debug.Log($"[AUTOTEST] wave {r.wave} done: lvl {r.level} gold {r.gold} kills {r.kills} hp {r.hp:0}/{r.MaxHP:0} dmg {r.dmgDealt:0} peakEnemies {peak} weapons {r.weapons.Count} items {r.items.Count} low {lowHp * 100:0}% hits {r.hitsThisWave} realTime {Time.realtimeSinceStartup - waveStart:0.0}s");
                        Shot("w" + r.wave.ToString("00") + "_shop_a"); yield return Real(0.3f);
                        BuyStuff();
                        g.ui.ShowShop();
                        yield return Real(0.3f);
                        Shot("w" + r.wave.ToString("00") + "_shop_b"); yield return Real(0.3f);
                        if (r.wave >= maxWaves) { Finish(); yield break; }
                        g.NextWave(r.routeOffer > 0 && (r.wave + 1) % 5 != 0 && Random.value < 0.5f ? r.routeOffer : 0);
                    }
                }
                else if (g.state == GState.Over)
                {
                    var r = g.run;
                    Debug.Log($"[AUTOTEST] RUN OVER at wave {r.wave}: lvl {r.level} kills {r.kills} dmg {r.dmgDealt:0}");
                    yield return Real(3f);
                    Shot("99_gameover");
                    Finish(); yield break;
                }
                yield return null;
            }
        }

        IEnumerator MidShot(int w)
        {
            yield return new WaitForSeconds(w % 5 == 0 ? 10f : 12f);
            if (Game.I.state == GState.Playing && Game.I.run.wave == w) Shot("w" + w.ToString("00") + "_mid");
            if (w == 1)
            {
                Game.I.SetPause(true);
                yield return Real(0.4f);
                Shot("w01_pause");
                yield return Real(0.3f);
                Game.I.SetPause(false);
            }
        }

        int PickLevel()
        {
            var o = Game.I.run.levelOpts;
            if (smart)
            {
                var r = Game.I.run;
                int melee = 0; foreach (var w in r.weapons) if (w.def.Melee) melee++;
                bool isMelee = melee * 2 >= r.weapons.Count;
                int bi = 0; float bs = -1;
                for (int i = 0; i < o.Length; i++)
                {
                    float sc = o[i].tier * 4;
                    foreach (var m in o[i].mods)
                    {
                        if (m.s == Stat.Damage || m.s == Stat.AtkSpeed) sc += 5;
                        else if ((m.s == Stat.Melee && isMelee) || (m.s == Stat.Spell && !isMelee)) sc += 5;
                        else if (m.s == Stat.MaxHP || m.s == Stat.Armor) sc += r.MaxHP < 60 ? 4 : 2;
                        else if (m.s == Stat.Crit || m.s == Stat.Lifesteal) sc += 2;
                    }
                    if (sc > bs) { bs = sc; bi = i; }
                }
                return bi;
            }
            int best = 0; int bt = -1;
            for (int i = 0; i < o.Length; i++)
            {
                int score = o[i].tier * 10;
                foreach (var m in o[i].mods) if (m.s == Stat.Damage || m.s == Stat.AtkSpeed || m.s == Stat.MaxHP) score += 3;
                if (score > bt) { bt = score; best = i; }
            }
            return best;
        }

        void BuyStuff()
        {
            var r = Game.I.run;
            if (smart) { SmartBuy(r); return; }
            for (int pass = 0; pass < 10; pass++)
            {
                for (int i = 0; i < r.shop.Length; i++)
                {
                    var it = r.shop[i].it;
                    bool hurtsHp = false;
                    if (it != null && it.mods != null) foreach (var m in it.mods) if (m.s == Stat.MaxHP && m.v < 0) hurtsHp = true;
                    if (hurtsHp && r.MaxHP < 40) continue;
                    if (r.CantBuy(i) == null) r.Buy(i);
                }
                foreach (var w in r.weapons.ToArray()) if (r.weapons.Contains(w) && r.MergePartner(w) != null) r.Merge(w);
                if (r.gold > r.RerollCost + 40) r.Reroll(); else break;
            }
        }

        /// <summary>Build-focused buyer: commits to the most common Đạo, merges, skips -HP items, keeps a reserve.</summary>
        void SmartBuy(RunState r)
        {
            for (int pass = 0; pass < 12; pass++)
            {
                var tagCount = new int[(int)Tag.COUNT];
                foreach (var w in r.weapons) foreach (var t in w.def.tags) tagCount[(int)t]++;
                int fav = 0; for (int t = 1; t < tagCount.Length; t++) if (tagCount[t] > tagCount[fav]) fav = t;
                int best = -1; float bs = 0;
                for (int i = 0; i < r.shop.Length; i++)
                {
                    var s = r.shop[i];
                    if (r.CantBuy(i) != null) continue;
                    float sc;
                    if (s.w != null)
                    {
                        bool merge = r.MergeTarget(s.w, s.tier) != null;
                        if (r.weapons.Count >= r.maxWeapons && !merge) continue;
                        sc = 4 + s.tier * 3 + (s.w.HasTag((Tag)fav) ? 6 : 0) + (merge ? 4 : 0);
                    }
                    else
                    {
                        bool hurts = false; float pos = 0;
                        if (s.it.mods != null) foreach (var m in s.it.mods) { if (m.s == Stat.MaxHP && m.v < 0) hurts = true; if (m.v > 0) pos += 1; }
                        if (hurts && r.MaxHP < 80) continue;
                        sc = 2 + s.it.tier * 3 + pos + (string.IsNullOrEmpty(s.it.desc) ? 0 : 2);
                    }
                    sc /= Mathf.Max(1f, s.price / 30f);
                    if (sc > bs) { bs = sc; best = i; }
                }
                if (best >= 0) { r.Buy(best); continue; }
                foreach (var w in r.weapons.ToArray()) if (r.weapons.Contains(w) && r.MergePartner(w) != null) r.Merge(w);
                if (r.gold > r.RerollCost + 30) r.Reroll(); else break;
            }
        }

        void Shot(string n)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, n + ".png"));
            Debug.Log("[AUTOTEST] shot " + n);
        }

        void Finish()
        {
            var r = Game.I.run;
            Debug.Log($"[AUTOTEST] DONE errors={errors} wave={(r != null ? r.wave : 0)} level={(r != null ? r.level : 0)}");
            Time.timeScale = 1f;
            StartCoroutine(Quit());
        }

        IEnumerator Quit()
        {
            yield return Real(1.5f);
            Application.Quit();
        }

        // ---------------------------------------------------------------- bot input
        public Vector2 Move(Player p)
        {
            Vector2 pos = p.Pos, f = Vector2.zero;
            // fight: approach the nearest enemy to ~1.8 units, back off from crowds that get too close
            Enemy near = Enemy.Nearest(pos, 30f);
            if (near != null)
            {
                Vector2 d = near.Pos - pos;
                float want = near.boss ? 3.2f : 1.8f;
                f += d.normalized * Mathf.Clamp(d.magnitude - want - near.radius, -1.5f, 1.5f);
            }
            foreach (var e in Enemy.All)
            {
                Vector2 d = pos - e.Pos;
                float dist = d.magnitude;
                if (dist < 1.6f + e.radius && dist > 0.01f) f += d / dist * (2f / Mathf.Max(0.3f, dist - e.radius)) * (e.boss ? 3f : 0.6f);
            }
            foreach (var pr in Projectile.All)
            {
                if (pr == null) continue;
                Vector2 d = pos - (Vector2)pr.transform.position;
                if (d.magnitude < 2.5f) f += d.normalized * 0.8f;
            }
            f += -pos * 0.05f;
            if (f.magnitude < 0.25f && Orb.All.Count > 0)
            {
                Orb best = null; float bd = 1e9f;
                foreach (var o in Orb.All) { float dd = ((Vector2)o.transform.position - pos).sqrMagnitude; if (dd < bd) { bd = dd; best = o; } }
                if (best != null) f = ((Vector2)best.transform.position - pos).normalized;
            }
            f += new Vector2(-f.y, f.x) * 0.35f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector2.zero;
        }

        public bool WantSkill(Player p)
        {
            int n = 0;
            foreach (var e in Enemy.All)
            {
                float d = Vector2.Distance(e.Pos, p.Pos);
                if (e.boss && d < 6f) return true;
                if (d < 4f) n++;
            }
            if (n >= 6 && !skillShot) { skillShot = true; StartCoroutine(SkillShot()); }
            return n >= 6;
        }
        bool skillShot;
        IEnumerator SkillShot()
        {
            yield return new WaitForSeconds(0.12f);
            Shot("skill_" + Game.I.run.ch.id);
        }

        public bool WantDash(Player p)
        {
            foreach (var e in Enemy.All) if (Vector2.Distance(e.Pos, p.Pos) < e.radius + 0.9f) return true;
            return false;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VK
{
    /// <summary>
    /// Launch with -autotest [-shots dir] [-waves N] [-god] [-char i] [-kiep k] [-speed s]:
    /// a bot plays the game, takes screenshots and logs a balance summary per wave.
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        string shotDir;
        int maxWaves = 6, charIdx, kiep;
        public static int Errors;
        bool god, smart, skillBuild;   // -skillbuild: invests in Thần Thông (cards, Thần Lực / Tĩnh Tâm, skill items)
        int metaLv, langArg = -1;
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
                    case "-lang": int.TryParse(args[i + 1], out langArg); break;
                    case "-speed": float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out speed); break;
                }
            }
            god = System.Array.IndexOf(args, "-god") >= 0;
            smart = System.Array.IndexOf(args, "-smart") >= 0;
            skillBuild = System.Array.IndexOf(args, "-skillbuild") >= 0;
            ux = System.Array.IndexOf(args, "-uxtest") >= 0;
            if (string.IsNullOrEmpty(shotDir)) shotDir = Path.Combine(Application.persistentDataPath, "shots");
            Directory.CreateDirectory(shotDir);
            Application.logMessageReceived += (msg, st, type) =>
            {
                if (type == LogType.Exception || type == LogType.Error) { Errors++; Debug.Log("[AUTOTEST] ERROR CAPTURED: " + msg + "\n" + st); }
            };
            // unlock everything in the test save so all screens can be checked
            Game.I.save.bestWave = 20; Game.I.save.totalKills = 2000; Game.I.save.bestLevel = 25; Game.I.save.wins = 1;
            Game.I.save.maxKiepUnlocked = 5;
            for (int m = 0; m < Game.I.save.meta.Length; m++) Game.I.save.meta[m] = Mathf.Clamp(metaLv, 0, Content.MetaMax);
            Game.I.player.god = god;
            Game.I.ui.ApplyLanguage(langArg >= 0 ? langArg : Loc.VI);
            if (System.Array.IndexOf(args, "-skilltest") >= 0) { skill = gameObject.AddComponent<SkillTest>(); return; }
            StartCoroutine(Run());
        }
        SkillTest skill;

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
            g.ui.ShowSettings(false); yield return Real(0.6f); Shot("06_settings"); yield return Real(0.3f);
            g.ui.ShowMenu(); yield return Real(0.3f);
            if (ux) yield return MenuFocusCheck();

            Time.timeScale = speed;
            g.StartRun(charIdx, kiep);
            g.player.god = god;
            int seenWave = 0;
            float waveStart = Time.realtimeSinceStartup;
            int peak = 0;
            float lowHp = 1f;
            float skill0 = 0f, dmg0 = 0f, skillRaw0 = 0f, dmgRaw0 = 0f; int casts0 = 0;   // skill / total damage at wave start, for the per-wave skill share
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
                        peak = 0; lowHp = 1f; casts0 = Skills.CastCount; skill0 = g.run.skillEff; dmg0 = g.run.dmgEff; skillRaw0 = g.run.skillDmg; dmgRaw0 = g.run.dmgDealt;
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
                        bool full = g.run.Count(g.run.chestItem.id) >= g.run.chestItem.Cap;
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
                        Debug.Log($"[AUTOTEST] wave {r.wave} done: lvl {r.level} gold {r.gold} kills {r.kills} hp {r.hp:0}/{r.MaxHP:0} dmg {r.dmgDealt:0} peakEnemies {peak} weapons {r.weapons.Count} items {r.items.Count} low {lowHp * 100:0}% hits {r.hitsThisWave} realTime {Time.realtimeSinceStartup - waveStart:0.0}s skill {100f * (r.skillEff - skill0) / Mathf.Max(1f, r.dmgEff - dmg0):0.0}% skillRaw {100f * (r.skillDmg - skillRaw0) / Mathf.Max(1f, r.dmgDealt - dmgRaw0):0.0}% casts {Skills.CastCount - casts0} sp {r.stats[Stat.SkillPower]:0} haste {r.stats[Stat.SkillHaste]:0} cards {r.skillCards.Count} build {string.Join(",", r.weapons.ConvertAll(w => w.def.id + w.tier))}");
                        Shot("w" + r.wave.ToString("00") + "_shop_a"); yield return Real(0.3f);
                        if (ux && r.wave == 1) yield return ShopFocusChecks();
                        BuyStuff();
                        g.ui.ShowShop();
                        yield return Real(0.3f);
                        Shot("w" + r.wave.ToString("00") + "_shop_b"); yield return Real(0.3f);
                        if (r.wave >= maxWaves) { Finish(); yield break; }
                        bool risky = smart ? lowHp > 0.75f : Random.value < 0.5f;   // a sensible player only gambles when the last wave went well
                        int route = r.routeOffer > 0 && (r.wave + 1) % 5 != 0 && risky ? r.routeOffer : 0;
                        if (ux && r.wave == 1) { yield return CameraCheck(route); continue; }
                        g.NextWave(route);
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
                    if (o[i].card != null)
                    {   // Ngộ Đạo: the plain bot never takes a card; the skill bot takes the signature one, else the shared one
                        if (!skillBuild) continue;
                        float cs = o[i].card.charId != null ? 1000f : 900f;
                        if (cs > bs) { bs = cs; bi = i; }
                        continue;
                    }
                    bool skillOnly = o[i].mods.Length > 0 && System.Array.TrueForAll(o[i].mods, m => m.s == Stat.SkillPower || m.s == Stat.SkillHaste);
                    if (skillOnly && !skillBuild) continue;   // the plain bot treats Thần Lực / Tĩnh Tâm as worthless
                    float sc = o[i].tier * 4;
                    foreach (var m in o[i].mods)
                    {
                        if (m.s == Stat.SkillPower || m.s == Stat.SkillHaste) { if (skillBuild) sc += 6; continue; }
                        if (m.s == Stat.Damage || m.s == Stat.AtkSpeed) sc += 5;
                        else if ((m.s == Stat.Melee && isMelee) || (m.s == Stat.Spell && !isMelee)) sc += 5;
                        else if (m.s == Stat.MaxHP || m.s == Stat.Armor) sc += r.MaxHP < 60 ? 4 : 2;
                        else if (m.s == Stat.Crit || m.s == Stat.Lifesteal) sc += 2;
                    }
                    if (sc > bs) { bs = sc; bi = i; }
                }
                if (o[bi].card != null) Debug.Log("[AUTOTEST] ngodao pick " + o[bi].card.id);
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
                        sc = 4 + s.tier * 3 + (s.w.HasTag((Tag)fav) ? 6 : 0) + (merge ? 4 : 0) + (r.weapons.Count < 4 ? 10 : 0);   // damage first
                    }
                    else
                    {
                        bool hurts = false; float pos = 0;
                        if (s.it.mods != null) foreach (var m in s.it.mods) { if (m.s == Stat.MaxHP && m.v < 0) hurts = true; if (m.v > 0) pos += 1; }
                        if (hurts && r.MaxHP < 80) continue;
                        bool skillItem = System.Array.IndexOf(Content.SkillItemIds, s.it.id) >= 0;
                        if (skillItem && !skillBuild) continue;   // the plain bot ignores Thần Thông items
                        sc = 2 + s.it.tier * 3 + pos + (string.IsNullOrEmpty(s.it.desc) ? 0 : 2) + (skillItem ? 6 : 0);
                    }
                    sc /= Mathf.Max(1f, s.price / 30f);
                    if (sc > bs) { bs = sc; best = i; }
                }
                if (best >= 0) { r.Buy(best); continue; }
                foreach (var w in r.weapons.ToArray()) if (r.weapons.Contains(w) && r.MergePartner(w) != null) r.Merge(w);
                if (r.gold > r.RerollCost + 30) r.Reroll(); else break;
            }
        }

        public void Shot(string n)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, n + ".png"));
            Debug.Log("[AUTOTEST] shot " + n);
        }

        public void Finish()
        {
            var r = Game.I.run;
            if (skill != null) Debug.Log($"[AUTOTEST] SKILL CHECKS passed={SkillTest.Pass} failed={SkillTest.Fail}");
            if (ux) Debug.Log($"[AUTOTEST] UX CHECKS passed={uxPass} failed={uxFail}");
            Debug.Log($"[AUTOTEST] DONE errors={Errors} wave={(r != null ? r.wave : 0)} level={(r != null ? r.level : 0)}");
            Time.timeScale = 1f;
            StartCoroutine(Quit());
        }

        IEnumerator Quit()
        {
            yield return Real(1.5f);
            Application.Quit();
        }

        // ---------------------------------------------------------------- UX checks (-uxtest)
        bool ux;
        int uxPass, uxFail;

        void Check(string name, bool ok, string detail)
        {
            if (ok) uxPass++; else uxFail++;
            Debug.Log("[AUTOTEST] CHECK " + name + " " + (ok ? "PASS" : "FAIL") + " " + detail);
        }

        static Vector2 Center(Button b) { var rt = (RectTransform)b.transform; return rt.TransformPoint(rt.rect.center); }

        static string Label(Button b)
        {
            var t = b != null ? b.transform.Find("label") : null;
            var tx = t != null ? t.GetComponent<Text>() : null;
            return tx != null ? tx.text : "";
        }

        /// <summary>Visible buttons whose label contains <paramref name="label"/>, left to right.</summary>
        static List<Button> Buttons(string label)
        {
            var res = new List<Button>();
            label = Loc.Tr(label);   // the screens are built in the current language
            foreach (var b in FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.isActiveAndEnabled && Label(b).Contains(label)) res.Add(b);
            res.Sort((a, c) => Center(a).x.CompareTo(Center(c).x));
            return res;
        }

        /// <summary>The real mouse cursor is over a button (that would legitimately take the focus).</summary>
        static bool MouseOverButton()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var hits = new List<RaycastResult>();
            es.RaycastAll(new PointerEventData(es) { position = Input.mousePosition }, hits);
            foreach (var h in hits) if (h.gameObject.GetComponentInParent<Button>() != null) return true;
            return false;
        }

        /// <summary>Main menu: the first arrow press puts the frame on "Nhập Đạo".</summary>
        IEnumerator MenuFocusCheck()
        {
            var nav = Game.I.GetComponent<PadNav>();
            nav.Hide();
            yield return null;
            if (MouseOverButton()) { Debug.Log("[AUTOTEST] CHECK menu_default SKIP (mouse over a button)"); yield break; }
            nav.Navigate(Vector2.down);
            var play = Buttons("Nhập Đạo");
            Check("menu_default", play.Count == 1 && nav.Selected == play[0], "selected='" + Label(nav.Selected) + "'");
            nav.Hide();
            yield return null;
        }

        /// <summary>Shop: a pressed button keeps the frame after the rebuild, arrows continue from there, and a hovered button takes the focus.</summary>
        IEnumerator ShopFocusChecks()
        {
            var g = Game.I;
            var nav = g.GetComponent<PadNav>();
            var locks = Buttons("Khóa");
            if (locks.Count >= 2)
            {
                nav.Select(locks[1]);
                yield return null;
                Vector2 p = Center(locks[1]);
                nav.Submit();   // locks card 2: the whole shop is rebuilt
                yield return null;
                var s = nav.Selected;
                Check("keep_after_press", s != null && Vector2.Distance(Center(s), p) < 20f,
                    s != null ? "moved " + Vector2.Distance(Center(s), p).ToString("0") + "px" : "nothing selected");
                nav.Navigate(Vector2.right);
                s = nav.Selected;
                Check("move_from_kept", s != null && Center(s).x > p.x + 50f && Mathf.Abs(Center(s).y - p.y) < 80f,
                    s != null ? "now '" + Label(s) + "' dx " + (Center(s).x - p.x).ToString("0") : "nothing selected");
                foreach (var slot in g.run.shop) if (slot != null) slot.locked = false;
                g.ui.ShowShop();
                yield return null;
            }
            else Debug.Log("[AUTOTEST] CHECK keep_after_press SKIP (" + locks.Count + " lock buttons)");

            var target = Buttons("Đường Bằng");
            if (target.Count == 0) target = Buttons("BẮT ĐẦU ĐỢT");
            nav.Hide();
            var pd = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(target[0].gameObject, pd, ExecuteEvents.pointerEnterHandler);
            nav.Navigate(Vector2.up);
            Check("hover_then_keys", nav.Selected == target[0], "selected='" + Label(nav.Selected) + "'");
            ExecuteEvents.Execute(target[0].gameObject, pd, ExecuteEvents.pointerExitHandler);
            nav.Hide();
            yield return null;
        }

        /// <summary>Ends wave 1 far from the centre; the camera must already be on the player in the first frame of wave 2.</summary>
        IEnumerator CameraCheck(int route)
        {
            var g = Game.I;
            g.player.transform.position = new Vector3(15f, 8.5f, 0f);
            yield return Real(1.5f);   // the camera settles on the corner
            g.NextWave(route);
            yield return new WaitForEndOfFrame();
            Vector2 cam = g.cam.transform.position, pl = g.player.Pos;
            Check("camera_snap", Vector2.Distance(cam, pl) < 0.05f, "camera " + cam.ToString("0.00") + " player " + pl.ToString("0.00"));
        }

        // ---------------------------------------------------------------- bot input
        public Vector2 Move(Player p)
        {
            if (Passive) return Vector2.zero;
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
            if (!noDodge && BossHazard.DangerAt(pos, 0.4f) > 0f) return Dodge(pos, f);
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector2.zero;
        }

        /// <summary>Standing in (or about to be in) a boss hazard: 8 directions x 2 steps, take the safest point; ties go to the direction closest to f.</summary>
        static Vector2 Dodge(Vector2 pos, Vector2 f)
        {
            Vector2 best = Vector2.zero; float bd = 9f, bdot = -2f;
            Vector2 fn = f.sqrMagnitude > 0.0001f ? f.normalized : Vector2.zero;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f; var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                foreach (float step in new[] { 1.5f, 3f })
                {
                    var pt = pos + dir * step;
                    pt.x = Mathf.Clamp(pt.x, -VK.Arena.HalfW + 0.5f, VK.Arena.HalfW - 0.5f); pt.y = Mathf.Clamp(pt.y, -VK.Arena.HalfH + 0.5f, VK.Arena.HalfH - 0.5f);
                    float d = BossHazard.DangerAt(pt, 0.4f), dot = Vector2.Dot(dir, fn);
                    if (d < bd - 0.001f || (Mathf.Abs(d - bd) <= 0.001f && dot > bdot)) { bd = d; bdot = dot; best = dir; }
                }
            }
            return best;
        }

        /// <summary>Casts like a player who presses E whenever it is ready and worth it: a boss or elite in reach, or 3 enemies inside the skill's own reach.</summary>
        public bool WantSkill(Player p)
        {
            var r = Game.I.run;
            if (r.ch.id == "ma_tu" && r.hp < r.MaxHP * 0.5f) return false;   // the rite costs HP: a player keeps it for when there is HP to spend
            float reach = SkillReach(r);
            int n = 0;
            foreach (var e in Enemy.All)
            {
                if (e == null || e.dead) continue;
                float d = Vector2.Distance(e.Pos, p.Pos);
                if ((e.boss || e.elite) && d < reach + 2f) return true;
                if (d < reach) n++;
            }
            if (n >= 6 && !skillShot) { skillShot = true; StartCoroutine(SkillShot()); }
            return n >= 3;
        }

        static float SkillReach(RunState r)
        {
            switch (r.ch.id)
            {
                case "kiem_tu": return SkillNum.KiemHomeRange;
                case "dan_tu": return (r.skillStage >= 2 ? SkillNum.DanR3 : SkillNum.DanR) + 0.5f;
                case "the_tu": return (r.skillStage >= 1 ? SkillNum.TheR2 : SkillNum.TheR) + 0.5f;
                case "phu_tu": return SkillNum.PhuR;
                case "loi_tu": return SkillNum.LoiRange * 0.8f;
                default: return 5f;   // Ma Tu: the rite pays off when a crowd is close
            }
        }
        bool skillShot;
        IEnumerator SkillShot()
        {
            yield return new WaitForSeconds(0.12f);
            Shot("skill_" + Game.I.run.ch.id);
        }

        /// <summary>Set by the boss tests' tank player: the bot neither walks nor dashes, so every hit lands.</summary>
        public static bool Passive;
        /// <summary>Test switch: the bot ignores boss hazards (baseline for T_bot_dodge).</summary>
        public static bool noDodge;
        public bool WantDash(Player p)
        {
            if (Passive) return false;
            if (!noDodge && BossHazard.DangerAt(p.Pos, 0.15f) > 0f) return true;
            foreach (var e in Enemy.All) if (Vector2.Distance(e.Pos, p.Pos) < e.radius + 0.9f) return true;
            return false;
        }
    }
}

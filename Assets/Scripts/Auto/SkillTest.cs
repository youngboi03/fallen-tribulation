using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace VK
{
    /// <summary>
    /// Launch with -skilltest [-only prefix]: deterministic scenarios for the Thần Thông rework.
    /// Scenarios are methods named T_&lt;prefix&gt;_&lt;name&gt; returning IEnumerator, run in name order.
    /// </summary>
    public class SkillTest : MonoBehaviour
    {
        public static int Pass, Fail;
        string only = "";

        public static void Check(string name, bool ok, string detail)
        {
            if (ok) Pass++; else Fail++;
            Debug.Log("[AUTOTEST] CHECK " + name + " " + (ok ? "PASS" : "FAIL") + " " + detail);
        }

        void Start()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-only") only = args[i + 1];
            StartCoroutine(RunAll());
        }

        IEnumerator RunAll()
        {
            yield return new WaitForSecondsRealtime(1f);
            var list = new List<MethodInfo>();
            foreach (var m in typeof(SkillTest).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                if (m.Name.StartsWith("T_" + only) && m.ReturnType == typeof(IEnumerator)) list.Add(m);
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            foreach (var m in list)
            {
                Debug.Log("[AUTOTEST] SCENARIO " + m.Name);
                yield return StartCoroutine((IEnumerator)m.Invoke(this, null));
            }
            Game.I.bot.Finish();
        }

        // ---------------------------------------------------------------- helpers
        /// <summary>Clean arena: run started, spawner off, no weapons, invulnerable player.</summary>
        public IEnumerator Arena(int ci, int wave, int stage, params string[] cards)
        {
            var g = Game.I;
            tankHold = false;
            g.StartRun(ci, 0);
            g.dir.enabled = false;
            g.dir.EndWave();
            foreach (var e in Enemy.Snapshot()) if (e != null) Destroy(e.gameObject);
            Enemy.All.Clear();
            var r = g.run;
            r.wave = wave; r.skillStage = stage; r.skillCards = new List<string>(cards);
            foreach (var w in r.weapons.ToArray()) w.DestroyView();
            r.weapons.Clear(); r.Recalc();
            g.player.god = true;
            Random.InitState(1234);
            yield return null;
        }

        public List<Enemy> Pack(int n, float r0, float r1, EnemyDef def = null)
        {
            var res = new List<Enemy>();
            var pp = Game.I.player.Pos;
            for (int i = 0; i < n; i++)
            {
                float a = i * 2.39996f;
                float rad = Mathf.Lerp(r0, r1, (i % 5) / 4f);
                var d = def ?? Content.Enemies[i % 2 == 0 ? 0 : Content.Enemies.FindIndex(x => x.id == "thiet_boi_thu")];
                res.Add(Enemy.Spawn(d, pp + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad, Game.I.run.wave, 1f, 1f));
            }
            return res;
        }

        public Enemy Elite(Vector2 p) => Enemy.Spawn(Content.Enemies.Find(x => x.id == "ma_tuong"), p, Game.I.run.wave, 1f, 1f);

        /// <summary>Boss of a wave slot at Kiếp 0 with its brain, through the WaveDirector spawn path.</summary>
        public Enemy BossAt(Vector2 p, int slot) => WaveDirector.SpawnBoss(Game.I.run.Boss(slot), p, Game.I.run.wave, 1f, 1f, slot, 0);

        public float Dmg(string src) => Game.I.run.skillDmgBy.TryGetValue(src, out var v) ? v : 0f;


        // ---------------------------------------------------------------- boss helpers (part C)
        public Enemy boss;
        bool tankHold;

        /// <summary>Like Arena, at the given kiep, then spawns the boss of that wave slot through the same numbers WaveDirector uses.</summary>
        public IEnumerator BossArena(string bossId, int kiep, int wave)
        {
            var g = Game.I;
            tankHold = false;
            g.StartRun(0, kiep);
            g.dir.enabled = false;
            g.dir.EndWave();
            foreach (var e in Enemy.Snapshot()) if (e != null) Destroy(e.gameObject);
            Enemy.All.Clear();
            var r = g.run;
            r.wave = wave; r.skillStage = 0; r.skillCards = new List<string>();
            foreach (var w in r.weapons.ToArray()) w.DestroyView();
            r.weapons.Clear(); r.Recalc();
            g.player.god = true;
            Random.InitState(1234);
            yield return null;
            var def = Content.Bosses.First(b => b.id == bossId);
            boss = WaveDirector.SpawnBoss(def, new Vector2(0f, 5f), wave, KiepNum.HpMul(kiep, wave), KiepNum.DmgMul(kiep, wave), wave / 5 - 1, kiep);
            yield return null;
        }

        /// <summary>Waits s seconds of gameplay time (sums Game.Dt).</summary>
        public IEnumerator Sec(float s)
        {
            float t = 0f;
            while (t < s) { yield return null; t += Game.Dt; }
        }

        /// <summary>Player cannot die or dodge and stays at the arena centre, so every hit lands and can be counted.</summary>
        public void Tank()
        {
            var g = Game.I;
            g.player.god = false;
            g.run.stats[Stat.Dodge] = 0f;
            g.run.hp = 1e6f;
            g.player.transform.position = Vector3.zero;
            if (!tankHold) { tankHold = true; StartCoroutine(HoldCentre()); }
        }

        IEnumerator HoldCentre()
        {
            while (tankHold) { Game.I.player.transform.position = Vector3.zero; Game.I.run.hp = 1e6f; yield return null; }
        }

        // ---------------------------------------------------------------- core (Task 1)
        IEnumerator T_core_sp()
        {
            yield return Arena(0, 1, 0); var r = Game.I.run;
            int[] w = { 1, 5, 10, 15, 20 }; float[] sp = { 6, 29, 102, 179, 113 };   // spec §3.1 after the 27/10 balance pass (r: 1.5 -> 0.15)
            for (int i = 0; i < 5; i++)
            {
                Check("sp_ratio_" + w[i], Mathf.Abs(r.SPAt(w[i]) / WaveDirector.ExpectedTrashHp(w[i]) - Content.SkillR(w[i])) < 0.01f, r.SPAt(w[i]).ToString("0.0"));
                Check("sp_value_" + w[i], Mathf.Abs(r.SPAt(w[i]) / sp[i] - 1f) < 0.15f, r.SPAt(w[i]).ToString("0.0") + " vs " + sp[i]);
            }
        }

        IEnumerator T_core_hp()
        {
            yield return Arena(0, 10, 0);
            var e = Enemy.Spawn(Content.Enemies[0], new Vector2(3, 3), 10, 1f, 1f);
            Check("hp_formula", Mathf.Abs(e.maxHp - Content.Enemies[0].hp * Enemy.WaveHpMul(10)) < 0.01f, e.maxHp.ToString("0.0"));
        }

        IEnumerator T_core_cd()
        {
            yield return Arena(0, 1, 0); var r = Game.I.run;
            Check("cd_base", Mathf.Approximately(r.SkillCooldown, r.ch.skillCd), r.SkillCooldown.ToString());
            r.levelStats[Stat.SkillHaste] += 100; r.Recalc();
            Check("cd_haste100", Mathf.Abs(r.SkillCooldown - r.ch.skillCd / 2f) < 0.01f, r.SkillCooldown.ToString());
            r.levelStats[Stat.SkillHaste] += 10000; r.Recalc();
            Check("cd_floor", Mathf.Approximately(r.SkillCooldown, SkillNum.MinCd), r.SkillCooldown.ToString());
        }
        // ---------------------------------------------------------------- base (Task 2)
        IEnumerator T_base_formula()
        {
            yield return Arena(0, 10, 0); var r = Game.I.run;
            r.levelStats[Stat.Crit] -= 1000; r.levelStats[Stat.SkillPower] += 100; r.Recalc();
            var e = Elite(new Vector2(4, 0));
            Random.InitState(7); float d = Skills.Hit(e, 1f, "t", Vector2.right);
            Check("formula_power", d >= r.SP * 2f * 0.92f && d <= r.SP * 2f * 1.08f, d + " / " + r.SP * 2f);
            r.levelStats[Stat.SkillPower] -= 100; r.levelStats[Stat.Damage] += 100; r.Recalc();
            Random.InitState(7); float d2 = Skills.Hit(e, 1f, "t", Vector2.right);
            Check("formula_damage_half", Mathf.Abs(d2 / d - 0.75f) < 0.001f, (d2 / d).ToString("0.000"));
            r.riteT = 5f; Random.InitState(7); float d3 = Skills.Hit(e, 1f, "t", Vector2.right);
            Check("no_weapon_mult", d3 > 0f && Mathf.Abs(d3 - d2) < 0.001f, d3 + " / " + d2);
            Check("dmg_recorded", r.skillDmg >= d + d2 + d3 - 0.01f && Dmg("t") > 0, r.skillDmg.ToString("0"));
        }

        IEnumerator T_base_boss()
        {
            yield return Arena(0, 10, 0); var r = Game.I.run; r.levelStats[Stat.Crit] -= 1000; r.Recalc();
            var el = Elite(new Vector2(4, 0)); var b = BossAt(new Vector2(-4, 0), 1);
            Random.InitState(7); float a = Skills.Hit(el, 1f, "t", Vector2.right);
            Random.InitState(7); float c = Skills.Hit(b, 1f, "t", Vector2.right);
            Check("boss_half", Mathf.Abs(c / a - SkillNum.BossMul) < 0.001f, (c / a).ToString("0.000"));
        }

        IEnumerator T_base_cast()
        {
            for (int ci = 0; ci < 6; ci++)
            {
                yield return Arena(ci, 3, 0); var r = Game.I.run; var p = Game.I.player;
                Pack(20, 1.5f, 4.5f); Elite(new Vector2(5.5f, 0)); int n0 = Enemy.All.Count;
                r.hp = r.MaxHP * 0.5f; float hp0 = r.hp; int fired = Projectile.SkillFired;
                p.SetSkillReady(); p.TryCast();
                string id = r.ch.id; int frozen = Enemy.All.FindAll(e => e.Frozen).Count + (n0 - Enemy.All.Count);   // stunned, or already dead
                if (id == "kiem_tu") Check("kiem_shots", Projectile.SkillFired - fired == 12, "");
                if (id == "dan_tu") Check("dan_heal", r.hp >= hp0 + r.MaxHP * SkillNum.DanHeal - 0.5f, r.hp.ToString("0"));
                if (id == "the_tu") Check("the_invuln", p.InvulnLeft >= 1.9f, p.InvulnLeft.ToString("0.00"));
                if (id == "phu_tu" || id == "loi_tu") Check(id + "_freeze", frozen >= 3, frozen + "");
                if (id == "ma_tu") Check("ma_rite", r.riteT > 4.9f && r.hp < hp0, r.riteT.ToString("0.0"));
                Check(id + "_cd_spent", p.SkillCdLeft > 1f, p.SkillCdLeft.ToString("0.0"));
                yield return new WaitForSeconds(3f);
                string src = id == "kiem_tu" ? "kiem" : id == "dan_tu" ? "dan_nova" : id == "the_tu" ? "the_wave" : id == "phu_tu" ? "phu" : id == "loi_tu" ? "loi" : null;
                if (src != null) Check(id + "_dmg", Dmg(src) > 0, Dmg(src).ToString("0"));
            }
        }

        IEnumerator T_base_empty()   // Review Focus 1
        {
            for (int ci = 0; ci < 6; ci++) for (int st = 0; st < 4; st++)
            {
                yield return Arena(ci, 18, st); var p = Game.I.player; int e0 = AutoTest.Errors;
                p.SetSkillReady(); bool cast = p.TryCast(); yield return new WaitForSeconds(2.5f);
                Check("empty_" + ci + "_" + st, cast && p.SkillCdLeft > 0f && AutoTest.Errors == e0, AutoTest.Errors - e0 + " errors");
            }
        }
        // ---------------------------------------------------------------- fx (Task 3)
        IEnumerator T_fx_all()
        {
            yield return Arena(0, 1, 0); int e0 = AutoTest.Errors, n0 = Fx.ActiveCount; Vector2 p = Game.I.player.Pos;
            Fx.Sigil(p, Color.cyan, 2f, 1f); Fx.Apparition(Res.Tile(Content.Chars[0].sprite), p, Color.cyan, 6f, 0.9f);
            Fx.Pillar(p, Color.white, 8f, 0.6f); Fx.ScreenTint(Color.red, 0.5f, 0.3f);
            Check("fx_spawned", Fx.ActiveCount >= n0 + 3, Fx.ActiveCount + "");
            Check("tint_clamped", Game.I.ui.TintAlpha <= 0.1201f, Game.I.ui.TintAlpha.ToString("0.000"));
            for (int s = 0; s < 4; s++) SkillFx.Ladder(s, p, Color.cyan);
            foreach (var c in new[] { "sk_chime", "sk_boom", "sk_whoosh", "sk_choir" }) Check("sfx_" + c, Res.Clip(c) != null, "");
            yield return new WaitForSeconds(0.25f); Game.I.bot.Shot("fx_ladder");
            Check("fx_no_errors", AutoTest.Errors == e0, "");
        }
        // ---------------------------------------------------------------- Kiếm / Đan II-IV (Task 4)
        IEnumerator T_kd_kiem()
        {
            yield return Arena(0, 8, 1); Pack(20, 1.5f, 4.5f); Game.I.player.SetSkillReady(); Game.I.player.TryCast();
            yield return new WaitForSeconds(3f); Check("kiem2_return", Dmg("kiem_return") > 0, "");
            yield return Arena(0, 13, 2); Pack(20, 1.5f, 4.5f); BossAt(Game.I.player.Pos + new Vector2(2.5f, 0), 1);   // the boss outlives the swords' first pass so the orbit has a target
            int f0 = Projectile.SkillFired; Game.I.player.SetSkillReady(); Game.I.player.TryCast();
            Check("kiem3_16", Projectile.SkillFired - f0 == 16, "");
            for (float t = 0f; t < 7f && Dmg("kiem_orbit") <= 0f; t += 0.25f) yield return new WaitForSeconds(0.25f);
            Check("kiem3_orbit", Dmg("kiem_orbit") > 0, "kiem " + Dmg("kiem").ToString("0") + " return " + Dmg("kiem_return").ToString("0") + " orbit " + Dmg("kiem_orbit").ToString("0") + " enemies " + Enemy.All.Count + " skillProj " + Projectile.CountSkill());
            yield return Arena(0, 18, 3); Pack(20, 1.5f, 4.5f); Game.I.player.SetSkillReady(); Game.I.player.TryCast(); yield return new WaitForSeconds(3f);
            Check("kiem4_giant", Dmg("kiem_giant") > 0 && Dmg("kiem_return") > 0, "keeps stage II");
        }

        IEnumerator T_kd_dan()
        {
            yield return Arena(1, 8, 1); Pack(12, 5f, 8f); Game.I.player.SetSkillReady(); Game.I.player.TryCast(); yield return new WaitForSeconds(2f);
            Check("dan2_pills", Dmg("dan_pill") > 0, "");
            yield return Arena(1, 13, 2); var pp = Game.I.player.Pos;
            for (int i = 0; i < 10; i++) Projectile.FireHostile(pp + Random.insideUnitCircle * 3f, Vector2.zero, 1f, Color.red);
            Game.I.player.SetSkillReady(); Game.I.player.TryCast();
            Check("dan3_bullets", Projectile.CountHostile() == 0, Projectile.CountHostile() + "");
            Check("dan3_guard", Mathf.Abs(Skills.ModifyIncoming(10f) - 7f) < 0.01f, "");
            yield return new WaitForSeconds(3.2f); Check("dan3_guard_ends", Mathf.Abs(Skills.ModifyIncoming(10f) - 10f) < 0.01f, "");
            yield return Arena(1, 18, 3); Pack(20, 1.5f, 4.5f); Game.I.player.SetSkillReady(); Game.I.player.TryCast(); yield return new WaitForSeconds(2f);
            Check("dan4_followup", Dmg("dan_followup") > 0, "");
        }

        IEnumerator T_kd_cleared()   // Review Focus 2
        {
            yield return Arena(1, 18, 3); Pack(20, 1.5f, 4.5f); var r = Game.I.run; int e0 = AutoTest.Errors;
            Game.I.player.SetSkillReady(); Game.I.player.TryCast(); Check("runtime_has_pending", SkillRuntime.I != null && SkillRuntime.I.PendingCount > 0, "");
            Game.I.EndWave(); float d0 = r.skillDmg;
            yield return new WaitForSeconds(1.5f);
            Check("runtime_cleared", r.skillDmg == d0 && AutoTest.Errors == e0, (r.skillDmg - d0).ToString("0"));
        }

        IEnumerator T_kd_paused()   // Review Focus 3
        {
            yield return Arena(0, 13, 2); Pack(20, 1.5f, 4.5f); var r = Game.I.run;
            Game.I.player.SetSkillReady(); Game.I.player.TryCast(); yield return new WaitForSeconds(0.3f);
            Check("paused_has_pending", Projectile.CountSkill() > 0, "");
            Game.I.SetPause(true); float d0 = r.skillDmg; yield return new WaitForSecondsRealtime(2f);
            Check("runtime_paused", r.skillDmg == d0, (r.skillDmg - d0).ToString("0")); Game.I.SetPause(false);
        }

        // ---------------------------------------------------------------- Thể / Phù II-IV (Task 5)
        IEnumerator T_tp_the()
        {
            yield return Arena(2, 8, 1); Pack(16, 1.5f, 3.6f); Game.I.player.SetSkillReady(); Game.I.player.TryCast();
            Check("the2_stun", Enemy.All.FindAll(e => e.Frozen).Count >= 3, "");
            yield return Arena(2, 13, 2); Pack(16, 4.2f, 5.8f); Game.I.player.SetSkillReady(); Game.I.player.TryCast(); yield return new WaitForSeconds(1f);
            Check("the3_fissure", Dmg("the_fissure") > 0, "");
            yield return Arena(2, 18, 3); Pack(16, 1.5f, 4.5f); Game.I.player.SetSkillReady(); Game.I.player.TryCast();
            Check("the4_invuln", Game.I.player.InvulnLeft >= 2.9f, ""); yield return new WaitForSeconds(3.3f);
            Check("the4_after", Dmg("the_after") > 0, "");
        }

        IEnumerator T_tp_phu()
        {
            yield return Arena(3, 3, 0); Pack(10, 1.5f, 4f); var w = Elite(new Vector2(9, 0)); w.Freeze(1f);
            Game.I.player.SetSkillReady(); Game.I.player.TryCast(); var f = Enemy.All.Find(e => e.SkillFrozen);
            Check("phu_flag", f != null && Mathf.Approximately(f.FrozenBonus, 1.3f) && !w.SkillFrozen, "");
            yield return Arena(3, 8, 1); Pack(12, 7f, 11f); Game.I.player.SetSkillReady(); Game.I.player.TryCast(); yield return new WaitForSeconds(1.5f);
            Check("phu2_flyer", Dmg("phu_flyer") > 0, "");
            yield return Arena(3, 13, 2); Pack(16, 1.5f, 4f); Game.I.player.SetSkillReady(); Game.I.player.TryCast(); yield return new WaitForSeconds(3f);
            Check("phu3_burst", Dmg("phu_burst") > 0, "");
            yield return Arena(3, 18, 3); var far = Elite(new Vector2(7.5f, 0)); Game.I.player.SetSkillReady(); Game.I.player.TryCast();
            Check("phu4_wide", far.SkillFrozen && Mathf.Approximately(far.FrozenBonus, 1.5f), "");
        }

        IEnumerator T_tp_chain()   // Review Focus 4
        {
            yield return Arena(3, 13, 2); var r = Game.I.run; r.levelStats[Stat.Crit] -= 1000; r.Recalc();
            r.items.Add(Content.It("hoa_long_chau"));
            foreach (var e in Pack(30, 1f, 3f)) e.hp = r.SP * SkillNum.PhuHit * 1.6f;   // survives the first hit (frozen +30%), not the bursts
            int e0 = AutoTest.Errors; Game.I.player.SetSkillReady(); Game.I.player.TryCast(); yield return new WaitForSeconds(4f);
            Check("phu_chain", Dmg("phu_burst") > 0 && AutoTest.Errors == e0 && Enemy.All.Count < 30, Enemy.All.Count + " left");
        }

        // ---------------------------------------------------------------- Ma / Lôi II-IV (Task 6)
        IEnumerator T_ml_ma()
        {
            yield return Arena(4, 8, 1); Pack(12, 1.5f, 2.8f); Game.I.player.SetSkillReady(); Game.I.player.TryCast(); yield return null;
            Check("ma2_nova", Dmg("ma_nova") > 0, "");
            yield return Arena(4, 13, 2); Pack(12, 1.5f, 4f); var r = Game.I.run; Game.I.player.SetSkillReady(); Game.I.player.TryCast();
            Check("ma3_guard", Mathf.Abs(Skills.ModifyIncoming(10f) - 6f) < 0.01f, "");
            var far = Pack(6, 6.5f, 8f); float t0 = r.riteT; for (int i = 0; i < 5; i++) far[i].Damage(far[i].hp + 1f, false, Vector2.zero, 0f, null);   // outside the nova
            Check("ma3_extend", r.riteT >= t0 + 0.99f, (r.riteT - t0).ToString("0.00"));
            yield return Arena(4, 18, 3); Elite(new Vector2(4.2f, 0)); Game.I.player.SetSkillReady(); Game.I.player.TryCast(); yield return null;
            Check("ma4_nova", Dmg("ma_nova") > 0 && Mathf.Approximately(Skills.RiteLifesteal, 40f), "");
        }

        IEnumerator T_ml_loi()
        {
            yield return Arena(5, 8, 1); Pack(16, 1.5f, 5f); Game.I.player.SetSkillReady(); Game.I.player.TryCast(); yield return new WaitForSeconds(0.5f);
            Check("loi2_chain", Dmg("loi_chain") > 0, "");
            yield return Arena(5, 13, 2); Pack(16, 0.8f, 2.3f); Game.I.player.SetSkillReady(); Game.I.player.TryCast(); yield return new WaitForSeconds(2f);
            Check("loi3_field", Dmg("loi_field") > 0, "");
            yield return Arena(5, 18, 3); Pack(16, 1.5f, 6f); var el = Elite(new Vector2(6, 0)); Game.I.player.SetSkillReady(); Game.I.player.TryCast();
            Check("loi4_12", Skills.LastStrikes == 12, Skills.LastStrikes + ""); yield return new WaitForSeconds(1f);
            Check("loi4_big", Dmg("loi_big") > 0 && Skills.LastBigTarget == el, "");
        }

        IEnumerator T_ml_paused()   // Review Focus 3
        {
            yield return Arena(5, 13, 2); Pack(16, 0.8f, 2.3f); var r = Game.I.run; Game.I.player.SetSkillReady(); Game.I.player.TryCast();
            yield return new WaitForSeconds(0.5f); Check("field_has_pending", SkillRuntime.I.PendingCount > 0, "");
            Game.I.SetPause(true); float d0 = r.skillDmg;
            yield return new WaitForSecondsRealtime(2f);
            Check("field_paused", r.skillDmg == d0, ""); Game.I.SetPause(false);
        }

        // ---------------------------------------------------------------- signature cards: Kiếm / Đan / Thể (Task 9)
        IEnumerator T_sig1_kiem()
        {
            float per0 = 0f;
            for (int pass = 0; pass < 2; pass++)
            {   // pass 0 without Bách Kiếm Triều, pass 1 with it: damage per sword hit must drop to 0.7
                yield return pass == 0 ? Arena(0, 10, 0) : Arena(0, 10, 0, "bach_kiem");
                var r = Game.I.run; r.levelStats[Stat.Crit] -= 1000; r.Recalc();
                var el = Elite(new Vector2(5, 0)); el.hp = 1e9f; el.maxHp = 1e9f;
                int f0 = Projectile.SkillFired; Game.I.player.SetSkillReady(); Game.I.player.TryCast();
                if (pass == 1) Check("bach_20", Projectile.SkillFired - f0 == 20, (Projectile.SkillFired - f0) + "");
                yield return new WaitForSeconds(2.5f);
                float per = Dmg("kiem") / Mathf.Max(1, el.skillHits);
                if (pass == 0) per0 = per; else Check("bach_07", Mathf.Abs(per / per0 - 0.7f) < 0.06f, (per / per0).ToString("0.00"));
            }
            yield return Arena(0, 10, 0, "kiem_tam"); var r2 = Game.I.run;
            var big = Elite(new Vector2(6, 0)); big.hp = 1e9f; big.maxHp = 1e9f; Pack(20, 2f, 4f);
            Game.I.player.SetSkillReady(); Game.I.player.TryCast(); yield return new WaitForSeconds(2.5f);
            Check("kiem_tam", big.skillHits >= 6, big.skillHits + "");
            yield return Arena(0, 10, 0, "kiem_khi"); r2 = Game.I.run; r2.kiemKhi = 11;
            Skills.Hit(Elite(new Vector2(3, 0)), 0.01f, "kiem", Vector2.right);
            Check("kiem_khi_12", r2.kiemKhi == 12, r2.kiemKhi + "");
            Check("kiem_khi_block", Skills.ModifyIncoming(10f) == 0f && r2.kiemKhi == 0, r2.kiemKhi + "");
        }

        IEnumerator T_sig1_dan()
        {
            yield return Arena(1, 10, 0, "cuu_chuyen"); var r = Game.I.run; var p = Game.I.player;
            r.hp = r.MaxHP * 0.5f; float hp0 = r.hp; Pack(10, 1.5f, 3f); p.SetSkillReady(); p.TryCast();
            Check("cuu_chuyen", r.hp >= hp0 + 0.45f * r.MaxHP - 0.5f, r.hp + " / " + (hp0 + 0.45f * r.MaxHP));
            yield return Arena(1, 10, 0, "bao_viem"); r = Game.I.run; p = Game.I.player;
            Pack(10, 2f, 3f); var far = Pack(1, 4.8f, 4.8f)[0]; p.SetSkillReady(); p.TryCast();
            Check("bao_viem_radius", far.skillHits >= 1, far.skillHits + "");
            r.levelStats[Stat.Crit] -= 1000; r.Recalc();
            var a = Elite(new Vector2(8, 3)); var b = Elite(new Vector2(-8, 3)); a.ApplyBurn(1f);
            Random.InitState(7); float da = Skills.Hit(a, 1f, "t", Vector2.right); Random.InitState(7); float db = Skills.Hit(b, 1f, "t", Vector2.right);
            Check("bao_viem_burning", Mathf.Abs(da / db - 1.5f) < 0.01f, (da / db).ToString("0.000"));
            yield return Arena(1, 10, 0, "ho_menh"); r = Game.I.run; p = Game.I.player; p.god = false;
            yield return new WaitForSeconds(1.2f);   // wave-start grace
            r.hp = r.MaxHP * 0.35f; p.SetSkillReady(); int c0 = Skills.CastCount; p.Hurt(0.1f * r.MaxHP, p.Pos + Vector2.right);
            Check("ho_menh_cast", Skills.CastCount == c0 + 1 && r.hoMenhUsed, (Skills.CastCount - c0) + "");
            yield return new WaitForSeconds(0.6f); p.SetSkillReady(); c0 = Skills.CastCount; p.Hurt(0.02f * r.MaxHP, p.Pos + Vector2.right);
            Check("ho_menh_once", Skills.CastCount == c0, (Skills.CastCount - c0) + "");
        }

        IEnumerator T_sig1_the()
        {
            yield return Arena(2, 10, 0, "bat_dong"); var p = Game.I.player;
            p.SetSkillReady(); p.TryCast(); yield return new WaitForSeconds(0.4f);   // the wave has thrown the first crowd out; a second one steps in while the hero is still invulnerable
            Pack(8, 0.5f, 0.9f); yield return new WaitForSeconds(0.6f);
            Check("bat_dong", Dmg("the_thorns") > 0f, Dmg("the_thorns").ToString("0"));
            yield return Arena(2, 10, 0, "thiet_bo_sam"); p = Game.I.player; p.SetSkillReady(); p.TryCast();
            Check("armor_on", Skills.TempArmor == 5f, Skills.TempArmor + "");
            yield return new WaitForSeconds(5.1f); Check("armor_off", Skills.TempArmor == 0f, Skills.TempArmor + "");
            yield return Arena(2, 10, 0, "son_bang"); p = Game.I.player; var pk = Pack(8, 4.5f, 4.5f); foreach (var e in pk) { e.hp = 1e9f; e.maxHp = 1e9f; }   // survive the wave so the pull can be measured
            p.SetSkillReady(); p.TryCast(); yield return new WaitForSeconds(0.3f);
            bool ok = true; string dbg = "";
            foreach (var e in pk) { bool good = e != null && !e.dead && Vector2.Distance(e.Pos, p.Pos) < 3.2f && e.skillHits >= 1; ok &= good; dbg += e == null || e.dead ? "x " : Vector2.Distance(e.Pos, p.Pos).ToString("0.0") + "/" + e.skillHits + " "; }
            Check("son_bang", ok, dbg);
        }

        // ---------------------------------------------------------------- signature cards: Phù / Ma / Lôi (Task 10)
        void Tough(IEnumerable<Enemy> es) { foreach (var e in es) { e.hp = 1e9f; e.maxHp = 1e9f; } }

        IEnumerator T_sig2_phu()
        {
            yield return Arena(3, 10, 0, "han_bang"); var p = Game.I.player;
            var pk = Pack(3, 2f, 3f); Tough(pk); p.SetSkillReady(); p.TryCast(); yield return new WaitForSeconds(3.5f);
            Check("han_bang", pk[0].Frozen, "");
            yield return Arena(3, 10, 0, "ho_than"); var r = Game.I.run; p = Game.I.player; p.god = false;
            yield return new WaitForSeconds(1.2f);   // wave-start grace
            p.SetSkillReady(); p.TryCast(); float hp0 = r.hp;
            for (int i = 0; i < 3; i++) { float a = i * 2.0944f; Projectile.FireHostile(p.Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3f, new Vector2(-Mathf.Cos(a), -Mathf.Sin(a)) * 6f, 5f, Color.red); }
            yield return new WaitForSeconds(1.5f);
            Check("ho_than", Mathf.Approximately(r.hp, hp0) && Projectile.CountHostile() == 0 && Skills.Talismans == 0, r.hp + "/" + hp0 + " hostile " + Projectile.CountHostile() + " talismans " + Skills.Talismans);
            yield return Arena(3, 10, 0, "truy_hon"); p = Game.I.player;
            var two = Pack(2, 2f, 3f); Tough(two); two[0].SkillFreeze(3f, 1.3f); two[0].hp = 1f; two[0].Damage(10f, false, Vector2.zero, 0f, null);
            yield return new WaitForSeconds(1f);
            Check("truy_hon", Dmg("phu_truyhon") > 0f && two[1].SkillFrozen, Dmg("phu_truyhon").ToString("0") + " " + two[1].SkillFrozen);
        }

        IEnumerator T_sig2_ma()
        {
            yield return Arena(4, 10, 0, "huyet_hai"); var r = Game.I.run; var p = Game.I.player;
            float before = r.hp; p.SetSkillReady(); p.TryCast();
            Check("huyet_hai", Mathf.Abs(r.riteT - 8f) < 0.05f && Mathf.Abs((before - r.hp) - before * 0.25f) <= 1f, r.riteT + " hp " + before + "->" + r.hp);
            yield return Arena(4, 10, 0, "thi_huyet"); r = Game.I.run; p = Game.I.player;
            r.hp = r.MaxHP * 0.5f; p.SetSkillReady(); p.TryCast(); float h0 = r.hp;
            foreach (var e in Pack(3, 2f, 3f)) e.Damage(e.hp + 1f, false, Vector2.zero, 0f, null);
            Check("thi_huyet", Mathf.Abs(r.hp - (h0 + 0.03f * r.MaxHP)) <= 0.5f, r.hp + " vs " + (h0 + 0.03f * r.MaxHP));
            yield return Arena(4, 10, 0, "huyet_anh"); p = Game.I.player;
            var el = Elite(new Vector2(3, 0)); el.hp = 1e9f; el.maxHp = 1e9f; p.SetSkillReady(); p.TryCast(); yield return new WaitForSeconds(2.2f);
            Check("huyet_anh", Dmg("ma_shadow") > 0f, Dmg("ma_shadow").ToString("0"));
        }

        IEnumerator T_sig2_loi()
        {
            yield return Arena(5, 10, 0, "loi_dinh"); var p = Game.I.player;
            var pk = Pack(10, 2f, 6f); Tough(pk); p.SetSkillReady(); p.TryCast(); int strikes = Skills.LastStrikes;
            yield return new WaitForSeconds(1.9f);
            Check("loi_dinh", strikes == 5 && pk.Exists(e => !e.dead && e.Frozen), strikes + " strikes");
            yield return Arena(5, 10, 0, "loi_linh"); p = Game.I.player;
            p.SetSkillReady(); p.TryCast(); yield return new WaitForSeconds(0.4f);   // the bolts have thrown nothing out of range; a crowd then steps into the ring
            var ring = Pack(6, 0.8f, 1.4f); Tough(ring);
            for (int i = 0; i < 30; i++)
            {   // enemies shove each other apart, so hold the crowd inside the ring for 1.5 s
                foreach (var e in ring) { Vector2 rel = e.Pos - p.Pos; e.transform.position = p.Pos + rel.normalized * Mathf.Min(rel.magnitude, 1.2f); }
                yield return new WaitForSeconds(0.05f);
            }
            Check("loi_linh", Dmg("loi_aura") > 0f, Dmg("loi_aura").ToString("0"));
            yield return Arena(5, 10, 0, "tich_loi"); p = Game.I.player; p.SetSkillReady(); p.TryCast(); float cd0 = p.SkillCdLeft;
            for (int i = 0; i < 3; i++) Skills.OnWeaponCrit();
            Check("tich_loi_3", Mathf.Abs(cd0 - p.SkillCdLeft - 0.9f) < 0.02f, (cd0 - p.SkillCdLeft).ToString("0.00"));
            for (int i = 0; i < 20; i++) Skills.OnWeaponCrit();
            Check("tich_loi_cap", Mathf.Abs(cd0 - p.SkillCdLeft - 4f) < 0.02f, (cd0 - p.SkillCdLeft).ToString("0.00"));
        }

        // ---------------------------------------------------------------- skill items and shop bias (Task 11)
        void GiveItem(string id) { var r = Game.I.run; r.items.Add(Content.Items.Find(x => x.id == id)); r.Recalc(); }

        IEnumerator T_item_stats()
        {
            yield return Arena(0, 5, 0); var r = Game.I.run; r.Recalc();
            var want = new[] { ("ngung_than_huong", Stat.SkillPower, 15f), ("thanh_tam_phu", Stat.SkillHaste, 5f), ("than_thong_ngoc_gian", Stat.SkillPower, 30f) };
            foreach (var (id, st, v) in want)
            {
                float b = r.stats[st]; GiveItem(id);
                Check("stat_" + id, Mathf.Abs(r.stats[st] - b - v) < 0.001f, (r.stats[st] - b).ToString("0.0"));
            }
            float aw = r.stats[Stat.AtkSpeed]; r.items.RemoveAll(x => x.id == "than_thong_ngoc_gian"); r.Recalc();
            Check("ngoc_gian_atk", Mathf.Abs(r.stats[Stat.AtkSpeed] - aw - 5f) < 0.001f, "");
        }

        IEnumerator T_item_bo_doan()
        {
            yield return Arena(0, 5, 0); var r = Game.I.run; var p = Game.I.player; GiveItem("tinh_toa_bo_doan");
            r.hp = r.MaxHP * 0.5f; float h0 = r.hp; p.SetSkillReady(); p.TryCast();
            Check("bo_doan", Mathf.Abs(r.hp - (h0 + 0.03f * r.MaxHP)) <= 0.5f, r.hp + " vs " + (h0 + 0.03f * r.MaxHP));
        }

        IEnumerator T_item_bao_thap()
        {
            yield return Arena(0, 10, 0); var r = Game.I.run; var p = Game.I.player; GiveItem("linh_lung_bao_thap");
            r.levelStats[Stat.Crit] -= 1000; r.Recalc(); var e = Elite(new Vector2(8, 3));
            Random.InitState(7); float d0 = Skills.Hit(e, 1f, "t", Vector2.right);
            for (int i = 0; i < 5; i++) { p.SetSkillReady(); p.TryCast(); }
            float b = 1f + r.stats[Stat.SkillPower] / 100f + r.stats[Stat.Damage] / 200f;
            Random.InitState(7); float d5 = Skills.Hit(e, 1f, "t", Vector2.right);
            Check("bao_thap_5", Mathf.Abs(d5 / d0 - (b + 0.20f) / b) < 0.005f, (d5 / d0).ToString("0.000") + " vs " + ((b + 0.2f) / b).ToString("0.000"));
            for (int i = 0; i < 7; i++) { p.SetSkillReady(); p.TryCast(); }
            Random.InitState(7); float d12 = Skills.Hit(e, 1f, "t", Vector2.right);
            Check("bao_thap_cap", Mathf.Abs(d12 / d0 - (b + 0.40f) / b) < 0.005f && r.linhLung == 10, (d12 / d0).ToString("0.000") + " stack " + r.linhLung);
            Skills.OnWaveStart(r); Check("bao_thap_reset", r.linhLung == 0, "");
        }

        IEnumerator T_item_thien_thu()
        {
            yield return Arena(0, 10, 0); var r = Game.I.run; r.levelStats[Stat.Crit] -= 1000; r.Recalc();
            var w = r.AddWeapon(Content.W("thiet_kiem"), 0); var e = Elite(new Vector2(8, 3));
            Random.InitState(7); float s0 = Skills.Hit(e, 1f, "t", Vector2.right); Random.InitState(7); float w0 = Combat.Roll(w, out _);
            GiveItem("thien_thu_vo_tu");
            Random.InitState(7); float s1 = Skills.Hit(e, 1f, "t", Vector2.right); Random.InitState(7); float w1 = Combat.Roll(w, out _);
            Check("thien_thu_skill", Mathf.Abs(s1 / s0 - 1.5f) < 0.01f, (s1 / s0).ToString("0.000"));
            Check("thien_thu_weapon", Mathf.Abs(w1 / w0 - 0.85f) < 0.01f, (w1 / w0).ToString("0.000"));
        }

        IEnumerator T_item_shop_bias()
        {
            yield return Arena(0, 8, 0); var r = Game.I.run; r.skillCards.Clear(); r.Recalc();
            float plain = SkillItemShare(r, 3000);
            r.skillCards.Add("tu_khi"); float biased = SkillItemShare(r, 3000);
            Check("shop_bias", biased - plain >= 0.08f, plain.ToString("0.000") + " -> " + biased.ToString("0.000"));
        }

        float SkillItemShare(RunState r, int rolls)
        {
            int items = 0, skill = 0;
            for (int i = 0; i < rolls; i++)
            {
                r.RollShop();
                foreach (var sl in r.shop) if (sl != null && sl.it != null) { items++; if (System.Array.IndexOf(Content.SkillItemIds, sl.it.id) >= 0) skill++; }
                foreach (var sl in r.shop) if (sl != null) sl.sold = true;   // force a fresh roll next time
            }
            return items == 0 ? 0f : skill / (float)items;
        }

        // ---------------------------------------------------------------- UI (Task 12)
        int UiCount(string s)
        {
            string tr = Loc.Tr(s); int n = 0;
            foreach (var t in Object.FindObjectsOfType<UnityEngine.UI.Text>())
                if (t.isActiveAndEnabled && (t.text.Contains(s) || t.text.Contains(tr))) n++;
            return n;
        }

        IEnumerator T_ui_ngodao()
        {
            yield return Arena(0, 8, 1); var r = Game.I.run; r.level = 10; r.pendingLevels = 1; r.RollLevelOpts(); Game.I.ui.ShowLevelUp();
            yield return new WaitForSecondsRealtime(0.4f);
            Check("ui_ngodao_cards", UiCount("Thần Thông · ") >= 2, UiCount("Thần Thông · ") + " card labels");
            Check("ui_ngodao_title", UiCount("NGỘ ĐẠO") >= 1, "");
            Check("ui_ngodao_uyap", UiCount("Uy Áp") >= 1, "");
            Game.I.bot.Shot("ui_ngodao"); yield return new WaitForSecondsRealtime(0.6f);
        }

        IEnumerator T_ui_relic()
        {
            yield return Arena(0, 5, 1); var r = Game.I.run; r.stageUpWave = 5; r.chests = 1; r.relicChoices = r.RollRelics(); Game.I.ui.ShowRelics();
            yield return new WaitForSecondsRealtime(0.4f);
            Check("ui_relic_line", UiCount("TIẾN GIAI") >= 1 && UiCount(Skills.FormName(r.ch, 1)) >= 1, "");
            Game.I.bot.Shot("ui_relic"); yield return new WaitForSecondsRealtime(0.6f);
        }

        IEnumerator T_ui_hud()
        {
            yield return Arena(0, 5, 1); var r = Game.I.run;
            yield return new WaitForSecondsRealtime(0.3f);
            Check("ui_hud", UiCount(Skills.FormName(r.ch, 1)) >= 1 && UiCount("Giai II") >= 1, "");
            Game.I.bot.Shot("ui_hud"); yield return new WaitForSecondsRealtime(0.6f);
        }

        IEnumerator T_ui_synergy()
        {
            yield return Arena(0, 8, 1, "bach_kiem"); Game.I.run.RollShop(); Game.I.ui.ShowShop();
            yield return new WaitForSecondsRealtime(0.4f);
            Check("ui_synergy", UiCount("THẦN THÔNG") >= 1 && UiCount(Content.SkillCard("bach_kiem").name) >= 1, "");
            Game.I.bot.Shot("ui_synergy"); yield return new WaitForSecondsRealtime(0.6f);
        }

        // ---------------------------------------------------------------- item economy (balance pass)
        IEnumerator T_eco_chest()
        {
            yield return Arena(0, 15, 0); var r = Game.I.run;
            r.activeRoute = 0; r.eliteChests = 0; bool ok = Mathf.Approximately(r.EliteChestChance, 0.3f);
            r.eliteChests = 2; ok &= Mathf.Approximately(r.EliteChestChance, 0.1f);
            r.activeRoute = 2; r.eliteChests = 0; ok &= Mathf.Approximately(r.EliteChestChance, 0.6f);
            Check("chest_chance", ok, "");
            int before = Orb.All.FindAll(o => o != null && o.IsChest).Count;
            for (int i = 0; i < 40; i++) { var e = Elite(new Vector2(6 + (i % 5), 3 + i / 5)); e.Damage(e.hp + 1f, false, Vector2.zero, 0f, null); }
            yield return null;
            int dropped = Orb.All.FindAll(o => o != null && o.IsChest).Count - before;
            Check("chest_falloff", dropped >= 1 && dropped <= 10 && dropped == r.eliteChests, dropped + " chests from 40 elites on Tinh Anh Tu Tap");
            Game.I.BeginWave(r.wave); Game.I.dir.enabled = false; Game.I.dir.EndWave(); Check("chest_reset", r.eliteChests == 0, "");
        }

        IEnumerator T_eco_cap()
        {
            yield return Arena(0, 8, 0); var r = Game.I.run;
            var common = Content.Items.Find(i => i.tier == 0 && i.max == 0);
            var tien = Content.Items.Find(i => i.tier == 3 && i.max == 0);
            Check("cap_values", common.Cap == 6 && Content.Items.TrueForAll(i => i.max > 0 || i.Cap == Content.StackCap[i.tier]) && Content.StackCap[1] == 4
                && Content.StackCap[2] == 3 && Content.StackCap[3] == 2 && (tien == null || tien.Cap == 2), "");
            for (int i = 0; i < 6; i++) r.items.Add(common);
            r.Recalc();
            bool seen = false;
            for (int k = 0; k < 300; k++)
            {
                r.RollShop(); foreach (var sl in r.shop) { if (sl != null && sl.it == common) seen = true; if (sl != null) sl.sold = true; }
                if (r.RollChestItem() == common) seen = true;
            }
            Check("cap_offer", !seen, "");
            r.shop[0].it = common; r.shop[0].w = null; r.shop[0].sold = false; r.shop[0].price = 1; r.gold = 999;
            Check("cap_buy", r.CantBuy(0) != null, r.CantBuy(0) ?? "buyable");
        }

        // ---------------------------------------------------------------- Ngộ Đạo (Task 8)
        IEnumerator T_nd_roll()
        {
            yield return Arena(0, 8, 1); var r = Game.I.run; r.level = 10; r.pendingLevels = 1; r.RollLevelOpts();
            var o = r.levelOpts; int stats = System.Array.FindAll(o, x => x.card == null).Length;
            Check("nd_shape", o.Length == 4 && stats == 2 && System.Array.Exists(o, x => x.card != null && x.card.charId == "kiem_tu")
                && System.Array.Exists(o, x => x.card != null && x.card.charId == null), stats + " stat cards");
            var sig = System.Array.Find(o, x => x.card != null && x.card.charId != null);
            r.ChooseLevel(System.Array.IndexOf(o, sig)); bool again = false;
            for (int i = 0; i < 50; i++) { r.pendingLevels = 1; r.RollLevelOpts(); again |= System.Array.Exists(r.levelOpts, x => x.card == sig.card); }
            Check("nd_norepeat", !again, "");
            r.skillCards.AddRange(new[] { "bach_kiem", "kiem_tam", "kiem_khi" }); r.pendingLevels = 1; r.RollLevelOpts();
            Check("nd_fallback", System.Array.FindAll(r.levelOpts, x => x.card != null && x.card.charId == null).Length == 2, "");
        }

        IEnumerator T_nd_exhausted()   // Review Focus 5
        {
            yield return Arena(0, 18, 3); var r = Game.I.run; foreach (var c in Content.SkillCards) r.skillCards.Add(c.id);
            r.level = 30; r.pendingLevels = 1; r.RollLevelOpts();
            Check("nd_exhausted", r.levelOpts.Length == 4 && System.Array.TrueForAll(r.levelOpts, x => x.card == null && x.mods.Length > 0), "");
        }

        IEnumerator T_nd_gate()
        {
            yield return Arena(0, 8, 1); var r = Game.I.run; r.level = 10; bool seen = false;
            for (int i = 0; i < 100; i++) { r.pendingLevels = 1; r.RollLevelOpts(); seen |= System.Array.Exists(r.levelOpts, x => x.card != null && x.card.id == "hoa_an"); }
            Check("nd_gate_closed", !seen, "");
            r.AddWeapon(Content.W("hoa_truong"), 0); r.AddWeapon(Content.W("hoa_truong"), 0); seen = false;
            for (int i = 0; i < 100; i++) { r.pendingLevels = 1; r.RollLevelOpts(); seen |= System.Array.Exists(r.levelOpts, x => x.card != null && x.card.id == "hoa_an"); }
            Check("nd_gate_open", seen, "");
            int sp = 0, dm = 0; r.level = 5;
            for (int i = 0; i < 4000; i++) { r.pendingLevels = 1; r.RollLevelOpts(); foreach (var x in r.levelOpts) { if (x.mods[0].s == Stat.SkillPower) sp++; if (x.mods[0].s == Stat.Damage) dm++; } }
            Check("level_weights", sp > dm * 0.4f && sp < dm * 0.6f, sp + "/" + dm);
        }

        IEnumerator T_nd_shared()
        {
            yield return Arena(0, 3, 0, "song_thi"); Pack(20, 1.5f, 4.5f); int f0 = Projectile.SkillFired; Game.I.player.SetSkillReady(); Game.I.player.TryCast();
            yield return new WaitForSeconds(0.8f); Check("song_thi", Projectile.SkillFired - f0 == 24, (Projectile.SkillFired - f0) + "");
            yield return Arena(0, 3, 0, "tu_khi"); var pk = Pack(6, 2f, 3f); var p = Game.I.player; p.SetSkillReady(); p.TryCast(); float cd = p.SkillCdLeft;
            for (int i = 0; i < 4; i++) pk[i].Damage(pk[i].hp + 1f, false, Vector2.zero, 0f, null);
            Check("tu_khi", Mathf.Abs(cd - p.SkillCdLeft - 0.6f) < 0.02f, (cd - p.SkillCdLeft).ToString("0.00"));
            yield return Arena(0, 3, 0, "du_ba"); var r = Game.I.run; var w = r.AddWeapon(Content.W("thiet_kiem"), 0);
            float c0 = w.Cooldown(r.stats); Game.I.player.SetSkillReady(); Game.I.player.TryCast();
            float a0 = r.stats[Stat.AtkSpeed];
            Check("du_ba", Mathf.Abs(w.Cooldown(r.stats) - c0 * (1f + a0 / 100f) / (1f + (a0 + 35f) / 100f)) < 0.01f, "");
            yield return Arena(0, 10, 0, "than_nhan"); r = Game.I.run; r.levelStats[Stat.Crit] += 85; r.Recalc(); bool all = true;
            foreach (var e in Pack(20, 2f, 4f)) { Skills.Hit(e, 0.01f, "t", Vector2.right); all &= Skills.LastHitCrit; }
            Check("than_nhan", all, "");
            yield return Arena(0, 10, 0, "hoa_an", "loi_an", "doc_an"); var four = Pack(4, 1f, 1.5f);
            Skills.Hit(four[0], 0.01f, "t", Vector2.right);
            Check("dao_an", four[0].Burning && four[0].Poisoned && Dmg("loi_an") > 0, "");
            yield return Arena(0, 10, 0, "phu_an"); int fr = 0;
            foreach (var e in Pack(40, 2f, 6f)) { Skills.Hit(e, 0.01f, "t", Vector2.right); if (e.Frozen) fr++; }
            Check("phu_an", fr >= 4 && fr <= 18, fr + "/40");
        }

        // ---------------------------------------------------------------- Tiến Giai, Uy Áp (Task 7)
        IEnumerator T_prog_stage()
        {
            yield return Arena(0, 5, 0); var r = Game.I.run; var b = BossAt(new Vector2(0, 4), 0);
            b.Damage(b.hp + 1f, false, Vector2.zero, 0f, null); yield return new WaitForSeconds(0.2f);
            Check("stage_up", r.skillStage == 1 && r.stageUpWave == 5 && r.stageCallout, r.skillStage + "");
            yield return Arena(0, 20, 3); r = Game.I.run; b = BossAt(new Vector2(0, 4), 3);
            b.Damage(b.hp + 1f, false, Vector2.zero, 0f, null); yield return new WaitForSeconds(0.2f);
            Check("stage_cap", r.skillStage == 3, r.skillStage + "");
            Game.I.player.SetSkillReady(); Game.I.player.TryCast(); Check("callout_once", !r.stageCallout, "");
        }

        IEnumerator T_prog_uyap()
        {
            yield return Arena(0, 9, 1); var r = Game.I.run; float hp = r.MaxHP, ar = r.stats[Stat.Armor];
            Game.I.OnLevelUp(10); r.Recalc();
            Check("uyap_10", Mathf.Approximately(r.MaxHP - hp, 10f) && Mathf.Approximately(r.stats[Stat.Armor] - ar, 1f), "");
            float dm = r.stats[Stat.Damage], sp = r.stats[Stat.SkillPower]; Game.I.OnLevelUp(20); r.Recalc();
            Check("uyap_20", Mathf.Approximately(r.stats[Stat.Damage] - dm, 10f) && Mathf.Approximately(r.stats[Stat.SkillPower] - sp, 10f), "");
        }

        IEnumerator T_prog_guard()
        {
            yield return Arena(0, 18, 3); var r = Game.I.run; var p = Game.I.player; p.god = false; r.level = 30; r.hp = 5f;
            yield return new WaitForSeconds(1.2f);   // wave-start grace (ResetForWave gives 1 s of i-frames)
            p.Hurt(1000f, p.Pos + Vector2.right);
            Check("nguyen_anh", Mathf.Approximately(r.hp, 1f) && Game.I.state == GState.Playing && r.guardUsed && p.InvulnLeft >= 0.9f, r.hp + "");
            Skills.OnWaveStart(r); Check("guard_reset", !r.guardUsed, "");
        }

        // all four auras side by side for the visual review (Luyện Khí / Trúc Cơ / Kim Đan / Nguyên Anh)
        IEnumerator T_prog_aura()
        {
            for (int i = 0; i < 4; i++)
            {
                yield return Arena(0, 3, 0); Game.I.run.level = i * 10 + 1; yield return new WaitForSecondsRealtime(0.3f);
                Game.I.bot.Shot("aura_" + i); yield return new WaitForSecondsRealtime(0.6f);
            }
        }

        IEnumerator T_ml_shots()   // all 24 forms for visual review; stage IV also in a 100-enemy crowd (spec section 13)
        {
            for (int ci = 0; ci < 6; ci++) for (int st = 0; st < 4; st++)
            {
                yield return Arena(ci, 3 + st * 5, st); Pack(st == 3 ? 100 : 20, 1f, st == 3 ? 8f : 4.5f);
                Game.I.player.SetSkillReady(); Game.I.player.TryCast(); yield return new WaitForSeconds(0.35f);
                Game.I.bot.Shot("form_" + ci + "_" + st);
                yield return new WaitForSecondsRealtime(0.6f);   // the capture is written at end of frame: do not rebuild the arena before it lands
            }
        }

        // ---------------------------------------------------------------- boss data (part C, task 1)
        IEnumerator T_boss_data()
        {
            yield return Arena(0, 1, 0);
            foreach (var id in new[] { "b1", "b1b", "b2", "b2b", "b3", "b4" })
            {
                var k = Content.Kit(id);
                Check("kit_" + id, k != null && k.Tier(BTier.Base).Count(m => !m.ult) == 2 && k.Tier(BTier.P2).Count() == 1
                      && k.Ult(BTier.Base) != null && k.Ult(BTier.I) != null && k.Ult(BTier.II) != null
                      && k.Tier(BTier.I).Count(m => !m.ult) == 1 && k.Tier(BTier.II).Count(m => !m.ult) == 1, id);
                // ult rows: max(tele, tele2) >= 1.0, because for a two-stage ult the final strike (tele2) is the telegraphed signature
                Check("tele_" + id, k.moves.All(m => m.after != null || (m.ult ? Mathf.Max(m.tele, m.tele2) >= 1.0f : m.tele >= (m.tier >= BTier.I ? 0.5f : 0.6f))), id);
                Check("riders_" + id, k.moves.Where(m => m.after != null).All(m => k.Move(m.after) != null), id);
                Check("ids_" + id, k.moves.Select(m => m.id).Distinct().Count() == k.moves.Count && k.moves.All(m => !string.IsNullOrEmpty(m.id)), id);
            }
            foreach (var id in new[] { "spiderling", "bat_small", "cocoon", "war_flag" })
                Check("minion_" + id, Content.Minion(id) != null && Content.Minion(id).minWave == 99, id);
            Check("static_beh", Content.Minion("cocoon").beh == EBeh.Static && Content.Minion("war_flag").beh == EBeh.Static, "");
        }

        IEnumerator T_boss_kiep()
        {
            yield return Arena(0, 1, 0);
            Check("ramp_cap", Mathf.Approximately(KiepNum.Ramp(20), 1.3f), "");
            Check("hp_k5_w20", Mathf.Abs(KiepNum.HpMul(5, 20) - 2.43f) < 0.01f, KiepNum.HpMul(5, 20).ToString("0.00"));
            Check("dmg_k3_w10", Mathf.Abs(KiepNum.DmgMul(3, 10) - (1 + 0.18f * 3 * (0.2f + 10 / 12f))) < 0.001f, "");
            Check("hp_k0", Mathf.Approximately(KiepNum.HpMul(0, 20), 1f), "");
            Check("boss_cd", Mathf.Approximately(KiepNum.BossCd[0], 1f) && Mathf.Approximately(KiepNum.BossCd[5], 0.85f) && Mathf.Approximately(KiepNum.BossShot[3], 1.06f), "");
            Check("tele_floor", Mathf.Approximately(KiepNum.TeleFloor(2), 0.6f) && Mathf.Approximately(KiepNum.TeleFloor(3), 0.5f), "");
        }

        // ---------------------------------------------------------------- boss hazards (part C, task 2)
        BossHazard Hz(Enemy owner, HShape sh, Vector2 pos, float tele, float active, float tick = 0f)
        {
            var h = BossHazard.Spawn(owner, sh, pos, tele, active, 10f, new Color(1f, 0.3f, 0.2f), tick);
            h.radius = 2f;
            return h;
        }

        IEnumerator T_boss_hz_once()
        {
            yield return Arena(0, 1, 0); Tank(); BossHazard.ClearAll();
            Hz(null, HShape.Circle, Vector2.zero, 0.6f, 1f);
            yield return Sec(0.5f);
            Check("hz_once_early", BossHazard.PlayerHits == 0, BossHazard.PlayerHits.ToString());
            yield return Sec(1.5f);
            Check("hz_once_total", BossHazard.PlayerHits == 1, BossHazard.PlayerHits.ToString());
            Check("hz_once_expired", BossHazard.All.Count == 0, BossHazard.All.Count.ToString());
        }

        IEnumerator T_boss_hz_tick()
        {
            yield return Arena(0, 1, 0); Tank(); BossHazard.ClearAll();
            Hz(null, HShape.Circle, Vector2.zero, 0.6f, 3f, 0.25f);
            yield return Sec(4f);
            Check("hz_tick_cap", BossHazard.PlayerHits >= 5 && BossHazard.PlayerHits <= 7, BossHazard.PlayerHits.ToString());
        }

        IEnumerator T_boss_hz_forming()
        {
            yield return Arena(0, 1, 0); Tank(); BossHazard.ClearAll();
            Hz(null, HShape.Circle, Vector2.zero, 0f, 1f);
            yield return Sec(0.4f);
            Check("hz_forming_early", BossHazard.PlayerHits == 0, BossHazard.PlayerHits.ToString());
            yield return Sec(0.6f);
            Check("hz_forming_hit", BossHazard.PlayerHits == 1, BossHazard.PlayerHits.ToString());
        }

        IEnumerator T_boss_hz_shapes()
        {
            yield return Arena(0, 1, 0); BossHazard.ClearAll();
            var bar = Hz(null, HShape.Bar, Vector2.zero, 1f, 1f); bar.len = 9f; bar.width = 1f; bar.angleDeg = 0f;
            Check("hz_bar", bar.Contains(new Vector2(4, 0), 0.3f) && !bar.Contains(new Vector2(0, 2), 0.3f), "");
            var ring = Hz(null, HShape.Ring, Vector2.zero, 1f, 1f); ring.radius = 3f; ring.radius2 = 2f;
            Check("hz_ring", ring.Contains(new Vector2(2.5f, 0), 0.3f) && !ring.Contains(new Vector2(1, 0), 0.3f), "");
            var cone = Hz(null, HShape.Cone, Vector2.zero, 1f, 1f); cone.radius = 5f; cone.arcDeg = 70f; cone.angleDeg = 0f;
            Check("hz_cone", cone.Contains(new Vector2(3, 0.5f), 0.3f) && !cone.Contains(new Vector2(0, 3), 0.3f), "");
            var ann = Hz(null, HShape.Annulus, Vector2.zero, 1f, 1f); ann.radius = 30f; ann.radius2 = 6f;
            Check("hz_annulus", ann.Contains(new Vector2(10, 0), 0.3f) && !ann.Contains(new Vector2(3, 0), 0.3f), "");
            var gap = Hz(null, HShape.Ring, Vector2.zero, 1f, 1f); gap.radius = 3f; gap.radius2 = 2f; gap.gapDirsDeg = new List<float> { 0f }; gap.gapWidthDeg = 40f;
            Check("hz_gap", !gap.Contains(new Vector2(2.5f, 0), 0.3f) && gap.Contains(new Vector2(0, 2.5f), 0.3f), "");
            BossHazard.ClearAll();
        }

        IEnumerator T_boss_hz_clear()
        {
            yield return Arena(0, 1, 0); BossHazard.ClearAll();
            var a = BossAt(new Vector2(6, 6), 1); var b = BossAt(new Vector2(-6, 6), 2);
            for (int i = 0; i < 3; i++) Hz(a, HShape.Circle, new Vector2(8, 0), 1f, 5f);
            Hz(b, HShape.Circle, new Vector2(-8, 0), 1f, 5f);
            Check("hz_clear_pre", BossHazard.All.Count == 4, BossHazard.All.Count.ToString());
            BossHazard.ClearOwner(a);
            Check("hz_clear_owner", BossHazard.All.Count == 1 && BossHazard.All[0].owner == b, BossHazard.All.Count.ToString());
            Game.I.dir.EndWave();
            Check("hz_clear_wave", BossHazard.All.Count == 0, BossHazard.All.Count.ToString());
            yield return null;
        }

        IEnumerator T_boss_hz_danger()
        {
            yield return Arena(0, 1, 0); BossHazard.ClearAll();
            var h = Hz(null, HShape.Circle, new Vector2(5, 0), 1f, 2f);
            yield return Sec(0.7f);   // about 0.3 s before it goes live
            Check("hz_danger_near", BossHazard.DangerAt(new Vector2(5, 0), 0.5f) > 0.5f, "");
            Check("hz_danger_far", BossHazard.DangerAt(new Vector2(-8, 0), 0.5f) < 0.5f, "");
            Check("hz_danger_early", BossHazard.DangerAt(new Vector2(5, 0), 0.1f) < 0.5f, "");
            BossHazard.ClearAll();
        }

        IEnumerator T_boss_hz_iframe()
        {
            yield return Arena(0, 1, 0); Tank(); BossHazard.ClearAll();
            var p = Game.I.player;
            p.slowT = 0f; p.slowMul = 1f;
            var h = Hz(null, HShape.Circle, Vector2.zero, 0.3f, 3f); h.slow = 0.5f;
            p.Invuln(0.7f);   // covers the telegraph plus the first 0.4 s of the live phase
            yield return Sec(0.65f);
            Check("hz_iframe_blocked", BossHazard.PlayerHits == 0 && p.slowT <= 0f && h.Live, BossHazard.PlayerHits + " slowT=" + p.slowT);
            yield return Sec(0.5f);
            Check("hz_iframe_lands", BossHazard.PlayerHits == 1 && p.slowT > 0f, BossHazard.PlayerHits + " slowT=" + p.slowT);
            yield return Sec(1.5f);
            Check("hz_iframe_once", BossHazard.PlayerHits == 1, BossHazard.PlayerHits.ToString());
            BossHazard.ClearAll();
        }

        // ---------------------------------------------------------------- boss brain (part C, task 3)
        /// <summary>BossArena, tank player at the centre, boss held until the wave-start i-frames are over, world cleared.</summary>
        IEnumerator BossPrep(string bossId, int kiep, int wave, Vector2 bossPos)
        {
            yield return BossArena(bossId, kiep, wave); Tank();
            boss.brain.Hold = true; boss.transform.position = bossPos;
            yield return Sec(1.05f);
            BossHazard.ClearAll(); Projectile.ClearAll(false);
            boss.transform.position = bossPos;
            boss.brain.Hold = false;
        }

        /// <summary>Waits until the brain logs more than n moves (or timeout).</summary>
        IEnumerator WaitLog(BossBrain b, int n, float timeout)
        {
            float t = 0f;
            while (b.Log.Count <= n && t < timeout) { yield return null; t += Game.Dt; }
        }

        IEnumerator WaitPhase2(BossBrain b, float timeout)
        {
            float t = 0f;
            while (b.Phase != 2 && t < timeout) { yield return null; t += Game.Dt; }
        }

        IEnumerator T_boss_phase()
        {
            yield return BossArena("b1", 0, 5); var b = boss.brain;
            float spd = boss.speed; b.ForceMove("doc_nhan_xung"); yield return Sec(0.3f);   // a move in flight
            var h = BossHazard.Spawn(boss, HShape.Circle, new Vector2(5, 5), 1f, 20f, 1f, Color.red);
            Check("p1", b.Phase == 1, ""); boss.hp = boss.maxHp * 0.49f; yield return Sec(0.1f);
            Check("p2_deferred", b.Phase == 1 && b.PhasePending && b.Current != null && b.Current.id == "doc_nhan_xung", b.Current != null ? b.Current.id : "none");
            yield return WaitPhase2(b, 10f);
            Check("p2", b.Phase == 2, ""); Check("hz_cleared", BossHazard.All.Count == 0, BossHazard.All.Count.ToString());
            Check("p2_hold", b.Hold && b.Current == null && b.Transitions == 1, "");
            yield return Sec(1.5f);
            Check("no_enrage_speed", Mathf.Approximately(boss.speed, spd), boss.speed + " vs " + spd);
            Check("p2_released", !b.Hold, "");
        }

        IEnumerator T_boss_ult_k0()
        {
            yield return BossArena("b1", 0, 5); var b = boss.brain; var ub = b.kit.Ult(BTier.Base);
            Check("marks_k0", b.UltMarks.Count == 2 && Mathf.Approximately(b.UltMarks[0], 0.75f) && Mathf.Approximately(b.UltMarks[1], 0.25f), string.Join(",", b.UltMarks));
            yield return Sec(0.5f);
            boss.hp = boss.maxHp * 0.74f; int n = b.Log.Count;
            yield return WaitLog(b, n, 8f);
            Check("ult75_base", b.Log.Count > n && b.Log[n] == ub.id, b.Log.Count > n ? b.Log[n] : "none");
            yield return Sec(1f);
            boss.hp = boss.maxHp * 0.24f; n = b.Log.Count;
            yield return WaitLog(b, n, 10f);
            Check("ult25_base", b.Log.Count > n && b.Log[n] == ub.id, b.Log.Count > n ? b.Log[n] : "none");
            Check("ult25_after_p2", b.Phase == 2 && b.Phase2At <= b.UltStartedAt(ub.id), b.Phase2At + " vs " + b.UltStartedAt(ub.id));
            yield return Sec(3f);
            Check("ult_k0_count", b.Log.Count(id => id == ub.id) == 2, string.Join(",", b.Log));
        }

        IEnumerator T_boss_ult_k3()
        {
            yield return BossArena("b2", 3, 10); var b = boss.brain;
            Check("marks_k3", b.UltMarks.Count == 2, string.Join(",", b.UltMarks));
            yield return Sec(0.5f);
            boss.hp = boss.maxHp * 0.74f; int n = b.Log.Count;
            yield return WaitLog(b, n, 8f);
            Check("ult75_k3_base", b.Log.Count > n && b.Log[n] == b.kit.Ult(BTier.Base).id, b.Log.Count > n ? b.Log[n] : "none");
            yield return Sec(1f);
            boss.hp = boss.maxHp * 0.24f; n = b.Log.Count;
            yield return WaitLog(b, n, 15f);
            Check("ult25_k3_I", b.Log.Count > n && b.Log[n] == b.kit.Ult(BTier.I).id, b.Log.Count > n ? b.Log[n] : "none");
            Check("ult_k3_noII", !b.Log.Contains(b.kit.Ult(BTier.II).id), string.Join(",", b.Log));
        }

        IEnumerator T_boss_ult_multi()   // Review Focus 1: Base ult, then phase 2 (once), then set I, then set II
        {
            yield return BossArena("b3", 5, 15); var b = boss.brain; var k = b.kit;
            Check("marks_k5", b.UltMarks.Count == 3 && Mathf.Approximately(b.UltMarks[1], 0.5f), string.Join(",", b.UltMarks));
            yield return Sec(0.3f);
            boss.hp = boss.maxHp * 0.9f;
            boss.Damage(boss.maxHp * 0.7f, false, Vector2.zero, 0f, null);
            int n0 = b.Log.Count; float t = 0f;
            var ultIds = new[] { k.Ult(BTier.Base).id, k.Ult(BTier.I).id, k.Ult(BTier.II).id };
            while (t < 30f && b.Log.Skip(n0).Count(id => ultIds.Contains(id)) < 3) { yield return null; t += Game.Dt; }
            yield return Sec(1f);
            var ults = b.Log.Skip(n0).Where(id => ultIds.Contains(id)).ToList();
            Check("multi_order", ults.SequenceEqual(ultIds), string.Join(",", ults));
            Check("multi_phase_once", b.Phase == 2 && b.Transitions == 1, b.Transitions.ToString());
            float tb = b.UltStartedAt(ultIds[0]), ti = b.UltStartedAt(ultIds[1]), tii = b.UltStartedAt(ultIds[2]);
            Check("multi_seq", tb < b.Phase2At && b.Phase2At < ti && ti < tii, tb + " < " + b.Phase2At + " < " + ti + " < " + tii);
            Check("multi_no_regular", b.Log.Skip(n0).Take(3).SequenceEqual(ultIds), string.Join(",", b.Log.Skip(n0)));
        }

        IEnumerator T_boss_ult_nocancel()   // a running ult is never cut by the phase change
        {
            yield return BossArena("b2", 0, 10); var b = boss.brain; var u = b.kit.Ult(BTier.Base);
            yield return Sec(0.3f);
            boss.hp = boss.maxHp * 0.74f; int n = b.Log.Count;
            yield return WaitLog(b, n, 8f);
            Check("nocancel_started", b.Log.Count > n && b.Log[n] == u.id, b.Log.Count > n ? b.Log[n] : "none");
            yield return Sec(0.5f);
            int hz = BossHazard.All.Count(h => h.moveId == u.id);
            boss.hp = boss.maxHp * 0.4f; yield return Sec(0.3f);
            Check("nocancel_running", b.Phase == 1 && b.Current == u && BossHazard.All.Count(h => h.moveId == u.id) >= hz && hz > 0, hz + " hz, phase " + b.Phase);
            yield return WaitPhase2(b, 12f);
            float len = (u.reps - 1) * (b.EffTele(u) + u.gap) + b.EffTele(u) + 0.2f;   // full length of the Zones ult
            Check("nocancel_complete", b.Phase == 2 && b.Phase2At - b.UltStartedAt(u.id) >= len - 0.1f, (b.Phase2At - b.UltStartedAt(u.id)).ToString("0.00") + " vs " + len.ToString("0.00"));
        }

        IEnumerator T_boss_ult_tele()   // every ultimate run through the brain telegraphs >= 1.0 s
        {
            foreach (var id in new[] { "b1", "b1b", "b2", "b2b", "b3", "b4" })
                foreach (var kp in new[] { 0, 3, 5 })
                {
                    yield return BossArena(id, kp, 5); var b = boss.brain;
                    foreach (var m in b.kit.moves.Where(x => x.ult && (x.tier == BTier.Base || (x.tier == BTier.I && BossBrain.Sets(kp) >= 1) || (x.tier == BTier.II && BossBrain.Sets(kp) == 2))))
                        Check("ult_tele_" + m.id + "_k" + kp, b.EffTele(m) >= 1f || (m.tele2 >= 1f && b.EffTele(m) >= KiepNum.TeleFloor(kp)), b.EffTele(m).ToString("0.00"));
                }
            // and the implemented primitive ults really use it
            foreach (var pair in new[] { ("b1b", "huyet_nguyet_da_yen"), ("b2", "thien_hoa_lac"), ("b1", "ta_nhan_phong_an"), ("b3", "huyet_ha_dao_luu"), ("b3", "huyet_vu") })
            {
                yield return BossArena(pair.Item1, 5, 15); var b = boss.brain; b.LastTele = -1f;
                b.ForceMove(pair.Item2); yield return Sec(0.2f);
                Check("ult_tele_run_" + pair.Item2, b.Log.Contains(pair.Item2) && b.LastTele >= 1f, b.LastTele.ToString("0.00"));
            }
        }

        IEnumerator T_boss_select()
        {
            yield return BossArena("b1", 0, 5); var b = boss.brain; b.Hold = true;
            var picks1 = new List<BossMove>(); var picks2 = new List<BossMove>();
            for (int i = 0; i < 20; i++) picks1.Add(b.Pick(true));
            b.Hold = false; boss.hp = boss.maxHp * 0.49f;   // the 75% ult plays first, then the transition (Hold during it)
            yield return WaitPhase2(b, 10f);
            Check("sel_phase2", b.Phase == 2 && b.Hold, "");
            for (int i = 0; i < 20; i++) picks2.Add(b.Pick(true));
            var all = picks1.Concat(picks2).ToList();
            bool noRepeat = true; for (int i = 1; i < all.Count; i++) if (all[i] == null || all[i] == all[i - 1]) noRepeat = false;
            Check("sel_no_repeat", all[0] != null && noRepeat, string.Join(",", all.Select(m => m == null ? "null" : m.id)));
            Check("sel_no_set", all.All(m => m != null && (m.tier == BTier.Base || m.tier == BTier.P2) && !m.ult && m.after == null), "");
            Check("sel_p2_late", picks1.All(m => m.tier != BTier.P2), "");
            Check("sel_p2_early", picks2.Take(2).Any(m => m.tier == BTier.P2), string.Join(",", picks2.Take(2).Select(m => m.id)));
        }

        IEnumerator T_boss_unlock()
        {
            yield return BossArena("b1", 3, 5); var b = boss.brain; b.Hold = true;
            var ps = new List<BossMove>(); for (int i = 0; i < 40; i++) ps.Add(b.Pick(true));
            Check("unlock_k3_I", ps.Any(m => m.tier == BTier.I), string.Join(",", ps.Select(m => m.id).Distinct()));
            Check("unlock_k3_noII", ps.All(m => m.tier != BTier.II) && !b.Unlocked(b.kit.Move("an_mu")), "");
            yield return BossArena("b1", 5, 5); b = boss.brain; b.Hold = true;
            ps.Clear(); for (int i = 0; i < 40; i++) ps.Add(b.Pick(true));
            Check("unlock_k5_p1_noII", ps.All(m => m.tier != BTier.II) && !b.Unlocked(b.kit.Move("an_mu")), "");
            b.Hold = false; boss.hp = boss.maxHp * 0.49f;
            yield return WaitPhase2(b, 10f); yield return Sec(1.1f);
            Check("unlock_k5_p2_II", b.Phase == 2 && b.Unlocked(b.kit.Move("an_mu")), "");
            b.ForceMove("doc_nhan_xung"); int n = b.Log.Count; float t = 0f;
            while (t < 8f && !b.Log.Skip(n).Contains("an_mu")) { yield return null; t += Game.Dt; }
            var tail = b.Log.Skip(n).ToList(); int ia = tail.IndexOf("an_mu");
            Check("unlock_rider", ia > 0 && tail[ia - 1] == "doc_nhan_xung", string.Join(",", tail));
            Check("rider_puddle", BossHazard.All.Any(h => h.moveId == "an_mu"), "");
        }

        IEnumerator FirstBossHit(bool frozen, float[] outT)
        {
            yield return BossPrep("b1", 0, 5, new Vector2(0, 5)); var b = boss.brain;
            int h0 = BossBrain.HitCount; b.ForceMove("doc_nhan_xung");
            if (frozen) boss.Freeze(5f);
            float t = 0f;
            while (BossBrain.HitCount == h0 && t < 6f) { yield return null; t += Game.Dt; }
            outT[0] = BossBrain.HitCount > h0 ? t : 99f;
        }

        IEnumerator T_boss_frozen()   // Review Focus 4
        {
            var a = new float[1]; var f = new float[1];
            yield return FirstBossHit(false, a);
            yield return FirstBossHit(true, f);
            Check("frozen_hit", a[0] < 3f, a[0].ToString("0.00"));
            Check("frozen_later", f[0] < 90f && f[0] - a[0] >= 0.4f, a[0].ToString("0.00") + " vs " + f[0].ToString("0.00"));
        }

        IEnumerator T_boss_cancel_dash()   // a cancelled dash stops writing velocity and leaves nothing behind
        {
            yield return BossPrep("b1", 0, 5, new Vector2(-8, 5)); var b = boss.brain;
            b.ForceMove("doc_nhan_xung");
            float t = 0f;
            while (!b.Dashing && t < 4f) { yield return null; t += Game.Dt; }
            Check("cancel_was_dashing", b.Dashing, t.ToString("0.00"));
            yield return null;
            b.Cancel(); b.Hold = true;
            Vector2 p0 = boss.Pos;
            yield return Sec(1f);
            Check("cancel_stopped", !b.Dashing && b.vel == Vector2.zero && b.Current == null, b.vel.ToString());
            Check("cancel_no_motion", (boss.Pos - p0).magnitude < 0.3f, (boss.Pos - p0).magnitude.ToString("0.00"));
            Check("cancel_clean", !BossHazard.All.Any(h => h.owner == boss), BossHazard.All.Count.ToString());
        }

        /// <summary>Force one move; every damage event (hazard hit, boss hit or new bullet) must come after a boss telegraph was already
        /// on screen for at least the Kiếp 0 floor, and the primitive must report a telegraph >= the floor.</summary>
        IEnumerator Prim(string kind, string bossId, int wave, string moveId, Vector2 bossPos, int volley, System.Func<BossBrain, IEnumerator> during = null)
        {
            yield return BossPrep(bossId, 0, wave, bossPos); var b = boss.brain; b.LastTele = -1f;
            int ph0 = BossHazard.PlayerHits, bh0 = BossBrain.HitCount, pr0 = Projectile.CountHostile();
            b.ForceMove(moveId);
            for (int i = 0; i < 4 && b.Current == null; i++) yield return null;
            b.Hold = true;   // nothing but the forced move may hurt
            if (during != null) yield return during(b);
            bool teleSeen = false, changed = false, teleFirst = false; int firstVolley = -1; float t = 0f, teleAt = -1f;
            while (t < 8f && !changed)
            {
                yield return null; t += Game.Dt;
                int pr = Projectile.CountHostile();
                if (BossHazard.PlayerHits != ph0 || BossBrain.HitCount != bh0 || pr != pr0)
                {
                    changed = true; teleFirst = teleSeen; firstVolley = pr - pr0;
                }
                if (!teleSeen && BossHazard.All.Any(h => h.owner == boss && h.Telegraphing)) { teleSeen = true; teleAt = t; }
            }
            float floor = KiepNum.TeleFloor(0);
            Check("prim_" + kind + "_fires", changed && b.Log.Contains(moveId), moveId + " t=" + t.ToString("0.00"));
            Check("prim_" + kind + "_tele_first", teleFirst, moveId);
            Check("prim_" + kind + "_tele_floor", b.LastTele >= floor && t - teleAt >= floor - 0.05f, "tele " + b.LastTele.ToString("0.00") + " shown " + (t - teleAt).ToString("0.00"));
            if (volley > 0) Check("prim_" + kind + "_volley", firstVolley == volley, firstVolley + " vs " + volley);
        }

        IEnumerator T_boss_prims()
        {
            yield return Prim("dash", "b1", 5, "doc_nhan_xung", new Vector2(0, 5), 0);
            yield return Prim("fan", "b2", 10, "hoa_cau_te_xa", new Vector2(0, 5), 5);
            yield return Prim("ring", "b3", 15, "huyet_luan", new Vector2(0, 5), 12);
            yield return Prim("spiral", "b3", 15, "huyet_ha_dao_luu", new Vector2(0, 5), 6);
            yield return Prim("cone", "b2", 10, "liet_diem_tam_kich", new Vector2(0, 3), 0);
            yield return Prim("zones", "b4", 20, "thien_loi_cam_vuc", new Vector2(0, 5), 0);
            yield return Prim("pillars", "b4", 20, "thien_ma_phap_tuong", new Vector2(0, 5), 0);
            yield return Prim("sweep", "b1", 5, "ma_nhan_quet", new Vector2(0, 5), 0);
            yield return Prim("nova", "b2", 10, "viem_hoan", Vector2.zero, 0, NovaIntoRing);
            yield return Prim("line", "b2b", 10, "doc_ty_troi", new Vector2(0, 5), 0);
            yield return Prim("blink", "b3", 15, "huyet_bo", new Vector2(0, 5), 0, BlinkIntoCircle);
            BossHazard.ClearAll(); Projectile.ClearAll(false);
        }

        // ---------------------------------------------------------------- task 4 primitives
        void Place(Vector2 p) { tankHold = false; Game.I.player.transform.position = p; }

        /// <summary>Forces a move, waits until it started, then holds the brain so no other move follows.</summary>
        IEnumerator Fire(BossBrain b, string id)
        {
            b.ForceMove(id);
            for (int i = 0; i < 4 && b.Current == null; i++) yield return null;
            b.Hold = true;
        }

        BossHazard TeleOf(HShape s) => BossHazard.All.FirstOrDefault(h => h != null && h.owner == boss && h.shape == s && h.Telegraphing);

        /// <summary>Nova scenario helper: puts the player 5 units from the boss, outside every gap.</summary>
        IEnumerator NovaIntoRing(BossBrain b)
        {
            BossHazard h = null;
            for (int i = 0; i < 10 && h == null; i++) { h = TeleOf(HShape.Annulus); if (h == null) yield return null; }
            if (h == null || h.gapDirsDeg == null || h.gapDirsDeg.Count == 0) yield break;
            float ang = h.gapDirsDeg[0] + h.angleDeg + 0.5f * 360f / h.gapDirsDeg.Count;
            Place(boss.Pos + new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * 5f);
        }

        /// <summary>Blink scenario helper: steps into the slash telegraph so the hit lands.</summary>
        IEnumerator BlinkIntoCircle(BossBrain b)
        {
            BossHazard h = null;
            for (int i = 0; i < 10 && h == null; i++) { h = TeleOf(HShape.Circle); if (h == null) yield return null; }
            if (h != null) Place(h.pos);
        }

        IEnumerator T_boss_prim_sweep()
        {
            yield return BossPrep("b1", 0, 5, new Vector2(0, 5)); var b = boss.brain;
            int ph0 = BossHazard.PlayerHits;
            yield return Fire(b, "ma_nhan_quet"); yield return Sec(2.6f);
            Check("sweep_on_arc_hit", BossHazard.PlayerHits - ph0 == 1, (BossHazard.PlayerHits - ph0).ToString());
            yield return BossPrep("b1", 0, 5, new Vector2(0, 5)); b = boss.brain;
            ph0 = BossHazard.PlayerHits;
            yield return Fire(b, "ma_nhan_quet"); yield return null; Place(new Vector2(0, 8.5f)); yield return Sec(2.6f);
            Check("sweep_behind_boss_safe", BossHazard.PlayerHits - ph0 == 0, (BossHazard.PlayerHits - ph0).ToString());
            // the telegraph is a wedge covering the swept arc, drawn before the beam exists
            yield return BossPrep("b1", 0, 5, new Vector2(0, 5)); b = boss.brain;
            yield return Fire(b, "ma_nhan_quet"); yield return Sec(0.2f);
            var w = TeleOf(HShape.Cone);
            Check("sweep_wedge", w != null && w.arcDeg >= 90f && w.radius >= 9f, w == null ? "none" : w.arcDeg + " r" + w.radius);
            BossHazard.ClearAll();
        }

        IEnumerator T_boss_prim_nova()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                yield return BossPrep("b2", 0, 10, Vector2.zero); var b = boss.brain;
                int ph0 = BossHazard.PlayerHits;
                yield return Fire(b, "viem_hoan");
                BossHazard h = null;
                for (int i = 0; i < 10 && h == null; i++) { h = TeleOf(HShape.Annulus); if (h == null) yield return null; }
                if (h == null || h.gapDirsDeg == null) { Check("nova_tele", false, "no annulus telegraph"); continue; }
                float step = 360f / h.gapDirsDeg.Count;
                float ang = h.gapDirsDeg[0] + h.angleDeg + (pass == 0 ? 0f : step * 0.5f);
                if (pass == 0) Check("nova_tele", h.gapDirsDeg.Count == 2 && Mathf.Abs(h.gapWidthDeg - 35f) < 0.1f && h.Telegraphing, h.gapDirsDeg.Count + " gaps w" + h.gapWidthDeg);
                Place(boss.Pos + new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * 5f);
                yield return Sec(2.5f);
                int hits = BossHazard.PlayerHits - ph0;
                Check(pass == 0 ? "nova_in_gap_safe" : "nova_outside_gap_hit", hits == (pass == 0 ? 0 : 1), hits.ToString());
            }
            BossHazard.ClearAll();
        }

        IEnumerator T_boss_prim_line()
        {
            yield return BossPrep("b2b", 0, 10, new Vector2(0, 5)); var b = boss.brain;
            int ph0 = BossHazard.PlayerHits;
            yield return Fire(b, "doc_ty_troi");
            yield return Sec(0.1f);
            var bars = BossHazard.All.Where(h => h != null && h.owner == boss && h.shape == HShape.Bar && h.Telegraphing).ToList();
            Check("line_three_bars", bars.Count == 3, bars.Count.ToString());
            bool edge = bars.Count > 0 && bars.All(h => Mathf.Abs(h.len - Edge(h.pos, h.angleDeg)) < 0.2f && (h.pos - boss.Pos).magnitude < 0.3f);
            Check("line_to_edge", edge, bars.Count > 0 ? bars[0].len.ToString("0.0") : "-");
            var pl = Game.I.player; float t = 0f;
            while (t < 2.5f && BossHazard.PlayerHits == ph0) { yield return null; t += Game.Dt; }
            float slowT = pl.slowT, slowMul = pl.slowMul;
            Check("line_hit", BossHazard.PlayerHits - ph0 == 1, (BossHazard.PlayerHits - ph0).ToString());
            Check("line_slow", Mathf.Abs(slowMul - 0.7f) < 0.01f && slowT > 1.0f && slowT <= 1.2f, slowMul.ToString("0.00") + " t" + slowT.ToString("0.00"));
            // grid: 5 vertical + 3 horizontal lines, floor 1.0 s for the ultimate
            yield return BossPrep("b2b", 0, 10, new Vector2(0, 5)); b = boss.brain; b.LastTele = -1f;
            yield return Fire(b, "thien_la_dia_vong"); yield return Sec(0.1f);
            int n = BossHazard.All.Count(h => h != null && h.owner == boss && h.shape == HShape.Bar && h.Telegraphing);
            Check("line_grid", n == 8 && b.LastTele >= 1f, n + " tele " + b.LastTele);
            // between: two nearest live pools, skipped with fewer than two
            yield return BossPrep("b2b", 0, 10, new Vector2(0, 5)); b = boss.brain;
            BossHazard.Spawn(boss, HShape.Circle, new Vector2(-5, 0), 0.01f, 30f, 1f, Color.green, 0.5f).radius = 1f;
            BossHazard.Spawn(boss, HShape.Circle, new Vector2(6, 3), 0.01f, 30f, 1f, Color.green, 0.5f).radius = 1f;
            BossHazard.Spawn(boss, HShape.Circle, new Vector2(9, -8), 0.01f, 30f, 1f, Color.green, 0.5f).radius = 1f;
            yield return Sec(0.1f);
            yield return Fire(b, "ty_dan"); yield return Sec(0.2f);
            var seg = TeleOf(HShape.Segment);
            bool ok = seg != null && (((seg.a - new Vector2(-5, 0)).magnitude < 0.1f && (seg.b - new Vector2(6, 3)).magnitude < 0.1f) || ((seg.b - new Vector2(-5, 0)).magnitude < 0.1f && (seg.a - new Vector2(6, 3)).magnitude < 0.1f));
            Check("line_between", ok, seg == null ? "none" : seg.a + " " + seg.b);
            BossHazard.ClearAll();
            yield return BossPrep("b2b", 0, 10, new Vector2(0, 5)); b = boss.brain;
            BossHazard.Spawn(boss, HShape.Circle, new Vector2(-5, 0), 0.01f, 30f, 1f, Color.green, 0.5f).radius = 1f;
            yield return Sec(0.1f);
            yield return Fire(b, "ty_dan"); yield return Sec(0.3f);
            Check("line_between_skip", TeleOf(HShape.Segment) == null, "");
            BossHazard.ClearAll();
        }

        static float Edge(Vector2 o, float deg)
        {
            float r = deg * Mathf.Deg2Rad; Vector2 d = new Vector2(Mathf.Cos(r), Mathf.Sin(r)); float best = 999f;
            if (d.x > 0.001f) best = Mathf.Min(best, (VK.Arena.HalfW - o.x) / d.x); else if (d.x < -0.001f) best = Mathf.Min(best, (-VK.Arena.HalfW - o.x) / d.x);
            if (d.y > 0.001f) best = Mathf.Min(best, (VK.Arena.HalfH - o.y) / d.y); else if (d.y < -0.001f) best = Mathf.Min(best, (-VK.Arena.HalfH - o.y) / d.y);
            return best;
        }

        IEnumerator T_boss_prim_summon()
        {
            yield return BossPrep("b1b", 0, 5, new Vector2(0, 5)); var b = boss.brain;
            Game.I.dir.TeleOnly = true; Game.I.dir.enabled = true;
            int max = 0; float t = 0f; bool markFirst = false, spawned = false; bool statsOk = false; string statDetail = "none"; b.LastTele = -1f;
            float exp = 0.2f * WaveDirector.ExpectedTrashHp(5) * KiepNum.HpMul(0, 5);
            System.Action sample = () =>
            {
                var live = Enemy.All.Where(e => e != null && !e.dead && e.summoner == b).ToList();
                if (live.Count > max)
                {
                    max = live.Count;
                    statsOk = live.All(e => e.def.id == "bat_small" && Mathf.Abs(e.maxHp - exp) < 0.01f && Mathf.Abs(e.dmg - 0.3f * boss.dmg) < 0.01f);
                    statDetail = live[0].maxHp + " vs " + exp + " dmg " + live[0].dmg + " vs " + 0.3f * boss.dmg;
                }
                if (!markFirst && !spawned && BossHazard.All.Any(h => h != null && h.owner == boss && h.Telegraphing)) markFirst = true;
                if (live.Count > 0) spawned = true;
            };
            for (int cast = 0; cast < 3; cast++)
            {
                int n0 = b.Log.Count;
                b.Hold = false; b.ForceMove("bay_doi");
                float w = 0f;
                while (w < 4f && (b.Log.Count == n0 || b.Current != null)) { yield return null; w += Game.Dt; sample(); }
                b.Hold = true;
            }
            while (t < 1.5f) { yield return null; t += Game.Dt; sample(); }
            Check("summon_tele_first", markFirst && b.LastTele >= KiepNum.TeleFloor(0), "mark " + markFirst + " tele " + b.LastTele.ToString("0.00"));
            Check("summon_cap", max == 6, "max " + max);
            Check("summon_bat_stats", statsOk, statDetail);
            yield return Sec(5.5f);
            int left = Enemy.All.Count(e => e != null && !e.dead && e.summoner == b);
            Check("summon_lifetime", left == 0, left.ToString());
            Game.I.dir.TeleOnly = false; Game.I.dir.enabled = false;
            BossHazard.ClearAll();
        }

        IEnumerator T_boss_prim_static()   // cocoons and flags are not pushed around by the crowd separation
        {
            yield return BossPrep("b2b", 0, 10, new Vector2(0, 5));
            Game.I.player.god = true;
            var a = Enemy.Spawn(Content.Minion("cocoon"), new Vector2(6, -3), 10, 1f, 1f);
            var c = Enemy.Spawn(Content.Minion("war_flag"), new Vector2(6.1f, -3f), 10, 1f, 1f);
            Vector2 pa = a.Pos, pc = c.Pos;
            yield return Sec(1f);
            Check("static_no_drift", (a.Pos - pa).magnitude < 0.001f && (c.Pos - pc).magnitude < 0.001f, (a.Pos - pa).magnitude.ToString("0.000"));
            Destroy(a.gameObject); Destroy(c.gameObject); Enemy.All.RemoveAll(e => e == null || e.dead);
        }

        IEnumerator T_boss_prim_blink()
        {
            foreach (var cfg in new[] { ("b3", 15, "huyet_bo"), ("b1b", 5, "da_hanh") })
            {
                yield return BossPrep(cfg.Item1, 0, cfg.Item2, new Vector2(0, 5)); var b = boss.brain;
                Vector2 p0 = Game.I.player.Pos, from = boss.Pos;
                yield return Fire(b, cfg.Item3);
                float t = 0f;
                while (t < 3f && (boss.Pos - from).magnitude < 1f) { yield return null; t += Game.Dt; }
                float d = (boss.Pos - p0).magnitude;
                Check("blink_land_" + cfg.Item3, t < 3f && d >= 2.45f && d <= 4.05f && Mathf.Abs(boss.Pos.x) <= VK.Arena.HalfW && Mathf.Abs(boss.Pos.y) <= VK.Arena.HalfH, d.ToString("0.00") + " t" + t.ToString("0.00"));
            }
            BossHazard.ClearAll();
        }
    }
}

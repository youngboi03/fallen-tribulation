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
    public partial class SkillTest : MonoBehaviour
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
            tankHold = false; AutoTest.Passive = false;
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
        Vector2 tankPos;

        /// <summary>Like Arena, at the given kiep, then spawns the boss of that wave slot through the same numbers WaveDirector uses.</summary>
        public IEnumerator BossArena(string bossId, int kiep, int wave)
        {
            var g = Game.I;
            tankHold = false; AutoTest.Passive = false;
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
            tankPos = Vector2.zero; AutoTest.Passive = true;
            g.player.transform.position = Vector3.zero;
            if (!tankHold) { tankHold = true; StartCoroutine(HoldCentre()); }
        }

        IEnumerator HoldCentre()
        {
            while (tankHold) { Game.I.player.transform.position = tankPos; Game.I.run.hp = 1e6f; yield return null; }
        }

        /// <summary>Pins the tank player at p every frame (the bot walks away from hazards otherwise).</summary>
        void PinAt(Vector2 p)
        {
            tankPos = p; Game.I.player.transform.position = p;
            if (!tankHold) { tankHold = true; StartCoroutine(HoldCentre()); }
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
            Check("hp_k5_w20", Mathf.Abs(KiepNum.HpMul(5, 20) - 2.30f) < 0.01f, KiepNum.HpMul(5, 20).ToString("0.00"));
            Check("dmg_k3_w10", Mathf.Abs(KiepNum.DmgMul(3, 10) - (1 + 0.16f * 3 * (0.2f + 10 / 12f))) < 0.001f, "");
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
            Game.I.player.ReduceSkillCd(-999f);   // no Thần Thông cast may kill a cocoon or a summoned minion that a test counts
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

        IEnumerator T_boss_music()   // phase 2 switches to the climax track
        {
            yield return BossArena("b2", 0, 10); var b = boss.brain;
            Sfx.Music("boss");
            Check("music_boss", Sfx.CurrentTrack == "boss", Sfx.CurrentTrack);
            yield return Sec(0.3f);
            boss.hp = boss.maxHp * 0.45f;
            yield return WaitPhase2(b, 20f);
            yield return Sec(1f);
            Check("music_climax", Sfx.CurrentTrack == "boss_climax", Sfx.CurrentTrack);
        }

        // ---- cutscenes (Task 8): they run on real time, gameplay Sec() would never advance
        bool HasAura(Enemy e) { var t = e.transform.Find("aura"); var c = t ? t.GetComponent<SpriteRenderer>().color : Color.clear; return Mathf.Abs(c.r - 0.7f) < 0.02f && Mathf.Abs(c.g - 0.4f) < 0.02f && c.b > 0.98f; }   // Kiếp Biến purple

        IEnumerator T_cine_kiepbien()   // frozen world, purple aura at the end, then 0.6 s grace
        {
            yield return BossArena("b2", 3, 10); var b = boss.brain; var c = Game.I.cinema;
            c.testMode = true; c.Skips = 0; c.OnBossSpawn(b);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("kb_cinematic", Game.I.cinematic && c.Active && b.Hold, "");
            Vector2 p0 = boss.Pos;
            yield return new WaitForSecondsRealtime(2.5f);
            Check("kb_frozen", Vector2.Distance(boss.Pos, p0) < 0.01f, boss.Pos + " vs " + p0);
            yield return new WaitForSecondsRealtime(2.2f);
            Check("kb_done", !Game.I.cinematic && !c.Active && HasAura(boss) && c.Skips == 0, "");
            yield return Sec(0.8f);
            Check("kb_released", !b.Hold, "");
            c.testMode = false;
        }

        IEnumerator T_weapon_out()   // idle weapons point away from the hero, also right after the wave reset
        {
            yield return Arena(0, 3, 0); var g = Game.I; var r = g.run;
            foreach (var d in Content.Weapons) r.AddWeapon(d, 0);
            yield return new WaitForSeconds(0.6f);
            g.player.ResetForWave(); yield return new WaitForSeconds(0.05f);
            var bad = new List<string>();
            foreach (var w in r.weapons) if (w.OutDot(g.player.Pos) < 0.7f) bad.Add(w.def.id + " " + w.OutDot(g.player.Pos).ToString("0.00"));
            Check("weapon_points_out", bad.Count == 0, string.Join(", ", bad));
        }

        IEnumerator T_cine_k0()   // Kiếp 0: no Kiếp Biến, only the grace period
        {
            yield return BossArena("b2", 0, 10); var b = boss.brain; var c = Game.I.cinema;
            c.testMode = true; Game.I.save.bossIntroSeen.Add("b2"); c.Skips = 0; c.OnBossSpawn(b);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("k0_no_cinematic", !Game.I.cinematic && !HasAura(boss), "");
            yield return Sec(1f);
            Check("k0_released", !b.Hold, "");
            c.testMode = false;
        }

        IEnumerator T_cine_skip()   // Skip at 0.6 s lands on the same end state; ESC path leaves pause usable
        {
            yield return BossArena("b2", 3, 10); var b = boss.brain; var c = Game.I.cinema;
            c.testMode = true; c.Skips = 0; c.OnBossSpawn(b);
            yield return new WaitForSecondsRealtime(0.6f);
            c.Skip();
            yield return new WaitForSecondsRealtime(0.3f);
            Check("skip_end", !Game.I.cinematic && !c.Active && HasAura(boss) && c.Skips == 1, Game.I.cinematic + " " + c.Active + " " + HasAura(boss) + " " + c.Skips);
            Check("skip_not_paused", !Game.I.paused, "");
            Game.I.SetPause(true);
            Check("skip_pause_ok", Game.I.paused, "");
            Game.I.SetPause(false);
            yield return Sec(0.8f);
            Check("skip_released", !b.Hold, "");
            c.testMode = false;
        }

        IEnumerator T_cine_mathan()   // Kiếp 5 at 50%: Ma Thân, then phase 2 with the set I ultimate next
        {
            yield return BossArena("b4", 5, 15); var b = boss.brain; var c = Game.I.cinema;
            c.testMode = true; c.Attach(b);
            float r0 = boss.radius; var k = b.kit;
            yield return Sec(0.3f);
            boss.hp = boss.maxHp * 0.49f;
            float t = 0f;
            while (!c.Active && t < 40f) { yield return null; t += Game.Dt; }
            Check("mt_started", c.Active && Game.I.cinematic, t.ToString("0.0"));
            yield return new WaitForSecondsRealtime(1f);
            c.Skip();
            yield return new WaitForSecondsRealtime(0.3f);
            Check("mt_end", b.Phase == 2 && !Game.I.cinematic && Mathf.Approximately(boss.radius / r0, 1.15f) && Sfx.CurrentTrack == "boss_climax", boss.radius / r0 + " " + Sfx.CurrentTrack);
            int n = b.Log.Count; var ui = k.Ult(BTier.I).id; t = 0f;
            while (t < 10f && !b.Log.Skip(n).Contains(ui)) { yield return null; t += Game.Dt; }
            Check("mt_ult_next", b.Log.Skip(n).Contains(ui), string.Join(",", b.Log.Skip(n)));
            c.testMode = false;
        }

        IEnumerator T_endshot_lose()   // screenshots: the moment of death, then the run summary
        {
            yield return Arena(0, 12, 0); var g = Game.I;
            g.endSeq.testMode = true; g.OnPlayerDeath();
            yield return new WaitForSecondsRealtime(1.2f); g.bot.Shot("end_death");
            yield return new WaitForSecondsRealtime(0.9f); g.bot.Shot("end_death_title");
            float w = 0f; while (g.ui.screen != UIScreen.GameOver && w < 8f) { yield return null; w += Time.unscaledDeltaTime; }
            Check("end_lose_summary", g.ui.screen == UIScreen.GameOver && Mathf.Approximately(Time.timeScale, 1f), Time.timeScale.ToString());
            yield return new WaitForSecondsRealtime(0.9f); g.bot.Shot("end_lose");
            yield return new WaitForSecondsRealtime(0.3f);   // the capture happens at the end of the frame: do not let the next test start first
            g.endSeq.testMode = false;
        }

        IEnumerator T_endshot_win()
        {
            yield return Arena(0, 20, 0); var g = Game.I;
            g.endSeq.testMode = true; g.endSeq.Play(true, () => g.FinishRun(true));
            yield return new WaitForSecondsRealtime(1.0f); g.bot.Shot("end_win_seq");
            yield return new WaitForSecondsRealtime(1.0f); g.bot.Shot("end_win_title");
            float w = 0f; while (g.ui.screen != UIScreen.GameOver && w < 8f) { yield return null; w += Time.unscaledDeltaTime; }
            Check("end_win_summary", g.ui.screen == UIScreen.GameOver && Mathf.Approximately(Time.timeScale, 1f), Time.timeScale.ToString());
            yield return new WaitForSecondsRealtime(0.9f); g.bot.Shot("end_win");
            yield return new WaitForSecondsRealtime(0.3f);   // the capture happens at the end of the frame: do not let the next test start first
            g.endSeq.testMode = false;
        }

        IEnumerator T_introshot()   // screenshot of the first-meeting boss card
        {
            yield return BossArena("b1", 0, 5); var b = boss.brain; var c = Game.I.cinema;
            c.testMode = true; Game.I.save.bossIntroSeen.Clear();
            c.OnBossSpawn(b);
            yield return new WaitForSecondsRealtime(1.3f);
            Game.I.bot.Shot("boss_intro");
            yield return new WaitForSecondsRealtime(1.5f);
            c.testMode = false;
        }

        IEnumerator T_cine_intro_first()   // card on the first meeting only, then remembered
        {
            yield return BossArena("b1", 0, 5); var b = boss.brain; var c = Game.I.cinema;
            c.testMode = true; Game.I.save.bossIntroSeen.Clear();
            c.OnBossSpawn(b);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("intro_first_card", c.CardUp && Game.I.cinematic, "");
            yield return new WaitForSecondsRealtime(2.4f);
            Check("intro_first_seen", !c.CardUp && Game.I.save.bossIntroSeen.Contains("b1"), "");
            yield return Sec(0.8f);
            c.OnBossSpawn(b);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("intro_second_none", !c.CardUp, "");
            c.testMode = false;
        }

        IEnumerator T_cine_intro_b4()   // the final boss shows its card every time
        {
            yield return BossArena("b4", 0, 20); var c = Game.I.cinema;
            c.testMode = true; Game.I.save.bossIntroSeen.Add("b4");
            c.OnBossSpawn(boss.brain);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("intro_b4_card", c.CardUp, "");
            c.Skip();
            yield return new WaitForSecondsRealtime(0.3f);
            Check("intro_b4_skip", !c.CardUp && !Game.I.cinematic, "");
            c.testMode = false;
        }

        IEnumerator T_cine_oldsave()   // a save without the field (null list) loads and shows the intro for an alternate boss
        {
            yield return BossArena("b1b", 0, 5); var c = Game.I.cinema;
            c.testMode = true; Game.I.save.bossIntroSeen = null;
            Check("oldsave_needs", BossCinema.NeedsIntro("b1b"), "");
            c.OnBossSpawn(boss.brain);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("oldsave_card", c.CardUp, "");
            c.Skip();
            yield return new WaitForSecondsRealtime(0.3f);
            Check("oldsave_seen", Game.I.save.bossIntroSeen != null && Game.I.save.bossIntroSeen.Contains("b1b"), "");
            c.testMode = false;
        }

        IEnumerator T_shot_intro()   // look at the card: b1 and the long-named b4 in the current language
        {
            foreach (var id in new[] { "b1", "b4" })
            {
                yield return BossArena(id, 0, id == "b1" ? 5 : 20); var c = Game.I.cinema;
                c.testMode = true; Game.I.save.bossIntroSeen.Remove(id);
                c.OnBossSpawn(boss.brain);
                yield return new WaitForSecondsRealtime(1.4f);
                Game.I.bot.Shot("intro_" + id + "_l" + Loc.Lang);
                yield return new WaitForSecondsRealtime(0.4f);
                c.Skip();
                yield return new WaitForSecondsRealtime(0.3f);
                c.testMode = false;
            }
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

        // ---------------------------------------------------------------- task 5 signature hooks and cleanup
        int Summoned(BossBrain b, string defId = null) => Enemy.All.Count(e => e != null && !e.dead && e.summoner == b && (defId == null || e.def.id == defId));
        List<Enemy> SummonedList(BossBrain b, string defId) => Enemy.All.Where(e => e != null && !e.dead && e.summoner == b && e.def.id == defId).ToList();
        static void KillEnemy(Enemy e) { if (e != null && !e.dead) e.Damage(e.hp + 1f, false, Vector2.zero, 0f, null); }

        /// <summary>Waits until the brain has no running move (or timeout).</summary>
        IEnumerator WaitIdle(BossBrain b, float timeout)
        {
            float t = 0f;
            while (b != null && b.Current != null && t < timeout) { yield return null; t += Game.Dt; }
        }

        IEnumerator T_boss_hook_clones()
        {
            yield return BossPrep("b3", 0, 15, new Vector2(0, 5)); var b = boss.brain;
            int pr0 = Projectile.CountHostile();
            yield return Fire(b, "ma_anh_huyet_y"); yield return Sec(1.8f);   // clones after 0.7 s, then the shared ring after 0.7 s more
            int c = BossHooks.Count(b, "clone");
            Check("clones_two", c == 2, c.ToString());
            Check("clones_outline", boss.Highlighted, "");
            bool spriteOnly = BossHooks.Objects(b, "clone").All(o => o.GetComponentInChildren<Enemy>() == null && o.GetComponentInChildren<Collider2D>() == null && o.transform.IsChildOf(BossHooks.Root));
            Check("clones_sprite_only", c > 0 && spriteOnly, "");
            Check("clones_fire", Projectile.CountHostile() - pr0 >= 12, (Projectile.CountHostile() - pr0).ToString());
            yield return Sec(6f);
            Check("clones_gone", BossHooks.Count(b, "clone") == 0 && !boss.Highlighted, BossHooks.Count(b, "clone").ToString());
            BossHazard.ClearAll(); Projectile.ClearAll(false);
        }

        IEnumerator T_boss_hook_cocoons()
        {
            yield return BossPrep("b2b", 0, 10, new Vector2(0, 5)); var b = boss.brain; var r = Game.I.run;
            Game.I.player.ReduceSkillCd(-999f);   // no Thần Thông cast may break the cocoon that must hatch
            yield return Fire(b, "ken_doc"); yield return Sec(0.9f);
            var cs = SummonedList(b, "cocoon");
            Check("cocoon_two", cs.Count == 2, cs.Count.ToString());
            Check("cocoon_hp", cs.Count > 0 && cs.All(e => Mathf.Abs(e.maxHp - 0.02f * boss.maxHp) < 0.01f && e.def.beh == EBeh.Static && e.transform.IsChildOf(BossHooks.Root)), cs.Count > 0 ? cs[0].maxHp + " vs " + 0.02f * boss.maxHp : "-");
            int kills0 = r.kills, orbs0 = Orb.All.Count;
            if (cs.Count > 0) KillEnemy(cs[0]);
            yield return null;
            Check("cocoon_no_credit", r.kills == kills0 && Orb.All.Count == orbs0, "kills " + (r.kills - kills0) + " orbs " + (Orb.All.Count - orbs0));
            yield return Sec(3.4f);
            int sp = Summoned(b, "spiderling");
            Check("cocoon_hatch", sp == 2 && Summoned(b, "cocoon") == 0, "spiders " + sp + " cocoons " + Summoned(b, "cocoon"));
            // cocoon cap 3: three casts in a row
            yield return BossPrep("b2b", 0, 10, new Vector2(0, 5)); b = boss.brain;
            int maxC = 0;
            for (int cast = 0; cast < 3; cast++)
            {
                b.Hold = false; b.ForceMove("ken_doc"); int n0 = b.Log.Count; float w = 0f;
                while (w < 3f && (b.Log.Count == n0 || b.Current != null)) { yield return null; w += Game.Dt; maxC = Mathf.Max(maxC, Summoned(b, "cocoon")); }
                b.Hold = true;
            }
            Check("cocoon_cap", maxC == 3, maxC.ToString());
            // spiderling cap 6: Vạn Trùng Phệ, 6 edge cocoons x 2
            yield return BossPrep("b2b", 0, 10, new Vector2(0, 5)); b = boss.brain;
            yield return Fire(b, "van_trung_phe"); yield return Sec(1.3f);
            int edgeC = Summoned(b, "cocoon");
            Check("cocoon_edge_six", edgeC == 6, edgeC.ToString());
            int maxS = 0; float t = 0f;
            while (t < 4f) { yield return null; t += Game.Dt; maxS = Mathf.Max(maxS, Summoned(b, "spiderling")); }
            Check("spider_cap", maxS == 6, maxS.ToString());
            BossHazard.ClearAll();
        }

        IEnumerator T_boss_hook_flags()
        {
            yield return BossPrep("b2", 0, 10, new Vector2(0, 0)); var b = boss.brain;
            yield return Fire(b, "phan_thien_chien_ky"); yield return Sec(1.5f);
            var fl = SummonedList(b, "war_flag");
            Check("flags_four", fl.Count == 4, fl.Count.ToString());
            Check("flags_hp", fl.Count > 0 && fl.All(e => Mathf.Abs(e.maxHp - 0.04f * boss.maxHp) < 0.01f), fl.Count > 0 ? fl[0].maxHp.ToString("0") : "-");
            Check("flags_corners", fl.Count > 0 && fl.All(e => Mathf.Abs(Mathf.Abs(e.Pos.x) - 13f) < 0.01f && Mathf.Abs(Mathf.Abs(e.Pos.y) - 7f) < 0.01f), "");
            System.Func<Enemy, int> beams = f => BossHazard.All.Count(h => h != null && h.anchor == f && h.Live && h.dmg > 0f);
            Check("flags_beams", fl.Count == 4 && fl.All(f => beams(f) == 1), string.Join(",", fl.Select(f => beams(f))));
            if (fl.Count == 4)
            {
                var dead = fl[0];
                KillEnemy(dead); yield return null;
                Check("flag_beam_off", BossHazard.All.Count(h => h != null && h.anchor == dead) == 0, "");
                Check("flag_others_live", fl.Skip(1).All(f => !f.dead && beams(f) == 1), string.Join(",", fl.Skip(1).Select(f => beams(f))));
            }
            yield return WaitIdle(b, 10f); yield return Sec(0.3f);
            Check("flags_end", Summoned(b, "war_flag") == 0 && !BossHazard.All.Any(h => h != null && h.moveId == "phan_thien_chien_ky"), Summoned(b, "war_flag").ToString());
            BossHazard.ClearAll();
        }

        IEnumerator T_boss_hook_ghosts()
        {
            yield return BossPrep("b4", 0, 20, new Vector2(0, 5)); var b = boss.brain;
            yield return Fire(b, "van_ma_quy_tong");
            float t = 0f; int maxG = 0; float ringDmg = -1f;
            while (t < 14f && (b.Current != null || t < 1f))
            {
                yield return null; t += Game.Dt; maxG = Mathf.Max(maxG, BossHooks.Count(b, "ghost"));
                var pr = Projectile.All.FirstOrDefault(p => p != null && !p.Dead && p.Hostile && p.moveId == "huyet_luan");
                if (pr != null && ringDmg < 0f) ringDmg = pr.Dmg;
            }
            var want = new[] { "ma_nhan_quet", "huyet_anh_luot", "viem_hoan", "doc_ty_troi", "huyet_luan" }.Select(id => id + " x0.70").ToList();
            Check("ghosts_order", b.HookLog.SequenceEqual(want), string.Join(",", b.HookLog));
            Check("ghosts_seen", maxG >= 1, maxG.ToString());
            float exp = boss.dmg * 0.55f * 0.7f;
            Check("ghosts_dmg", Mathf.Abs(ringDmg - exp) < 0.01f, ringDmg.ToString("0.00") + " vs " + exp.ToString("0.00"));
            yield return Sec(0.6f);
            Check("ghosts_gone", BossHooks.Count(b, "ghost") == 0, BossHooks.Count(b, "ghost").ToString());
            BossHazard.ClearAll(); Projectile.ClearAll(false);
        }

        /// <summary>Everything a boss fight leaves: hazards, boss bullets, hook objects, summoned or breakable minions.</summary>
        string Leftovers() => "hz " + BossHazard.All.Count(h => h != null) + " pr " + Projectile.CountHostile() + " hook " + BossHooks.Count() + " min " + Enemy.All.Count(e => e != null && !e.dead && e.summoner != null);
        bool Clean() => BossHazard.All.Count(h => h != null) == 0 && Projectile.CountHostile() == 0 && BossHooks.Count() == 0 && Enemy.All.Count(e => e != null && !e.dead && e.summoner != null) == 0;

        IEnumerator T_boss_clean_mid()   // Review Focus 2: boss dies halfway through its set II ultimate
        {
            foreach (var cfg in new[] { ("b1", 5), ("b1b", 5), ("b2", 10), ("b2b", 10), ("b3", 15), ("b4", 19) })   // b4 at 19: wave 20 would finish (and win) the test run
            {
                yield return BossPrep(cfg.Item1, 5, cfg.Item2, new Vector2(0, 5)); var b = boss.brain; var r = Game.I.run;
                if (b.kit.Move("ken_doc") != null) { yield return Fire(b, "ken_doc"); yield return WaitIdle(b, 3f); b.Hold = false; }
                if (b.kit.Move("ma_anh_huyet_y") != null) { yield return Fire(b, "ma_anh_huyet_y"); yield return WaitIdle(b, 3f); b.Hold = false; }
                var u = b.kit.Ult(BTier.II);
                yield return Fire(b, u.id); yield return Sec(1.6f);
                int chests0 = r.chests + Orb.All.Count(o => o != null && o.IsChest);
                Check("clean_mid_running_" + cfg.Item1, b.Current == u, b.Current != null ? b.Current.id : "none");
                KillEnemy(boss);
                yield return new WaitForSecondsRealtime(0.5f);   // the boss kill ends the wave, so game time stops
                Check("clean_mid_" + cfg.Item1, Clean(), Leftovers());
                yield return new WaitForSecondsRealtime(2.2f);
                Check("clean_mid_chest_" + cfg.Item1, Game.I.state != GState.Playing && r.chests >= chests0 + 1, Game.I.state + " chests " + r.chests + " vs " + chests0);
                Check("clean_mid_still_" + cfg.Item1, Clean(), Leftovers());
            }
        }

        IEnumerator T_boss_clean_over()   // the player dies mid-ult: run over, then a fresh run is clean
        {
            yield return BossPrep("b2", 5, 10, new Vector2(0, 5)); var b = boss.brain;
            yield return Fire(b, "phan_thien_chien_ky"); yield return Sec(1.6f);
            Check("clean_over_running", b.Current != null && Summoned(b, "war_flag") > 0, Leftovers());
            tankHold = false; var g = Game.I; g.player.god = false; g.run.hp = 0f; g.OnPlayerDeath();
            yield return new WaitForSecondsRealtime(2.6f);
            Check("clean_over_state", g.state == GState.Over, g.state.ToString());
            bool ok = true;
            try { g.StartRun(0, 0); } catch (System.Exception ex) { ok = false; Debug.Log("[AUTOTEST] clean_over exception " + ex); }
            yield return null; yield return null;
            Check("clean_over_restart", ok && g.state == GState.Playing, g.state.ToString());
            Check("clean_over_clean", Clean() && Enemy.All.Count(e => e != null && e.boss) == 0, Leftovers());
        }

        /// <summary>Forces a hook move: the first damage (a live damaging hazard, a hit or a new boss bullet) comes after a telegraph that was on
        /// screen at least minTele; LastTele at that moment is at least minTele; every hazard and hook object stays inside the arena; after the move, no hook objects are left.</summary>
        IEnumerator HookPrim(string bossId, int wave, string moveId, float minTele)
        {
            yield return BossPrep(bossId, 0, wave, new Vector2(0, 5)); var b = boss.brain; b.LastTele = -1f;
            int ph0 = BossHazard.PlayerHits, bh0 = BossBrain.HitCount, pr0 = Projectile.CountHostile();
            yield return Fire(b, moveId);
            bool teleSeen = false, dmgSeen = false, teleFirst = false, inside = true; float t = 0f, teleAt = -1f, dmgAt = -1f, teleAtDmg = -1f; string outside = "";
            const float Tol = 0.35f;
            while (t < 16f && (b.Current != null || t < 0.5f))
            {
                yield return null; t += Game.Dt;
                if (!teleSeen && BossHazard.All.Any(h => h != null && h.owner == boss && h.Telegraphing)) { teleSeen = true; teleAt = t; }
                if (!dmgSeen && (BossHazard.All.Any(h => h != null && h.owner == boss && h.Live && h.dmg > 0f) || BossHazard.PlayerHits != ph0 || BossBrain.HitCount != bh0 || Projectile.CountHostile() > pr0))
                { dmgSeen = true; dmgAt = t; teleFirst = teleSeen; teleAtDmg = b.LastTele; }
                foreach (var h in BossHazard.All)
                    if (h != null && (Mathf.Abs(h.pos.x) > VK.Arena.HalfW + Tol || Mathf.Abs(h.pos.y) > VK.Arena.HalfH + Tol)) { inside = false; outside = "hz " + h.moveId + " " + h.pos; }
                foreach (var o in BossHooks.Objects(b, null))
                    if (Mathf.Abs(o.transform.position.x) > VK.Arena.HalfW + Tol || Mathf.Abs(o.transform.position.y) > VK.Arena.HalfH + Tol) { inside = false; outside = o.kind + " " + o.transform.position; }
            }
            Check("hook_" + moveId + "_ran", b.Log.Contains(moveId) && dmgSeen && b.Current == null, "t=" + t.ToString("0.0") + " dmg " + dmgSeen);
            Check("hook_" + moveId + "_tele_first", teleFirst && dmgAt - teleAt >= minTele - 0.05f, "shown " + (dmgAt - teleAt).ToString("0.00"));
            Check("hook_" + moveId + "_tele_floor", teleAtDmg >= minTele - 0.001f, teleAtDmg.ToString("0.00"));
            Check("hook_" + moveId + "_inside", inside, outside);
            yield return Sec(0.3f);
            Check("hook_" + moveId + "_no_leftover", BossHooks.Count(b) == 0 && Summoned(b, "war_flag") == 0, BossHooks.Count(b).ToString());
            BossHazard.ClearAll(); Projectile.ClearAll(false);
        }

        IEnumerator T_boss_hook_ult_b1b_bats() { yield return HookPrim("b1b", 5, "van_buc_trieu", 1f); }
        IEnumerator T_boss_hook_ult_b2_firecity() { yield return HookPrim("b2", 10, "liet_hoa_thanh", 1f); }
        IEnumerator T_boss_hook_ult_b2_flags() { yield return HookPrim("b2", 10, "phan_thien_chien_ky", 1f); }
        IEnumerator T_boss_hook_ult_b2b_sea() { yield return HookPrim("b2b", 10, "doc_hai", 1f); }
        IEnumerator T_boss_hook_ult_b3_clones() { yield return HookPrim("b3", 15, "huyet_anh_van_sat", 1f); }
        IEnumerator T_boss_hook_ult_b4_hand() { yield return HookPrim("b4", 20, "thien_ma_giang_the", 1f); }
        IEnumerator T_boss_hook_ult_b4_ghosts() { yield return HookPrim("b4", 20, "van_ma_quy_tong", 1f); }
        IEnumerator T_boss_hook_b4_gates() { yield return HookPrim("b4", 20, "hu_khong_mon", KiepNum.TeleFloor(0)); }
        IEnumerator T_boss_hook_ult_b4_heaven()
        {
            yield return HookPrim("b4", 20, "thien_dao_tich_diet", KiepNum.TeleFloor(0));   // stage 1 keeps its own telegraph; stage 2 is the 1 s+ part
            Check("hook_heaven_stage2_tele", boss != null && boss.brain.LastTele >= 1.5f - 0.001f, boss != null ? boss.brain.LastTele.ToString("0.00") : "-");
        }

        IEnumerator T_boss_hook_heaven_safe()   // stage 2: hit outside the safe circle, safe inside it
        {
            for (int pass = 0; pass < 2; pass++)
            {
                yield return BossPrep("b4", 0, 20, new Vector2(0, 5)); var b = boss.brain;
                yield return Fire(b, "thien_dao_tich_diet");
                float t = 0f; BossHazard strike = null;
                while (t < 10f && strike == null) { yield return null; t += Game.Dt; strike = BossHazard.All.FirstOrDefault(h => h != null && h.holes != null && h.Telegraphing); }
                if (strike == null) { Check("heaven_strike", false, "none"); continue; }
                int ph0 = BossHazard.PlayerHits;
                Place(pass == 0 ? Vector2.zero : new Vector2(6f, 3f));
                float w = 0f;   // keep the player there: the bot would walk into the safe circle
                while (b.Current != null && w < 6f) { Game.I.player.transform.position = pass == 0 ? Vector2.zero : new Vector2(6f, 3f); yield return null; w += Game.Dt; }
                int hits = BossHazard.PlayerHits - ph0;
                Check(pass == 0 ? "heaven_safe_inside" : "heaven_hit_outside", hits == (pass == 0 ? 0 : 1), hits.ToString());
            }
            BossHazard.ClearAll();
        }

        IEnumerator T_boss_hook_sea_safe()   // a drifting safe patch protects, the flood elsewhere ticks
        {
            yield return BossPrep("b2b", 0, 10, new Vector2(0, 5)); var b = boss.brain;
            yield return Fire(b, "doc_hai");
            BossHazard flood = null; float t = 0f;
            while (t < 3f && flood == null) { yield return null; t += Game.Dt; flood = BossHazard.All.FirstOrDefault(h => h != null && h.holes != null && h.moveId == "doc_hai"); }
            Check("sea_patches", flood != null && flood.holes.Count == 3 && Mathf.Abs(flood.holeR - 2f) < 0.01f, flood == null ? "none" : flood.holes.Count.ToString());
            if (flood == null) yield break;
            int ph0 = BossHazard.PlayerHits; tankHold = false;
            Vector2 start = flood.holes[0]; t = 0f;
            while (t < 3.5f) { yield return null; t += Game.Dt; if (flood != null) Game.I.player.transform.position = flood.holes[0]; }
            Check("sea_safe_in_patch", BossHazard.PlayerHits == ph0, (BossHazard.PlayerHits - ph0).ToString());
            Check("sea_patch_drifts", flood != null && (flood.holes[0] - start).magnitude > 1f, flood != null ? (flood.holes[0] - start).magnitude.ToString("0.0") : "-");
            Vector2 far = new Vector2(VK.Arena.HalfW - 0.5f, VK.Arena.HalfH - 0.5f);
            if (flood != null && flood.holes.Any(h => (h - far).magnitude < 3f)) far = -far;
            Place(far);
            yield return Sec(1.2f);
            Check("sea_flood_ticks", BossHazard.PlayerHits - ph0 >= 2, (BossHazard.PlayerHits - ph0).ToString());
            BossHazard.ClearAll();
        }

        // ---------------------------------------------------------------- task 6: every move of every boss, hit caps, screenshots
        static readonly string[] BossIds = { "b1", "b1b", "b2", "b2b", "b3", "b4" };
        static int BossWave(string id) => id == "b1" || id == "b1b" ? 5 : id == "b2" || id == "b2b" ? 10 : id == "b3" ? 15 : 20;
        static string F3(float v) => v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);

        // [BOSSHIT] tracking: largest single hit (fraction of the player's max hp), their sum, and the move ids
        float hitMax, hitSum; int hitN; readonly List<string> hitIds = new List<string>();
        void OnBossLog(string msg, string st, LogType t)
        {
            if (!msg.StartsWith("[BOSSHIT]")) return;
            int i = msg.LastIndexOf("pct=");
            if (i < 0 || !float.TryParse(msg.Substring(i + 4).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) return;
            hitMax = Mathf.Max(hitMax, v); hitSum += v; hitN++;
            var parts = msg.Split(' '); if (parts.Length > 1) hitIds.Add(parts[1]);
        }
        void HitTrack(bool on) { Application.logMessageReceived -= OnBossLog; if (on) Application.logMessageReceived += OnBossLog; HitReset(); }
        void HitReset() { hitMax = hitSum = 0f; hitN = 0; hitIds.Clear(); }

        /// <summary>Clears what is left, puts the boss at (0,5), forces id, holds the brain once it started and waits (bounded) until the move is over.</summary>
        IEnumerator Exec(BossBrain b, string id, float timeout)
        {
            BossHazard.ClearAll(); Projectile.ClearAll(false); BossHooks.ClearOwner(b);
            boss.transform.position = new Vector2(0f, 5f);
            b.Hold = false; b.ForceMove(id);
            for (int i = 0; i < 6 && b.Current == null; i++) yield return null;
            b.Hold = true;
            yield return WaitIdle(b, timeout);
        }

        /// <summary>Phase 2 without playing the queued ultimates: hp to 49%, the queue is emptied until the transition has run, brain held.</summary>
        IEnumerator ForcePhase2(BossBrain b)
        {
            BossHazard.ClearAll(); Projectile.ClearAll(false); BossHooks.ClearOwner(b);
            var q = (Queue<KeyValuePair<BossMove, float>>)typeof(BossBrain).GetField("ults", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(b);
            b.Hold = true; boss.hp = boss.maxHp * 0.49f;
            float t = 0f;
            while (b.Phase != 2 && t < 5f) { q.Clear(); yield return null; t += Game.Dt; }
            q.Clear();
            while (b.Staggered && t < 8f) { yield return null; t += Game.Dt; }
            q.Clear(); b.Hold = true;
            BossHazard.ClearAll(); Projectile.ClearAll(false); boss.transform.position = new Vector2(0f, 5f);
            Check("p2_forced_" + b.kit.bossId, b.Phase == 2 && !b.Staggered, "t=" + t.ToString("0.0"));
        }

        static List<string> ExpectedLog(BossBrain b, BossMove m)
        {
            var l = new List<string> { m.id };
            foreach (var r in b.kit.moves) if (r.after == m.id && b.Unlocked(r)) l.Add(r.id);
            return l;
        }

        /// <summary>Forces m (its unlocked riders follow) with the tank player at the centre and checks: telegraph before the first damage and at least
        /// the floor (1 s for ultimate signature stages), no logged error, summon caps, Log, rider effects, everything gone again, hazards inside the arena, sprite pieces.</summary>
        IEnumerator MoveCheck(int k, BossMove m, string tag)
        {
            var b = boss.brain;
            BossHazard.ClearAll(); Projectile.ClearAll(false); BossHooks.ClearOwner(b);
            b.casts.Clear(); b.LastTele = -1f;
            boss.transform.position = new Vector2(0f, 5f);
            if (m.id == "ty_dan")   // Between needs two live pools (harmless ones)
                foreach (var pp in new[] { new Vector2(-7f, -4f), new Vector2(7f, 5f), new Vector2(8f, -6f) })
                { var pool = BossHazard.Spawn(boss, HShape.Circle, pp, 0.01f, 6f, 0f, Color.green, 0.5f); pool.radius = 1f; }
            if (m.id == "ty_dan") yield return Sec(0.15f);   // pools count once their 0.01 s telegraph is over
            var exp = ExpectedLog(b, m);
            int n0 = b.Log.Count, err0 = AutoTest.Errors, ph0 = BossHazard.PlayerHits, bh0 = BossBrain.HitCount, mins0 = BossMoves.Alive(b);
            HitReset();
            float floor = KiepNum.TeleFloor(k), need = m.ult && m.tele2 <= 0f ? 1f : floor;
            b.Hold = false; b.ForceMove(m.id);
            for (int i = 0; i < 6 && b.Current == null; i++) yield return null;
            b.Hold = true;
            float maxTele = -1f; bool teleSeen = false, dmgSeen = false, teleFirst = false, effect = false, clean = false, inside = true;
            float t = 0f, teleAt = -1f, dmgAt = -1f, endT = -1f;
            int maxAlive = 0, maxSpider = 0, maxCocoon = 0, maxFlag = 0, peakSingle = 0, peakTotal = 0;
            string outside = "";
            var seen = new HashSet<string>();
            const float Tail = 5f, CleanMax = 16f, Tol = 0.35f;
            while (t < 60f)
            {
                yield return null; t += Game.Dt;
                if (endT < 0f && b.Current == null && b.Log.Count > n0) endT = t;
                maxTele = Mathf.Max(maxTele, b.LastTele);   // a later sub-move (fire city fans) may lower LastTele
                bool tele = false, live = false; int tot = 0, single = 0;
                foreach (var h in BossHazard.All)
                {
                    if (h == null) continue;
                    int pc = h.PieceCount; tot += pc; if (pc > single) single = pc;
                    if (!string.IsNullOrEmpty(h.moveId)) seen.Add(h.moveId);
                    if (h.owner == boss) { effect = true; if (h.Telegraphing) tele = true; if (h.Live && h.dmg > 0f) live = true; }
                    if (Mathf.Abs(h.pos.x) > VK.Arena.HalfW + Tol || Mathf.Abs(h.pos.y) > VK.Arena.HalfH + Tol) { inside = false; outside = "hz " + h.moveId + " " + h.pos; }
                }
                foreach (var o in BossHooks.Objects(b, null))
                    if (Mathf.Abs(o.transform.position.x) > VK.Arena.HalfW + Tol || Mathf.Abs(o.transform.position.y) > VK.Arena.HalfH + Tol) { inside = false; outside = o.kind + " " + o.transform.position; }
                foreach (var p in Projectile.All) if (p != null && !p.Dead && p.Hostile && !string.IsNullOrEmpty(p.moveId)) seen.Add(p.moveId);
                int pr = Projectile.CountHostile(), mins = BossMoves.Alive(b);
                if (pr > 0 || mins > 0 || BossHooks.Count(b) > 0) effect = true;
                if (!teleSeen && tele) { teleSeen = true; teleAt = t; }
                if (!dmgSeen && (live || BossHazard.PlayerHits != ph0 || BossBrain.HitCount != bh0 || pr > 0 || mins > mins0)) { dmgSeen = true; dmgAt = t; teleFirst = teleSeen; }
                maxAlive = Mathf.Max(maxAlive, mins);
                maxSpider = Mathf.Max(maxSpider, BossMoves.Alive(b, "spiderling"));
                maxCocoon = Mathf.Max(maxCocoon, BossMoves.Alive(b, "cocoon"));
                maxFlag = Mathf.Max(maxFlag, BossMoves.Alive(b, "war_flag"));
                peakSingle = Mathf.Max(peakSingle, single); peakTotal = Mathf.Max(peakTotal, tot);
                clean = BossHazard.All.Count(h => h != null) == 0 && pr == 0 && BossHooks.Count() == 0;
                if (endT >= 0f && t >= endT + Tail && (clean || t >= endT + CleanMax)) break;
                if (endT < 0f && t > 40f) break;
            }
            string det = "t=" + t.ToString("0.0");
            Check(tag + "_ended", endT >= 0f, det);
            Check(tag + "_log", b.Log.Skip(n0).SequenceEqual(exp), string.Join(",", b.Log.Skip(n0)) + " vs " + string.Join(",", exp));
            Check(tag + "_effect", dmgSeen && effect, "dmg " + dmgSeen + " effect " + effect);
            Check(tag + "_tele_first", teleFirst && dmgAt - teleAt >= need - 0.06f, "shown " + (dmgAt - teleAt).ToString("0.00") + " need " + need.ToString("0.00"));
            Check(tag + "_tele_floor", maxTele >= (m.ult ? 1f : floor) - 0.001f, maxTele.ToString("0.00"));
            foreach (var r in b.kit.moves)
                if (r.after == m.id && b.Unlocked(r) && r.id != "phun_moi") Check(tag + "_rider_" + r.id, seen.Contains(r.id), string.Join(",", seen));
            int cap = b.kit.summonCap;
            Check(tag + "_caps", maxAlive <= cap && maxSpider <= cap && maxCocoon <= (m.id == "ken_doc" ? BossHooks.CocoonCap : 6) && maxFlag <= 4,
                "alive " + maxAlive + " spider " + maxSpider + " cocoon " + maxCocoon + " flag " + maxFlag + " cap " + cap);
            Check(tag + "_noerr", AutoTest.Errors == err0, (AutoTest.Errors - err0).ToString());
            Check(tag + "_inside", inside, outside);
            Check(tag + "_clean", clean, Leftovers());
            Debug.Log("[PERF] " + tag + " single=" + peakSingle + " total=" + peakTotal);
            Check(tag + "_pieces", peakSingle <= 100 && peakTotal <= 300, "single " + peakSingle + " total " + peakTotal);
            Debug.Log("[MOVEHIT] " + tag + " max=" + F3(hitMax) + " total=" + F3(hitSum) + " hits=" + hitN);
            BossHooks.ClearOwner(b); BossHazard.ClearAll(); Projectile.ClearAll(false);
        }

        /// <summary>Every move unlocked at this Kiếp: set I and base in phase 1, then (phase 2 forced) the phase 2 move, the set II rider hosts and the set II ultimate.</summary>
        IEnumerator MovesScenario(string id, int k)
        {
            yield return BossPrep(id, k, BossWave(id), new Vector2(0f, 5f)); var b = boss.brain; var kit = b.kit;
            b.Hold = true; var dir = Game.I.dir; dir.TeleOnly = true; dir.enabled = true; HitTrack(true);
            int sets = BossBrain.Sets(k); string pre = "mv_" + id + "_k" + k + "_";
            foreach (var m in kit.moves.Where(x => x.after == null && (x.tier == BTier.Base || (x.tier == BTier.I && sets >= 1))).ToList())
                yield return MoveCheck(k, m, pre + m.id);
            yield return ForcePhase2(b);
            foreach (var m in kit.moves.Where(x => x.after == null && b.Unlocked(x) && (x.tier == BTier.P2 || x.tier == BTier.II || kit.moves.Any(r => r.after == x.id && b.Unlocked(r)))).ToList())
                yield return MoveCheck(k, m, pre + "p2_" + m.id);
            HitTrack(false); dir.TeleOnly = false; dir.enabled = false;
            BossHazard.ClearAll(); Projectile.ClearAll(false);
        }

        IEnumerator T_boss_moves_b1_k0() { yield return MovesScenario("b1", 0); }
        IEnumerator T_boss_moves_b1_k3() { yield return MovesScenario("b1", 3); }
        IEnumerator T_boss_moves_b1_k5() { yield return MovesScenario("b1", 5); }
        IEnumerator T_boss_moves_b1b_k0() { yield return MovesScenario("b1b", 0); }
        IEnumerator T_boss_moves_b1b_k3() { yield return MovesScenario("b1b", 3); }
        IEnumerator T_boss_moves_b1b_k5() { yield return MovesScenario("b1b", 5); }
        IEnumerator T_boss_moves_b2_k0() { yield return MovesScenario("b2", 0); }
        IEnumerator T_boss_moves_b2_k3() { yield return MovesScenario("b2", 3); }
        IEnumerator T_boss_moves_b2_k5() { yield return MovesScenario("b2", 5); }
        IEnumerator T_boss_moves_b2b_k0() { yield return MovesScenario("b2b", 0); }
        IEnumerator T_boss_moves_b2b_k3() { yield return MovesScenario("b2b", 3); }
        IEnumerator T_boss_moves_b2b_k5() { yield return MovesScenario("b2b", 5); }
        IEnumerator T_boss_moves_b3_k0() { yield return MovesScenario("b3", 0); }
        IEnumerator T_boss_moves_b3_k3() { yield return MovesScenario("b3", 3); }
        IEnumerator T_boss_moves_b3_k5() { yield return MovesScenario("b3", 5); }
        IEnumerator T_boss_moves_b4_k0() { yield return MovesScenario("b4", 0); }
        IEnumerator T_boss_moves_b4_k3() { yield return MovesScenario("b4", 3); }
        IEnumerator T_boss_moves_b4_k5() { yield return MovesScenario("b4", 5); }

        /// <summary>Max hp of a normal smart-bot run (-smart -meta 2) at that boss wave: about 39 / 100 / 230 / 450 at waves 5 / 10 / 15 / 20 (Logs f_k3 wave lines).
        /// The level-1 hp of a bare test run says nothing about a wave 20 boss hit.</summary>
        void SetRefHp(int wave)
        {
            var r = Game.I.run; float target = wave <= 5 ? 39f : wave <= 10 ? 100f : wave <= 15 ? 230f : 450f;
            r.levelStats[Stat.MaxHP] += target - r.MaxHP; r.Recalc(); r.stats[Stat.Dodge] = 0f; r.hp = 1e6f;
        }

        // ---------------------------------------------------------------- hit caps (data for the Task 11 tuning pass)
        /// <summary>Tank player stands still through the ultimates unlocked at each Kiếp, the Ult(II) at every Kiếp, and the biggest single moves.
        /// Logs [HITCAP] id k=K move=M max=P; only the hard-safety bound (not one-shot) is asserted. The spec section 1 caps are tuning targets.</summary>
        IEnumerator T_boss_hitcap()
        {
            HitTrack(true);
            foreach (var id in BossIds)
                foreach (int k in new[] { 0, 3, 5 })
                {
                    yield return BossPrep(id, k, BossWave(id), new Vector2(0f, 5f)); var b = boss.brain; var kit = b.kit;
                    Game.I.dir.TeleOnly = true; Game.I.dir.enabled = true;
                    var ids = new List<string>();
                    foreach (var tr in k == 0 ? new[] { BTier.Base, BTier.II } : k == 3 ? new[] { BTier.I, BTier.II } : new[] { BTier.Base, BTier.I, BTier.II })
                    { var u = kit.Ult(tr); if (u != null && !ids.Contains(u.id)) ids.Add(u.id); }
                    if (id == "b1") ids.Add("doc_nhan_xung");
                    float max = 0f; string maxMove = "";
                    foreach (var mid in ids)
                    {
                        SetRefHp(BossWave(id));
                        tankPos = mid == "thien_dao_tich_diet" ? new Vector2(6f, 3f) : mid == "liet_hoa_thanh" ? new Vector2(9f, 0f) : Vector2.zero;   // stage 2 / the fire city are safe at the centre
                        Game.I.player.transform.position = tankPos;
                        HitReset();
                        yield return Exec(b, mid, 40f); yield return Sec(2.5f);
                        Debug.Log("[HITCAP] " + id + " k=" + k + " move=" + mid + " max=" + F3(hitMax) + " total=" + F3(hitSum) + " hits=" + hitN);
                        if (hitMax > max) { max = hitMax; maxMove = mid; }
                        Check("hitcap_safe_" + id + "_k" + k + "_" + mid, hitMax <= 0.9f, F3(hitMax));
                    }
                    Debug.Log("[HITCAP] " + id + " k=" + k + " max=" + F3(max) + " move=" + maxMove);
                    tankPos = Vector2.zero;
                    BossHooks.ClearOwner(b); BossHazard.ClearAll(); Projectile.ClearAll(false);
                    Game.I.dir.TeleOnly = false; Game.I.dir.enabled = false;
                }
            HitTrack(false);
        }

        // ---------------------------------------------------------------- task 10: the bot dodges boss hazards
        /// <summary>Smart bot (not passive, not god) fights each boss at Kiếp 0 for 25 s with and without dodging; hazard hits must drop to <= 40%.</summary>
        IEnumerator T_bot_dodge()
        {
            int sumNo = 0, sumDodge = 0;
            foreach (var id in BossIds)
            {
                var hits = new int[2];
                for (int run = 0; run < 2; run++)
                {
                    yield return BossPrep(id, 0, BossWave(id), new Vector2(0f, 5f)); var b = boss.brain;
                    SetRefHp(BossWave(id)); tankHold = false; AutoTest.Passive = false; AutoTest.noDodge = run == 0;
                    Game.I.player.god = false; BossHazard.PlayerHits = 0;
                    float t = 0f;
                    while (t < 25f) { Game.I.run.hp = 1e6f; yield return null; t += Game.Dt; }
                    hits[run] = BossHazard.PlayerHits;
                    AutoTest.noDodge = false;
                    BossHooks.ClearOwner(b); BossHazard.ClearAll(); Projectile.ClearAll(false);
                }
                Debug.Log("[DODGE] " + id + " nodge=" + hits[0] + " dodge=" + hits[1]);
                Check("dodge_" + id, hits[1] <= hits[0], hits[1] + " vs " + hits[0]);
                sumNo += hits[0]; sumDodge += hits[1];
            }
            AutoTest.Passive = false; AutoTest.noDodge = false;
            Check("dodge_total", sumNo < 5 || sumDodge <= 0.4f * sumNo, sumDodge + " vs " + sumNo);
        }

        // ---------------------------------------------------------------- riders and primitives that had no test yet
        IEnumerator T_boss_extra_blinkecho()   // Huyết Phản: an afterimage slashes the same spot again after the gap, with the rider damage
        {
            yield return BossPrep("b3", 5, 15, new Vector2(0f, 5f)); var b = boss.brain;
            yield return ForcePhase2(b); HitTrack(true);
            b.Hold = false; b.ForceMove("huyet_bo");
            for (int i = 0; i < 6 && b.Current == null; i++) yield return null;
            b.Hold = true;
            Vector2 slashPos = Vector2.zero, echoPos = Vector2.zero; float echoDmg = -1f, echoTele = -1f, echoR = -1f, t = 0f; bool haveSlash = false, haveEcho = false;
            while (t < 8f && (b.Current != null || t < 0.3f))
            {
                yield return null; t += Game.Dt;
                var s = BossHazard.All.FirstOrDefault(h => h != null && h.moveId == "huyet_bo" && h.shape == HShape.Circle);
                if (s != null && !haveSlash) { haveSlash = true; slashPos = s.pos; }
                var e = BossHazard.All.FirstOrDefault(h => h != null && h.moveId == "huyet_phan");
                if (e != null && !haveEcho) { haveEcho = true; echoPos = e.pos; echoDmg = e.dmg; echoTele = e.tele; echoR = e.radius; PinAt(e.pos); }
            }
            yield return Sec(0.3f);
            Check("echo_log", b.Log.Skip(b.Log.Count - 2).SequenceEqual(new[] { "huyet_bo", "huyet_phan" }), string.Join(",", b.Log));
            Check("echo_same_spot", haveSlash && haveEcho && (echoPos - slashPos).magnitude < 0.01f && Mathf.Abs(echoR - 2.6f) < 0.01f, slashPos + " " + echoPos + " r" + echoR);
            Check("echo_dmg", Mathf.Abs(echoDmg - boss.dmg * 0.5f) < 0.01f, echoDmg.ToString("0.00") + " vs " + (boss.dmg * 0.5f).ToString("0.00"));
            Check("echo_tele", echoTele >= Mathf.Max(0.5f, KiepNum.TeleFloor(5)) - 0.001f, echoTele.ToString("0.00"));
            HitTrack(false); BossHazard.ClearAll();
        }

        IEnumerator T_boss_extra_phunmoi()   // Phun Mồi: every 2nd Nọc Phun cast spawns one spiderling, at most 3 alive from the move
        {
            yield return BossPrep("b2b", 5, 10, new Vector2(0f, 5f)); var b = boss.brain;
            yield return ForcePhase2(b);
            var dir = Game.I.dir; dir.TeleOnly = true; dir.enabled = true;
            var series = new List<int>(); int maxAll = 0;
            for (int c = 1; c <= 8; c++)
            {
                b.Hold = false; b.ForceMove("noc_phun");
                for (int i = 0; i < 6 && b.Current == null; i++) yield return null;
                b.Hold = true;
                float t = 0f;
                while (b.Current != null && t < 12f) { yield return null; t += Game.Dt; maxAll = Mathf.Max(maxAll, BossMoves.Alive(b)); }
                yield return Sec(2f);
                maxAll = Mathf.Max(maxAll, BossMoves.Alive(b));
                series.Add(Summoned(b, "spiderling"));
                boss.transform.position = new Vector2(0f, 5f);
            }
            int casts = b.casts.TryGetValue("noc_phun", out var cc) ? cc : -1;
            Check("phun_cadence", series.SequenceEqual(new[] { 0, 1, 1, 2, 2, 3, 3, 3 }), string.Join(",", series));
            Check("phun_casts", casts == 8, casts.ToString());
            Check("phun_cap", maxAll <= b.kit.summonCap, maxAll.ToString());
            dir.TeleOnly = false; dir.enabled = false;
            BossHooks.ClearOwner(b); BossHazard.ClearAll();
        }

        IEnumerator RunSynth(BossBrain b, BossMove m, float settle, List<BossHazard> got)
        {
            BossHazard.ClearAll(); b.Hold = true; boss.transform.position = new Vector2(0f, 5f);
            StartCoroutine(BossBrain.Flat(BossMoves.Run(b, m)));
            yield return Sec(settle);
            foreach (var h in BossHazard.All) if (h != null && h.moveId == m.id) got.Add(h);
        }

        static bool InArena(Vector2 p, float tol = 0.01f) => Mathf.Abs(p.x) <= VK.Arena.HalfW + tol && Mathf.Abs(p.y) <= VK.Arena.HalfH + tol;

        IEnumerator T_boss_extra_zones()   // layouts no kit row uses directly: geometry stays inside the arena (HalfW 17, HalfH 10)
        {
            yield return BossPrep("b2b", 0, 10, new Vector2(0f, 5f)); var b = boss.brain;
            float HW = VK.Arena.HalfW, HH = VK.Arena.HalfH;
            Check("arena_size", Mathf.Approximately(HW, 17f) && Mathf.Approximately(HH, 10f), HW + "x" + HH);
            foreach (int n in new[] { 1, 3, 4 })   // Quadrants: one rectangle per picked quadrant
            {
                var got = new List<BossHazard>();
                var m = new BossMove { id = "t_quad" + n, name = "t", kind = BMove.Zones, tier = BTier.Base, tele = 0.7f, dmg = 0.1f, layout = ZLayout.Quadrants, n = n };
                yield return RunSynth(b, m, 0.15f, got);
                var cells = new HashSet<string>(); bool geo = true;
                foreach (var h in got)
                {
                    geo &= h.shape == HShape.Bar && Mathf.Abs(h.len - HW) < 0.01f && Mathf.Abs(h.width - HH) < 0.01f && Mathf.Abs(h.angleDeg) < 0.01f;
                    geo &= h.pos.x >= -HW - 0.01f && h.pos.x + h.len <= HW + 0.01f && h.pos.y - h.width * 0.5f >= -HH - 0.01f && h.pos.y + h.width * 0.5f <= HH + 0.01f;
                    cells.Add((h.pos.x > -0.01f ? "R" : "L") + (h.pos.y > 0f ? "U" : "D"));
                }
                Check("zones_quadrants_" + n, got.Count == n && cells.Count == n && geo, got.Count + " cells " + cells.Count + " geo " + geo);
                yield return Sec(1.2f);
            }
            {   // EdgeRing: n points evenly round the edge, inset 1.2
                var got = new List<BossHazard>();
                var m = new BossMove { id = "t_edge", name = "t", kind = BMove.Zones, tier = BTier.Base, tele = 0.7f, dmg = 0.1f, layout = ZLayout.EdgeRing, n = 6, radius = 1f };
                yield return RunSynth(b, m, 0.15f, got);
                var angs = got.Select(h => Mathf.Atan2(h.pos.y, h.pos.x) * Mathf.Rad2Deg).OrderBy(a => a).ToList();
                bool even = angs.Count == 6; for (int i = 0; even && i < 6; i++) even &= Mathf.Abs(Mathf.DeltaAngle(angs[i], angs[(i + 1) % 6]) - 60f) < 0.6f;
                bool onEdge = got.All(h => InArena(h.pos) && Mathf.Abs(h.pos.magnitude - (Edge(Vector2.zero, Mathf.Atan2(h.pos.y, h.pos.x) * Mathf.Rad2Deg) - 1.2f)) < 0.05f);
                Check("zones_edgering", got.Count == 6 && even && onEdge, got.Count + " even " + even + " edge " + onEdge);
                yield return Sec(1.2f);
            }
            {   // Radial: n points on a ring of radius 4 round the boss
                var got = new List<BossHazard>();
                var m = new BossMove { id = "t_radial", name = "t", kind = BMove.Zones, tier = BTier.Base, tele = 0.7f, dmg = 0.1f, layout = ZLayout.Radial, n = 4, radius = 1f };
                yield return RunSynth(b, m, 0.15f, got);
                int on4 = got.Count(h => Mathf.Abs((h.pos - boss.Pos).magnitude - 4f) < 0.05f);
                Check("zones_radial", got.Count == 4 && got.All(h => InArena(h.pos)) && on4 >= 3, got.Count + " on ring " + on4);
                yield return Sec(1.2f);
            }
            {   // Between: n points evenly on the segment boss -> player
                var got = new List<BossHazard>();
                var m = new BossMove { id = "t_between", name = "t", kind = BMove.Zones, tier = BTier.Base, tele = 0.7f, dmg = 0.1f, layout = ZLayout.Between, n = 3, radius = 1f };
                Vector2 bp = boss.Pos, pp = Game.I.player.Pos;
                yield return RunSynth(b, m, 0.15f, got);
                bool ok = got.Count == 3;
                for (int i = 0; ok && i < 3; i++) { int ii = i; ok &= got.Any(h => (h.pos - Vector2.Lerp(bp, pp, (ii + 1f) / 4f)).magnitude < 0.05f); }
                Check("zones_between", ok && got.All(h => InArena(h.pos)), got.Count.ToString());
                yield return Sec(1.2f);
            }
            BossHazard.ClearAll();
        }

        IEnumerator T_boss_extra_annulus()   // Nova hook "annulus" (no kit row uses it): fire closes from radius to the safe radius2 round the player's spot
        {
            yield return BossPrep("b2", 0, 10, new Vector2(0f, 5f)); var b = boss.brain;
            for (int pass = 0; pass < 2; pass++)
            {
                var m = new BossMove { id = "t_ann" + pass, name = "t", kind = BMove.Nova, tier = BTier.Base, tele = 0.8f, dmg = 0.2f, radius = 12f, radius2 = 5f, dur = 1f, life = 1.5f, tick = 0.5f, hook = "annulus", reps = 1 };
                BossHazard.ClearAll(); PinAt(Vector2.zero);
                int ph0 = BossHazard.PlayerHits;
                var got = new List<BossHazard>();
                yield return RunSynth(b, m, 0.2f, got);
                var tele = got.FirstOrDefault(h => h.Telegraphing);
                if (pass == 0) Check("annulus_tele", tele != null && tele.shape == HShape.Annulus && Mathf.Abs(tele.radius - 12f) < 0.01f && Mathf.Abs(tele.radius2 - 5f) < 0.01f, tele == null ? "none" : tele.shape + " " + tele.radius + "/" + tele.radius2);
                PinAt(new Vector2(pass == 0 ? 3f : 8f, 0f));
                yield return Sec(3.8f);
                int hits = BossHazard.PlayerHits - ph0;
                Check(pass == 0 ? "annulus_inside_safe" : "annulus_outside_hit", pass == 0 ? hits == 0 : hits >= 2, hits.ToString());
                Check("annulus_gone_" + pass, !BossHazard.All.Any(h => h != null && h.owner == boss), BossHazard.All.Count.ToString());
            }
            PinAt(Vector2.zero); BossHazard.ClearAll();
        }

        IEnumerator T_boss_extra_clone_dmg()   // clone bullets carry boss damage x the move factor (no ghost discount)
        {
            yield return BossPrep("b3", 0, 15, new Vector2(0f, 5f)); var b = boss.brain;
            yield return Fire(b, "ma_anh_huyet_y"); yield return Sec(1.8f);
            var ps = Projectile.All.Where(p => p != null && !p.Dead && p.Hostile && p.moveId == "ma_anh_huyet_y").ToList();
            float exp = boss.dmg * 0.35f;
            Check("clone_dmg_count", ps.Count >= 12, ps.Count.ToString());
            Check("clone_dmg_value", ps.Count > 0 && ps.All(p => Mathf.Abs(p.Dmg - exp) < 0.01f), ps.Count > 0 ? ps[0].Dmg.ToString("0.00") + " vs " + exp.ToString("0.00") : "-");
            yield return Sec(6f);
            BossHazard.ClearAll(); Projectile.ClearAll(false);
        }

        IEnumerator T_boss_extra_phase_clean()   // phase change: hooks, minions, hazards and bullets of the boss are gone
        {
            yield return BossPrep("b2b", 3, 10, new Vector2(0f, 5f)); var b = boss.brain;
            var dir = Game.I.dir; dir.TeleOnly = true; dir.enabled = true;
            yield return Fire(b, "van_trung_phe"); yield return WaitIdle(b, 12f); yield return Sec(4f);   // cocoons hatch, spiderlings stay
            var hz = BossHazard.Spawn(boss, HShape.Circle, new Vector2(8f, 4f), 0.2f, 60f, 0f, Color.green, 0.5f); hz.radius = 1f;
            var pr = Projectile.FireHostile(new Vector2(-8f, 5f), Vector2.zero, 1f, Color.red); pr.owner = boss;
            int sp = Summoned(b, "spiderling");
            Check("pc_setup", sp > 0 && BossHazard.All.Count(h => h != null) > 0 && Projectile.CountHostile() > 0, "spiders " + sp);
            b.Hold = false; boss.hp = boss.maxHp * 0.49f;
            yield return WaitPhase2(b, 30f);
            Check("pc_phase2", b.Phase == 2, b.Phase.ToString());
            Check("pc_clean", Summoned(b) == 0 && BossHooks.Count() == 0 && !BossHazard.All.Any(h => h != null && h.owner == boss) && Projectile.CountHostile() == 0, Leftovers());
            yield return Sec(1.5f);
            Check("pc_still_clean", Summoned(b) == 0 && BossHooks.Count() == 0 && Projectile.CountHostile() == 0 && BossMoves.Pending(b) == 0, Leftovers());
            dir.TeleOnly = false; dir.enabled = false;
            BossHooks.ClearOwner(b); BossHazard.ClearAll(); Projectile.ClearAll(false);
        }

        /// <summary>Boss moves that keep creating things after the start: ghosts, a spiral, falling zones, cocoons.</summary>
        static readonly (string id, string move, float at)[] MidUlt =
        {
            ("b4", "van_ma_quy_tong", 3.0f), ("b3", "huyet_ha_dao_luu", 2.0f), ("b2", "thien_hoa_lac", 1.0f), ("b2b", "van_trung_phe", 0.7f),
        };

        IEnumerator T_boss_clean_world()   // StartRun while a boss is mid-ult: nothing of it is created after ClearWorld, in that frame or the next 0.5 s
        {
            foreach (var cfg in MidUlt)
            {
                yield return BossPrep(cfg.id, 5, BossWave(cfg.id), new Vector2(0f, 5f)); var b = boss.brain;
                yield return Fire(b, cfg.move); yield return Sec(cfg.at);
                bool running = b.Current != null && !Clean(); string before = Leftovers();
                var g = Game.I; tankHold = false; int err0 = AutoTest.Errors;
                g.StartRun(0, 0); g.dir.enabled = false;
                bool bad = !Clean(); string what = bad ? "same frame " + Leftovers() : "";
                float t = 0f;
                while (t < 0.6f) { yield return null; t += Game.Dt; if (!Clean() && !bad) { bad = true; what = "t=" + t.ToString("0.00") + " " + Leftovers(); } }
                Check("cw_running_" + cfg.id, running, before);
                Check("cw_clean_" + cfg.id, !bad && AutoTest.Errors == err0 && Enemy.All.Count(e => e != null && e.boss) == 0, what + " errors " + (AutoTest.Errors - err0));
            }
        }

        IEnumerator T_boss_clean_menu()   // back to the menu mid-ult leaves nothing (Game.Dt is 0 outside play, so this waits in frames and real time)
        {
            foreach (var cfg in MidUlt)
            {
                yield return BossPrep(cfg.id, 5, BossWave(cfg.id), new Vector2(0f, 5f)); var b = boss.brain;
                yield return Fire(b, cfg.move); yield return Sec(cfg.at);
                bool running = b.Current != null && !Clean(); string before = Leftovers();
                var g = Game.I; tankHold = false; int err0 = AutoTest.Errors;
                g.GoMenu();
                bool bad = !Clean(); string what = bad ? "same frame " + Leftovers() : "";
                for (int f = 0; f < 40; f++) { yield return null; if (!Clean() && !bad) { bad = true; what = "frame " + f + " " + Leftovers(); } }
                yield return new WaitForSecondsRealtime(0.5f);
                if (!Clean() && !bad) { bad = true; what = "late " + Leftovers(); }
                Check("menu_running_" + cfg.id, running, before);
                Check("menu_clean_" + cfg.id, !bad && g.state == GState.Menu && AutoTest.Errors == err0 && Enemy.All.Count(e => e != null && e.boss) == 0, what + " state " + g.state + " errors " + (AutoTest.Errors - err0));
            }
        }

        // ---------------------------------------------------------------- screenshots (run with -skilltest -only shot): Logs/shots/shot_*.png
        /// <summary>Boss of id at Kiếp k forced into moveId; one shot at `at` seconds (default 0.75 x its telegraph) and optionally a second at `at2`.</summary>
        IEnumerator ShotOf(string file, string id, int k, string moveId, float at = -1f, float at2 = -1f)
        {
            yield return BossPrep(id, k, BossWave(id), new Vector2(0f, 5f)); var b = boss.brain;
            var dir = Game.I.dir; dir.TeleOnly = true; dir.enabled = true;
            yield return Fire(b, moveId);
            if (at < 0f) at = b.EffTele(b.kit.Move(moveId)) * 0.75f;
            yield return Sec(at);
            Game.I.bot.Shot(file);
            if (at2 > at)
            {
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Sec(Mathf.Max(0.05f, at2 - at - 0.4f));
                Game.I.bot.Shot(file + "_live");
            }
            yield return new WaitForSecondsRealtime(0.6f);   // the capture is written at end of frame: do not rebuild the arena before it lands
            dir.TeleOnly = false; dir.enabled = false;
            BossHazard.ClearAll(); Projectile.ClearAll(false);
        }

        IEnumerator UltShot(string id, int k, BTier tier, string suffix, float at = -1f, float at2 = -1f)
        {
            var u = Content.Kit(id).Ult(tier);
            yield return ShotOf("shot_" + id + "_ult" + suffix, id, k, u.id, at, at2);
        }

        IEnumerator T_shot_b1_ultII() { yield return UltShot("b1", 5, BTier.II, "II", 1.2f, 2.8f); }
        IEnumerator T_shot_b1b_ultII() { yield return UltShot("b1b", 5, BTier.II, "II", 0.9f, 2.4f); }
        IEnumerator T_shot_b2_ultII() { yield return UltShot("b2", 5, BTier.II, "II", 0.9f, 3.0f); }
        IEnumerator T_shot_b2b_ultII() { yield return UltShot("b2b", 5, BTier.II, "II", 1.1f, 3.0f); }
        IEnumerator T_shot_b3_ultII() { yield return UltShot("b3", 5, BTier.II, "II", 0.8f, 2.4f); }
        IEnumerator T_shot_b4_ultII() { yield return UltShot("b4", 5, BTier.II, "II", 1.5f, 4.4f); }

    }
}

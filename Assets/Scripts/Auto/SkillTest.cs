using System.Collections;
using System.Collections.Generic;
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

        public Enemy BossAt(Vector2 p, int slot) => Enemy.Spawn(Game.I.run.Boss(slot), p, Game.I.run.wave, 1f, 1f, true, slot);

        public float Dmg(string src) => Game.I.run.skillDmgBy.TryGetValue(src, out var v) ? v : 0f;

        // ---------------------------------------------------------------- core (Task 1)
        IEnumerator T_core_sp()
        {
            yield return Arena(0, 1, 0); var r = Game.I.run;
            int[] w = { 1, 5, 10, 15, 20 }; float[] sp = { 6, 31, 122, 271, 377 };   // spec §3.1
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
            yield return Arena(0, 13, 2); Pack(20, 1.5f, 4.5f); int f0 = Projectile.SkillFired; Game.I.player.SetSkillReady(); Game.I.player.TryCast();
            Check("kiem3_16", Projectile.SkillFired - f0 == 16, ""); yield return new WaitForSeconds(4.5f);
            Check("kiem3_orbit", Dmg("kiem_orbit") > 0, "");
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

        IEnumerator T_ml_shots()   // all 24 forms for visual review; stage IV also in a 100-enemy crowd (spec section 13)
        {
            for (int ci = 0; ci < 6; ci++) for (int st = 0; st < 4; st++)
            {
                yield return Arena(ci, 3 + st * 5, st); Pack(st == 3 ? 100 : 20, 1f, st == 3 ? 8f : 4.5f);
                Game.I.player.SetSkillReady(); Game.I.player.TryCast(); yield return new WaitForSeconds(0.35f);
                Game.I.bot.Shot("form_" + ci + "_" + st);
            }
        }
    }
}

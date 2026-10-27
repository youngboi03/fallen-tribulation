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
    }
}

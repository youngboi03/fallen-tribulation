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
    }
}

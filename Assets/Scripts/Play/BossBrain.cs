using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VK
{
    /// <summary>
    /// Boss AI (spec section 3): two phases, ultimate queue at hp thresholds, weighted move selection,
    /// move coroutines from BossMoves. Lives on the boss GameObject; Enemy.Update calls Tick for EBeh.Boss.
    /// </summary>
    public class BossBrain : MonoBehaviour
    {
        public Enemy e;
        public BossKit kit;
        public int kiep;

        public int Phase { get; private set; } = 1;
        /// <summary>No moves, no walking (phase transitions, cinematics, tests).</summary>
        public bool Hold;
        public BossMove Current { get; private set; }
        /// <summary>Ids of moves started (riders included), in order.</summary>
        public readonly List<string> Log = new List<string>();
        /// <summary>Hp fractions that queue an ultimate for this Kiếp (spec 3.2), highest first.</summary>
        public readonly List<float> UltMarks = new List<float>();
        /// <summary>When set, called instead of the short phase transition (Kiếp 5 Ma Thân cutscene).</summary>
        public Action<BossBrain> PhaseTwoHook;

        /// <summary>Seconds of gameplay since spawn.</summary>
        public float FightT { get; private set; }
        public float Phase2At { get; private set; } = -1f;
        public int Transitions { get; private set; }
        /// <summary>True during the short phase transition: the boss takes damage but no new crowd control.</summary>
        public bool Staggered { get; private set; }

        // ---- state written by BossMoves
        /// <summary>Velocity while a move runs (dash); zero otherwise.</summary>
        public Vector2 vel;
        /// <summary>The boss body hurts through the dash check, so normal contact damage is off.</summary>
        public bool Dashing;
        /// <summary>Points the last host move left behind (dash end, bullet landings, pillars) for its riders.</summary>
        public readonly List<Vector2> lastPoints = new List<Vector2>();
        public Vector2 laneA, laneB;
        public float laneWidth;
        /// <summary>Hazards spawned per rider id, for live caps.</summary>
        public readonly Dictionary<string, List<BossHazard>> owned = new Dictionary<string, List<BossHazard>>();

        readonly List<BTier> markTier = new List<BTier>();
        readonly Queue<BossMove> ults = new Queue<BossMove>();
        float[] cdLeft = new float[0];   // per kit.moves index
        readonly Dictionary<string, float> ultStart = new Dictionary<string, float>();
        int marksPassed, p2Picks;
        bool p2Done;
        BossMove forced, prev;
        float restT;
        Coroutine running;

        public static int HitCount;

        /// <summary>0 = base set (Kiếp 0-2), 1 = + set I (3-4), 2 = + set II (5).</summary>
        public static int Sets(int kiep) => kiep >= 5 ? 2 : kiep >= 3 ? 1 : 0;

        public void Init(Enemy en, BossKit k, int kp)
        {
            e = en; kit = k; kiep = Mathf.Clamp(kp, 0, 5);
            UltMarks.Clear(); markTier.Clear();
            int s = Sets(kiep);
            UltMarks.Add(0.75f); markTier.Add(BTier.Base);
            if (s == 2) { UltMarks.Add(0.5f); markTier.Add(BTier.I); }
            UltMarks.Add(0.25f); markTier.Add(s == 0 ? BTier.Base : s == 1 ? BTier.I : BTier.II);
            cdLeft = new float[kit.moves.Count];
            restT = 2f;
            Debug.Log("[BOSS] spawn " + kit.bossId + " k=" + kiep);
        }

        public bool Unlocked(BossMove m)
        {
            if (m == null) return false;
            switch (m.tier)
            {
                case BTier.Base: return true;
                case BTier.P2: return Phase == 2;
                case BTier.I: return Sets(kiep) >= 1;
                default: return Sets(kiep) == 2 && Phase == 2;
            }
        }

        float PhaseMul => Phase == 2 ? 0.85f : 1f;
        public float Cd(BossMove m) => m.cd * KiepNum.BossCd[kiep] * PhaseMul;
        public float Rest() => kit.rest * KiepNum.BossCd[kiep] * PhaseMul;
        /// <summary>Boss bullet speed factor for this Kiếp.</summary>
        public float ShotMul => KiepNum.BossShot[kiep];

        public void ForceMove(string id)
        {
            var m = kit.Move(id);
            if (m == null) { Debug.Log("[BOSS] ForceMove unknown " + id); return; }
            forced = m;
        }

        /// <summary>Gameplay time the ultimate with this id last started (FightT), or +inf when never.</summary>
        public float UltStartedAt(string id) => ultStart.TryGetValue(id, out var t) ? t : float.PositiveInfinity;

        // ------------------------------------------------------------------ per frame
        public Vector2 Tick(float dt, Vector2 dir, float dist)
        {
            FightT += Game.Dt;
            for (int i = 0; i < cdLeft.Length; i++) if (cdLeft[i] > 0f) cdLeft[i] -= dt;
            CheckMarks();
            CheckPhase();
            if (Current != null) return vel;
            if (Hold) return Vector2.zero;

            if (forced != null) { var f = forced; forced = null; Begin(f); return vel; }
            restT -= dt;
            if (restT <= 0f)
            {
                var m = ults.Count > 0 ? ults.Dequeue() : Pick(false);
                if (m != null) { Begin(m); return vel; }
            }
            return dir * e.speed;
        }

        void CheckMarks()
        {
            float frac = e.maxHp > 0f ? e.hp / e.maxHp : 1f;
            while (marksPassed < UltMarks.Count && frac <= UltMarks[marksPassed])
            {
                var u = kit.Ult(markTier[marksPassed]);
                if (u != null) ults.Enqueue(u);
                marksPassed++;
            }
        }

        void CheckPhase()
        {
            if (Phase != 1 || e.hp > e.maxHp * 0.5f) return;
            Phase = 2; Transitions++; Phase2At = FightT;
            p2Picks = 0; p2Done = false;
            Debug.Log("[BOSS] phase2 t=" + FightT.ToString("0.0"));
            Cancel();
            if (PhaseTwoHook != null) PhaseTwoHook(this);
            else StartCoroutine(ShortTransition());
        }

        /// <summary>Spec 3.1: 1 s stagger, boss hazards and bullets cleared, red ring, shake, banner, climax music.</summary>
        IEnumerator ShortTransition()
        {
            Hold = true; Staggered = true;
            BossHazard.ClearOwner(e);
            Projectile.ClearHostileWithin(e.Pos, 999f);
            Fx.Ring(e.Pos, new Color(1f, 0.25f, 0.25f), 1f, 7f, 0.6f);
            Fx.Shake(0.4f, 0.4f);
            Sfx.Play("heavy", 0.9f, 0f, 0.7f);
            if (Game.I.ui != null) Game.I.ui.Banner("", e.def.name + " · Phase II", kit.col);
            Sfx.Music("boss_climax");
            yield return BossMoves.Wait(this, 1f);
            Staggered = false; Hold = false;
            restT = Mathf.Min(restT, 0.3f);
        }

        // ------------------------------------------------------------------ selection (spec 3.3)
        static float Weight(BossMove m) => m.tier == BTier.Base ? 2f : m.tier == BTier.P2 ? 3f : 1.5f;

        /// <summary>Picks the next regular move (never an ultimate or a rider, never the previous move). ignoreCd: tests.</summary>
        public BossMove Pick(bool ignoreCd)
        {
            var c = new List<BossMove>();
            for (int i = 0; i < kit.moves.Count; i++)
            {
                var m = kit.moves[i];
                if (m.ult || m.after != null || m == prev || !Unlocked(m)) continue;
                if (!ignoreCd && cdLeft[i] > 0f) continue;
                c.Add(m);
            }
            if (c.Count == 0) return null;
            BossMove pick = null;
            if (Phase == 2 && !p2Done && p2Picks >= 1) pick = c.FirstOrDefault(m => m.tier == BTier.P2);   // P2 forced within the first 2 picks
            if (pick == null)
            {
                float tot = 0f; foreach (var m in c) tot += Weight(m);
                float r = UnityEngine.Random.value * tot;
                foreach (var m in c) { r -= Weight(m); if (r <= 0f) { pick = m; break; } }
                if (pick == null) pick = c[c.Count - 1];
            }
            prev = pick;
            if (Phase == 2) { p2Picks++; if (pick.tier == BTier.P2) p2Done = true; }
            return pick;
        }

        // ------------------------------------------------------------------ running moves
        void Begin(BossMove m)
        {
            if (!m.ult && m.after == null) prev = m;
            Current = m; vel = Vector2.zero;
            Mark(m);
            if (m.ult && Game.I.ui != null) Game.I.ui.Banner("", m.name, kit.col);
            running = StartCoroutine(RunMove(m));
        }

        void Mark(BossMove m)
        {
            Log.Add(m.id);
            if (m.ult) ultStart[m.id] = FightT;
            Debug.Log("[BOSS] move " + m.id);
        }

        IEnumerator RunMove(BossMove m)
        {
            yield return BossMoves.Run(this, m);
            vel = Vector2.zero; Dashing = false;
            foreach (var r in kit.moves)
            {
                if (r.after != m.id || !Unlocked(r)) continue;
                Current = r; Mark(r);
                yield return BossMoves.Run(this, r);
                vel = Vector2.zero; Dashing = false;
            }
            int mi = kit.moves.IndexOf(m); if (mi >= 0) cdLeft[mi] = Cd(m);
            restT = Rest();
            Current = null; running = null;
        }

        void Cancel()
        {
            if (running != null) StopCoroutine(running);
            running = null; Current = null; vel = Vector2.zero; Dashing = false;
        }

        /// <summary>Boss died or vanished: stop every move coroutine and remove its hazards.</summary>
        public void Stop()
        {
            StopAllCoroutines();
            running = null; Current = null; vel = Vector2.zero; Dashing = false; Hold = true;
            BossHazard.ClearOwner(e);
        }

        public void OnDeath()
        {
            Debug.Log("[BOSS] kill " + kit.bossId + " t=" + FightT.ToString("0.0"));
            Stop();
        }

        // ------------------------------------------------------------------ hits on the player
        /// <summary>Hurts the player; on a real hit by a boss logs [BOSSHIT] moveId pct=P (P = damage / player max HP).</summary>
        public static bool HurtPlayer(Enemy owner, string moveId, float dmg, Vector2 from)
        {
            var g = Game.I;
            var p = g.player;
            float hp0 = g.run != null ? g.run.hp : 0f;
            bool hit = p.Hurt(dmg, from);
            if (hit && owner != null && owner.boss)
            {
                if (string.IsNullOrEmpty(moveId)) moveId = owner.brain != null && owner.brain.Current != null ? owner.brain.Current.id : "contact";
                float lost = g.run != null ? Mathf.Max(0f, hp0 - g.run.hp) : dmg;
                float max = g.run != null ? g.run.MaxHP : 100f;
                HitCount++;
                Debug.Log("[BOSSHIT] " + moveId + " pct=" + (lost / max).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
            }
            return hit;
        }
    }
}

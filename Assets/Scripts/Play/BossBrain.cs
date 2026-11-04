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
        /// <summary>Moves run by hook helpers (ghosts), as "id xMUL".</summary>
        public readonly List<string> HookLog = new List<string>();
        /// <summary>Hp fractions that queue an ultimate for this Kiếp (spec 3.2), highest first.</summary>
        public readonly List<float> UltMarks = new List<float>();
        /// <summary>When set, called instead of the short phase transition (Kiếp 5 Ma Thân cutscene).
        /// The transition only starts when no move is running, so there is no cancelled move to clean up; the hook
        /// itself must clear the boss telegraphs/hazards (BossHazard.ClearOwner) and bullets (Projectile.ClearOwner) it wants gone
        /// (hook objects and minions are already cleared by BossHooks.ClearOwner when phase 2 begins),
        /// and must set Hold back to false when it ends.</summary>
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
        /// <summary>Casts seen per host move id (rider cadence), and minions queued but not yet spawned per def id (Summon cap).</summary>
        public readonly Dictionary<string, int> casts = new Dictionary<string, int>();
        public readonly Dictionary<string, int> pending = new Dictionary<string, int>();

        readonly List<BTier> markTier = new List<BTier>();
        /// <summary>Queued ultimates with the hp mark that queued them (threshold order).</summary>
        readonly Queue<KeyValuePair<BossMove, float>> ults = new Queue<KeyValuePair<BossMove, float>>();
        float[] cdLeft = new float[0];   // per kit.moves index
        readonly Dictionary<string, float> ultStart = new Dictionary<string, float>();
        int marksPassed, p2Picks;
        bool p2Done;
        BossMove forced, prev;
        float restT;
        Coroutine running;

        // ---- hook proxies (BossHooks): a brain on a clone / ghost / gate that runs primitives from its own body
        /// <summary>Real brain this proxy works for (null on the real boss).</summary>
        public BossBrain master;
        /// <summary>Where moves originate: the hook object for a proxy, else the boss.</summary>
        public Transform body;
        /// <summary>Damage multiplier of every move this brain runs (ghosts 0.7).</summary>
        public float dmgMul = 1f;
        public Vector2 Pos => body != null ? (Vector2)body.position : e.Pos;

        public void InitProxy(BossBrain real, BossKit k, Transform t, float mul)
        {
            master = real; e = real.e; kit = k; kiep = real.kiep; body = t; dmgMul = mul;
            cdLeft = new float[kit.moves.Count];
            Hold = true;
        }

        /// <summary>A proxy moves its own body (dash); the real boss is moved by Enemy.Update.</summary>
        void Update()
        {
            if (master == null || body == null || vel == Vector2.zero || e == null) return;
            body.position = Arena.Clamp((Vector2)body.position + vel * BossMoves.Step(this), e.radius * 0.5f);
        }

        public static int HitCount;
        /// <summary>Telegraph time the last primitive actually used (tests).</summary>
        public float LastTele = -1f;

        /// <summary>Effective telegraph of a move: ultimates at least 1.0 s (two-stage ults keep stage 1, their tele2 is the 1 s+ part), others at least the Kiếp floor (spec 3.2, 3.4).</summary>
        public float EffTele(BossMove m) => Mathf.Max(m.tele, m.ult && m.tele2 <= 0f ? 1f : KiepNum.TeleFloor(kiep));

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
            if (Current != null) return vel;
            CheckPhase();
            if (Hold) return Vector2.zero;

            if (forced != null) { var f = forced; forced = null; Begin(f); return vel; }
            restT -= dt;
            if (restT <= 0f)
            {
                var m = ults.Count > 0 ? ults.Dequeue().Key : PhasePending ? null : Pick(false);
                if (m != null) { Begin(m); return vel; }
            }
            // between moves: close in, then circle the player at mid range instead of standing still
            Vector2 side = new Vector2(-dir.y, dir.x) * (marksPassed % 2 == 0 ? 1f : -1f);
            Vector2 mv = dist > 5f ? dir : dist < 3f ? side - dir * 0.6f : side + dir * 0.2f;
            return mv.normalized * e.speed * 1.4f;
        }

        void CheckMarks()
        {
            float frac = e.maxHp > 0f ? e.hp / e.maxHp : 1f;
            while (marksPassed < UltMarks.Count && frac <= UltMarks[marksPassed])
            {
                var u = kit.Ult(markTier[marksPassed]);
                if (u != null) ults.Enqueue(new KeyValuePair<BossMove, float>(u, UltMarks[marksPassed]));
                marksPassed++;
            }
        }

        /// <summary>Hp is at or below 50% but phase 2 has not begun yet.</summary>
        public bool PhasePending => Phase == 1 && e.hp <= e.maxHp * 0.5f;

        /// <summary>Phase 2 is deferred until no move runs and no queued ultimate from a mark above 50% is left,
        /// so a big hit plays Base ult, then the transition, then the set I / II ults (spec 3.2, 4.4). Never cancels a move.</summary>
        void CheckPhase()
        {
            if (!PhasePending || Current != null) return;
            foreach (var u in ults) if (u.Value > 0.5f) return;
            Phase = 2; Transitions++; Phase2At = FightT;
            BossHooks.ClearOwner(this);   // clones, ghosts, cocoons, flags and summons never cross a phase change
            p2Picks = 0; p2Done = false;
            Debug.Log("[BOSS] phase2 t=" + FightT.ToString("0.0"));
            if (PhaseTwoHook != null) PhaseTwoHook(this);
            else running = StartCoroutine(Flat(ShortTransition()));
        }

        /// <summary>Spec 3.1: 1 s stagger, boss hazards and bullets cleared, red ring, shake, banner, climax music.</summary>
        IEnumerator ShortTransition()
        {
            Hold = true; Staggered = true;
            BossHazard.ClearOwner(e);
            Projectile.ClearHostileWithin(e.Pos, 999f);
            var g = Game.I;
            Fx.Hitstop(0.12f);
            Fx.Ring(e.Pos, kit.col, 1f, 11f, 0.9f);
            Fx.Ring(e.Pos, Color.white, 1f, 6f, 0.5f);
            Fx.Shake(0.5f, 0.6f);
            Sfx.Play("heavy", 0.9f, 0f, 1f);
            Sfx.Music("boss_climax");
            if (g.player != null)
            {   // shockwave: pushes the player out, no damage
                Vector2 away = g.player.Pos - e.Pos; float d = away.magnitude;
                if (d < 10f) g.player.Shove((d > 0.01f ? away / d : Vector2.right) * (14f * (1f - d / 12f)));
            }
            fxOn = true; fxZoom = g.cam != null ? g.cam.orthographicSize : 0f; fxScale = e.ScaleMul;
            const float Len = 1.1f;
            for (float x = 0f; x < Len; x += BossMoves.Step(this))
            {
                float k = x / Len, env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 1.15f));   // up and back
                if (g.cam != null && fxZoom > 0f) g.cam.orthographicSize = fxZoom / (1f + 0.2f * env);
                if (g.player != null)
                {   // frame the boss even at the arena wall (camSlack lets the view pass it), then keep the player on screen too
                    float hh = g.cam.orthographicSize, hw = hh * g.cam.aspect;
                    Vector2 f = Vector2.Lerp(g.player.Pos, e.Pos, 0.5f * env), pp = g.player.Pos, bp = e.Pos;
                    f.x = Mathf.Clamp(f.x, bp.x - hw * 0.5f, bp.x + hw * 0.5f); f.y = Mathf.Clamp(f.y, bp.y - hh * 0.5f, bp.y + hh * 0.5f);
                    f.x = Mathf.Clamp(f.x, pp.x - hw * 0.85f, pp.x + hw * 0.85f); f.y = Mathf.Clamp(f.y, pp.y - hh * 0.85f, pp.y + hh * 0.85f);
                    g.camFocus = Vector2.Lerp(pp, f, Mathf.Clamp01(env * 2f));
                    g.camSlack = 5f * env;
                }
                if (g.cinema != null) g.cinema.Tint(new Color(kit.col.r * 0.2f, kit.col.g * 0.2f, kit.col.b * 0.2f, 0.4f * env));
                if (k < 0.2f) e.FlashWhite(1f);
                e.SetScaleMul(fxScale * (1f + 0.2f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 2f))));
                yield return null;
            }
            EndFx();
            Staggered = false; Hold = false;
            restT = Mathf.Min(restT, 0.3f);
            running = null;
        }

        bool fxOn; float fxZoom, fxScale;
        /// <summary>Puts back everything the phase transition changed (camera, focus, veil, scale); safe to call twice.</summary>
        void EndFx()
        {
            if (!fxOn) return;
            fxOn = false;
            var g = Game.I;
            if (g == null) return;
            if (g.cam != null && fxZoom > 0f) g.cam.orthographicSize = fxZoom;
            g.camFocus = null; g.camSlack = 0f;
            if (g.cinema != null) g.cinema.Tint(Color.clear);
            if (e != null) e.SetScaleMul(fxScale);
        }

        void OnDestroy() => EndFx();

        /// <summary>Runs nested IEnumerators inline on one Unity coroutine, so StopCoroutine stops the whole move tree.</summary>
        public static IEnumerator Flat(IEnumerator root)
        {
            var st = new Stack<IEnumerator>();
            st.Push(root);
            while (st.Count > 0)
            {
                var top = st.Peek();
                if (!top.MoveNext()) { st.Pop(); continue; }
                if (top.Current is IEnumerator sub) { st.Push(sub); continue; }
                yield return top.Current;
            }
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
            if (m.ult && m.id == BossHooks.Signature(kit.bossId) && Game.I.ui != null) Game.I.ui.Banner("", m.name, kit.col);   // only the signature move is named
            running = StartCoroutine(Flat(RunMove(m)));
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

        /// <summary>Stops the running move and removes everything it left (telegraphs, hazards, bullets of this boss).</summary>
        public void Cancel()
        {
            if (running != null) StopCoroutine(running);
            EndFx();
            running = null; Current = null; vel = Vector2.zero; Dashing = false;
            if (Staggered) { Staggered = false; Hold = false; }
            e.SetHidden(false);
            BossHazard.ClearOwner(e);
            Projectile.ClearOwner(e);
            BossHooks.ClearOwner(this);
        }

        /// <summary>Boss died or vanished: stop every move coroutine and remove its hazards.</summary>
        public void Stop()
        {
            StopAllCoroutines();
            EndFx();
            running = null; Current = null; vel = Vector2.zero; Dashing = false; Hold = true;
            if (e != null) e.SetHidden(false);
            BossHazard.ClearOwner(e);
            Projectile.ClearOwner(e);
            BossHooks.ClearOwner(this);
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

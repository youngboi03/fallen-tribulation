using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    /// <summary>A weapon owned by the player: orbits them, auto-aims, animates its attack.</summary>
    public class WeaponInst
    {
        public WeaponDef def;
        public int tier;
        public float cd;
        public int hitCount;

        Transform tf;
        SpriteRenderer sr, glowSr;
        bool duBaGlow;
        int anim;           // 0 idle, 1 thrust out, 2 thrust back, 3 swing out, 4 swing back
        float at, adur, spinBase;
        Vector2 a0, a1, aimDir = Vector2.up;
        int maxHits;
        readonly HashSet<Enemy> hitSet = new HashSet<Enemy>();

        public WeaponInst(WeaponDef d, int t) { def = d; tier = t; }

        public float Dmg => def.dmg * Content.TierMul[tier];
        public Sprite Icon => Res.Tile(def.sprite);

        public float Cooldown(StatBlock s)
        {
            float a = s[Stat.AtkSpeed];
            var run = Game.I.run;
            if (run != null && Game.I.combo >= 30 && run.Has("sat_luc_an")) a += 25f;
            if (run != null && SkillRuntime.I != null && SkillRuntime.I.duBaT > 0f && run.HasCard("du_ba")) a += 35f;
            float c = def.cd * Content.TierCd[tier];
            return a >= 0 ? c / (1f + a / 100f) : c * (1f - a / 100f);
        }

        public float Range(StatBlock s)
        {
            float k = s[Stat.Range] / 100f * (def.Melee ? 0.5f : 1f);
            return Mathf.Max(1.2f, def.range * (1f + k));
        }

        public float SplashR(StatBlock s) => def.splash * (1f + Mathf.Max(-50f, s[Stat.Range]) / 250f);

        public void CreateView(Vector3 at)
        {
            if (tf) return;
            var go = new GameObject("W_" + def.id);
            tf = go.transform;
            tf.position = at;
            var g = new GameObject("glow");
            g.transform.SetParent(tf, false);
            g.transform.localScale = Vector3.one * 1.3f;
            glowSr = g.AddComponent<SpriteRenderer>();
            glowSr.sprite = Res.Glow; glowSr.sharedMaterial = Res.AddMat; glowSr.sortingOrder = 79;
            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Icon;
            sr.sharedMaterial = Res.FlashMat;
            sr.sortingOrder = 80;
            ApplyTier();
        }

        void ApplyTier()
        {
            if (!tf) return;
            sr.color = def.tint;
            var tc = Content.TierColor[tier];
            glowSr.color = new Color(tc.r, tc.g, tc.b, tier == 0 ? 0f : 0.25f + tier * 0.1f);
            tf.localScale = Vector3.one * (0.85f + tier * 0.07f);
        }

        public void SetTier(int t)
        {
            tier = Mathf.Clamp(t, 0, 3);
            ApplyTier();
            if (tf)
            {
                Fx.Ring(tf.position, Content.TierColor[tier], 0.2f, 2f, 0.5f);
                Fx.Burst(tf.position, Content.TierColor[tier], 16, 5f, 0.12f, 0.5f, true);
            }
        }

        public Transform View => tf;

        public void DestroyView() { if (tf) Object.Destroy(tf.gameObject); tf = null; }

        public void Drop()
        {
            if (!tf) return;
            tf.position += new Vector3(Random.Range(-0.4f, 0.4f), -0.4f, 0);
            tf.rotation = Quaternion.Euler(0, 0, Random.Range(60f, 120f) * (Random.value < 0.5f ? 1 : -1));
            sr.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            glowSr.color = Color.clear;
        }

        void SetRot(Vector2 d) => tf.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - Res.SpriteAngle(def.sprite));

        /// <summary>Cosine between where the art points and the direction from <paramref name="from"/> to the weapon (1 = pointing straight out).</summary>
        public float OutDot(Vector2 from) { if (!tf) return 0f; float a = (tf.eulerAngles.z + Res.SpriteAngle(def.sprite)) * Mathf.Deg2Rad; return Vector2.Dot(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), ((Vector2)tf.position - from).normalized); }

        /// <summary>New wave: drop any half-finished attack and face outward from the hero.</summary>
        public void ResetForWave(Vector2 heroPos)
        {
            anim = 0; at = 0;
            if (!tf) return;
            Vector2 d = (Vector2)tf.position - heroPos;
            aimDir = d.sqrMagnitude > 0.01f ? d.normalized : Vector2.up;
            SetRot(aimDir);
        }

        static float EaseOut(float k) => 1f - (1f - k) * (1f - k);
        static float EaseIn(float k) => k * k;

        public void Tick(Vector2 home, float dt)
        {
            if (!tf) return;
            var st = Game.I.run.stats;
            bool duBa = SkillRuntime.I != null && SkillRuntime.I.duBaT > 0f && Game.I.run.HasCard("du_ba");
            if (duBa)
            {   // Dư Ba: the weapon glows in the skill color
                var sc = Game.I.run.ch.skillColor; glowSr.color = new Color(sc.r, sc.g, sc.b, 0.6f + Mathf.Sin(Time.time * 12f) * 0.15f); duBaGlow = true;
            }
            else if (duBaGlow) { duBaGlow = false; ApplyTier(); }
            switch (anim)
            {
                case 0: Idle(home, dt, st); break;
                case 1: case 2: Thrust(home, dt); break;
                default: Swing(home, dt, st); break;
            }
        }

        void Idle(Vector2 home, float dt, StatBlock st)
        {
            Vector2 pos = Vector2.Lerp(tf.position, home, 1f - Mathf.Exp(-18f * dt));
            tf.position = pos;
            float rng = Range(st);
            var tgt = Enemy.Nearest(pos, rng);
            Vector2 want = tgt != null ? ((Vector2)tgt.transform.position - pos).normalized : (pos - Game.I.player.Pos).normalized;
            if (want.sqrMagnitude < 0.01f) want = Vector2.up;
            aimDir = Vector2.Lerp(aimDir, want, 1f - Mathf.Exp(-22f * dt)).normalized;
            SetRot(aimDir);
            cd -= dt;
            if (cd <= 0 && tgt != null)
            {
                cd = Cooldown(st);
                Attack(tgt, pos, rng, st);
            }
        }

        void Attack(Enemy tgt, Vector2 pos, float rng, StatBlock st)
        {
            Vector2 tp = tgt.transform.position;
            Vector2 d = tp - pos;
            Vector2 dir = d.sqrMagnitude > 0.0001f ? d.normalized : aimDir;
            aimDir = dir;
            switch (def.type)
            {
                case WType.Thrust:
                {
                    a0 = pos;
                    float len = Mathf.Min(d.magnitude + 0.6f, rng + 0.3f);
                    a1 = pos + dir * len;
                    anim = 1; at = 0; adur = 0.07f + len * 0.025f;
                    hitSet.Clear(); maxHits = 1 + def.pierce;
                    Fx.Streak(pos + dir * len * 0.5f, dir, len, new Color(1f, 1f, 1f, 0.35f), 0.12f, 0.12f);
                    Sfx.Play("swing", 0.22f, 0.15f, 1f);
                    break;
                }
                case WType.Swing:
                {
                    a0 = pos;
                    a1 = pos + dir * Mathf.Min(d.magnitude, rng);
                    anim = 3; at = 0; adur = 0.15f;
                    spinBase = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - Res.SpriteAngle(def.sprite);
                    Sfx.Play("swing", 0.3f, 0.1f, 1f);
                    break;
                }
                case WType.Bolt:
                case WType.Fly:
                    Projectile.FireWeapon(this, pos + dir * 0.3f, dir);
                    if (Game.I.run.Has("phan_than_phu") && Random.value < 0.3f)
                        Projectile.FireWeapon(this, pos + dir * 0.3f, Quaternion.Euler(0, 0, Random.Range(-18f, 18f)) * dir);
                    tf.position = pos - dir * 0.25f;
                    Fx.Glow(pos + dir * 0.4f, new Color(def.fx.r, def.fx.g, def.fx.b, 0.7f), 0.7f, 0.12f);
                    Sfx.Play(def.type == WType.Fly ? "swing" : "shoot", 0.22f, 0.12f);
                    break;
                case WType.Chain:
                    Combat.Chain(this, pos + dir * 0.4f, tgt);
                    tf.position = pos - dir * 0.2f;
                    break;
            }
        }

        void Thrust(Vector2 home, float dt)
        {
            at += dt;
            if (anim == 1)
            {
                float k = Mathf.Clamp01(at / adur);
                Vector2 p = Vector2.LerpUnclamped(a0, a1, EaseOut(k));
                tf.position = p;
                SetRot(aimDir);
                if (hitSet.Count < maxHits)
                {
                    Vector2 tip = p + aimDir * 0.35f;
                    var list = Enemy.Snapshot();
                    foreach (var e in list)
                    {
                        if (e.dead || hitSet.Contains(e)) continue;
                        if (Vector2.Distance(e.Pos, tip) < e.radius + 0.42f)
                        {
                            hitSet.Add(e);
                            Combat.Hit(e, this, aimDir);
                            Fx.Burst(tip, Color.white, 3, 5f, 0.08f, 0.15f, true);
                            if (hitSet.Count >= maxHits) break;
                        }
                    }
                    Enemy.Release(list);
                }
                if (k >= 1f) { anim = 2; at = 0; a0 = tf.position; }
            }
            else
            {
                float k = Mathf.Clamp01(at / 0.14f);
                tf.position = Vector2.Lerp(a0, home, k * k * (3 - 2 * k));
                if (k >= 1f) anim = 0;
            }
        }

        void Swing(Vector2 home, float dt, StatBlock st)
        {
            at += dt;
            if (anim == 3)
            {
                float k = Mathf.Clamp01(at / adur);
                Vector2 perp = new Vector2(-aimDir.y, aimDir.x);
                tf.position = Vector2.Lerp(a0, a1, EaseIn(k)) + perp * Mathf.Sin(k * Mathf.PI) * 0.7f;
                tf.rotation = Quaternion.Euler(0, 0, spinBase + 120f - k * 240f);
                if (k >= 1f)
                {
                    float r = SplashR(st);
                    int hitN = Combat.Splash(a1, r, this, 1f, null);
                    if (def.chains > 0)
                    {
                        var first = Enemy.Nearest(a1, 3.5f);
                        if (first != null) Combat.Chain(this, a1, first);
                    }
                    Fx.SlashArc(a1, aimDir, new Color(1f, 1f, 1f, 0.8f), r * 2.2f, 0.18f);
                    Fx.Ring(a1, new Color(1f, 0.9f, 0.7f, 0.7f), 0.2f, r, 0.22f);
                    Fx.Burst(a1, new Color(0.8f, 0.7f, 0.55f, 0.8f), 7, 5f, 0.12f, 0.35f);
                    if (hitN > 0) Fx.Shake(def.dmg >= 16 ? 0.12f : 0.07f, 0.12f);
                    Sfx.Play("heavy", 0.25f, 0.15f, 1f, 0.06f);
                    anim = 4; at = 0; a0 = a1;
                }
            }
            else
            {
                float k = Mathf.Clamp01(at / 0.2f);
                tf.position = Vector2.Lerp(a0, home, k * k * (3 - 2 * k));
                if (k >= 1f) anim = 0;
            }
        }
    }

    public static class Combat
    {
        public static float Roll(WeaponInst w, out bool crit)
        {
            var run = Game.I.run;
            var st = run.stats;
            float pct = st[Stat.Damage] + (w.def.Melee ? st[Stat.Melee] : st[Stat.Spell]) + run.bloodStacks;
            float d = w.Dmg * Mathf.Max(0.1f, 1f + pct / 100f) * run.DamageMult();
            w.hitCount++;
            crit = Random.value * 100f < st[Stat.Crit] + w.def.crit || (run.Has("bat_quai") && w.hitCount % 8 == 0);
            if (crit) d *= 1.5f + st[Stat.CritDmg] / 100f;
            return d * Random.Range(0.92f, 1.08f);
        }

        public static void Hit(Enemy e, WeaponInst w, Vector2 dir, float mult = 1f)
        {
            if (e == null || e.dead || w == null) return;
            var run = Game.I.run;
            float d = Roll(w, out bool crit) * mult;
            if (e.HasStatus && run.Has("pha_quan_kich")) d *= 1.4f;
            if (w.def.burn > 0) e.ApplyBurn(d * w.def.burn * run.BurnMul * 0.5f);
            if (w.def.poison > 0) e.ApplyPoison(d * w.def.poison * run.PoisonMul / 3f);
            if (w.def.freeze > 0 && Random.value < w.def.freeze) e.Freeze(1.2f);
            Vector2 ePos = e.Pos;
            e.Damage(d, crit, dir, w.def.knock, w);
            if (crit) Skills.OnWeaponCrit();
            if (Random.value * 100f < run.stats[Stat.Lifesteal] + (run.riteT > 0 ? Skills.RiteLifesteal : 0f)) Game.I.player.Heal(1);
            if (crit && run.Has("loi_linh_chau"))
            {
                var skip = new HashSet<Enemy> { e };
                for (int i = 0; i < 2; i++)
                {
                    var n = Enemy.Nearest(ePos, 4f, skip);
                    if (n == null) break;
                    skip.Add(n);
                    Fx.Lightning(ePos, n.Pos, new Color(0.6f, 0.8f, 1f), 0.1f, 0.15f);
                    n.Damage(d * 0.6f, false, n.Pos - ePos, 0.2f, null, new Color(0.6f, 0.8f, 1f));
                }
                Sfx.Play("zap", 0.2f, 0.1f, 1f);
            }
        }

        public static int Splash(Vector2 p, float r, WeaponInst w, float mult, Enemy except)
        {
            int n = 0;
            var list = Enemy.Snapshot();
            foreach (var e in list)
                if (!e.dead && e != except && Vector2.Distance(e.Pos, p) < r + e.radius) { Hit(e, w, e.Pos - p, mult); n++; }
            Enemy.Release(list);
            return n;
        }

        public static void Chain(WeaponInst w, Vector2 from, Enemy first)
        {
            var run = Game.I.run;
            int chains = w.def.chains + run.ExtraChains + (w.tier >= 2 ? 1 : 0);
            var hit = new HashSet<Enemy>();
            Vector2 a = from;
            Enemy cur = first;
            float mult = 1f;
            for (int i = 0; i <= chains && cur != null; i++)
            {
                Vector2 cp = cur.Pos;
                Fx.Lightning(a, cp, w.def.fx, 0.16f, 0.18f);
                Fx.Lightning(a, cp, Color.white, 0.05f, 0.12f, 6, 0.2f);
                Fx.Glow(cp, new Color(w.def.fx.r, w.def.fx.g, w.def.fx.b, 0.8f), 0.9f, 0.15f);
                hit.Add(cur);
                Hit(cur, w, cp - a, mult);
                a = cp;
                mult *= 0.85f;
                cur = Enemy.Nearest(cp, 3.8f, hit);
            }
            Sfx.Play("zap", 0.28f, 0.15f, 1f);
        }
    }
}

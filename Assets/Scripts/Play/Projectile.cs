using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    public class Projectile : MonoBehaviour
    {
        public static readonly List<Projectile> All = new List<Projectile>();

        bool hostile, homing, dead;
        Vector2 vel;
        float dmg, life, radius, splash, spinSpd, trailT;
        int pierce;
        WeaponInst src;
        SkillShot ss;
        int phase;                       // 0 flying, 1 returning to the player, 2 orbiting the player
        float orbitLeft, orbitAng;
        Dictionary<Enemy, float> orbitHit;
        public static int SkillFired;
        public static int CountSkill() { int n = 0; foreach (var p in All) if (p != null && !p.dead && p.ss != null) n++; return n; }
        public static int CountHostile() { int n = 0; foreach (var p in All) if (p != null && !p.dead && p.hostile) n++; return n; }
        Color col;
        ProjLook look;
        SpriteRenderer sr, glow;
        readonly HashSet<Enemy> hits = new HashSet<Enemy>();

        static Projectile Make(Vector2 pos)
        {
            var go = new GameObject("proj");
            go.transform.position = pos;
            var p = go.AddComponent<Projectile>();
            var g = new GameObject("glow");
            g.transform.SetParent(go.transform, false);
            p.glow = g.AddComponent<SpriteRenderer>();
            p.glow.sprite = Res.Glow; p.glow.sharedMaterial = Res.AddMat;
            var b = new GameObject("core");
            b.transform.SetParent(go.transform, false);
            p.sr = b.AddComponent<SpriteRenderer>();
            All.Add(p);
            return p;
        }

        public static void FireHostile(Vector2 pos, Vector2 vel, float dmg, Color c)
        {
            var p = Make(pos);
            p.hostile = true; p.vel = vel; p.dmg = dmg; p.life = 6f; p.radius = 0.17f; p.col = c;
            p.sr.sprite = Res.Circle; p.sr.color = Color.Lerp(c, Color.white, 0.55f); p.sr.sortingOrder = 151;
            p.sr.transform.localScale = Vector3.one * 0.34f;
            p.glow.color = new Color(c.r, c.g, c.b, 0.95f); p.glow.sortingOrder = 150;
            p.glow.transform.localScale = Vector3.one * 1.0f;
        }

        public static void FireWeapon(WeaponInst w, Vector2 pos, Vector2 dir)
        {
            var run = Game.I.run;
            var p = Make(pos);
            var d = w.def;
            p.src = w; p.vel = dir * d.speed; p.radius = 0.25f; p.col = d.fx; p.look = d.look;
            p.life = w.Range(run.stats) / d.speed * 1.2f + (d.type == WType.Fly ? 0.9f : 0f);
            p.pierce = d.pierce + run.ExtraPierce;
            p.splash = w.SplashR(run.stats);
            p.homing = d.type == WType.Fly;
            p.sr.sortingOrder = 91; p.glow.sortingOrder = 90;
            switch (d.look)
            {
                case ProjLook.Glow:
                    p.sr.sprite = Res.Circle; p.sr.color = Color.Lerp(d.fx, Color.white, 0.5f);
                    p.sr.transform.localScale = Vector3.one * 0.3f;
                    p.glow.color = new Color(d.fx.r, d.fx.g, d.fx.b, 0.9f);
                    p.glow.transform.localScale = Vector3.one * 1.1f;
                    break;
                case ProjLook.Spin:
                    p.sr.sprite = w.Icon; p.sr.color = d.tint; p.spinSpd = 720f;
                    p.sr.transform.localScale = Vector3.one * 0.7f;
                    p.glow.color = new Color(d.fx.r, d.fx.g, d.fx.b, 0.45f);
                    p.glow.transform.localScale = Vector3.one * 0.8f;
                    break;
                default:
                    p.sr.sprite = w.Icon; p.sr.color = d.tint;
                    p.sr.transform.localScale = Vector3.one * (0.8f + w.tier * 0.05f);
                    p.glow.color = new Color(d.fx.r, d.fx.g, d.fx.b, 0.5f);
                    p.glow.transform.localScale = Vector3.one * 1f;
                    break;
            }
            p.Orient();
        }

        public static Projectile FireSkill(SkillShot s, Vector2 pos, Vector2 dir)
        {
            var p = Make(pos);
            p.ss = s; SkillFired++;
            p.vel = dir.normalized * s.speed; p.radius = s.radius; p.col = s.col; p.look = s.look;
            p.life = s.life; p.pierce = s.pierce; p.homing = s.homing;
            p.sr.sortingOrder = 91; p.glow.sortingOrder = 90;
            p.sr.sprite = s.sprite != null ? s.sprite : Res.Circle; p.sr.color = s.tint;
            p.sr.transform.localScale = Vector3.one * 0.85f;
            p.glow.color = new Color(s.col.r, s.col.g, s.col.b, 0.55f);
            p.glow.transform.localScale = Vector3.one * 1.1f;
            p.Orient();
            return p;
        }

        void Orient()
        {
            if (look == ProjLook.Point && !hostile)
                sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg - (ss != null ? ss.spriteAngle : src != null ? Res.SpriteAngle(src.def.sprite) : 90f));
        }

        void Update()
        {
            if (dead || !Game.I.Playing) return;
            float dt = Game.Dt;
            life -= dt;
            if (life <= 0 && !EndFlight()) return;
            Vector2 pos = transform.position;
            if (ss != null && phase == 2) { OrbitUpdate(dt); return; }
            if (ss != null && phase == 1)
            {
                var pl2 = Game.I.player;
                Vector2 to = pl2.Pos - pos;
                if (to.magnitude < 0.5f) { if (!StartOrbit()) Kill(); return; }
                vel = to.normalized * (ss.speed * 1.15f);
            }

            if (homing)
            {
                var t = Enemy.Nearest(pos, ss != null ? ss.homingRange : 7f, hits);
                if (t != null)
                {
                    Vector2 want = (t.Pos - pos).normalized * vel.magnitude;
                    vel = Vector3.RotateTowards(vel, want, 8f * dt, 0f);
                }
            }
            pos += vel * dt;
            transform.position = pos;
            if (Mathf.Abs(pos.x) > Arena.HalfW + 0.5f || Mathf.Abs(pos.y) > Arena.HalfH + 0.5f) { Kill(); return; }

            if (spinSpd != 0) sr.transform.Rotate(0, 0, -spinSpd * dt);
            else Orient();
            trailT -= dt;
            if (trailT <= 0 && !hostile && look != ProjLook.Spin)
            {
                trailT = 0.03f;
                var c1 = col; c1.a = 0;
                Fx.Particle(pos, -vel * 0.05f, new Color(col.r, col.g, col.b, 0.6f), c1, 0.25f, 0.05f, 0.25f, Res.Glow, true, 2f, 89);
            }
            if (hostile) glow.transform.localScale = Vector3.one * (0.9f + Mathf.Sin(Time.time * 20f) * 0.12f);

            if (hostile)
            {
                var pl = Game.I.player;
                if (Vector2.Distance(pos, pl.Pos) < radius + pl.radius * 0.8f)
                {
                    bool wasDashing = pl.IsDashing;
                    pl.Hurt(dmg, pos);
                    if (!wasDashing) Kill();
                }
                return;
            }

            var list = Enemy.All;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.dead || hits.Contains(e) || (phase == 1 && pierce < 0)) continue;
                if (((Vector2)e.transform.position - pos).sqrMagnitude < (radius + e.radius) * (radius + e.radius))
                {
                    OnHit(e, pos);
                    break;  // the list may have changed; continue next frame
                }
            }
        }

        void OnHit(Enemy e, Vector2 pos)
        {
            hits.Add(e);
            if (ss != null)
            {
                if (ss.blastR > 0f)
                {
                    Skills.Splash(pos, ss.blastR, ss.blastCoef, ss.src, 1.5f, (en, d) => ss.onHit?.Invoke(en));
                    Fx.Ring(pos, ss.col, 0.2f, ss.blastR, 0.3f);
                    Fx.Burst(pos, ss.col, 12, 6f, 0.14f, 0.4f, true);
                    Sfx.Play("heavy", 0.3f, 0.2f, 1.2f);
                    Kill();
                    return;
                }
                bool back = phase == 1;
                Skills.Hit(e, back ? ss.returnCoef : ss.coef, back ? ss.returnSrc : ss.src, vel.normalized, 1f);
                ss.onHit?.Invoke(e);
                if (ss.freeze > 0f && !e.dead) e.SkillFreeze(ss.freeze, ss.freezeBonus);
                pierce--;
                if (pierce < 0 && !back && !EndFlight()) return;
                return;
            }
            Combat.Hit(e, src, vel.normalized);
            if (splash > 0)
            {
                Combat.Splash(pos, splash, src, 0.6f, e);
                Fx.Ring(pos, col, 0.2f, splash, 0.25f);
                Fx.Glow(pos, new Color(col.r, col.g, col.b, 0.7f), splash * 2f, 0.22f);
                Fx.Burst(pos, col, 8, 4f, 0.12f, 0.35f, true);
                Sfx.Play("heavy", 0.18f, 0.2f, 1.5f, 0.05f);
            }
            pierce--;
            if (pierce < 0) Kill();
        }

        /// <summary>Flight is over: start the return leg if the shot has one. Returns false when the shot was destroyed instead.</summary>
        bool EndFlight()
        {
            if (ss != null && phase == 0 && ss.returnSrc != null)
            {
                phase = 1; life = 3f; pierce = 2; homing = false; hits.Clear();
                return true;
            }
            Kill();
            return false;
        }

        bool StartOrbit()
        {
            if (ss == null || ss.orbitTime <= 0f) return false;
            phase = 2; orbitLeft = ss.orbitTime; life = 99f;
            Vector2 rel = (Vector2)transform.position - Game.I.player.Pos;
            orbitAng = Mathf.Atan2(rel.y, rel.x);
            orbitHit = new Dictionary<Enemy, float>();
            return true;
        }

        void OrbitUpdate(float dt)
        {
            orbitLeft -= dt;
            if (orbitLeft <= 0f) { Kill(); return; }
            orbitAng += 6.2832f * dt;
            Vector2 pp = Game.I.player.Pos;
            Vector2 pos = pp + new Vector2(Mathf.Cos(orbitAng), Mathf.Sin(orbitAng)) * SkillNum.KiemOrbitR;
            transform.position = pos;
            vel = new Vector2(-Mathf.Sin(orbitAng), Mathf.Cos(orbitAng)) * ss.speed;
            Orient();
            var list = Enemy.All;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.dead) continue;
                if (((Vector2)e.transform.position - pos).sqrMagnitude >= (radius + e.radius) * (radius + e.radius)) continue;
                if (orbitHit.TryGetValue(e, out float last) && Time.time - last < SkillNum.KiemOrbitRehit) continue;
                orbitHit[e] = Time.time;
                Skills.Hit(e, ss.orbitCoef, "kiem_orbit", vel, 0.6f);
                break;
            }
        }

        void Kill()
        {
            if (dead) return;
            dead = true;
            All.Remove(this);
            Fx.Burst(transform.position, col, hostile ? 3 : 4, 3f, 0.08f, 0.2f, true);
            Destroy(gameObject);
        }

        /// <summary>Remove enemy bullets inside the circle; returns how many were removed.</summary>
        public static int ClearHostileWithin(Vector2 p, float r)
        {
            int n = 0;
            foreach (var pr in new List<Projectile>(All))
            {
                if (pr == null || pr.dead || !pr.hostile) continue;
                if (Vector2.Distance(pr.transform.position, p) > r) continue;
                pr.Kill(); n++;
            }
            return n;
        }

        public static void ClearAll(bool fx)
        {
            foreach (var p in new List<Projectile>(All))
            {
                if (!p) continue;
                if (fx) p.Kill(); else { p.dead = true; Destroy(p.gameObject); }
            }
            All.Clear();
        }
    }

    public class Orb : MonoBehaviour
    {
        public static readonly List<Orb> All = new List<Orb>();
        int value;
        bool heal, chest, magnet;
        Vector2 vel;
        float t, mt;
        SpriteRenderer sr, glow;

        public static void Drop(Vector2 pos, int total)
        {
            while (total > 0)
            {
                int v = total >= 15 ? 5 : 1;
                total -= v;
                Create(pos, v, false);
            }
        }

        public static void DropHeal(Vector2 pos) => Create(pos, 0, true);

        public static void DropChest(Vector2 pos)
        {
            Create(pos, 0, false);
            var o = All[All.Count - 1];
            o.chest = true;
            o.name = "chest";
            o.sr.sprite = Res.Tile(89); o.sr.color = Color.white;
            o.sr.transform.localScale = Vector3.one * 1.1f;
            o.glow.color = new Color(1f, 0.8f, 0.3f, 0.7f);
            o.glow.transform.localScale = Vector3.one * 2.2f;
            o.vel *= 0.4f;
        }

        static void Create(Vector2 pos, int v, bool heal)
        {
            var go = new GameObject(heal ? "fruit" : "orb");
            go.transform.position = pos;
            var o = go.AddComponent<Orb>();
            o.value = v; o.heal = heal;
            o.vel = Random.insideUnitCircle.normalized * Random.Range(1.5f, 4f);
            var g = new GameObject("glow");
            g.transform.SetParent(go.transform, false);
            o.glow = g.AddComponent<SpriteRenderer>();
            o.glow.sprite = Res.Glow; o.glow.sharedMaterial = Res.AddMat; o.glow.sortingOrder = -100;
            var b = new GameObject("core");
            b.transform.SetParent(go.transform, false);
            o.sr = b.AddComponent<SpriteRenderer>();
            o.sr.sortingOrder = -99;
            if (heal)
            {
                o.sr.sprite = Res.Tile(115); b.transform.localScale = Vector3.one * 0.7f;
                o.glow.color = new Color(1f, 0.3f, 0.3f, 0.5f); g.transform.localScale = Vector3.one * 1.1f;
            }
            else
            {
                o.sr.sprite = Res.Diamond;
                bool big = v >= 5;
                o.sr.color = big ? new Color(1f, 0.85f, 0.35f) : new Color(0.45f, 1f, 0.9f);
                b.transform.localScale = Vector3.one * (big ? 0.42f : 0.28f);
                o.glow.color = big ? new Color(1f, 0.8f, 0.3f, 0.55f) : new Color(0.3f, 1f, 0.9f, 0.4f);
                g.transform.localScale = Vector3.one * (big ? 1.1f : 0.75f);
            }
            All.Add(o);
        }

        void Update()
        {
            var g = Game.I;
            if (!g.Playing) return;
            float dt = Game.Dt;
            t += dt;
            Vector2 pos = transform.position;
            Vector2 pp = g.player.Pos;
            float d = Vector2.Distance(pos, pp);
            if (chest) glow.color = new Color(1f, 0.8f, 0.3f, 0.5f + Mathf.Sin(t * 6f) * 0.25f);
            float pr = chest ? 1.6f : heal ? 1.3f : 2.2f * Mathf.Max(0.3f, 1f + g.run.stats[Stat.Pickup] / 100f);
            if (!magnet && t > 0.3f && d < pr) magnet = true;
            if (magnet)
            {
                mt += dt;
                vel = (pp - pos).normalized * Mathf.Min(28f, 5f + mt * 40f);
            }
            else vel *= Mathf.Max(0, 1f - 6f * dt);
            pos += vel * dt;
            transform.position = pos;
            sr.transform.localPosition = new Vector3(0, Mathf.Sin(t * 5f + value) * 0.06f, 0);
            if (d < 0.45f) Collect();
        }

        void Collect()
        {
            var g = Game.I;
            if (chest)
            {
                g.run.chests++;
                Fx.Text(g.player.Pos + Vector2.up, "Bảo Rương!", new Color(1f, 0.8f, 0.3f), 1.1f, 1f);
                Fx.Burst(transform.position, new Color(1f, 0.8f, 0.3f), 20, 6f, 0.14f, 0.6f, true);
                Sfx.Play("coin", 0.8f); Sfx.Play("open", 0.6f);
            }
            else if (heal) { g.player.Heal(3); Sfx.Play("pluck", 0.5f, 0f, 1.8f); }
            else { g.run.GainOrb(value); g.PickupSound(); g.ui.Hint("orb", "Linh Khí vừa là <color=#7CF0FF>kinh nghiệm</color> vừa là <color=#FFD266>Linh Thạch</color> để mua đồ giữa các đợt."); }
            All.Remove(this);
            Destroy(gameObject);
        }

        public static void CollectAll()
        {
            var g = Game.I;
            foreach (var o in new List<Orb>(All))
            {
                if (!o) continue;
                if (o.chest) g.run.chests++;
                else if (!o.heal) g.run.GainOrb(o.value);
                Destroy(o.gameObject);
            }
            All.Clear();
        }
    }
}

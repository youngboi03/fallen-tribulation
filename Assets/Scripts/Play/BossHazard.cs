using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    public enum HShape { Circle, Bar, Cone, Ring, Annulus, Segment }

    /// <summary>
    /// Non-projectile boss danger: beam, zone, ring, cone or line with a telegraph then a live phase.
    /// Fairness (spec 3.4): tick 0 = at most one hit; tick above 0 = at most one hit per max(tick, 0.5) s;
    /// tele 0 still gets a 0.5 s forming time. Timers run on Game.Dt scaled by the owner's freeze factor.
    /// For Ring/Annulus the gap directions are rotated by angleDeg (so spinDeg turns the gaps).
    /// slow = fraction of player move speed removed for slowDur (default 1) s after a hit (0.3 = 30% slower).
    /// </summary>
    public class BossHazard : MonoBehaviour
    {
        public static readonly List<BossHazard> All = new List<BossHazard>();
        public static int PlayerHits;
        public const float FormTime = 0.5f, MinTick = 0.5f;

        public Enemy owner;
        public HShape shape;
        public Vector2 pos;
        public float radius, radius2, len, width, angleDeg, arcDeg, gapWidthDeg, slow;
        /// <summary>How long the slow lasts after a hit (s).</summary>
        public float slowDur = 1f;
        public List<float> gapDirsDeg;
        public Vector2 a, b;
        public float tele, active, dmg, tick;
        public Color col;
        public Vector2 vel;
        public float spinDeg;
        /// <summary>Boss move that made it ([BOSSHIT] attribution).</summary>
        public string moveId;
        /// <summary>Breakable object that carries this hazard (war flag); the hazard ends when it dies.</summary>
        public Enemy anchor { get => anchorE; set { anchorE = value; anchored = value != null; } }
        Enemy anchorE; bool anchored;
        /// <summary>Safe holes cut out of the shape (centres, radius holeR); Contains is false inside one.</summary>
        public List<Vector2> holes;
        public float holeR;
        static bool hinted;

        float age, hitCd;
        bool hitDone, gone, forming;
        readonly List<SpriteRenderer> pieces = new List<SpriteRenderer>();
        int used;
        static Material defMat;

        public bool Live => !gone && age >= tele && age < tele + active;
        /// <summary>Still in its telegraph (or forming) time.</summary>
        public bool Telegraphing => !gone && age < tele;

        public static BossHazard Spawn(Enemy owner, HShape shape, Vector2 pos, float tele, float active, float dmg, Color col, float tick = 0f)
        {
            var go = new GameObject("bosshazard");
            go.transform.position = pos;
            var h = go.AddComponent<BossHazard>();
            h.owner = owner; h.shape = shape; h.pos = pos;
            h.forming = tele <= 0f;
            h.tele = h.forming ? FormTime : tele;
            h.active = active; h.dmg = dmg; h.col = col; h.tick = tick;
            All.Add(h);
            return h;
        }

        public static void ClearOwner(Enemy owner)
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var h = All[i];
                if (h == null) { All.RemoveAt(i); continue; }
                if (h.owner == owner) h.Kill();
            }
        }

        public static void ClearAll()
        {
            for (int i = All.Count - 1; i >= 0; i--) if (All[i] != null) All[i].Kill();
            All.Clear();
            PlayerHits = 0;
        }

        void Kill()
        {
            if (gone) return;
            gone = true;
            All.Remove(this);
            Destroy(gameObject);
        }

        void OnDestroy() { All.Remove(this); }

        // ------------------------------------------------------------------ geometry
        static float AngDiff(float x, float y) => Mathf.Abs(Mathf.DeltaAngle(x, y));

        /// <summary>True when a disc of radius r at p overlaps the hazard shape (ignores the lifecycle phase).</summary>
        public bool Contains(Vector2 p, float r)
        {
            if (holes != null) foreach (var c in holes) if ((p - c).sqrMagnitude <= holeR * holeR) return false;
            switch (shape)
            {
                case HShape.Circle:
                    return (p - pos).sqrMagnitude <= (radius + r) * (radius + r);
                case HShape.Bar:
                {
                    float ar = angleDeg * Mathf.Deg2Rad;
                    Vector2 d = new Vector2(Mathf.Cos(ar), Mathf.Sin(ar)), q = p - pos;
                    float t = Vector2.Dot(q, d), s = Mathf.Abs(q.x * d.y - q.y * d.x);
                    return t >= -r && t <= len + r && s <= width * 0.5f + r;
                }
                case HShape.Segment:
                {
                    Vector2 ab = b - a; float l2 = ab.sqrMagnitude;
                    float t = l2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2) : 0f;
                    return (p - (a + ab * t)).sqrMagnitude <= (width * 0.5f + r) * (width * 0.5f + r);
                }
                case HShape.Cone:
                {
                    Vector2 q = p - pos; float dist = q.magnitude;
                    if (dist > radius + r) return false;
                    if (dist < 0.001f) return true;
                    float margin = Mathf.Asin(Mathf.Min(1f, r / dist)) * Mathf.Rad2Deg;
                    return AngDiff(Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg, angleDeg) <= arcDeg * 0.5f + margin;
                }
                case HShape.Ring:
                case HShape.Annulus:
                {
                    Vector2 q = p - pos; float dist = q.magnitude;
                    if (dist > radius + r || dist < radius2 - r) return false;
                    if (gapDirsDeg != null && gapDirsDeg.Count > 0 && dist > 0.001f)
                    {
                        float ang = Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg;
                        foreach (var g in gapDirsDeg) if (AngDiff(ang, g + angleDeg) <= gapWidthDeg * 0.5f) return false;
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>1 when p is inside a live hazard, or one that goes live within lookahead seconds; else 0.</summary>
        public static float DangerAt(Vector2 p, float lookahead)
        {
            var pl = Game.I != null ? Game.I.player : null;
            float pr = pl != null ? pl.radius : 0.36f;
            foreach (var h in All)
            {
                if (h == null || h.gone) continue;
                if (h.tick <= 0f && h.hitDone) continue;
                bool soon = h.age < h.tele && h.tele - h.age <= lookahead;
                if ((h.Live || soon) && h.Contains(p, pr)) return 1f;
            }
            return 0f;
        }

        // ------------------------------------------------------------------ lifecycle
        void Update()
        {
            var g = Game.I;
            if (!gone && anchored && (anchorE == null || anchorE.dead)) { Kill(); return; }
            if (gone || g == null || !g.Playing) return;
            float dt = Game.Dt;
            if (owner != null && !owner.dead) dt *= owner.TimeScale;
            age += dt;
            if (vel != Vector2.zero) { pos += vel * dt; transform.position = pos; }
            if (spinDeg != 0f) angleDeg += spinDeg * dt;
            if (age >= tele + active) { Kill(); return; }

            if (age >= tele)
            {
                if (!hinted && owner != null && active > 0f && g.ui != null)
                {   // spec 3.4: first live boss hazard explains that bullet-clearing skills do not remove it
                    hinted = true;
                    g.ui.Hint("boss_hazard", "Vùng sát thương của boss không phải đạn: chiêu xoá đạn không xoá được, hãy né ra ngoài.");
                }
                hitCd -= dt;
                var p = g.player;
                if (p != null && p.gameObject.activeInHierarchy && (tick > 0f || !hitDone) && hitCd <= 0f && Contains(p.Pos, p.radius))
                {
                    if (BossBrain.HurtPlayer(owner, moveId, dmg, pos))
                    {   // only a real hit consumes the hazard: god/iframe/dash/dodge leave it armed
                        hitDone = true;
                        hitCd = Mathf.Max(tick, MinTick);
                        PlayerHits++;
                        if (slow > 0f) { float m = 1f - Mathf.Clamp01(slow); p.slowMul = p.slowT > 0f ? Mathf.Min(p.slowMul, m) : m; p.slowT = slowDur; }
                    }
                }
            }
            Draw();
        }

        // ------------------------------------------------------------------ drawing
        void Put(Sprite sp, Vector2 center, float sx, float sy, float rotDeg, Color c, int order, bool add)
        {
            if (used >= pieces.Count)
            {
                var go = new GameObject("p");
                go.transform.SetParent(transform, false);
                var n = go.AddComponent<SpriteRenderer>();
                if (defMat == null) defMat = n.sharedMaterial;
                pieces.Add(n);
            }
            var sr = pieces[used++];
            sr.gameObject.SetActive(true);
            sr.sprite = sp;
            var t = sr.transform;
            t.position = center; t.rotation = Quaternion.Euler(0, 0, rotDeg); t.localScale = new Vector3(sx, sy, 1f);
            sr.color = c; sr.sortingOrder = order;
            sr.sharedMaterial = add ? Res.AddMat : defMat;
        }

        static Vector2 Dir(float deg) { float r = deg * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), Mathf.Sin(r)); }

        void Draw()
        {
            used = 0;
            bool live = age >= tele;
            float prog = live ? 1f : Mathf.Clamp01(age / tele);
            int order = live ? 130 : -200;
            float fillA = live ? 0.8f : (forming ? 0.08f : 0.12f) + 0.2f * prog;
            Color fill = new Color(col.r, col.g, col.b, fillA);
            Color core = new Color(col.r, col.g, col.b, 0.35f);
            Color line = live ? new Color(1f, 1f, 1f, 0.9f)
                : new Color(Mathf.Lerp(col.r, 1f, 0.4f), Mathf.Lerp(col.g, 1f, 0.4f), Mathf.Lerp(col.b, 1f, 0.4f), forming ? 0.35f : 0.85f);
            int lo = order + (live ? 5 : 2);
            var W = Res.White;

            switch (shape)
            {
                case HShape.Circle:
                    Put(Res.Circle, pos, radius * 2f, radius * 2f, 0, fill, order, live);
                    if (!live) Put(Res.Circle, pos, radius * 2f * prog, radius * 2f * prog, 0, core, order + 1, false);
                    Put(Res.Ring, pos, radius * 2f / 0.9f, radius * 2f / 0.9f, 0, line, lo, live);
                    break;
                case HShape.Bar:
                case HShape.Segment:
                {
                    Vector2 s = shape == HShape.Bar ? pos : a;
                    Vector2 e = shape == HShape.Bar ? pos + Dir(angleDeg) * len : b;
                    Vector2 d = e - s; float l = d.magnitude;
                    float rot = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                    Vector2 mid = (s + e) * 0.5f, side = Dir(rot + 90f) * width * 0.5f;
                    Put(W, mid, l, width, rot, fill, order, live);
                    if (!live) Put(W, mid, l, width * prog, rot, core, order + 1, false);
                    else Put(W, mid, l, width * 0.35f, rot, line, lo, true);
                    Put(W, mid + side, l, 0.07f, rot, line, lo, live);
                    Put(W, mid - side, l, 0.07f, rot, line, lo, live);
                    break;
                }
                case HShape.Cone:
                {
                    int n = Mathf.Max(2, Mathf.CeilToInt(arcDeg / 6f));
                    float step = arcDeg / n;
                    float wd = 2f * radius * Mathf.Sin(step * 0.5f * Mathf.Deg2Rad) * 1.1f;
                    for (int i = 0; i < n; i++)
                    {
                        float ang = angleDeg - arcDeg * 0.5f + step * (i + 0.5f);
                        Put(W, pos + Dir(ang) * radius * 0.5f, radius, wd, ang, fill, order, live);
                    }
                    for (int k = -1; k <= 1; k += 2)
                    {
                        float edge = angleDeg + arcDeg * 0.5f * k;
                        Put(W, pos + Dir(edge) * radius * 0.5f, radius, 0.08f, edge, line, lo, live);
                    }
                    break;
                }
                case HShape.Ring:
                case HShape.Annulus:
                {
                    float rm = (radius + radius2) * 0.5f, th = radius - radius2;
                    int n = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * rm / 0.6f), 24, 96);
                    float step = 360f / n, arc = 2f * Mathf.PI * rm / n * 1.08f;
                    for (int i = 0; i < n; i++)
                    {
                        float ang = step * (i + 0.5f);
                        bool inGap = false;
                        if (gapDirsDeg != null) foreach (var gd in gapDirsDeg) if (AngDiff(ang, gd + angleDeg) <= gapWidthDeg * 0.5f) { inGap = true; break; }
                        if (inGap) continue;
                        Put(W, pos + Dir(ang) * rm, th, arc, ang, fill, order, live);
                    }
                    break;
                }
            }
            for (int i = used; i < pieces.Count; i++) pieces[i].gameObject.SetActive(false);
        }
    }
}

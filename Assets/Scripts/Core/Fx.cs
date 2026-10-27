using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    /// <summary>Pooled juice: particles, rings, damage numbers, lightning, shake and hit-stop.</summary>
    public static class Fx
    {
        class P
        {
            public Transform t; public SpriteRenderer sr; public Vector2 v; public float life, max, s0, s1, drag, spin, grav, aspect = 1f;
            public Color c0, c1; public bool active;
        }
        class TxtP { public Transform t; public TextMesh tm, sh; public float life, max, scale; public Vector2 v; public bool active; }
        class Bolt { public LineRenderer lr; public float life, max; public Color c; public bool active; }

        static readonly List<P> ps = new List<P>();
        static readonly Stack<P> freeP = new Stack<P>();
        static readonly List<TxtP> ts = new List<TxtP>();
        static readonly List<Bolt> bolts = new List<Bolt>();
        static Transform root;

        public static void Init() { root = new GameObject("FX").transform; }

        static P GetP(bool additive)
        {
            P p = freeP.Count > 0 ? freeP.Pop() : null;
            if (p == null)
            {
                if (ps.Count > 900) return null;
                var go = new GameObject("p");
                go.transform.SetParent(root);
                p = new P { t = go.transform, sr = go.AddComponent<SpriteRenderer>() };
                ps.Add(p);
            }
            p.t.gameObject.SetActive(true);
            p.sr.sharedMaterial = additive ? Res.AddMat : Res.FlashMat;
            p.active = true; p.spin = 0; p.grav = 0; p.aspect = 1f;
            return p;
        }

        public static void Particle(Vector2 pos, Vector2 vel, Color c0, Color c1, float size0, float size1, float life,
            Sprite spr = null, bool additive = false, float drag = 4f, int order = 110, float spin = 0, float grav = 0)
        {
            var p = GetP(additive);
            if (p == null) return;
            p.t.position = new Vector3(pos.x, pos.y, 0);
            p.t.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            p.sr.sprite = spr ?? Res.White;
            p.sr.sortingOrder = order;
            p.v = vel; p.c0 = c0; p.c1 = c1; p.s0 = size0; p.s1 = size1; p.life = 0; p.max = life; p.drag = drag; p.spin = spin; p.grav = grav;
            p.sr.color = c0; p.t.localScale = Vector3.one * size0;
        }

        public static void Burst(Vector2 pos, Color c, int n, float speed, float size = 0.14f, float life = 0.45f, bool additive = false)
        {
            for (int i = 0; i < n; i++)
            {
                var dir = Random.insideUnitCircle.normalized * Random.Range(0.35f, 1f) * speed;
                var c1 = c; c1.a = 0;
                Particle(pos, dir, c, c1, size * Random.Range(0.7f, 1.3f), size * 0.3f, life * Random.Range(0.7f, 1.2f), null, additive, 5f, 110, Random.Range(-400f, 400f));
            }
        }

        public static void Glow(Vector2 pos, Color c, float size, float life)
        {
            var c1 = c; c1.a = 0;
            Particle(pos, Vector2.zero, c, c1, size, size * 1.4f, life, Res.Glow, true, 0, 105);
        }

        public static void Ring(Vector2 pos, Color c, float r0, float r1, float life)
        {
            var c1 = c; c1.a = 0;
            Particle(pos, Vector2.zero, c, c1, r0 * 2, r1 * 2, life, Res.Ring, true, 0, 108);
        }

        public static void SlashArc(Vector2 pos, Vector2 dir, Color c, float size, float life)
        {
            var p = GetP(true);
            if (p == null) return;
            p.t.position = pos;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            p.t.rotation = Quaternion.Euler(0, 0, ang);
            p.sr.sprite = Res.Slash; p.sr.sortingOrder = 108;
            var c1 = c; c1.a = 0;
            p.v = dir * 2f; p.c0 = c; p.c1 = c1; p.s0 = size * 0.8f; p.s1 = size * 1.15f; p.life = 0; p.max = life; p.drag = 6; p.spin = 0; p.grav = 0;
        }

        /// <summary>Stretched streak (thrust trail) along dir, centered at pos.</summary>
        public static void Streak(Vector2 pos, Vector2 dir, float len, Color c, float width, float life)
        {
            var p = GetP(true);
            if (p == null) return;
            p.t.position = pos;
            p.t.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            p.sr.sprite = Res.White; p.sr.sortingOrder = 107;
            var c1 = c; c1.a = 0;
            p.v = Vector2.zero; p.c0 = c; p.c1 = c1; p.s0 = width; p.s1 = width * 0.2f; p.life = 0; p.max = life; p.drag = 0;
            p.aspect = len / width;
        }

        public static void Text(Vector2 pos, string s, Color c, float scale = 1f, float life = 0.7f)
        {
            TxtP tp = null;
            foreach (var x in ts) if (!x.active) { tp = x; break; }
            if (tp == null)
            {
                if (ts.Count > 160) return;
                var go = new GameObject("txt");
                go.transform.SetParent(root);
                var sh = new GameObject("sh");
                sh.transform.SetParent(go.transform);
                sh.transform.localPosition = new Vector3(0.04f, -0.04f, 0.01f);
                tp = new TxtP { t = go.transform, tm = MakeTM(go, 301), sh = MakeTM(sh, 300) };
                tp.sh.color = new Color(0, 0, 0, 0.85f);
                ts.Add(tp);
            }
            tp.t.gameObject.SetActive(true);
            tp.active = true;
            s = Loc.Tr(s);
            if (tp.tm.font != Res.Font)
            {
                tp.tm.font = tp.sh.font = Res.Font;
                tp.tm.GetComponent<MeshRenderer>().sharedMaterial = tp.sh.GetComponent<MeshRenderer>().sharedMaterial = Res.Font.material;
            }
            tp.tm.text = s; tp.sh.text = s; tp.tm.color = c;
            tp.t.position = pos + new Vector2(Random.Range(-0.2f, 0.2f), 0.3f);
            tp.v = new Vector2(Random.Range(-0.6f, 0.6f), 2.6f);
            tp.life = 0; tp.max = life; tp.scale = scale;
        }

        static TextMesh MakeTM(GameObject go, int order)
        {
            var tm = go.AddComponent<TextMesh>();
            tm.font = Res.Font;
            tm.fontSize = 64; tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Res.Font.material;
            mr.sortingOrder = order;
            return tm;
        }

        public static void Lightning(Vector2 a, Vector2 b, Color c, float width = 0.12f, float life = 0.18f, int segs = 8, float jag = 0.35f)
        {
            Bolt bo = null;
            foreach (var x in bolts) if (!x.active) { bo = x; break; }
            if (bo == null)
            {
                var go = new GameObject("bolt");
                go.transform.SetParent(root);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = Res.AddMat;
                lr.useWorldSpace = true;
                lr.sortingOrder = 140;
                lr.numCapVertices = 2;
                bo = new Bolt { lr = lr };
                bolts.Add(bo);
            }
            bo.active = true; bo.life = 0; bo.max = life; bo.c = c;
            bo.lr.gameObject.SetActive(true);
            bo.lr.positionCount = segs + 1;
            Vector2 d = b - a, n = new Vector2(-d.y, d.x).normalized;
            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs;
                float off = (i == 0 || i == segs) ? 0 : Random.Range(-jag, jag);
                var p = a + d * t + n * off;
                bo.lr.SetPosition(i, new Vector3(p.x, p.y, 0));
            }
            bo.lr.startWidth = width; bo.lr.endWidth = width * 0.6f;
            bo.lr.startColor = c; bo.lr.endColor = c;
        }

        /// <summary>Active pooled particles (for tests).</summary>
        public static int ActiveCount { get { int n = 0; foreach (var p in ps) if (p.active) n++; return n; } }

        /// <summary>Spinning magic circle lying under the feet, fading out.</summary>
        public static void Sigil(Vector2 pos, Color c, float radius, float life, float spin = 40f)
        {
            var c1 = c; c1.a = 0;
            Particle(pos, Vector2.zero, c, c1, radius * 2f, radius * 2.3f, life, Res.Sigil, true, 0, 106, spin);
            var p = ps.FindLast(x => x.active);
            if (p != null) p.aspect = 1f;
        }

        /// <summary>Full-screen colour wash (alpha clamped to 0.12 by the UI).</summary>
        public static void ScreenTint(Color c, float alpha, float life) => Game.I.ui.Tint(c, alpha, life);

        /// <summary>Enlarged additive copy of a sprite that rises behind the hero and fades (Dharma form).</summary>
        public static void Apparition(Sprite s, Vector2 pos, Color c, float scale, float life)
        {
            var c0 = new Color(c.r, c.g, c.b, 0.55f); var c1 = new Color(c.r, c.g, c.b, 0f);
            Particle(pos + Vector2.up * 0.6f, new Vector2(0, 1.2f), c0, c1, scale, scale * 1.15f, life, s, true, 1f, 101);
        }

        /// <summary>Vertical column of light.</summary>
        public static void Pillar(Vector2 pos, Color c, float height, float life)
        {
            var c0 = new Color(c.r, c.g, c.b, 0.8f); var c1 = new Color(c.r, c.g, c.b, 0f);
            Particle(pos + Vector2.up * height * 0.5f, Vector2.zero, c0, c1, height, height, life, Res.Glow, true, 0, 107);
            var p = ps.FindLast(x => x.active);
            if (p != null) p.aspect = 0.18f;
        }

        public static void Shake(float mag, float dur = 0.15f) => Game.I.AddShake(mag, dur);
        public static void Hitstop(float t) => Game.I.Hitstop(t);

        public static void Tick(float dt)
        {
            for (int i = 0; i < ps.Count; i++)
            {
                var p = ps[i];
                if (!p.active) continue;
                p.life += dt;
                if (p.life >= p.max) { p.active = false; p.t.gameObject.SetActive(false); freeP.Push(p); continue; }
                float k = p.life / p.max;
                p.v *= Mathf.Max(0, 1f - p.drag * dt);
                p.v.y -= p.grav * dt;
                p.t.position += (Vector3)(p.v * dt);
                if (p.spin != 0) p.t.Rotate(0, 0, p.spin * dt);
                float sc = Mathf.Lerp(p.s0, p.s1, k);
                p.t.localScale = new Vector3(sc * p.aspect, sc, 1f);
                p.sr.color = Color.Lerp(p.c0, p.c1, k);
            }
            foreach (var tp in ts)
            {
                if (!tp.active) continue;
                tp.life += dt;
                if (tp.life >= tp.max) { tp.active = false; tp.t.gameObject.SetActive(false); continue; }
                float k = tp.life / tp.max;
                tp.v *= Mathf.Max(0, 1f - 5f * dt);
                tp.t.position += (Vector3)(tp.v * dt);
                float pop = k < 0.15f ? Mathf.Lerp(1.6f, 1f, k / 0.15f) : 1f;
                tp.t.localScale = Vector3.one * tp.scale * pop * (k > 0.7f ? Mathf.Lerp(1f, 0.6f, (k - 0.7f) / 0.3f) : 1f);
                var c = tp.tm.color; c.a = k > 0.6f ? 1f - (k - 0.6f) / 0.4f : 1f; tp.tm.color = c;
                var sc = tp.sh.color; sc.a = c.a * 0.85f; tp.sh.color = sc;
            }
            foreach (var b in bolts)
            {
                if (!b.active) continue;
                b.life += dt;
                if (b.life >= b.max) { b.active = false; b.lr.gameObject.SetActive(false); continue; }
                var c = b.c; c.a = 1f - b.life / b.max;
                b.lr.startColor = c; b.lr.endColor = c;
            }
        }

        public static void ClearAll()
        {
            foreach (var p in ps) if (p.active) { p.active = false; p.t.gameObject.SetActive(false); freeP.Push(p); }
            foreach (var t in ts) { t.active = false; t.t.gameObject.SetActive(false); }
            foreach (var b in bolts) { b.active = false; b.lr.gameObject.SetActive(false); }
        }
    }

    public static class Ach
    {
        public static void Unlock(string id)
        {
            if (Game.I == null || Game.I.save == null || Game.I.save.Has(id)) return;
            Game.I.save.ach.Add(id);
            Platform.Services.UnlockAchievement(id);
            Game.I.save.daoHanh += 20;
            Game.I.save.Save();
            var def = System.Array.Find(Content.Achs, a => a.id == id);
            if (def != null && Game.I.ui != null) Game.I.ui.Toast("Đạt thành tựu: " + def.name, "+20 Đạo Hạnh");
            Sfx.Play("confirm", 0.7f, 0f, 1.2f);
        }
    }
}

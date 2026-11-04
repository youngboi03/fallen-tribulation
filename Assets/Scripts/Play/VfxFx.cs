using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    /// <summary>
    /// World-space player of one spell-effect strip (Res.Vfx): one SpriteRenderer, Game.Dt time, scaled to world units per effect pixel,
    /// pooled. One-shots recycle themselves at the last frame; loops run until life ends or Stop() (then fade out).
    /// </summary>
    public class VfxFx : MonoBehaviour
    {
        static readonly List<VfxFx> live = new List<VfxFx>();
        static readonly Stack<VfxFx> pool = new Stack<VfxFx>();
        static readonly List<VfxFx> scorches = new List<VfxFx>();
        /// <summary>Scorch decals kept at once; the oldest goes first.</summary>
        public const int ScorchCap = 24;
        public static int LiveCount => live.Count;
        public static int ScorchCount { get { scorches.RemoveAll(s => s == null || !s.gameObject.activeSelf); return scorches.Count; } }

        /// <summary>Mean footprint radius (px) of the top-down ground effects: world scale = radius / FootR.</summary>
        public static float FootR(string name)
        {
            switch (name) { case "poison_pool": case "ink_pool": case "blood_pool": return 36f; case "burning_ground": return 33f; case "blood_rain": return 34f; case "scorch": return 31f; case "rune_violet": case "rune_gold": case "rune_blood": return 46f; default: return 30f; }
        }

        SpriteRenderer sr;
        VfxClip clip;
        float age, life, speed = 1f, delay, fadeIn, fadeOut = 0.3f, stopT = -1f, baseScale = 1f, pulse;
        Transform follow; Vector2 off; int order; bool add, scorch;
        Color tint = Color.white;
        /// <summary>Extra scale over time: (age) -> multiplier (flare then burn-down); null = 1.</summary>
        public System.Func<float, float> scaleOf;
        public System.Func<float, float> alphaOf;
        public bool Finished => clip == null || !gameObject.activeSelf;
        public float Age => age;

        /// <summary>
        /// Plays strip <paramref name="name"/> at pos, unitsPerPx world units per effect pixel. life 0 = one clip (loops: until Stop).
        /// order = sorting order (relative to the follow target's y-sort when follow is set). delay = seconds before it shows;
        /// speed stretches time (meteor: the impact lands on time).
        /// </summary>
        public static VfxFx Play(string name, Vector2 pos, float unitsPerPx, float life = 0f, int order = 140, Transform follow = null, Vector2 offset = default,
            float speed = 1f, float delay = 0f, Color? tint = null, bool add = false, float fadeIn = 0f, float fadeOut = 0.3f)
        {
            var clip = Res.Vfx(name);
            if (clip == null) return null;
            VfxFx f = null;
            while (pool.Count > 0 && f == null) f = pool.Pop();
            if (f == null)
            {
                var go = new GameObject("vfx");
                f = go.AddComponent<VfxFx>();
                f.sr = go.AddComponent<SpriteRenderer>();
            }
            f.gameObject.SetActive(true);
            f.clip = clip; f.age = 0f; f.life = life; f.speed = speed; f.delay = delay; f.fadeIn = fadeIn; f.fadeOut = fadeOut; f.stopT = -1f;
            f.follow = follow; f.off = offset; f.order = order; f.add = add; f.scorch = false;
            f.tint = tint ?? Color.white;
            f.baseScale = unitsPerPx * VfxClip.Ppu;
            f.scaleOf = null; f.alphaOf = null;
            f.sr.sharedMaterial = add ? Res.AddMat : Sprite2DMat();
            f.transform.position = follow != null ? (Vector3)((Vector2)follow.position + offset) : (Vector3)pos;
            f.transform.localScale = Vector3.one * f.baseScale;
            f.sr.enabled = delay <= 0f;
            f.sr.sprite = clip.frames[0];
            f.sr.color = f.tint;
            live.Add(f);
            return f;
        }

        /// <summary>Ground effect (pool, burning ground, scorch) scaled so its mean footprint has world radius radius.</summary>
        public static VfxFx Ground(string name, Vector2 pos, float radius, float life = 0f, int order = -190, float fadeIn = 0.2f, float fadeOut = 0.6f)
            => Play(name, pos, radius / FootR(name), life, order, null, default, 1f, 0f, null, false, fadeIn, fadeOut);

        /// <summary>Scorch decal (no damage) that stays until wave end; at most ScorchCap, the oldest is dropped.</summary>
        public static VfxFx Scorch(Vector2 pos, float radius)
        {
            while (scorches.Count > 0 && (scorches[0] == null || !scorches[0].gameObject.activeSelf)) scorches.RemoveAt(0);
            if (scorches.Count >= ScorchCap) { var old = scorches[0]; scorches.RemoveAt(0); if (old != null) old.Recycle(); }
            var f = Ground("scorch", pos, radius, 0f, -195, 0.4f, 0.6f);
            if (f != null) { f.scorch = true; scorches.Add(f); }
            return f;
        }

        public static void ClearScorches()
        {
            foreach (var s in scorches.ToArray()) if (s != null) s.Recycle();
            scorches.Clear();
        }

        /// <summary>Recycles every effect that is not following a target (wave end, run reset).</summary>
        public static void ClearAll()
        {
            ClearScorches();
            foreach (var f in live.ToArray()) if (f != null && f.follow == null) f.Recycle();
        }

        static Material defMat;
        static Material Sprite2DMat()
        {
            if (defMat == null) { var t = new GameObject("tmp").AddComponent<SpriteRenderer>(); defMat = t.sharedMaterial; Destroy(t.gameObject); }
            return defMat;
        }

        /// <summary>Begins the fade-out (loops) or ends a one-shot at once.</summary>
        public void Stop() { if (clip != null && stopT < 0f) stopT = 0f; }

        void Recycle()
        {
            live.Remove(this);
            clip = null; follow = null; scaleOf = null; alphaOf = null;
            gameObject.SetActive(false);
            pool.Push(this);
        }

        void Update()
        {
            if (clip == null) return;
            var g = Game.I;
            if (g == null) return;
            float dt = Game.Dt;
            if (!g.Playing) dt = 0f;
            if (delay > 0f) { delay -= dt; if (delay > 0f) return; sr.enabled = true; }
            age += dt * speed;
            if (stopT >= 0f) stopT += dt;
            if (life > 0f && age >= life && stopT < 0f) stopT = 0f;
            int n = clip.frames.Length;
            int i = Mathf.FloorToInt(age * 1000f / Mathf.Max(1f, clip.ms));
            if (clip.loop) i %= n;
            else if (i >= n) { if (life > 0f && stopT < 0f) i = n - 1; else { Recycle(); return; } }
            sr.sprite = clip.frames[i];
            float a = 1f;
            if (fadeIn > 0f) a = Mathf.Min(a, age / fadeIn);
            if (stopT >= 0f) { a = Mathf.Min(a, 1f - stopT / Mathf.Max(0.01f, fadeOut)); if (stopT >= fadeOut) { Recycle(); return; } }
            if (alphaOf != null) a *= alphaOf(age);
            sr.color = new Color(tint.r, tint.g, tint.b, tint.a * Mathf.Clamp01(a));
            float sc = baseScale * (scaleOf != null ? scaleOf(age) : 1f);
            transform.localScale = new Vector3(sc, sc, 1f);
            if (follow != null)
            {
                transform.position = (Vector2)follow.position + off;
                sr.sortingOrder = Mathf.RoundToInt(-follow.position.y * 5f) + order;
            }
            else sr.sortingOrder = order;
        }

        void OnDestroy() { live.Remove(this); }
    }
}

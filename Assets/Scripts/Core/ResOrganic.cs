using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VK
{
    /// <summary>One exported spell-effect strip (tools/vfx_export.py): frames side by side, pivot in pixels from the top-left of a frame.</summary>
    public sealed class VfxClip
    {
        public string name;
        public Sprite[] frames;
        public float ms;
        public bool loop;
        public int w, h, count;
        public Vector2 pivot;
        public const float Ppu = 32f;
        public float Duration => frames.Length * ms / 1000f;
        /// <summary>World size of one effect pixel at scale 1.</summary>
        public const float UnitsPerPx = 1f / Ppu;
    }

    /// <summary>Procedural organic textures for boss hazards (irregular thin edge band, denser at the rim, grain) and the Vfx strip loader.</summary>
    public static partial class Res
    {
        const int Variants = 4;
        static Sprite[] blobs, blobRims, rags, ragLens;

        /// <summary>Irregular disc, base radius 0.46 of the unit (edge wobbles about 8% of that), alpha denser at the rim; 4 close variants to boil between.</summary>
        public static Sprite Blob(int v) { if (blobs == null) blobs = MakeSet(MakeBlob); return blobs[((v % Variants) + Variants) % Variants]; }
        /// <summary>Only the rim of Blob (a thin irregular ring, same outline): the visible edge line of a circle hazard.</summary>
        public static Sprite BlobRim(int v) { if (blobRims == null) blobRims = MakeSet(i => MakeBlob(i, true)); return blobRims[((v % Variants) + Variants) % Variants]; }
        /// <summary>Ragged bar/segment (x along, y across): edges wobble a few percent, alpha denser at the two long edges.</summary>
        public static Sprite Rag(int v) { if (rags == null) rags = MakeSet(i => MakeRag(i, false)); return rags[((v % Variants) + Variants) % Variants]; }
        /// <summary>Like Rag but also denser towards x = 1 (the outer end of a cone wedge).</summary>
        public static Sprite RagLen(int v) { if (ragLens == null) ragLens = MakeSet(i => MakeRag(i, true)); return ragLens[((v % Variants) + Variants) % Variants]; }

        /// <summary>Visual radius scale: a Blob drawn with diameter 2 * R * BlobScale has its mean edge at R.</summary>
        public const float BlobScale = 1f / 0.92f;

        static Sprite[] MakeSet(System.Func<int, Sprite> f) { var a = new Sprite[Variants]; for (int i = 0; i < Variants; i++) a[i] = f(i); return a; }

        static float Fbm(float x, float y)
        {
            return (Mathf.PerlinNoise(x, y) - 0.5f) * 0.65f + (Mathf.PerlinNoise(x * 2.1f + 7.3f, y * 2.1f + 1.7f) - 0.5f) * 0.35f;
        }

        static float Hash(int x, int y, int s)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + s * 2246822519u);
            h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
            return (h & 0xffff) / 65535f;
        }

        static Sprite MakeBlob(int v) => MakeBlob(v, false);

        static Sprite MakeBlob(int v, bool rimOnly)
        {
            const int N = 96;
            var tex = NewTex(N, N);
            var px = new Color32[N * N];
            float off = v * 0.23f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, w = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + w * w) * 0.5f, ang = Mathf.Atan2(w, u);
                    float cx = Mathf.Cos(ang), cy = Mathf.Sin(ang);
                    float e = 0.46f * (1f + 0.17f * Fbm(cx * 1.4f + 5f + off, cy * 1.4f + 5f + off) + 0.05f * Fbm(cx * 4f + 9f + off, cy * 4f + 9f - off));
                    float edge = Mathf.Clamp01((e - d) * N * 0.55f);
                    float k = Mathf.Clamp01(d / e);
                    float a = rimOnly ? edge * Mathf.SmoothStep(0.8f, 0.96f, k) : edge * Mathf.Lerp(0.42f, 1f, Mathf.SmoothStep(0.25f, 0.97f, k));
                    a *= 1f - 0.28f * Hash(x / 2, y / 2, v) * Mathf.Lerp(0.4f, 1f, 1f - k);   // grain, boils between variants
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            return Finish(tex, px, N, N);
        }

        static Sprite MakeRag(int v, bool along)
        {
            const int W = 128, H = 32;
            var tex = NewTex(W, H);
            var px = new Color32[W * H];
            float off = v * 0.19f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float s = (x + 0.5f) / W, c = Mathf.Abs((y + 0.5f) / H * 2f - 1f);
                    float hw = 0.9f + 0.1f * Fbm(s * 9f + 3f + off, 2f + (y < H / 2 ? 0f : 5f)) * 1.6f;
                    float edge = Mathf.Clamp01((hw - c) * H * 0.5f);
                    float k = Mathf.Clamp01(c / hw);
                    float a = edge * Mathf.Lerp(0.5f, 1f, Mathf.SmoothStep(0.15f, 0.95f, k));
                    if (along) a *= Mathf.Lerp(0.45f, 1f, s);
                    a *= 1f - 0.25f * Hash(x / 2, y / 2, v + 11) * (1f - k * 0.6f);
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            return Finish(tex, px, W, H);
        }

        static Texture2D NewTex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        static Sprite Finish(Texture2D tex, Color32[] px, int w, int h)
        {
            tex.SetPixels32(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Mathf.Max(w, h));   // the longer side is one world unit
        }

        // ---------------------------------------------------------------- spell-effect strips
        static Dictionary<string, VfxClip> vfx;
        static readonly Dictionary<string, VfxClip> vfxLoaded = new Dictionary<string, VfxClip>();

        /// <summary>Loads every strip up front: slicing one on its first cast would hitch mid-fight.</summary>
        public static void PreloadVfx() { foreach (var n in new List<string>(VfxInfo().Keys)) Vfx(n); }

        /// <summary>Spell-effect strip Assets/Resources/VFX/name.png sliced by vfx.json; null when missing. Cached.</summary>
        public static VfxClip Vfx(string name)
        {
            if (vfxLoaded.TryGetValue(name, out var c)) return c;
            var tex = Resources.Load<Texture2D>("VFX/" + name);
            var info = VfxInfo();
            if (tex == null || !info.TryGetValue(name, out var m)) { vfxLoaded[name] = null; return null; }
            tex.filterMode = FilterMode.Point;
            m.name = name;
            int cols = Mathf.Max(1, tex.width / Mathf.Max(1, m.w));   // long clips wrap into rows, top row first
            m.frames = new Sprite[m.count];
            for (int i = 0; i < m.frames.Length; i++)
                m.frames[i] = Sprite.Create(tex, new Rect(i % cols * m.w, tex.height - (i / cols + 1) * m.h, m.w, m.h), new Vector2(m.pivot.x / m.w, 1f - m.pivot.y / m.h), VfxClip.Ppu);
            return vfxLoaded[name] = m;
        }

        static Dictionary<string, VfxClip> VfxInfo()
        {
            if (vfx != null) return vfx;
            vfx = new Dictionary<string, VfxClip>();
            var ta = Resources.Load<TextAsset>("VFX/vfx");
            if (ta == null) return vfx;
            foreach (Match m in Regex.Matches(ta.text, "\"(\\w+)\"\\s*:\\s*\\{([^{}]*)\\}"))
            {
                string b = m.Groups[2].Value;
                var pv = Regex.Match(b, "\"pivot\"\\s*:\\s*\\[\\s*(-?\\d+)\\s*,\\s*(-?\\d+)\\s*\\]");
                var lp = Regex.Match(b, "\"loop\"\\s*:\\s*\"(\\w+)\"");
                vfx[m.Groups[1].Value] = new VfxClip
                {
                    w = Int(b, "w"), h = Int(b, "h"), ms = Int(b, "ms"), count = Int(b, "frames"),
                    pivot = new Vector2(float.Parse(pv.Groups[1].Value), float.Parse(pv.Groups[2].Value)),
                    loop = lp.Success && lp.Groups[1].Value == "loop",
                };
            }
            return vfx;
        }

        static int Int(string b, string key) => int.Parse(Regex.Match(b, "\"" + key + "\"\\s*:\\s*(\\d+)").Groups[1].Value);
    }
}

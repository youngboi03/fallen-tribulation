using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    /// <summary>Loads Kenney sprites/audio from Resources and builds procedural textures.</summary>
    public static class Res
    {
        static readonly Dictionary<int, Sprite> tiles = new Dictionary<int, Sprite>();
        static readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        static Font font;
        static Sprite white, circle, glow, ring, diamond, slash;
        static Material flashMat, addMat;

        public const int TalismanId = 200;

        public static Sprite Tile(int i)
        {
            if (tiles.TryGetValue(i, out var s)) return s;
            if (i == TalismanId) { s = MakeTalisman(); tiles[i] = s; return s; }
            s = Resources.Load<Sprite>("Tiles/tile_" + i.ToString("0000"));
            if (s == null)
            {   // fallback if the importer didn't mark it as a sprite
                var tex = Resources.Load<Texture2D>("Tiles/tile_" + i.ToString("0000"));
                if (tex != null) { tex.filterMode = FilterMode.Point; s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 16); }
            }
            tiles[i] = s;
            return s;
        }

        public static AudioClip Clip(string name)
        {
            if (!clips.TryGetValue(name, out var arr))
            {
                var list = new List<AudioClip>();
                var c = Resources.Load<AudioClip>("Audio/" + name);
                if (c != null) list.Add(c);
                for (int i = 0; i < 8; i++) { var v = Resources.Load<AudioClip>("Audio/" + name + i); if (v != null) list.Add(v); }
                arr = list.ToArray();
                clips[name] = arr;
            }
            return arr.Length == 0 ? null : arr[Random.Range(0, arr.Length)];
        }

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Material FlashMat
        {
            get
            {
                if (flashMat == null)
                {
                    var sh = Resources.Load<Shader>("Shaders/SpriteFlash");
                    flashMat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
                }
                return flashMat;
            }
        }

        public static Material AddMat
        {
            get
            {
                if (addMat == null)
                {
                    var sh = Resources.Load<Shader>("Shaders/SpriteAdd");
                    addMat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
                }
                return addMat;
            }
        }

        /// <summary>16x16 hand-drawn talisman (yellow paper, red seal glyph) in the Kenney style.</summary>
        static Sprite MakeTalisman()
        {
            string[] rows = {
                "................",
                "....OOOOOOOO....",
                "....OPPPPPPO....",
                "....OPPRRPPO....",
                "....OPRPPRPO....",
                "....OPPRRPPO....",
                "....OPPPPPPO....",
                "....OPRRRRPO....",
                "....OPPPRPPO....",
                "....OPRRRRPO....",
                "....OPPPRPPO....",
                "....OPPRPRPO....",
                "....OPRPPPRO....",
                "....OPPPPPPO....",
                "....OOOOOOOO....",
                "................" };
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var O = new Color32(60, 30, 25, 255); var P = new Color32(245, 214, 120, 255); var R = new Color32(200, 40, 35, 255); var T = new Color32(0, 0, 0, 0);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    char ch = rows[15 - y][x];
                    tex.SetPixel(x, y, ch == 'O' ? O : ch == 'P' ? P : ch == 'R' ? R : T);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
        }

        static Sprite Make(int size, System.Func<float, float, float> alpha, FilterMode fm = FilterMode.Bilinear, float ppu = -1)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = fm, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    byte a = (byte)(Mathf.Clamp01(alpha(u, v)) * 255);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu > 0 ? ppu : size);
        }

        /// <summary>1x1 world unit solid square.</summary>
        public static Sprite White => white ??= Make(4, (u, v) => 1f, FilterMode.Point);
        /// <summary>Hard-edged disc, diameter 1 unit.</summary>
        public static Sprite Circle => circle ??= Make(64, (u, v) => (1f - Mathf.Sqrt(u * u + v * v)) * 32f);
        /// <summary>Soft radial glow, diameter 1 unit.</summary>
        public static Sprite Glow => glow ??= Make(64, (u, v) => { float d = Mathf.Sqrt(u * u + v * v); return Mathf.Pow(Mathf.Clamp01(1f - d), 2f); });
        /// <summary>Thin ring, diameter 1 unit.</summary>
        public static Sprite Ring => ring ??= Make(128, (u, v) => { float d = Mathf.Sqrt(u * u + v * v); return 1f - Mathf.Abs(d - 0.9f) * 28f; });
        public static Sprite Diamond => diamond ??= Make(16, (u, v) => (1f - (Mathf.Abs(u) + Mathf.Abs(v))) * 8f, FilterMode.Point);
        /// <summary>Crescent slash arc.</summary>
        public static Sprite Slash => slash ??= Make(128, (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v);
            float band = 1f - Mathf.Abs(d - 0.78f) * 9f;
            float ang = Mathf.Atan2(v, u);           // arc in the upper half, thicker in the middle
            float fade = Mathf.Clamp01(Mathf.Sin(Mathf.Clamp(ang, 0f, Mathf.PI)) * 1.6f);
            return band * fade * (v > 0 ? 1f : 0f);
        });
    }
}

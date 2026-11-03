using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    /// <summary>Loads Kenney sprites/audio from Resources and builds procedural textures.</summary>
    public static class Res
    {
        static readonly Dictionary<int, Sprite> tiles = new Dictionary<int, Sprite>();
        static readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        static readonly Dictionary<string, Sprite[]> animCache = new Dictionary<string, Sprite[]>();
        static Font font;
        static Sprite white, circle, glow, ring, diamond, slash;
        static Material flashMat, addMat;

        public const int TalismanId = 200;

        /// <summary>Sprite ids of the PixelLab art.</summary>
        public static class Px
        {
            public const int KiemTu = 300, DanTu = 301, TheTu = 302, PhuTu = 303, MaTu = 304, LoiTu = 305;
            public const int HuyetThu = 310, MaBuc = 311, DocTrung = 312, DocChu = 313, OanHon = 314, ThietBoiThu = 315, TaTu = 316,
                DocNhanMa = 317, HoaMa = 318, MaTuong = 319, BaoHon = 320, Mimic = 321, AnhSat = 322, MimicSleep = 323;
            public const int Boss1 = 340, Boss2 = 341, Boss3 = 342, Boss4 = 343, Boss1b = 344, Boss2b = 345;
        }

        /// <summary>PixelLab sprites (Assets/Resources/PixelLab) addressed by id like the Kenney tiles; a missing file falls back to the Kenney tile.</summary>
        static readonly Dictionary<int, (string path, int fallback)> pixelLab = new Dictionary<int, (string, int)>
        {
            { Px.KiemTu, ("Chars/kiem_tu", 98) }, { Px.DanTu, ("Chars/dan_tu", 84) }, { Px.TheTu, ("Chars/the_tu", 86) },
            { Px.PhuTu, ("Chars/phu_tu", 99) }, { Px.MaTu, ("Chars/ma_tu", 112) }, { Px.LoiTu, ("Chars/loi_tu", 88) },
            { Px.HuyetThu, ("Enemies/huyet_thu", 123) }, { Px.MaBuc, ("Enemies/ma_buc", 120) }, { Px.DocTrung, ("Enemies/doc_trung", 108) },
            { Px.DocChu, ("Enemies/doc_chu", 122) }, { Px.OanHon, ("Enemies/oan_hon", 121) }, { Px.ThietBoiThu, ("Enemies/thiet_boi_thu", 124) },
            { Px.TaTu, ("Enemies/ta_tu", 111) }, { Px.DocNhanMa, ("Enemies/doc_nhan_ma", 109) }, { Px.HoaMa, ("Enemies/hoa_ma", 110) },
            { Px.MaTuong, ("Enemies/ma_tuong", 87) }, { Px.BaoHon, ("Enemies/bao_hon", 121) }, { Px.Mimic, ("Enemies/bao_ruong_yeu", 92) },
            { Px.AnhSat, ("Enemies/anh_sat", 111) }, { Px.MimicSleep, ("Enemies/bao_ruong_ngu", 89) },
            { Px.Boss1, ("Bosses/b1", 109) }, { Px.Boss2, ("Bosses/b2", 110) }, { Px.Boss3, ("Bosses/b3", 112) },
            { Px.Boss4, ("Bosses/b4", 111) }, { Px.Boss1b, ("Bosses/b1b", 120) }, { Px.Boss2b, ("Bosses/b2b", 122) },
        };

        /// <summary>Points sprite id <paramref name="id"/> at a PixelLab file if it exists; true when it does.</summary>
        public static bool RegisterPixelLab(int id, string path, int fallback)
        {
            if (Resources.Load<Sprite>("PixelLab/" + path) == null) return false;
            pixelLab[id] = (path, fallback);
            tiles.Remove(id);
            return true;
        }

        /// <summary>Direction the art of sprite <paramref name="id"/> points, in degrees: Kenney weapons point up, PixelLab icons (400-599) to the upper right.</summary>
        public static float SpriteAngle(int id) => id >= 400 && id < 600 ? 45f : 90f;

        /// <summary>Boss intro portrait (Task 12); null until the PNG exists, the card then uses the boss sprite.</summary>
        public static Sprite BossPortrait(string id) => Resources.Load<Sprite>("PixelLab/Portraits/" + id);

        public static Sprite Tile(int i)
        {
            if (tiles.TryGetValue(i, out var s)) return s;
            if (i == TalismanId) { s = MakeTalisman(); tiles[i] = s; return s; }
            if (pixelLab.TryGetValue(i, out var px))
            {
                s = Resources.Load<Sprite>("PixelLab/" + px.path);
                if (s == null) s = Tile(px.fallback);
                tiles[i] = s;
                return s;
            }
            s = Resources.Load<Sprite>("Tiles/tile_" + i.ToString("0000"));
            if (s == null)
            {   // fallback if the importer didn't mark it as a sprite
                var tex = Resources.Load<Texture2D>("Tiles/tile_" + i.ToString("0000"));
                if (tex != null) { tex.filterMode = FilterMode.Point; s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 16); }
            }
            tiles[i] = s;
            return s;
        }

        public static Sprite[] Anim(string id, string clip)
        {
            string key = id + "/" + clip;
            if (animCache.TryGetValue(key, out var cached)) return cached;
            var def = AnimDefs.Get(id, clip);
            if (def == null) { animCache[key] = null; return null; }
            var tex = Resources.Load<Texture2D>("PixelLab/Chars/" + id + "_" + clip);
            if (tex == null || tex.width != def.frames * tex.height) { animCache[key] = null; return null; }
            tex.filterMode = FilterMode.Point;
            int h = tex.height;
            var arr = new Sprite[def.frames];
            for (int i = 0; i < def.frames; i++)
                arr[i] = Sprite.Create(tex, new Rect(i * h, 0, h, h), new Vector2(0.5f, 0.5f), 48f);
            animCache[key] = arr;
            return arr;
        }

        public static void RegisterClip(string name, AudioClip c) { clips[name] = new[] { c }; }

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
                if (font == null)
                {
                    // Chinese uses an installed OS CJK font (nothing is shipped); other languages use the bundled rounded Baloo 2 (OFL).
                    if (Loc.Lang == Loc.ZH)
                        font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun", "PingFang SC",
                            "Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC", "Droid Sans Fallback", "Arial Unicode MS", "Arial" }, 32);
                    else font = Resources.Load<Font>("Fonts/Baloo2");
                    if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return font;
            }
        }

        public static void ResetFont() => font = null;

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

        static Sprite sigil;
        /// <summary>Code-drawn magic circle: outer ring, 24 ticks, inner hexagram, inner ring.</summary>
        public static Sprite Sigil => sigil ??= Make(256, (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v);
            float a = Mathf.Max(1f - Mathf.Abs(d - 0.95f) * 55f, 1f - Mathf.Abs(d - 0.45f) * 55f);
            float seg = Mathf.PI * 2f / 24f;
            float t = Mathf.Repeat(Mathf.Atan2(v, u), seg) / seg - 0.5f;
            if (d > 0.76f && d < 0.9f && Mathf.Abs(t) < 0.07f) a = 1f;
            if (d < 0.7f)
                for (int i = 0; i < 6; i++)
                {
                    float an = (30f + 60f * i) * Mathf.Deg2Rad;
                    a = Mathf.Max(a, 1f - Mathf.Abs(u * Mathf.Cos(an) + v * Mathf.Sin(an) - 0.35f) * 60f);
                }
            return a;
        });

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

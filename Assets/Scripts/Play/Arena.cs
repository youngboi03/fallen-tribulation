using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    /// <summary>Static tiled arena: floor, stone walls, banners and a formation circle; recolored per region.</summary>
    public class Arena : MonoBehaviour
    {
        public const float HalfW = 17f, HalfH = 10f;   // playable area
        public static readonly string[] RegionName = { "Hoang Sơn", "Hàn Băng Cốc", "Huyết Sát Hải", "Thiên Ma Giới" };
        static readonly Color[] FloorTint = { new Color(1f, 0.95f, 1.02f), new Color(0.7f, 0.85f, 1.1f), new Color(1.15f, 0.6f, 0.6f), new Color(0.8f, 0.65f, 1.15f) };
        static readonly Color[] WallTint = { new Color(0.75f, 0.73f, 0.78f), new Color(0.65f, 0.8f, 0.95f), new Color(0.8f, 0.5f, 0.5f), new Color(0.6f, 0.5f, 0.85f) };
        static readonly Color[] RingTint = { new Color(1f, 0.8f, 0.4f), new Color(0.5f, 0.9f, 1f), new Color(1f, 0.3f, 0.3f), new Color(0.75f, 0.4f, 1f) };
        static readonly Color[] BgTint = { new Color(0.05f, 0.04f, 0.08f), new Color(0.03f, 0.05f, 0.09f), new Color(0.08f, 0.03f, 0.04f), new Color(0.06f, 0.03f, 0.09f) };

        SpriteRenderer circleA, circleB, circleC;
        readonly List<SpriteRenderer> floor = new List<SpriteRenderer>(), walls = new List<SpriteRenderer>();
        readonly List<float> bright = new List<float>();
        readonly List<Vector2Int> floorCell = new List<Vector2Int>();
        static readonly Sprite[][] floorTiles = new Sprite[4][];   // per region: 16 Wang tiles by corner mask, empty = no PixelLab art
        // PixelLab floors keep their painted colours, only dimmed per region so characters stand out (snow needs the most)
        static readonly Color[] FloorDim = { new Color(0.8f, 0.78f, 0.78f), new Color(0.6f, 0.64f, 0.7f), new Color(0.78f, 0.72f, 0.72f), new Color(0.62f, 0.58f, 0.66f) };

        /// <summary>The 16 corner-mask tiles of a region's PixelLab floor (index = NW*8 + NE*4 + SW*2 + SE), or null.</summary>
        static Sprite[] FloorTiles(int region)
        {
            if (floorTiles[region] != null) return floorTiles[region].Length == 0 ? null : floorTiles[region];
            var tex = Resources.Load<Texture2D>("PixelLab/Floors/floor" + region);
            if (tex == null) { floorTiles[region] = new Sprite[0]; return null; }
            int n = tex.height;
            var arr = new Sprite[16];
            for (int i = 0; i < 16; i++) arr[i] = Sprite.Create(tex, new Rect(i * n, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return floorTiles[region] = arr;
        }

        /// <summary>Patchy second terrain: true where the upper terrain covers this grid vertex.</summary>
        static bool Upper(int region, int vx, int vy)
        {
            float f = Mathf.PerlinNoise(vx * 0.17f + region * 31.7f, vy * 0.17f + region * 17.3f);
            float g = Mathf.PerlinNoise(vx * 0.45f + 7.1f + region, vy * 0.45f + 3.3f);
            return f * 0.8f + g * 0.2f > (region == 2 ? 0.6f : region == 3 ? 0.64f : 0.55f);   // fewer bright crystals in the last region: they must not read as bullets
        }

        void ApplyFloorSprites(int region)
        {
            var tiles = FloorTiles(region);
            for (int i = 0; i < floor.Count; i++)
            {
                var c = floorCell[i];
                if (tiles == null) { floor[i].sprite = Res.Tile(0); continue; }
                int m = (Upper(region, c.x, c.y + 1) ? 8 : 0) | (Upper(region, c.x + 1, c.y + 1) ? 4 : 0)
                      | (Upper(region, c.x, c.y) ? 2 : 0) | (Upper(region, c.x + 1, c.y) ? 1 : 0);
                floor[i].sprite = tiles[m];
            }
        }
        Color[] fromF, fromW; Color toF, toW, toR, fromR;
        float themeT = 1f;
        public int Theme { get; private set; } = -1;

        public static Vector2 Clamp(Vector2 p, float r) =>
            new Vector2(Mathf.Clamp(p.x, -HalfW + r, HalfW - r), Mathf.Clamp(p.y, -HalfH + r, HalfH - r));

        public void Build()
        {
            var rng = new System.Random(1234);
            int w = (int)HalfW + 1, h = (int)HalfH + 1;
            for (int x = -w; x < w; x++)
                for (int y = -h; y < h; y++)
                {
                    bool wall = x == -w || x == w - 1 || y == -h || y == h - 1;
                    int tile;
                    if (wall) tile = y == h - 1 ? 40 : 57 + rng.Next(3);
                    else tile = rng.NextDouble() < 0.015 ? 24 : 0;
                    var go = new GameObject("t");
                    go.transform.SetParent(transform, false);
                    go.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = Res.Tile(tile);
                    sr.sortingOrder = wall ? -990 : -1000;
                    if (wall) walls.Add(sr);
                    else { floor.Add(sr); floorCell.Add(new Vector2Int(x, y)); bright.Add(0.62f + (float)rng.NextDouble() * 0.06f); }
                }
            for (int x = -w + 3; x < w - 2; x += 6)
            {
                var go = new GameObject("banner");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(x + 0.5f, h - 0.5f, 0);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Res.Tile(29);
                sr.sortingOrder = -989;
            }
            circleA = MakeCircle(9f, 0.07f);
            circleB = MakeCircle(6f, 0.06f);
            circleC = MakeCircle(12f, 0.04f);
            var v = new GameObject("vignette");
            v.transform.SetParent(transform, false);
            var vs = v.AddComponent<SpriteRenderer>();
            vs.sprite = Res.Glow; vs.sortingOrder = -985;
            vs.color = new Color(0.3f, 0.25f, 0.5f, 0.12f);
            v.transform.localScale = new Vector3(40f, 26f, 1f);
            SetTheme(0, true);
        }

        SpriteRenderer MakeCircle(float d, float a)
        {
            var go = new GameObject("formation");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * d;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Res.Ring; sr.sharedMaterial = Res.AddMat; sr.sortingOrder = -980; sr.color = new Color(1, 1, 1, a);
            return sr;
        }

        /// <summary>Recolor the arena for a region (0-3); fades over ~1.5s unless instant.</summary>
        public void SetTheme(int t, bool instant = false)
        {
            t = Mathf.Clamp(t, 0, RegionName.Length - 1);
            if (t == Theme) return;
            Theme = t;
            toF = FloorTint[t]; toW = WallTint[t]; toR = RingTint[t];
            fromF = new Color[floor.Count]; for (int i = 0; i < floor.Count; i++) fromF[i] = floor[i].color;
            fromW = new Color[walls.Count]; for (int i = 0; i < walls.Count; i++) fromW[i] = walls[i].color;
            fromR = circleA.color;
            ApplyFloorSprites(t);
            themeT = instant ? 0.999f : 0f;
            if (Game.I && Game.I.cam) Game.I.cam.backgroundColor = BgTint[t];
            Apply(instant ? 1f : 0f);
        }

        void Apply(float k)
        {
            for (int i = 0; i < floor.Count; i++)
            {
                float b = bright[i];
                // PixelLab floors are painted in their own colours: only dim them a little so characters stand out
                var target = FloorTiles(Theme) != null ? FloorDim[Theme] * (b / 0.65f) : new Color(toF.r * b, toF.g * b * 0.95f, toF.b * b);
                target.a = 1f;
                floor[i].color = Color.Lerp(fromF[i], target, k);
            }
            for (int i = 0; i < walls.Count; i++) walls[i].color = Color.Lerp(fromW[i], toW, k);
            var rc = Color.Lerp(new Color(fromR.r, fromR.g, fromR.b), toR, k);
            circleA.color = new Color(rc.r, rc.g, rc.b, 0.07f);
            circleC.color = new Color(rc.r, rc.g, rc.b, 0.04f);
        }

        void Update()
        {
            if (themeT < 1f)
            {
                themeT = Mathf.Min(1f, themeT + Time.unscaledDeltaTime / 1.5f);
                Apply(themeT);
            }
            if (circleA) circleA.transform.Rotate(0, 0, Time.deltaTime * 4f);
            Weather();
            if (circleB)
            {
                var c = toR;
                c.a = 0.05f + Mathf.Sin(Time.time * 0.8f) * 0.02f;
                circleB.color = c;
            }
        }

        float weatherT;
        /// <summary>Per-region ambience: drifting dust, snow, rising embers, demonic motes.</summary>
        void Weather()
        {
            var g = Game.I;
            if (g == null || g.paused || (g.state != GState.Playing && g.state != GState.Between) || Theme < 0) return;
            weatherT -= g.state == GState.Playing ? Game.Dt : Time.unscaledDeltaTime;
            if (weatherT > 0) return;
            weatherT = Theme == 1 ? 0.08f : 0.1f;
            float x = Random.Range(-HalfW - 2f, HalfW + 2f), y = Random.Range(-HalfH - 1f, HalfH + 1f);
            switch (Theme)
            {
                case 0:
                    Fx.Particle(new Vector2(x, y), new Vector2(Random.Range(0.3f, 0.8f), Random.Range(-0.1f, 0.2f)), new Color(0.95f, 0.85f, 0.6f, 0.35f),
                        new Color(0.95f, 0.85f, 0.6f, 0f), 0.08f, 0.05f, Random.Range(3f, 5f), Res.Circle, false, 0f, 104);
                    break;
                case 1:
                    Fx.Particle(new Vector2(x, HalfH + 2f), new Vector2(Random.Range(-0.6f, 0.2f), Random.Range(-1.6f, -0.9f)), new Color(0.95f, 0.98f, 1f, 0.7f),
                        new Color(0.9f, 0.95f, 1f, 0f), Random.Range(0.08f, 0.15f), 0.06f, Random.Range(9f, 14f), Res.Circle, false, 0f, 104, 0f);
                    break;
                case 2:
                    Fx.Particle(new Vector2(x, y), new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.5f, 1.2f)), new Color(1f, 0.35f, 0.15f, 0.7f),
                        new Color(1f, 0.1f, 0.05f, 0f), 0.12f, 0.03f, Random.Range(2f, 3.5f), Res.Glow, true, 0f, 104);
                    break;
                default:
                    Fx.Particle(new Vector2(x, y), Random.insideUnitCircle * 0.4f + Vector2.up * 0.3f, new Color(0.7f, 0.35f, 1f, 0.6f),
                        new Color(0.4f, 0.1f, 0.8f, 0f), 0.2f, 0.04f, Random.Range(2.5f, 4f), Res.Glow, true, 0f, 104, 90f);
                    break;
            }
        }
    }
}
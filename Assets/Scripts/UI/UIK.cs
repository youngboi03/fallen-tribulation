using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VK
{
    /// <summary>Tiny helpers to build uGUI from code.</summary>
    public static class UIK
    {
        // Hand-made look: warm ink and leather browns, cream "paper" text, cinnabar red and jade instead of neon.
        public static readonly Color Bg = new Color(0.10f, 0.08f, 0.06f, 0.93f);
        public static readonly Color Panel = new Color(0.21f, 0.16f, 0.12f, 0.97f);
        public static readonly Color Panel2 = new Color(0.29f, 0.22f, 0.16f, 0.98f);
        public static readonly Color Gold = new Color(0.96f, 0.77f, 0.38f);
        public static readonly Color Dim = new Color(0.78f, 0.70f, 0.58f);
        public static readonly Color Cyan = new Color(0.56f, 0.86f, 0.70f);
        public static readonly Color BtnBlue = new Color(0.30f, 0.43f, 0.38f);
        public static readonly Color BtnGold = new Color(0.74f, 0.50f, 0.15f);
        public static readonly Color BtnRed = new Color(0.72f, 0.23f, 0.17f);
        public static readonly Color BtnGrey = new Color(0.38f, 0.31f, 0.25f);
        static readonly Color Cream = new Color(1f, 0.96f, 0.87f);
        static readonly Color Ink = new Color(0.07f, 0.04f, 0.02f, 0.9f);

        static readonly System.Collections.Generic.HashSet<string> Rounded = new System.Collections.Generic.HashSet<string>
            { "card", "w", "vitals", "toast", "syn", "sum", "stats", "row", "opt", "matrix", "it", "inv", "hint", "c", "a", "btn", "bar" };

        static Sprite cut;

        /// <summary>A rounded rectangle with a slightly uneven edge, 9-sliced, so panels look cut by hand instead of ruled.</summary>
        public static Sprite Cut => cut != null ? cut : (cut = MakeCut(0f));

        static Sprite ringThin, ringThick;

        /// <summary>The same shape as <see cref="Cut"/> but only its edge, drawn as a child frame so outlines follow the uneven corners instead of puffing them up.</summary>
        static Sprite Ring(float d) => d <= 2f ? (ringThin != null ? ringThin : (ringThin = MakeCut(3f))) : (ringThick != null ? ringThick : (ringThick = MakeCut(6f)));

        static Sprite MakeCut(float ring)
        {
            {
                const int n = 96; const float half = 44f, rad = 18f;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float qx = Mathf.Abs(x + 0.5f - n / 2f) - (half - rad), qy = Mathf.Abs(y + 0.5f - n / 2f) - (half - rad);
                        float d = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - rad;
                        d += (Mathf.PerlinNoise(x * 0.11f + 3.7f, y * 0.11f + 1.3f) - 0.5f) * 3.4f;
                        float a = Mathf.Clamp01(0.5f - d);
                        if (ring > 0f) a *= Mathf.Clamp01(d + ring + 0.5f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                tex.SetPixels32(px); tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(32, 32, 32, 32));
            }
        }

        public static RectTransform R(Transform parent, string name = "r")
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchors (0..1) plus pixel offsets left, bottom, right, top (right/top as positive insets).</summary>
        public static RectTransform Anchor(this RectTransform rt, float minX, float minY, float maxX, float maxY, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>Fixed-size rect positioned relative to an anchor point.</summary>
        public static RectTransform Place(this RectTransform rt, float ax, float ay, float w, float h, float x = 0, float y = 0)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(ax, ay);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt) => rt.Anchor(0, 0, 1, 1);

        public static Image Img(Transform p, Color c, string name = "img")
        {
            var rt = R(p, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            if (Rounded.Contains(name))   // panels only: an Image whose sprite is replaced later (icons, portraits) must stay Simple or it stretches
            {
                img.sprite = Cut; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1.5f;
                if (c == Panel || c == Panel2)
                {
                    var sh = rt.gameObject.AddComponent<Shadow>();   // hard offset shadow, like a sticker
                    sh.effectColor = new Color(0.04f, 0.02f, 0.01f, 0.55f);
                    sh.effectDistance = new Vector2(5, -5);
                }
            }
            return img;
        }

        public static Image Icon(Transform p, Sprite s, Color c, float size)
        {
            var img = Img(p, c, "icon");
            img.type = Image.Type.Simple; img.pixelsPerUnitMultiplier = 1f;
            img.sprite = s;
            img.preserveAspect = true;
            img.raycastTarget = false;
            LE(img.gameObject, size, size);
            return img;
        }

        public static Text Txt(Transform p, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter, FontStyle st = FontStyle.Normal, bool shadow = true)
        {
            var rt = R(p, "txt");
            var t = rt.gameObject.AddComponent<LocText>();
            t.font = Res.Font;
            t.text = s;
            bool cjk = Loc.Lang == Loc.ZH;   // Baloo 2 is wider and taller than the old font: shrink a little so layouts still fit
            t.fontSize = cjk ? size : Mathf.Max(8, Mathf.RoundToInt(size * 0.92f));
            t.color = c == Color.white ? Cream : c;
            t.alignment = a;
            t.fontStyle = st;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.lineSpacing = cjk ? 1.05f : 0.9f;
            if (shadow)
            {
                var sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = Ink;
                sh.effectDistance = new Vector2(2, -2);
            }
            return t;
        }

        /// <summary>Big title in the brush face with a dark ink outline. Brush script reads badly in ALL CAPS: pass Title Case strings.</summary>
        public static Text Title(Transform p, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter)
        {
            var t = Txt(p, s, size, c, a, FontStyle.Normal, true);
            t.font = Res.TitleFont;
            t.lineSpacing = 1f;
            var ol = t.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0.04f, 0.02f, 0.02f, 0.95f);
            ol.effectDistance = new Vector2(Mathf.Max(2f, size / 30f), -Mathf.Max(2f, size / 30f));
            return t;
        }

        /// <summary>Ink blot that splashes in behind <paramref name="title"/> and then reveals it. Self-driven on unscaled time; dies with its parent.</summary>
        public static InkSplatFx InkSplat(Text title, Color tint, int seed = 0, Color? rim = null)
        {
            var rt = R(title.transform.parent, "inkSplat");
            rt.SetSiblingIndex(title.transform.GetSiblingIndex());
            var fx = rt.gameObject.AddComponent<InkSplatFx>();
            fx.Init(title, tint, seed, rim);
            return fx;
        }

        public static Button Btn(Transform p, string label, Action onClick, Color bg, int size = 26, float w = -1, float h = -1)
        {
            var img = Img(p, bg, "btn");
            var b = img.gameObject.AddComponent<Button>();
            var cb = b.colors;
            cb.normalColor = new Color(0.88f, 0.88f, 0.88f);
            cb.highlightedColor = Color.white;
            cb.selectedColor = new Color(0.88f, 0.88f, 0.88f);
            cb.pressedColor = new Color(0.65f, 0.65f, 0.65f);
            cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
            cb.fadeDuration = 0.06f;
            b.colors = cb;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            var ol = img.gameObject.AddComponent<Shadow>();   // chunky hard shadow under the button
            ol.effectColor = new Color(0.04f, 0.02f, 0.01f, 0.85f);
            ol.effectDistance = new Vector2(0, -6);
            // slight, stable tilt per button so the row never looks ruled
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, ((p.childCount * 37 + label.Length * 11) % 17 - 8) / 8f * 0.9f);
            var t = Txt(img.transform, label, size, Color.white);
            t.rectTransform.Fill();
            t.name = "label";
            if (onClick != null) b.onClick.AddListener(() => { Sfx.Play("click", 0.6f); onClick(); });
            img.gameObject.AddComponent<UIHover>();
            if (w > 0 || h > 0) LE(img.gameObject, w, h);
            return b;
        }

        public static void SetLabel(this Button b, string s) { var t = b.GetComponentInChildren<Text>(); if (t) t.text = s; }

        public static LayoutElement LE(GameObject go, float pw = -1, float ph = -1, float fw = -1, float fh = -1)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (pw >= 0) { le.preferredWidth = pw; le.minWidth = pw; }
            if (ph >= 0) { le.preferredHeight = ph; le.minHeight = ph; }
            if (fw >= 0) le.flexibleWidth = fw;
            if (fh >= 0) le.flexibleHeight = fh;
            return le;
        }

        public static VerticalLayoutGroup VL(GameObject go, float spacing, int pad, TextAnchor align = TextAnchor.UpperCenter, bool expandW = true)
        {
            var l = go.AddComponent<VerticalLayoutGroup>();
            l.spacing = spacing;
            l.padding = new RectOffset(pad, pad, pad, pad);
            l.childAlignment = align;
            l.childControlWidth = true; l.childControlHeight = true;
            l.childForceExpandWidth = expandW; l.childForceExpandHeight = false;
            return l;
        }

        public static HorizontalLayoutGroup HL(GameObject go, float spacing, int pad, TextAnchor align = TextAnchor.MiddleCenter, bool expandH = true)
        {
            var l = go.AddComponent<HorizontalLayoutGroup>();
            l.spacing = spacing;
            l.padding = new RectOffset(pad, pad, pad, pad);
            l.childAlignment = align;
            l.childControlWidth = true; l.childControlHeight = true;
            l.childForceExpandWidth = false; l.childForceExpandHeight = expandH;
            return l;
        }

        public static RectTransform Spacer(Transform p, float flex = 1f)
        {
            var r = R(p, "spacer");
            LE(r.gameObject, -1, -1, flex, flex);
            return r;
        }

        public static void Border(GameObject go, Color c, float d = 3f)
        {
            var f = Img(go.transform, c, "frame");
            f.sprite = Ring(d); f.type = Image.Type.Sliced; f.pixelsPerUnitMultiplier = 1.5f; f.raycastTarget = false;
            f.rectTransform.Fill();
            f.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                c.SetActive(false);
                UnityEngine.Object.Destroy(c);
            }
        }

        /// <summary>Progress bar; returns the fill rect (set anchorMax.x to the fraction).</summary>
        public static RectTransform Bar(Transform p, Color bg, Color fill, out Text label, int fontSize = 20)
        {
            var b = Img(p, bg, "bar");
            Border(b.gameObject, new Color(0, 0, 0, 0.6f), 2);
            var f = Img(b.transform, fill, "fill");
            f.raycastTarget = false;
            f.rectTransform.Anchor(0, 0, 1, 1);
            var shine = Img(f.transform, new Color(1, 1, 1, 0.15f), "shine");
            shine.rectTransform.Anchor(0, 0.55f, 1, 1);
            shine.raycastTarget = false;
            label = Txt(b.transform, "", fontSize, Color.white);
            label.rectTransform.Fill();
            return f.rectTransform;
        }

        public static void SetFill(RectTransform fill, float k)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(k), 1);
        }
    }

    public class UIHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        public Action onExit;
        public float hoverScale = 1.05f;
        float target = 1f;
        Action enter;

        /// <summary>The tooltip hook. Setting it makes the element a (silent) Button too, so PadNav can focus it and show the same tooltip.</summary>
        public Action onEnter
        {
            get => enter;
            set
            {
                enter = value;
                if (value != null && GetComponent<Button>() == null)
                {
                    var b = gameObject.AddComponent<Button>();
                    b.transition = Selectable.Transition.None;
                    b.navigation = new Navigation { mode = Navigation.Mode.None };
                }
            }
        }

        /// <summary>Gamepad focus entering or leaving: same hover pop and tooltip as the mouse, without the sound.</summary>
        public void PadFocus(bool on)
        {
            var b = GetComponent<Button>();
            target = on && (b == null || b.interactable) ? hoverScale : 1f;
            if (on) enter?.Invoke(); else onExit?.Invoke();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            var b = GetComponent<Button>();
            if (b == null || b.interactable) { target = hoverScale; Sfx.Play("tick", 0.25f, 0.05f, 1.2f); }
            if (b != null) PadNav.NoteHover(b, true);
            enter?.Invoke();
        }

        /// <summary>Touch has no hover: the tap shows the tooltip.</summary>
        public void OnPointerDown(PointerEventData e) { if (Platform.Touch) enter?.Invoke(); }

        public void OnPointerExit(PointerEventData e)
        {
            target = 1f;
            var b = GetComponent<Button>();
            if (b != null) PadNav.NoteHover(b, false);
            onExit?.Invoke();
        }

        void OnDisable() { target = 1f; transform.localScale = Vector3.one; }

        void Update()
        {
            float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1);
        }
    }

    /// <summary>Procedural ink splash (see <see cref="UIK.InkSplat"/>): blot punch 0 to 1.15 to 1 in 0.25 s, droplets fly out and settle, then the title fades and scales in.</summary>
    public class InkSplatFx : MonoBehaviour
    {
        static readonly Sprite[] blots = new Sprite[3];
        static Sprite drop;
        Text title; CanvasGroup titleCg; RectTransform blot, rimRt;
        RectTransform[] drops; Vector2[] target; float[] delay, size;
        float age, hw, hh;

        public float Age => age;
        public bool hold;   // true: wait (invisible) until the owner releases it

        static float Cap(float px, float py, float ax, float ay, float bx, float by, float ra, float rb)
        {   // coverage of a tapered capsule from a (radius ra) to b (radius rb)
            float dx = bx - ax, dy = by - ay, k = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy + 0.0001f));
            float d = Mathf.Sqrt((px - ax - dx * k) * (px - ax - dx * k) + (py - ay - dy * k) * (py - ay - dy * k)) - Mathf.Lerp(ra, rb, k);
            return Mathf.Clamp01(0.5f - d);
        }

        public static Sprite Blot(int variant)
        {
            variant = ((variant % 3) + 3) % 3;
            if (blots[variant] != null) return blots[variant];
            const int W = 512, H = 192;
            var rnd = new System.Random(7919 * (variant + 1));
            float F() => (float)rnd.NextDouble();
            float ph1 = F() * 6.28f, ph2 = F() * 6.28f, ph3 = F() * 6.28f, ox = F() * 90f, oy = F() * 90f;
            float cx = W / 2f, cy = H / 2f, ea = W * 0.40f, eb = H * 0.30f;
            var spikes = new float[9, 4];   // angle, start, length, width
            for (int i = 0; i < 9; i++)
            {
                float ang = (i / 9f) * 6.283f + F() * 0.5f;
                spikes[i, 0] = ang; spikes[i, 1] = 0.7f; spikes[i, 2] = 1.12f + F() * 0.3f; spikes[i, 3] = 5f + F() * 8f;
            }
            var drips = new float[3, 3];    // x, length, width
            for (int i = 0; i < 3; i++) { drips[i, 0] = cx + (F() - 0.5f) * ea * 1.5f; drips[i, 1] = 18f + F() * 16f; drips[i, 2] = 4f + F() * 4f; }
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f - cx) / ea, v = (y + 0.5f - cy) / eb;
                    float th = Mathf.Atan2(v, u), e = Mathf.Sqrt(u * u + v * v);
                    float rb = 1f + 0.16f * Mathf.Sin(3f * th + ph1) + 0.11f * Mathf.Sin(5f * th + ph2) + 0.07f * Mathf.Sin(9f * th + ph3)
                             + (Mathf.PerlinNoise(x * 0.09f + ox, y * 0.09f + oy) - 0.5f) * 0.28f;
                    float a = Mathf.Clamp01((rb - e) / 0.07f + 0.5f);
                    for (int i = 0; i < 9 && a < 1f; i++)
                    {
                        float c = Mathf.Cos(spikes[i, 0]), sn = Mathf.Sin(spikes[i, 0]);
                        a = Mathf.Max(a, Cap(x + 0.5f, y + 0.5f, cx + c * ea * spikes[i, 1], cy + sn * eb * spikes[i, 1],
                                             cx + c * ea * spikes[i, 2], cy + sn * eb * spikes[i, 2], spikes[i, 3], 0.8f));
                    }
                    for (int i = 0; i < 3 && a < 1f; i++)
                    {
                        float top = cy - eb * 0.55f, end = Mathf.Max(8f, top - drips[i, 1] - eb * 0.3f);   // image y grows upward, so drips run toward y = 0
                        a = Mathf.Max(a, Cap(x + 0.5f, y + 0.5f, drips[i, 0], top, drips[i, 0], end, drips[i, 2], drips[i, 2] * 0.55f));
                        a = Mathf.Max(a, Cap(x + 0.5f, y + 0.5f, drips[i, 0], end, drips[i, 0], end - 0.1f, drips[i, 2] * 1.15f, drips[i, 2] * 1.15f));
                    }
                    a *= 1f - 0.22f * Mathf.SmoothStep(0.55f, 0.9f, Mathf.PerlinNoise(x * 0.17f + oy, y * 0.17f + ox));   // dry-brush mottling
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px); tex.Apply();
            return blots[variant] = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite Drop()
        {
            if (drop != null) return drop;
            const int n = 32;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).magnitude;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(n / 2f - 1f - d + 0.5f) * 255f));
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px); tex.Apply();
            return drop = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        public void Init(Text t, Color tint, int seed, Color? rim = null)
        {
            title = t;
            var rt = (RectTransform)transform; var trt = t.rectTransform;
            float tw = Mathf.Min(t.preferredWidth, trt.rect.width);
            float w = tw * 1.2f + 150f, h = Mathf.Max(trt.rect.height, t.fontSize * 1.3f) * 1.3f;
            float cx = t.alignment == TextAnchor.MiddleLeft || t.alignment == TextAnchor.UpperLeft || t.alignment == TextAnchor.LowerLeft ? trt.rect.xMin + tw / 2f - 20f
                     : t.alignment == TextAnchor.MiddleRight ? trt.rect.xMax - tw / 2f + 20f : trt.rect.center.x;
            var par = (RectTransform)trt.parent;
            rt.anchorMin = rt.anchorMax = par.pivot; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = (Vector2)trt.localPosition + new Vector2(cx, trt.rect.center.y);
            hw = w / 2f; hh = h / 2f;

            if (rim.HasValue)   // lighter edge behind the blot, so a dark blot still reads on a dark red scene
            {
                var ri = UIK.Img(rt, rim.Value, "rim"); ri.sprite = InkSplatFx.Blot(seed); ri.raycastTarget = false;
                rimRt = ri.rectTransform.Fill(); rimRt.localScale = Vector3.zero;
            }
            var bi = UIK.Img(rt, tint, "blot"); bi.sprite = InkSplatFx.Blot(seed); bi.raycastTarget = false;
            blot = bi.rectTransform.Fill();
            blot.localScale = Vector3.zero;

            var rnd = new System.Random(seed * 31 + 5);
            int n = 10 + rnd.Next(11);
            drops = new RectTransform[n]; target = new Vector2[n]; delay = new float[n]; size = new float[n];
            for (int i = 0; i < n; i++)
            {
                float ang = (i + (float)rnd.NextDouble()) / n * 6.283f, f = 0.9f + (float)rnd.NextDouble() * 0.45f;
                target[i] = new Vector2(Mathf.Cos(ang) * hw * f * 0.8f, Mathf.Sin(ang) * hh * f * 1.15f);
                delay[i] = (float)rnd.NextDouble() * 0.08f;
                size[i] = 5f + (float)rnd.NextDouble() * (i % 5 == 0 ? 20f : 9f);
                var di = UIK.Img(rt, tint, "drop"); di.sprite = Drop(); di.raycastTarget = false;
                drops[i] = di.rectTransform; drops[i].anchorMin = drops[i].anchorMax = new Vector2(0.5f, 0.5f);
                drops[i].localScale = Vector3.zero;
            }
            titleCg = t.GetComponent<CanvasGroup>() ?? t.gameObject.AddComponent<CanvasGroup>();   // a back-to-back banner reuses the group the previous splat left
            titleCg.alpha = 0f; t.rectTransform.localScale = Vector3.one * 0.6f;
        }

        static float Out(float k) => 1f - (1f - Mathf.Clamp01(k)) * (1f - Mathf.Clamp01(k));

        void Update()
        {
            if (hold) return;
            age += Time.unscaledDeltaTime;
            float s = age < 0.15f ? Mathf.Lerp(0f, 1.15f, Out(age / 0.15f)) : age < 0.25f ? Mathf.Lerp(1.15f, 1f, (age - 0.15f) / 0.1f) : 1f + 0.02f * Mathf.Min(age - 0.25f, 2f);
            blot.localScale = new Vector3(s, s, 1f);
            if (rimRt) rimRt.localScale = new Vector3(s * 1.07f, s * 1.14f, 1f);
            for (int i = 0; i < drops.Length; i++)
            {
                float k = Mathf.Clamp01((age - 0.04f - delay[i]) / 0.4f), e = Out(k);
                var d = drops[i];
                d.anchoredPosition = target[i] * e;
                d.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(target[i].y, target[i].x) * Mathf.Rad2Deg);
                d.sizeDelta = new Vector2(size[i] * (1f + (1f - k) * 1.6f), size[i]);
                d.localScale = Vector3.one * (k <= 0f ? 0f : Mathf.Lerp(0.5f, 1f, e));
            }
            if (title != null)
            {
                float k = Out((age - 0.12f) / 0.3f);
                titleCg.alpha = k;
                title.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, k);
            }
        }

        void OnDestroy()
        {
            if (title != null) { titleCg.alpha = 1f; title.rectTransform.localScale = Vector3.one; }
        }
    }
}

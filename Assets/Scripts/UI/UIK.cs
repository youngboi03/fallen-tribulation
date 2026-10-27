using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VK
{
    /// <summary>Tiny helpers to build uGUI from code.</summary>
    public static class UIK
    {
        public static readonly Color Bg = new Color(0.06f, 0.05f, 0.09f, 0.94f);
        public static readonly Color Panel = new Color(0.11f, 0.09f, 0.16f, 0.96f);
        public static readonly Color Panel2 = new Color(0.16f, 0.13f, 0.23f, 0.98f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.4f);
        public static readonly Color Dim = new Color(0.66f, 0.63f, 0.74f);
        public static readonly Color Cyan = new Color(0.45f, 0.95f, 1f);
        public static readonly Color BtnBlue = new Color(0.22f, 0.3f, 0.5f);
        public static readonly Color BtnGold = new Color(0.62f, 0.45f, 0.12f);
        public static readonly Color BtnRed = new Color(0.5f, 0.18f, 0.2f);
        public static readonly Color BtnGrey = new Color(0.25f, 0.23f, 0.3f);

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
            return img;
        }

        public static Image Icon(Transform p, Sprite s, Color c, float size)
        {
            var img = Img(p, c, "icon");
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
            t.fontSize = size;
            t.color = c;
            t.alignment = a;
            t.fontStyle = st;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.lineSpacing = 1.05f;
            if (shadow)
            {
                var sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.85f);
                sh.effectDistance = new Vector2(2, -2);
            }
            return t;
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
            var ol = img.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(1f, 1f, 1f, 0.12f);
            ol.effectDistance = new Vector2(2, -2);
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
            var o = go.AddComponent<Outline>();
            o.effectColor = c;
            o.effectDistance = new Vector2(d, -d);
            var o2 = go.AddComponent<Outline>();
            o2.effectColor = c;
            o2.effectDistance = new Vector2(-d, d);
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

    public class UIHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action onEnter, onExit;
        public float hoverScale = 1.05f;
        float target = 1f;

        public void OnPointerEnter(PointerEventData e)
        {
            var b = GetComponent<Button>();
            if (b == null || b.interactable) { target = hoverScale; Sfx.Play("tick", 0.25f, 0.05f, 1.2f); }
            if (b != null) PadNav.NoteHover(b, true);
            onEnter?.Invoke();
        }

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
}

using UnityEngine;
using UnityEngine.UI;

namespace VK
{
    /// <summary>
    /// Gamepad / arrow-key navigation for every menu screen: D-pad or stick moves a gold frame to the
    /// nearest button in that direction, A / Enter presses it, B goes back. The frame hides again when the mouse moves.
    /// </summary>
    public class PadNav : MonoBehaviour
    {
        RectTransform scr, frame;
        Button sel;
        Vector2 lastMouse;
        float repeatT;
        bool active;

        public void Init(RectTransform screenRoot, Transform overlayParent)
        {
            scr = screenRoot;
            frame = UIK.R(overlayParent, "padFrame");
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            // four thin bars, so the frame never hides the button it surrounds
            Vector4[] edges = { new Vector4(0, 1, 1, 1), new Vector4(0, 0, 1, 0), new Vector4(0, 0, 0, 1), new Vector4(1, 0, 1, 1) };
            foreach (var e in edges)
            {
                var bar = UIK.Img(frame, UIK.Gold, "edge");
                bar.raycastTarget = false;
                var rt = bar.rectTransform;
                rt.anchorMin = new Vector2(e.x, e.y); rt.anchorMax = new Vector2(e.z, e.w);
                rt.sizeDelta = new Vector2(e.x == e.z ? 5 : 0, e.y == e.w ? 5 : 0);
                rt.anchoredPosition = Vector2.zero;
            }
            frame.gameObject.SetActive(false);
        }

        void Update()
        {
            var g = Game.I;
            if (g == null || g.ui == null || scr == null) return;
            bool menu = g.ui.screen != UIScreen.None && !(g.state == GState.Playing && !g.paused);
            if (!menu) { Hide(); return; }

            Vector2 mouse = Input.mousePosition;
            if ((mouse - lastMouse).sqrMagnitude > 25f) { lastMouse = mouse; Hide(); }

            Vector2 dir = Vector2.zero;
            float h = Input.GetAxisRaw("Horizontal"), v = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(h) > 0.5f || Mathf.Abs(v) > 0.5f) dir = Mathf.Abs(h) > Mathf.Abs(v) ? new Vector2(Mathf.Sign(h), 0) : new Vector2(0, Mathf.Sign(v));
            repeatT -= Time.unscaledDeltaTime;
            if (dir == Vector2.zero) repeatT = 0;
            else if (repeatT <= 0)
            {
                repeatT = 0.22f;
                if (!active || sel == null || !sel.isActiveAndEnabled) Pick(null, Vector2.zero);
                else Pick(sel, dir);
                active = true;
            }

            if (active && sel != null && sel.isActiveAndEnabled)
            {
                var rt = (RectTransform)sel.transform;
                frame.gameObject.SetActive(true);
                frame.position = rt.TransformPoint(rt.rect.center);
                frame.sizeDelta = rt.rect.size * rt.lossyScale.x / frame.lossyScale.x + new Vector2(14, 14);
                frame.SetAsLastSibling();
                if ((Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.Return)) && sel.interactable)
                {
                    var b = sel; sel = null;
                    b.onClick.Invoke();   // the screen is usually rebuilt; the next move picks a fresh button
                    active = Input.GetJoystickNames().Length > 0;
                    if (active) Pick(null, Vector2.zero);
                }
            }
            else if (active) { Pick(null, Vector2.zero); if (sel == null) Hide(); }

            if (Input.GetKeyDown(KeyCode.JoystickButton1)) Back(g);
        }

        void Hide() { active = false; if (frame) frame.gameObject.SetActive(false); }

        static void Back(Game g)
        {
            var s = g.ui.screen;
            if (s == UIScreen.Pause) g.SetPause(false);
            else if (s == UIScreen.CharSelect || s == UIScreen.Meta || s == UIScreen.Achievements || s == UIScreen.Codex) { Sfx.Play("click", 0.5f); g.ui.ShowMenu(); }
        }

        /// <summary>From <paramref name="from"/>, the closest interactable button roughly in <paramref name="dir"/>; with no origin, the top-left one.</summary>
        void Pick(Button from, Vector2 dir)
        {
            var all = scr.GetComponentsInChildren<Button>(false);
            Button best = null; float bestScore = float.MaxValue;
            Vector2 c = from != null ? Center(from) : Vector2.zero;
            foreach (var b in all)
            {
                if (b == from || !b.interactable || !b.isActiveAndEnabled) continue;
                Vector2 p = Center(b);
                float score;
                if (from == null) score = -p.y * 4f + p.x;   // top first, then left
                else
                {
                    Vector2 d = p - c;
                    float along = Vector2.Dot(d, dir);
                    if (along <= 1f) continue;
                    float side = Mathf.Abs(dir.x != 0 ? d.y : d.x);
                    score = along + side * 2.5f;
                }
                if (score < bestScore) { bestScore = score; best = b; }
            }
            if (best != null) { sel = best; if (from != null) Sfx.Play("tick", 0.3f, 0.05f, 1.4f); }
            else if (from == null) sel = null;
        }

        static Vector2 Center(Button b)
        {
            var rt = (RectTransform)b.transform;
            return rt.TransformPoint(rt.rect.center);
        }
    }
}

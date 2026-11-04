using UnityEngine;
using UnityEngine.UI;

namespace VK
{
    /// <summary>
    /// Gamepad / arrow-key navigation for every menu screen: D-pad or stick moves a gold frame to the
    /// nearest button in that direction, A / Enter presses it, B goes back. The frame hides again when the mouse moves.
    /// Screens are rebuilt after every press, so the frame remembers where it was and comes back to the nearest button.
    /// </summary>
    public class PadNav : MonoBehaviour
    {
        RectTransform scr, frame;
        Button sel, focus;
        Vector2 lastMouse;
        float repeatT;
        bool active;
        // last framed spot, valid while the same screen stays up
        bool hasMem; Vector2 memPos; UIScreen memScreen;
        static Button hovered;

        /// <summary>Called by <see cref="UIHover"/>: the button under the mouse, where the keys take over.</summary>
        public static void NoteHover(Button b, bool on)
        {
            if (on) hovered = b;
            else if (hovered == b) hovered = null;
        }

        /// <summary>The button the gold frame is on, or null while the frame is hidden.</summary>
        public Button Selected => active && sel != null && sel.isActiveAndEnabled ? sel : null;

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
            if (!menu) { Hide(); hasMem = false; return; }

            Vector2 mouse = Input.mousePosition;
            if (g.bot == null && (mouse - lastMouse).sqrMagnitude > 25f) { lastMouse = mouse; Hide(); }   // bot/tests: a real mouse crossing the window must not clear the frame

            Vector2 dir = Vector2.zero;
            var es = UnityEngine.EventSystems.EventSystem.current; var cs = es != null ? es.currentSelectedGameObject : null;
            bool typing = cs != null && cs.TryGetComponent(out InputField fld) && fld.isFocused;   // WASD types, it does not move the frame
            float h = Input.GetAxisRaw("Horizontal"), v = Input.GetAxisRaw("Vertical");
            if (!typing && (Mathf.Abs(h) > 0.5f || Mathf.Abs(v) > 0.5f)) dir = Mathf.Abs(h) > Mathf.Abs(v) ? new Vector2(Mathf.Sign(h), 0) : new Vector2(0, Mathf.Sign(v));
            repeatT -= Time.unscaledDeltaTime;
            if (dir == Vector2.zero) repeatT = 0;
            else if (repeatT <= 0)
            {
                repeatT = 0.22f;
                Navigate(dir);
            }

            if (active && Selected == null) Restore();   // the screen was rebuilt under the frame
            if (Selected != null)
            {
                memPos = Center(sel); memScreen = g.ui.screen; hasMem = true;
                var rt = (RectTransform)sel.transform;
                frame.gameObject.SetActive(true);
                frame.position = rt.TransformPoint(rt.rect.center);
                frame.sizeDelta = rt.rect.size * rt.lossyScale.x / frame.lossyScale.x + new Vector2(14, 14);
                frame.SetAsLastSibling();
                if (Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.Return)) Submit();
            }
            else if (active) Hide();

            var cur = Selected;
            if (cur != focus)   // the framed element gets the mouse-hover tooltip
            {
                if (focus) focus.GetComponent<UIHover>()?.PadFocus(false);
                focus = cur;
                if (cur) cur.GetComponent<UIHover>()?.PadFocus(true);
            }

            // Esc = Android back; while fighting the pause screen is closed by Game.Update's own Esc handling
            if (Input.GetKeyDown(KeyCode.JoystickButton1) || (Input.GetKeyDown(KeyCode.Escape) && !(g.state == GState.Playing && g.ui.screen == UIScreen.Pause))) Back(g);
        }

        /// <summary>A D-pad / arrow press: the first one shows the frame (on the hovered button, the remembered spot or the
        /// screen's main button), later ones move it to the nearest button in <paramref name="dir"/>.</summary>
        public void Navigate(Vector2 dir)
        {
            if (Selected != null) { Pick(sel, dir); return; }
            if (Usable(hovered)) sel = hovered;
            else Restore();
            active = sel != null;
        }

        /// <summary>A / Enter: presses the framed button; the frame stays and returns next to that spot if the screen is rebuilt.</summary>
        public void Submit()
        {
            var b = Selected;
            if (b == null || !b.interactable) return;
            memPos = Center(b); memScreen = Game.I.ui.screen; hasMem = true;
            b.onClick.Invoke();
        }

        /// <summary>Nearest button to the remembered spot on the same screen, otherwise the screen's main button.</summary>
        void Restore()
        {
            sel = null;
            if (hasMem && memScreen == Game.I.ui.screen)
            {
                float bd = float.MaxValue;
                foreach (var b in scr.GetComponentsInChildren<Button>(false))
                {
                    if (!Usable(b)) continue;
                    float d = (Center(b) - memPos).sqrMagnitude;
                    if (d < bd) { bd = d; sel = b; }
                }
            }
            if (sel == null) Pick(null, Vector2.zero);
        }

        bool Usable(Button b) => b != null && b.isActiveAndEnabled && b.interactable && b.transform.IsChildOf(scr);

        /// <summary>Puts the frame on <paramref name="b"/>.</summary>
        public void Select(Button b) { sel = b; active = b != null; }

        /// <summary>A dialog replaced the content of the same screen: the frame starts on its main button, not near the old spot.</summary>
        public void Forget() { hasMem = false; sel = null; }

        public void Hide() { active = false; if (frame) frame.gameObject.SetActive(false); }

        static void Back(Game g)
        {
            var s = g.ui.screen;
            if (s == UIScreen.Pause) { if (g.ui.pauseConfirm) g.ui.ShowPause(); else g.SetPause(false); }
            else if (s == UIScreen.Settings) { Sfx.Play("click", 0.5f); g.ui.SettingsBack(); }
            else if (s == UIScreen.Profiles) { Sfx.Play("click", 0.5f); g.ui.ProfileBack(); }
            else if (s == UIScreen.Shop) g.ui.PadCancel();
            else if (s == UIScreen.GameOver) g.GoMenu();   // the main menu ignores back: no accidental quit
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
                if (from == null) score = (b.GetComponent<PadDefault>() != null ? -1e6f : 0f) - p.y * 4f + p.x;   // the main button, else top first, then left
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

    /// <summary>Marks a screen's main button: the frame starts there when the screen opens.</summary>
    public class PadDefault : MonoBehaviour
    {
        public static Button Mark(Button b) { b.gameObject.AddComponent<PadDefault>(); return b; }
    }
}

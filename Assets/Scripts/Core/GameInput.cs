using UnityEngine;
using UnityEngine.UI;

namespace VK
{
    /// <summary>
    /// All gameplay input in one place: keyboard + mouse, gamepad (XInput layout: A dash, X skill, Start pause)
    /// and the on-screen touch pad on phones.
    /// </summary>
    public static class GameInput
    {
        /// <summary>Picks the keyboard or the touch wording for on-screen instructions.</summary>
        public static string K(string keyboard, string touch, string pad = null) => Platform.Touch ? touch : Pad && pad != null ? pad : keyboard;

        /// <summary>True while the last device used was a gamepad: prompts show (A)/(X) and the mouse cursor hides.</summary>
        public static bool Pad;

        /// <summary>Swaps the number-key wording of a sentence for the pad one while the pad is the active device.</summary>
        public static string PadKeys(string s) => Platform.Touch ? s.Replace("(phím 1-4)", "(chạm để chọn)").Replace("(phím 1-3)", "(chạm để chọn)").Replace("phím 1-4", "chạm để chọn") : Pad ? s.Replace("(phím 1-4)", "(D-pad + A)").Replace("(phím 1-3)", "(D-pad + A)").Replace("phím 1-4", "D-pad + (A)") : s;
        static Vector2 lastMouse;

        /// <summary>Once per frame (UIRoot.Update): gamepad button or stick sets <see cref="Pad"/>, key or mouse clears it.</summary>
        public static void PollDevice()
        {
            if (Game.I != null && Game.I.bot != null) { if (Cursor.visible == Pad && !Platform.Touch) Cursor.visible = !Pad; return; }   // bot/tests set Pad themselves; a real mouse crossing the window must not flip it
            bool joy = false;
            for (int i = 0; i < 12 && !joy; i++) joy = Input.GetKeyDown(KeyCode.JoystickButton0 + i);
            Vector2 m = Input.mousePosition;
            bool mouse = (m - lastMouse).sqrMagnitude > 25f || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            lastMouse = m;
            if (joy) Pad = true;
            else if (mouse || Input.anyKeyDown) Pad = false;
            else if (!Pad && !Input.anyKey && (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.5f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.5f)) Pad = true;
            if (Cursor.visible == Pad && !Platform.Touch) Cursor.visible = !Pad;
        }

        public static Vector2 Move
        {
            get
            {
                var v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                if (TouchPad.I != null) v += TouchPad.I.move;
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        public static bool Dash =>
            Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)
            || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.JoystickButton5)
            || (!Platform.Touch && Input.GetMouseButtonDown(1))
            || (TouchPad.I != null && TouchPad.I.dash);

        public static bool Skill =>
            Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.J)
            || Input.GetKeyDown(KeyCode.JoystickButton2) || Input.GetKeyDown(KeyCode.JoystickButton4)
            || (!Platform.Touch && Input.GetMouseButtonDown(0) && Time.frameCount - Game.I.unpauseFrame > 1)
            || (TouchPad.I != null && TouchPad.I.skill);

        public static bool Pause =>
            Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.JoystickButton7)
            || (TouchPad.I != null && TouchPad.I.pause);
    }

    /// <summary>Floating joystick on the left half of the screen, dash/skill/pause buttons on the right.</summary>
    [DefaultExecutionOrder(-90)]
    public class TouchPad : MonoBehaviour
    {
        public static TouchPad I;
        public Vector2 move;
        public bool dash, skill, pause;

        RectTransform root, stickBase, stickKnob, dashBtn, skillBtn, pauseBtn;
        Image skillFill;
        int stickFinger = -1;
        Vector2 stickOrigin;
        RectTransform safe;
        const float StickRadius = 110f;   // canvas units (1920x1080 reference)

        void Awake()
        {
            I = this;
            var cgo = new GameObject("TouchCanvas");
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;   // above the HUD, below menus/overlays
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            root = (RectTransform)cgo.transform;
            safe = UIK.R(root, "safe");   // the buttons live in the part of the screen that is not under a notch or gesture bar

            stickBase = Circle("stickBase", 260, new Color(1, 1, 1, 0.12f));
            stickKnob = Circle("stickKnob", 110, new Color(1, 1, 1, 0.35f));
            dashBtn = Button("Lướt", new Vector2(-150, 150), 170, new Color(0.45f, 0.85f, 1f, 0.35f));
            skillBtn = Button("Thần\nThông", new Vector2(-340, 110), 150, new Color(1f, 0.8f, 0.35f, 0.35f));
            skillFill = skillBtn.GetComponent<Image>();
            pauseBtn = Button("II", new Vector2(-70, 0), 110, new Color(1, 1, 1, 0.2f));
            pauseBtn.anchorMin = pauseBtn.anchorMax = new Vector2(0, 1);
            pauseBtn.anchoredPosition = new Vector2(580 * Platform.Boost + 20, -75);   // right of the HUD panel, clear of the synergy list
        }

        RectTransform Circle(string n, float size, Color c, Transform parent = null)
        {
            var img = new GameObject(n).AddComponent<Image>();
            img.transform.SetParent(parent ? parent : root, false);
            img.sprite = Res.Circle; img.color = c; img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size);
            return rt;
        }

        RectTransform Button(string label, Vector2 fromBottomRight, float size, Color c)
        {
            var rt = Circle("btn_" + label, size, c, safe);
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.anchoredPosition = fromBottomRight;
            var t = UIK.Txt(rt, label, 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Anchor(0, 0, 1, 1, 0, 0, 0, 0);
            t.raycastTarget = false;
            return rt;
        }

        static bool Hit(RectTransform rt, Vector2 screen) => RectTransformUtility.RectangleContainsScreenPoint(rt, screen, null)
            && (screen - (Vector2)rt.position).magnitude < rt.rect.width * rt.lossyScale.x * 0.6f;

        void Update()
        {
            dash = skill = pause = false;
            var g = Game.I;
            bool active = g != null && g.state == GState.Playing && !g.paused;
            root.gameObject.SetActive(active);
            if (!active) { move = Vector2.zero; stickFinger = -1; return; }

            var sa = Platform.Safe;   // pixels -> anchors
            safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height); safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            float scale = root.lossyScale.x;
            int n = Input.touchCount;
            bool mouseSim = n == 0 && (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0));
            if (mouseSim) n = 1;
            bool stickSeen = false;
            for (int i = 0; i < n; i++)
            {
                Touch t;
                if (mouseSim)
                    t = new Touch { fingerId = 99, position = Input.mousePosition,
                        phase = Input.GetMouseButtonDown(0) ? TouchPhase.Began : Input.GetMouseButtonUp(0) ? TouchPhase.Ended : TouchPhase.Moved };
                else t = Input.GetTouch(i);

                if (t.phase == TouchPhase.Began)
                {
                    if (Hit(dashBtn, t.position)) { dash = true; continue; }
                    if (Hit(skillBtn, t.position)) { skill = true; continue; }
                    if (Hit(pauseBtn, t.position)) { pause = true; continue; }
                    if (stickFinger < 0 && t.position.x < Screen.width * 0.5f) { stickFinger = t.fingerId; stickOrigin = t.position; }
                }
                if (t.fingerId != stickFinger) continue;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { stickFinger = -1; continue; }
                stickSeen = true;
                Vector2 d = (t.position - stickOrigin) / scale;
                if (d.magnitude > StickRadius) { stickOrigin += (d.normalized * (d.magnitude - StickRadius)) * scale; d = d.normalized * StickRadius; }   // follow the thumb
                move = d / StickRadius;
                if (move.magnitude < 0.15f) move = Vector2.zero;
            }
            if (!stickSeen) { stickFinger = -1; move = Vector2.zero; }

            bool showStick = stickFinger >= 0;
            Vector2 basePos = showStick ? stickOrigin / scale : new Vector2(260 + sa.xMin / scale, 240 + sa.yMin / scale);
            stickBase.anchoredPosition = basePos;
            stickKnob.anchoredPosition = basePos + move * StickRadius;
            stickBase.GetComponent<Image>().color = new Color(1, 1, 1, showStick ? 0.16f : 0.07f);
            bool ready = g.player != null && g.player.SkillReady >= 1f;
            skillFill.color = ready ? new Color(1f, 0.8f, 0.35f, 0.5f) : new Color(0.5f, 0.5f, 0.5f, 0.25f);
        }
    }
}

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VK
{
    public enum UIScreen { None, Menu, CharSelect, Meta, Achievements, LevelUp, Shop, Pause, GameOver, Chest, Event, Codex, Relic }

    public class UIRoot : MonoBehaviour
    {
        public UIScreen screen;
        RectTransform root, hud, scr, top;
        Image hurtFlash, tintImg;
        float tintLife, tintMax, tintA;
        public float TintAlpha => tintA;
        public void Tint(Color c, float alpha, float life)
        {
            if (tintImg == null) return;
            tintA = Mathf.Min(alpha, 0.12f); tintLife = tintMax = Mathf.Max(0.05f, life);
            tintImg.color = new Color(c.r, c.g, c.b, tintA);
        }
        float hurtA;

        // HUD
        RectTransform hpFill, xpFill, bossFill, bossBox, weaponRow, dashFill, skillFill;
        readonly RectTransform[] bossTicks = new RectTransform[3];
        Text skLblTxt; CharDef skLblCh; int skLblStage = -1;
        Text hpTxt, xpTxt, goldTxt, waveTxt, timerTxt, bossTxt, synTxt, killTxt, comboTxt;
        int lastCombo; float comboPop;
        string weaponKey = "", synKey = "";

        // banner + toasts
        Text bannerTitle, bannerSub;
        CanvasGroup bannerCg;
        float bannerT = 99f;
        class ToastItem { public RectTransform rt; public CanvasGroup cg; public float t; }
        readonly List<ToastItem> toasts = new List<ToastItem>();

        // selection state
        int selChar, selKiep;
        bool selInit;
        WeaponInst selWeapon;
        Text detailTxt;

        RunState Run => Game.I.run;
        SaveData Save => Game.I.save;

        public RectTransform Root => root;

        public void Init()
        {
            var cgo = new GameObject("UI");
            DontDestroyOnLoad(cgo);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            cgo.AddComponent<GraphicRaycaster>();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(es);
            }
            root = (RectTransform)cgo.transform;
            hud = UIK.R(root, "hud").Fill();
            hurtFlash = UIK.Img(root, new Color(0.8f, 0f, 0.05f, 0f), "hurt");
            hurtFlash.rectTransform.Fill();
            hurtFlash.raycastTarget = false;
            tintImg = UIK.Img(root, new Color(1, 1, 1, 0), "tint");
            tintImg.rectTransform.Fill();
            tintImg.raycastTarget = false;
            scr = UIK.R(root, "screen").Fill();
            top = UIK.R(root, "top").Fill();
            BuildBanner();
            gameObject.AddComponent<PadNav>().Init(scr, top);
        }

        // ================================================================== common
        public void HideScreen()
        {
            UIK.Clear(scr);
            screen = UIScreen.None;
            detailTxt = null;
        }

        RectTransform Overlay(float a = 0.82f)
        {
            UIK.Clear(scr);
            detailTxt = null;
            bannerT = 99f;
            var o = UIK.Img(scr, new Color(0.05f, 0.03f, 0.02f, a), "overlay");
            o.rectTransform.Fill();
            return o.rectTransform;
        }

        Text Title(Transform p, string s, float y, int size = 64, Color? c = null)
        {
            var t = UIK.Txt(p, s, size, c ?? UIK.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Place(0.5f, 1f, 1800, size + 30, 0, -y);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;   // long translations shrink to one line instead of wrapping over what is below
            t.resizeTextForBestFit = true; t.resizeTextMinSize = size / 2; t.resizeTextMaxSize = size;
            var ol = t.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0.25f, 0.1f, 0f, 0.8f);
            ol.effectDistance = new Vector2(2, -2);
            return t;
        }

        void BuildBanner()
        {
            var b = UIK.R(top, "banner").Place(0.5f, 0.72f, 1600, 200);
            bannerCg = b.gameObject.AddComponent<CanvasGroup>();
            bannerCg.blocksRaycasts = false; bannerCg.interactable = false; bannerCg.alpha = 0;
            bannerTitle = UIK.Txt(b, "", 72, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            bannerTitle.rectTransform.Anchor(0, 0.4f, 1, 1);
            var ol = bannerTitle.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0, 0, 0, 0.8f); ol.effectDistance = new Vector2(3, -3);
            bannerSub = UIK.Txt(b, "", 30, UIK.Dim);
            bannerSub.rectTransform.Anchor(0, 0, 1, 0.42f);
        }

        public void Banner(string title, string sub, Color c)
        {
            if (!string.IsNullOrEmpty(title)) { bannerTitle.text = title; bannerTitle.color = c; bannerSub.text = sub; }
            else { bannerTitle.text = sub; bannerTitle.color = c; bannerSub.text = ""; }
            bannerT = 0;
        }

        public void Toast(string title, string sub)
        {
            var box = UIK.Img(top, new Color(0.12f, 0.1f, 0.05f, 0.95f), "toast");
            UIK.Border(box.gameObject, UIK.Gold, 2);
            box.rectTransform.Place(0f, 0f, 460, 90, 20, 120 + toasts.Count * 100);   // bottom-left above the weapon bar: no buttons live there
            var cg = box.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            var t = UIK.Txt(box.transform, "<b>" + title + "</b>\n<size=20><color=#C8C2D8>" + sub + "</color></size>", 26, UIK.Gold);
            t.rectTransform.Anchor(0, 0, 1, 1, 14, 6, 14, 6);
            toasts.Add(new ToastItem { rt = box.rectTransform, cg = cg, t = 0 });
        }

        public void HurtFlash() => hurtA = 0.35f;

        // ------------------------------------------------------------ tutorial hints
        RectTransform hintBox; Text hintTxt; CanvasGroup hintCg; float hintT = 99f;
        readonly Queue<string[]> hintQueue = new Queue<string[]>();

        /// <summary>Shows a one-time tutorial hint (remembered in the save).</summary>
        public void Hint(string id, string text)
        {
            var s = Save;
            if (s.tips.Contains(id)) return;
            s.tips.Add(id); s.Save();
            hintQueue.Enqueue(new[] { id, text });
        }

        void BuildHint()
        {
            var img = UIK.Img(top, new Color(0.18f, 0.13f, 0.09f, 0.97f), "hint");
            hintBox = img.rectTransform;
            hintBox.Place(0.5f, 0f, 900, 104, 0, 112);
            UIK.Border(img.gameObject, UIK.Gold, 2);
            hintCg = img.gameObject.AddComponent<CanvasGroup>();
            hintCg.alpha = 0; hintCg.blocksRaycasts = false;
            var head = UIK.Txt(hintBox, "<b>HƯỚNG DẪN</b>", 18, UIK.Gold, TextAnchor.UpperLeft);
            head.rectTransform.Anchor(0, 0, 1, 1, 18, 8, 18, 8);
            hintTxt = UIK.Txt(hintBox, "", 24, Color.white, TextAnchor.MiddleLeft);
            hintTxt.rectTransform.Anchor(0, 0, 1, 1, 18, 6, 110, 22);
            var close = UIK.Txt(hintBox, "<size=16><color=#8A8499>(bấm để ẩn)</color></size>", 16, UIK.Dim, TextAnchor.LowerRight);
            close.rectTransform.Anchor(0, 0, 1, 1, 18, 6, 14, 6);
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => hintT = Mathf.Max(hintT, 7.5f));
        }

        static readonly string[] menuHints = { "levelup", "shop", "route", "chest", "event" };
        string hintId;

        void UpdateHint(float dt)
        {
            if (hintBox == null) BuildHint();
            hintT += dt;
            if (Game.I.state == GState.Playing)
            {   // shop/chest hints that did not get their turn wait for the next visit instead of covering the fight
                if (hintT < 7.5f && System.Array.IndexOf(menuHints, hintId) >= 0) hintT = 7.5f;
                int n = hintQueue.Count;
                for (int i = 0; i < n; i++)
                {
                    var h = hintQueue.Dequeue();
                    if (System.Array.IndexOf(menuHints, h[0]) >= 0) Save.tips.Remove(h[0]);
                    else hintQueue.Enqueue(h);
                }
            }
            if (hintT > 8f && hintQueue.Count > 0)
            {
                var h = hintQueue.Dequeue();
                hintId = h[0];
                hintTxt.text = h[1];
                hintT = 0;
                Sfx.Play("pluck", 0.6f, 0f, 1.2f);
            }
            float a = hintT < 0.25f ? hintT / 0.25f : hintT < 7.5f ? 1f : Mathf.Max(0, 1f - (hintT - 7.5f) / 0.5f);
            hintCg.alpha = a;
            hintCg.blocksRaycasts = a > 0.5f;
            // in a fight the hint sits low; on menu screens (shop, encounter, chest…) it sits over the title so it never covers buttons
            bool low = Game.I.state == GState.Playing && !Game.I.paused;
            float slide = hintT < 0.25f ? (1f - hintT / 0.25f) * 40f : 0f;
            hintBox.anchorMin = hintBox.anchorMax = hintBox.pivot = new Vector2(0.5f, low ? 0f : 1f);
            hintBox.anchoredPosition = new Vector2(0, low ? 112 - slide : -14 + slide);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            // banner
            bannerT += dt;
            float a = bannerT < 0.2f ? bannerT / 0.2f : bannerT < 2f ? 1f : Mathf.Max(0, 1f - (bannerT - 2f) / 0.5f);
            bannerCg.alpha = a;
            bannerCg.transform.localScale = Vector3.one * (bannerT < 0.2f ? Mathf.Lerp(1.3f, 1f, bannerT / 0.2f) : 1f);
            // toasts
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                t.t += dt;
                t.cg.alpha = t.t < 0.25f ? t.t / 0.25f : t.t > 3.2f ? Mathf.Max(0, 1f - (t.t - 3.2f) / 0.4f) : 1f;
                t.rt.anchoredPosition = new Vector2(20 - (t.t < 0.25f ? (1f - t.t / 0.25f) * 300f : 0), 120 + i * 100);
                if (t.t > 3.6f) { Destroy(t.rt.gameObject); toasts.RemoveAt(i); }
            }
            // hurt
            hurtA = Mathf.MoveTowards(hurtA, 0, dt * 1.2f);
            float lowHp = 0;
            if (Run != null && Game.I.state == GState.Playing) { float k = Run.hp / Run.MaxHP; if (k < 0.3f) lowHp = 0.08f + Mathf.Sin(Time.unscaledTime * 6f) * 0.05f; }
            hurtFlash.color = new Color(0.8f, 0f, 0.05f, Mathf.Max(hurtA, lowHp));

            if (tintLife > 0)
            {
                tintLife -= dt;
                var tc = tintImg.color; tc.a = tintLife > 0 ? tintA * tintLife / tintMax : 0f; tintImg.color = tc;
            }

            UpdateHUD();
            UpdateHint(dt);

            // keyboard shortcuts
            if (screen == UIScreen.LevelUp)
            {
                for (int i = 0; i < 4; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Game.I.ChooseLevel(i);
            }
            else if (screen == UIScreen.Shop && Input.GetKeyDown(KeyCode.R)) DoReroll();
            else if ((screen == UIScreen.CharSelect || screen == UIScreen.Meta || screen == UIScreen.Achievements || screen == UIScreen.Codex)
                     && Input.GetKeyDown(KeyCode.Escape)) { Sfx.Play("click", 0.5f); ShowMenu(); }
            else if (screen == UIScreen.Event)
            {
                for (int i = 0; i < 3; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Game.I.ChooseEvent(i);
            }
            else if (screen == UIScreen.Relic)
            {
                for (int i = 0; i < 3; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Game.I.TakeRelic(i);
            }
        }

        // ================================================================== HUD
        public void ShowHUD()
        {
            UIK.Clear(hud);
            weaponKey = ""; synKey = "";
            // top-left vitals
            var box = UIK.Img(hud, new Color(0.10f, 0.07f, 0.05f, 0.72f), "vitals");
            box.rectTransform.Place(0, 1, 470, 182, 16, -16);
            box.raycastTarget = false;
            hpFill = UIK.Bar(box.transform, new Color(0.2f, 0.05f, 0.07f), new Color(0.85f, 0.18f, 0.22f), out hpTxt, 24);
            hpFill.parent.GetComponent<RectTransform>().Place(0, 1, 440, 38, 15, -14);
            xpFill = UIK.Bar(box.transform, new Color(0.08f, 0.13f, 0.10f), new Color(0.45f, 0.82f, 0.62f), out xpTxt, 19);
            xpFill.parent.GetComponent<RectTransform>().Place(0, 1, 440, 28, 15, -58);
            var gIcon = UIK.Img(box.transform, new Color(1f, 0.85f, 0.35f), "gold");
            gIcon.sprite = Res.Diamond;
            gIcon.rectTransform.Place(0, 1, 28, 28, 18, -100);
            gIcon.raycastTarget = false;
            goldTxt = UIK.Txt(box.transform, "0", 30, UIK.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            goldTxt.rectTransform.Place(0, 1, 200, 40, 54, -94);
            dashFill = UIK.Bar(box.transform, new Color(0.12f, 0.10f, 0.08f), new Color(0.78f, 0.90f, 0.76f), out var dashLbl, 16);
            dashFill.parent.GetComponent<RectTransform>().Place(1, 1, 150, 22, -15, -104);
            dashLbl.text = GameInput.K("Lướt [Space]", "Lướt");
            skillFill = UIK.Bar(box.transform, new Color(0.15f, 0.1f, 0.05f), new Color(1f, 0.75f, 0.3f), out var skLbl, 18);
            skillFill.parent.GetComponent<RectTransform>().Place(0, 1, 440, 28, 15, -140);
            skLblTxt = skLbl; skLblCh = null; skLblStage = -1;   // text filled in Update (the form changes with Tiến Giai)

            // top-center wave & timer
            waveTxt = UIK.Txt(hud, "", 30, UIK.Dim, TextAnchor.MiddleCenter, FontStyle.Bold);
            waveTxt.rectTransform.Place(0.5f, 1, 900, 40, 0, -12);
            timerTxt = UIK.Txt(hud, "", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            timerTxt.rectTransform.Place(0.5f, 1, 300, 80, 0, -46);

            bossBox = UIK.R(hud, "boss").Place(0.5f, 1, 900, 60, 0, -120);
            bossFill = UIK.Bar(bossBox, new Color(0.15f, 0.04f, 0.06f), new Color(0.72f, 0.15f, 0.2f), out bossTxt, 22);
            bossFill.parent.GetComponent<RectTransform>().Anchor(0, 0, 1, 1);
            for (int i = 0; i < bossTicks.Length; i++)
            {   // ultimate thresholds (spec 3.1): thin gold marks over the bar
                var tk = UIK.Img(bossBox, new Color(1f, 0.85f, 0.4f, 0.9f), "ulttick").rectTransform;
                tk.Anchor(0.5f, 0f, 0.5f, 1f, -2, -4, -2, -4);
                tk.gameObject.SetActive(false);
                bossTicks[i] = tk;
            }
            bossBox.gameObject.SetActive(false);

            // top-right kills + synergies
            killTxt = UIK.Txt(hud, "", 24, UIK.Dim, TextAnchor.MiddleRight);
            killTxt.rectTransform.Place(1, 1, 400, 34, -20, -16);
            synTxt = UIK.Txt(hud, "", 22, Color.white, TextAnchor.UpperRight);
            synTxt.rectTransform.Place(1, 1, 400, 400, -20, -56);

            comboTxt = UIK.Txt(hud, "", 40, UIK.Gold, TextAnchor.MiddleRight, FontStyle.Bold);
            comboTxt.rectTransform.Place(1, 0.5f, 400, 110, -30, 40);
            var col = comboTxt.gameObject.AddComponent<Outline>(); col.effectColor = new Color(0.3f, 0.1f, 0, 0.9f); col.effectDistance = new Vector2(2, -2);

            // bottom-left weapons
            weaponRow = UIK.R(hud, "weapons").Place(0, 0, 900, 84, 16, 16);
            UIK.HL(weaponRow.gameObject, 8, 0, TextAnchor.LowerLeft, false);
        }

        void UpdateHUD()
        {
            var run = Run;
            if (run == null || hpFill == null) { if (hud) hud.gameObject.SetActive(false); return; }
            hud.gameObject.SetActive(Game.I.state == GState.Playing || Game.I.state == GState.Between || Game.I.state == GState.Over);
            UIK.SetFill(hpFill, run.hp / run.MaxHP);
            hpTxt.text = Mathf.CeilToInt(run.hp) + " / " + Mathf.RoundToInt(run.MaxHP);
            UIK.SetFill(xpFill, run.xp / run.XpNeed);
            xpTxt.text = Content.Realm(run.level) + (run.pendingLevels > 0 ? "  <color=#FFD266>(+" + run.pendingLevels + ")</color>" : "");
            goldTxt.text = run.gold.ToString();
            UIK.SetFill(dashFill, Game.I.player.DashReady);
            float skr = Game.I.player.SkillReady;
            UIK.SetFill(skillFill, skr);
            if (skLblTxt && (skLblCh != run.ch || skLblStage != run.skillStage))
            {
                skLblCh = run.ch; skLblStage = run.skillStage;
                skLblTxt.text = GameInput.K("[E] ", "") + Skills.FormName(run.ch, run.skillStage) + " · " + Skills.StageLabel(run.skillStage);
            }
            skillFill.GetComponent<Image>().color = skr >= 1f ? new Color(1f, 0.8f, 0.3f + Mathf.Sin(Time.unscaledTime * 6f) * 0.15f) : new Color(0.55f, 0.42f, 0.2f);
            var d = Game.I.dir;
            waveTxt.text = "ĐỢT " + run.wave + " / 20" + (run.kiep > 0 ? "   ·   KIẾP " + run.kiep : "") + (run.activeRoute > 0 ? "   ·   <color=#FF8A70>" + Content.RouteName[run.activeRoute] + "</color>" : "");
            if (d.bossWave) timerTxt.text = "";
            else
            {
                int s = Mathf.CeilToInt(Mathf.Max(0, d.timeLeft));
                timerTxt.text = s.ToString();
                timerTxt.color = s <= 5 && Game.I.state == GState.Playing ? new Color(1f, 0.5f, 0.4f) : Color.white;
            }
            bool showBoss = d.bossWave && d.boss != null && !d.boss.dead && Game.I.state == GState.Playing;
            bossBox.gameObject.SetActive(showBoss);
            if (showBoss)
            {
                UIK.SetFill(bossFill, d.boss.hp / d.boss.maxHp);
                var br = d.boss.brain;
                bossTxt.text = d.boss.def.name + (br != null ? "  ·  Phase " + (br.Phase == 2 ? "II" : "I") : "") + "   " + Mathf.CeilToInt(d.boss.hp) + " / " + Mathf.CeilToInt(d.boss.maxHp);
                for (int i = 0; i < bossTicks.Length; i++)
                {
                    bool on = br != null && i < br.UltMarks.Count;
                    if (bossTicks[i].gameObject.activeSelf != on) bossTicks[i].gameObject.SetActive(on);
                    if (on) { float v = br.UltMarks[i]; bossTicks[i].anchorMin = new Vector2(v, 0f); bossTicks[i].anchorMax = new Vector2(v, 1f); }
                }
            }
            killTxt.text = "Diệt: " + run.kills;
            int cb = Game.I.combo;
            if (cb != lastCombo) { if (cb > lastCombo) comboPop = 1f; lastCombo = cb; comboTxt.text = cb >= 5 ? "<size=24>LIÊN SÁT</size>\nx" + cb : ""; }
            comboPop = Mathf.MoveTowards(comboPop, 0, Time.unscaledDeltaTime * 6f);
            comboTxt.transform.localScale = Vector3.one * (1f + comboPop * 0.25f + Mathf.Min(cb, 150) / 600f);
            comboTxt.color = cb >= 100 ? new Color(1f, 0.4f, 0.3f) : cb >= 50 ? new Color(1f, 0.6f, 0.3f) : UIK.Gold;

            // weapons row (rebuild on change)
            var sb = new StringBuilder();
            foreach (var w in run.weapons) sb.Append(w.def.id).Append(w.tier);
            string key = sb.ToString();
            if (key != weaponKey)
            {
                weaponKey = key;
                UIK.Clear(weaponRow);
                foreach (var w in run.weapons)
                {
                    var slot = UIK.Img(weaponRow, new Color(0.13f, 0.10f, 0.07f, 0.9f), "w");
                    UIK.LE(slot.gameObject, 72, 72);
                    UIK.Border(slot.gameObject, Content.TierColor[w.tier], 2);
                    var ic = UIK.Img(slot.transform, w.def.tint, "i");
                    ic.sprite = w.Icon; ic.preserveAspect = true; ic.raycastTarget = false;
                    ic.rectTransform.Anchor(0, 0, 1, 1, 8, 8, 8, 8);
                }
            }
            // synergies
            sb.Clear();
            for (int i = 0; i < (int)Tag.COUNT; i++)
            {
                int c = run.tagCount[i];
                if (c == 0) continue;
                int tier = RunState.SynTier(c);
                sb.Append("<color=").Append(Content.TagHex((Tag)i)).Append("><b>").Append(Content.TagName[i]).Append("</b></color> ")
                  .Append(c).Append("  ");
                for (int k = 0; k < 3; k++) sb.Append(k < tier ? "<color=#FFD266>■</color>" : "<color=#555060>■</color>");
                sb.Append('\n');
            }
            string sk = sb.ToString();
            if (sk != synKey) { synKey = sk; synTxt.text = sk; }
        }

        // ================================================================== Menu
        public void ShowMenu()
        {
            screen = UIScreen.Menu;
            selInit = false;
            UIK.Clear(hud);
            hpFill = null;
            var o = Overlay(0.4f);
            LangButton(o);
            Title(o, "VẠN KIẾP TU TIÊN", 110, 110);
            var sub = UIK.Txt(o, "Một kiếm phá vạn kiếp  ·  Roguelite tu tiên", 32, UIK.Dim, TextAnchor.MiddleCenter, FontStyle.Italic);
            sub.rectTransform.Place(0.5f, 1, 1200, 50, 0, -235);

            var col = UIK.R(o, "menu").Place(0.5f, 0.5f, 420, 640, 0, -115);
            UIK.VL(col.gameObject, 7, 0, TextAnchor.MiddleCenter);
            PadDefault.Mark(UIK.Btn(col, "Nhập Đạo", ShowCharSelect, UIK.BtnGold, 34, -1, 74));
            UIK.Btn(col, "Tàng Kinh Các", ShowMeta, UIK.BtnBlue, 28, -1, 60);
            UIK.Btn(col, "Thành Tựu", ShowAchievements, UIK.BtnBlue, 28, -1, 60);
            UIK.Btn(col, "Đồ Giám", ShowCodex, UIK.BtnBlue, 28, -1, 60);
            Button sfxB = null, musB = null;
            sfxB = UIK.Btn(col, "", () => { Save.sfxVol = Cycle(Save.sfxVol); Save.Save(); sfxB.SetLabel(VolLabel("Âm hiệu", Save.sfxVol)); }, UIK.BtnGrey, 24, -1, 50);
            sfxB.SetLabel(VolLabel("Âm hiệu", Save.sfxVol));
            musB = UIK.Btn(col, "", () => { Save.musicVol = Cycle(Save.musicVol); Save.Save(); musB.SetLabel(VolLabel("Nhạc", Save.musicVol)); }, UIK.BtnGrey, 24, -1, 50);
            musB.SetLabel(VolLabel("Nhạc", Save.musicVol));
            SettingsButtons(col);
            UIK.Btn(col, "Rời Khỏi", () => { Save.Save(); Application.Quit(); }, UIK.BtnRed, 24, -1, 50);

            var info = UIK.Txt(o, "Đạo Hạnh: <color=#FFD266>" + Save.daoHanh + "</color>     Hoàn thành: <color=#7CF0FF>" +
                Mathf.FloorToInt(Save.Completion() * 100) + "%</color>     Thắng: " + Save.wins + "     Lượt chơi: " + Save.runs, 26, UIK.Dim);
            info.rectTransform.Place(0.5f, 0, 1600, 40, 0, 70);
            var cred = UIK.Txt(o, GameInput.K("WASD di chuyển · SPACE lướt · ESC tạm dừng · F11 toàn màn hình · ", "") + "Đồ họa & âm thanh: Kenney.nl (CC0)", 20, new Color(0.5f, 0.48f, 0.56f));
            cred.rectTransform.Place(0.5f, 0, 1600, 30, 0, 26);
        }

        static readonly string[] DmgNumNames = { "Tất cả", "Chỉ bạo kích", "Tắt" };
        public void ApplyLanguage(int lang)
        {
            Save.lang = lang; Save.Save();
            Loc.SetLang(lang);
            var font = Res.Font;   // swap the font on every live text (CJK needs an OS font)
            foreach (var t in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)) t.font = font;
            if (screen == UIScreen.Pause) ShowPause(); else ShowMenu();
        }

        /// <summary>Language picker in the top-right corner of the menu and pause screens.</summary>
        void LangButton(Transform o)
        {
            var b = UIK.Btn(o, "", () => ApplyLanguage((Save.lang + 1) % 3), UIK.BtnBlue, 24);
            b.SetLabel("Language: " + Loc.LangName[Loc.Lang]);
            b.GetComponent<RectTransform>().Place(1, 1, 230, 56, -30, -30);
        }

        void SettingsButtons(Transform col)
        {
            if (screen == UIScreen.Menu)
            {
                Button tb = null;
                tb = UIK.Btn(col, Save.tips.Count == 0 ? "Hướng dẫn: sẽ hiện lại" : "Xem lại hướng dẫn", () =>
                {
                    Save.tips.Clear(); Save.Save(); tb.SetLabel("Hướng dẫn: sẽ hiện lại");
                }, UIK.BtnGrey, 24, -1, 50);
            }
            Button shB = null, dnB = null;
            shB = UIK.Btn(col, "Rung màn hình: " + Mathf.RoundToInt(Save.shake * 100) + "%", () =>
            {
                Save.shake = Save.shake > 0.9f ? 0f : Save.shake + 0.5f; Save.Save();
                shB.SetLabel("Rung màn hình: " + Mathf.RoundToInt(Save.shake * 100) + "%");
            }, UIK.BtnGrey, 24, -1, 50);
            dnB = UIK.Btn(col, "Số sát thương: " + DmgNumNames[Save.dmgNumbers], () =>
            {
                Save.dmgNumbers = (Save.dmgNumbers + 1) % 3; Save.Save();
                dnB.SetLabel("Số sát thương: " + DmgNumNames[Save.dmgNumbers]);
            }, UIK.BtnGrey, 24, -1, 50);
        }

        static float Cycle(float v) { float n = Mathf.Round(v * 4f) / 4f + 0.25f; return n > 1.01f ? 0f : n; }
        static string VolLabel(string n, float v) => n + ": " + Mathf.RoundToInt(v * 100) + "%";

        // ================================================================== Character select
        public void ShowCharSelect()
        {
            screen = UIScreen.CharSelect;
            var o = Overlay(0.86f);
            if (!selInit)
            {
                selInit = true;
                selChar = Save.lastChar;
                selKiep = Save.lastKiep;
            }
            if (!Content.CharUnlocked(selChar, Save)) selChar = 0;
            selKiep = Mathf.Clamp(selKiep, 0, Save.maxKiepUnlocked);
            Title(o, "CHỌN ĐẠO LỘ", 60, 64);

            var row = UIK.R(o, "cards").Place(0.5f, 0.5f, 1820, 560, 0, 60);
            UIK.HL(row.gameObject, Content.Chars.Count > 5 ? 12 : 18, 0, TextAnchor.MiddleCenter, false);
            for (int i = 0; i < Content.Chars.Count; i++)
            {
                int ci = i;
                var c = Content.Chars[i];
                bool unlocked = Content.CharUnlocked(i, Save);
                bool sel = i == selChar;
                var card = UIK.Img(row, sel ? UIK.Panel2 : UIK.Panel, "card");
                UIK.LE(card.gameObject, Content.Chars.Count > 5 ? 288 : 340, sel ? 560 : 520);
                UIK.Border(card.gameObject, sel ? UIK.Gold : new Color(1, 1, 1, 0.1f), sel ? 3 : 2);
                UIK.VL(card.gameObject, 8, 18, TextAnchor.UpperCenter);
                bool en = Loc.Lang == Loc.EN;   // English runs longer: tighter type so the card still fits
                int fs = en ? 18 : 21;
                var portrait = UIK.Icon(card.transform, Res.Tile(c.sprite), unlocked ? Color.white : new Color(0.1f, 0.1f, 0.1f), en ? 104 : 128);
                UIK.Txt(card.transform, c.name, en ? 26 : 36, unlocked ? (sel ? UIK.Gold : Color.white) : UIK.Dim, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIK.Txt(card.transform, c.title, en ? 19 : 22, UIK.Dim, TextAnchor.MiddleCenter, FontStyle.Italic);
                if (unlocked)
                {
                    UIK.Txt(card.transform, c.desc, fs, new Color(0.96f, 0.91f, 0.81f));
                    UIK.Txt(card.transform, StatInfo.Fmt(c.mods), fs, Color.white);
                    var w = Content.W(c.startWeapon);
                    UIK.Txt(card.transform, "Khởi đầu: <color=#FFD266>" + w.name + "</color>", fs, UIK.Dim);
                    UIK.Txt(card.transform, "<color=#FFB347><b>[E] " + c.skill + "</b></color>\n<size=" + (fs - 2) + ">" + c.skillDesc + "</size>", fs, new Color(1f, 0.9f, 0.75f));
                    UIK.Spacer(card.transform);
                    var sbk = new StringBuilder("Kiếp đã vượt: ");
                    for (int k = 0; k <= Content.MaxKiep; k++) sbk.Append(Save.charWinKiep[i] >= k ? "<color=#FFD266>■</color> " : "<color=#4A4558>■</color> ");
                    UIK.Txt(card.transform, sbk.ToString(), 20, UIK.Dim);
                }
                else
                {
                    UIK.Spacer(card.transform);
                    UIK.Txt(card.transform, "CHƯA MỞ KHÓA", 28, new Color(1f, 0.45f, 0.45f), TextAnchor.MiddleCenter, FontStyle.Bold);
                    UIK.Txt(card.transform, c.unlockDesc, 22, UIK.Dim);
                    UIK.Spacer(card.transform);
                }
                var btn = card.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                btn.onClick.AddListener(() => { if (Content.CharUnlocked(ci, Save)) { selChar = ci; Sfx.Play("card", 0.6f); ShowCharSelect(); } else Sfx.Play("error", 0.5f); });
                if (sel) PadDefault.Mark(btn);
                card.gameObject.AddComponent<UIHover>().hoverScale = 1.03f;
            }

            // kiếp selector
            var kb = UIK.R(o, "kiep").Place(0.5f, 0, 1200, 70, 0, 170);
            UIK.HL(kb.gameObject, 16, 0, TextAnchor.MiddleCenter, true);
            UIK.Btn(kb, "<", () => { selKiep = Mathf.Max(0, selKiep - 1); ShowCharSelect(); }, UIK.BtnGrey, 30, 70, 60);
            var kt = UIK.Txt(kb, "KIẾP " + selKiep, 36, selKiep == 0 ? Color.white : new Color(1f, 0.5f, 0.4f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIK.LE(kt.gameObject, 340, 60);
            UIK.Btn(kb, ">", () => { if (selKiep < Save.maxKiepUnlocked) { selKiep++; ShowCharSelect(); } else Sfx.Play("error", 0.5f); }, UIK.BtnGrey, 30, 70, 60);
            var kd = UIK.Txt(o, Content.KiepDesc[selKiep] + (selKiep < Content.MaxKiep && selKiep == Save.maxKiepUnlocked ? "   <color=#8A8499>(Phi thăng ở Kiếp này để mở Kiếp tiếp theo)</color>" : ""), 22, UIK.Dim);
            kd.rectTransform.Place(0.5f, 0, 1600, 34, 0, 128);

            var back = UIK.Btn(o, "Quay Lại", ShowMenu, UIK.BtnGrey, 26);
            back.GetComponent<RectTransform>().Place(0, 0, 240, 64, 40, 36);
            var go = UIK.Btn(o, "BẮT ĐẦU", () => Game.I.StartRun(selChar, selKiep), UIK.BtnGold, 36);
            go.GetComponent<RectTransform>().Place(1, 0, 340, 80, -40, 30);
        }

        // ================================================================== Meta (Tàng Kinh Các)
        public void ShowMeta()
        {
            screen = UIScreen.Meta;
            var o = Overlay(0.9f);
            Title(o, "TÀNG KINH CÁC", 60, 64);
            var dh = UIK.Txt(o, "Đạo Hạnh: <color=#FFD266><b>" + Save.daoHanh + "</b></color>   <size=20>(nhận sau mỗi lượt chơi và từ thành tựu)</size>", 30, UIK.Dim);
            dh.rectTransform.Place(0.5f, 1, 1400, 44, 0, -150);
            var list = UIK.R(o, "list").Place(0.5f, 0.5f, 1100, 640, 0, -40);
            UIK.VL(list.gameObject, 12, 0, TextAnchor.UpperCenter);
            for (int i = 0; i < Content.Meta.Length; i++)
            {
                int mi = i;
                var m = Content.Meta[i];
                int lvl = Save.meta[i];
                var rowImg = UIK.Img(list, UIK.Panel, "row");
                UIK.LE(rowImg.gameObject, -1, 92);
                UIK.HL(rowImg.gameObject, 20, 14, TextAnchor.MiddleLeft, false);
                var name = UIK.Txt(rowImg.transform, "<b>" + m.name + "</b>\n<size=20><color=#A8A2B8>" + m.desc + "</color></size>", 28, Color.white, TextAnchor.MiddleLeft);
                UIK.LE(name.gameObject, 420, 64);
                var pips = new StringBuilder();
                for (int k = 0; k < Content.MetaMax; k++) pips.Append(k < lvl ? "<color=#FFD266>■</color> " : "<color=#4A4558>■</color> ");
                var pt = UIK.Txt(rowImg.transform, pips.ToString(), 34, Color.white);
                UIK.LE(pt.gameObject, 260, 64);
                UIK.Spacer(rowImg.transform);
                if (lvl >= Content.MetaMax) { var mx = UIK.Txt(rowImg.transform, "Viên mãn", 26, UIK.Gold); UIK.LE(mx.gameObject, 220, 60); }
                else
                {
                    int cost = Content.MetaCost(lvl);
                    var b = UIK.Btn(rowImg.transform, "Lĩnh ngộ  " + cost, () =>
                    {
                        if (Save.daoHanh < cost) { Sfx.Play("error", 0.6f); return; }
                        Save.daoHanh -= cost; Save.meta[mi]++; Save.Save();
                        Sfx.Play("breakthrough", 0.6f);
                        ShowMeta();
                    }, Save.daoHanh >= cost ? UIK.BtnGold : UIK.BtnGrey, 24, 220, 60);
                    b.interactable = Save.daoHanh >= cost;
                }
            }
            var back = UIK.Btn(o, "Quay Lại", ShowMenu, UIK.BtnGrey, 26);
            back.GetComponent<RectTransform>().Place(0, 0, 240, 64, 40, 36);
        }

        // ================================================================== Achievements
        public void ShowAchievements()
        {
            screen = UIScreen.Achievements;
            var o = Overlay(0.92f);
            Title(o, "THÀNH TỰU", 50, 60);
            int got = 0; foreach (var a in Content.Achs) if (Save.Has(a.id)) got++;
            var pct = UIK.Txt(o, "Hoàn thành tổng: <color=#7CF0FF><b>" + Mathf.FloorToInt(Save.Completion() * 100) + "%</b></color>    ·    Thành tựu " + got + "/" + Content.Achs.Length, 28, UIK.Dim);
            pct.rectTransform.Place(0.5f, 1, 1400, 40, 0, -130);

            var grid = UIK.R(o, "grid").Place(0, 1, 1460, 560, 40, -185);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(358, 90); gl.spacing = new Vector2(9, 9);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 4;
            foreach (var a in Content.Achs)
            {
                bool has = Save.Has(a.id);
                var cell = UIK.Img(grid, has ? new Color(0.2f, 0.16f, 0.07f, 0.95f) : UIK.Panel, "a");
                if (has) UIK.Border(cell.gameObject, UIK.Gold, 2);
                var t = UIK.Txt(cell.transform, "<b>" + a.name + "</b>\n<size=18>" + a.desc + "</size>", 23, has ? UIK.Gold : UIK.Dim, TextAnchor.MiddleLeft);
                t.rectTransform.Anchor(0, 0, 1, 1, 14, 4, 10, 4);
            }

            // char × kiếp matrix
            var mat = UIK.Img(o, UIK.Panel, "matrix");
            mat.rectTransform.Place(1, 1, 330, 560, -40, -185);
            UIK.VL(mat.gameObject, 6, 14, TextAnchor.UpperCenter);
            UIK.Txt(mat.transform, "<b>Phi Thăng theo Kiếp</b>", 24, UIK.Gold);
            UIK.Txt(mat.transform, "Kiếp 0 → 5", 22, UIK.Dim);
            for (int i = 0; i < Content.Chars.Count; i++)
            {
                var sb = new StringBuilder();
                for (int k = 0; k <= Content.MaxKiep; k++) sb.Append(Save.charWinKiep[i] >= k ? "<color=#FFD266>■</color> " : "<color=#4A4558>■</color> ");
                UIK.Txt(mat.transform, Content.Chars[i].name + "\n" + sb, 22, Content.CharUnlocked(i, Save) ? Color.white : UIK.Dim);
            }
            var back = UIK.Btn(o, "Quay Lại", ShowMenu, UIK.BtnGrey, 26);
            back.GetComponent<RectTransform>().Place(0, 0, 240, 64, 40, 36);
        }

        // ================================================================== Codex (Đồ Giám)
        public void ShowCodex()
        {
            screen = UIScreen.Codex;
            var o = Overlay(0.93f);
            Title(o, "ĐỒ GIÁM", 50, 60);
            var pct = UIK.Txt(o, "Đã ghi chép: <color=#7CF0FF><b>" + Save.CodexSeen() + "/" + SaveData.CodexTotal + "</b></color>", 28, UIK.Dim);
            pct.rectTransform.Place(0.5f, 1, 1000, 40, 0, -125);
            var body = UIK.R(o, "body").Place(0.5f, 1, 1500, 760, 0, -175);
            UIK.VL(body.gameObject, 8, 0, TextAnchor.UpperLeft);
            var detail = UIK.Txt(o, "<color=#8A8499>Rê chuột lên một ô để xem chi tiết.</color>", 22, Color.white);
            detail.rectTransform.Place(0.5f, 0, 1300, 90, 0, 110);

            void Section(string title, int count, System.Func<int, (string key, Sprite spr, Color tint, string name, string info)> get)
            {
                UIK.Txt(body, "<b>" + title + "</b>", 24, UIK.Gold, TextAnchor.MiddleLeft);
                var grid = UIK.R(body, "grid");
                var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
                gl.cellSize = new Vector2(64, 64); gl.spacing = new Vector2(8, 8);
                gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 20;
                UIK.LE(grid.gameObject, -1, 72 * Mathf.CeilToInt(count / 20f));
                for (int i = 0; i < count; i++)
                {
                    var e = get(i);
                    bool seen = Save.Seen(e.key);
                    var cell = UIK.Img(grid, seen ? UIK.Panel2 : new Color(0.11f, 0.08f, 0.06f), "c");
                    UIK.Border(cell.gameObject, seen ? new Color(1f, 0.82f, 0.4f, 0.5f) : new Color(1, 1, 1, 0.06f), 1);
                    var ic = UIK.Img(cell.transform, seen ? e.tint : new Color(0, 0, 0, 0.9f), "i");
                    ic.sprite = e.spr; ic.preserveAspect = true; ic.raycastTarget = false;
                    ic.rectTransform.Anchor(0, 0, 1, 1, 6, 6, 6, 6);
                    var h = cell.gameObject.AddComponent<UIHover>();
                    h.hoverScale = 1.1f;
                    string nm = e.name, inf = e.info;
                    h.onEnter = () => detail.text = seen ? "<b><color=#FFD266>" + nm + "</color></b>\n" + inf : "<b>???</b>\n<color=#8A8499>Chưa gặp.</color>";
                }
            }

            Section("VŨ KHÍ", Content.Weapons.Count, i =>
            {
                var w = Content.Weapons[i];
                return ("w:" + w.id, Res.Tile(w.sprite), w.tint, w.name, TagLine(w) + "  ·  " + w.desc);
            });
            Section("PHÁP BẢO", Content.Items.Count, i =>
            {
                var it = Content.Items[i];
                return ("i:" + it.id, Res.Tile(it.sprite), it.tint, it.name + " (" + Content.TierName[it.tier] + ")", ItemText(it).Replace("\n", "  ·  "));
            });
            Section("YÊU MA", Content.Enemies.Count + Content.Bosses.Length, i =>
            {
                var e = i < Content.Enemies.Count ? Content.Enemies[i] : Content.Bosses[i - Content.Enemies.Count];
                bool isBoss = i >= Content.Enemies.Count;
                return ("e:" + e.id, Res.Tile(e.sprite), e.tint, e.name + (isBoss ? " — BOSS" : e.elite ? " — tinh anh" : ""),
                    "Máu gốc " + e.hp + "  ·  Sát thương " + e.dmg + (e.minWave > 1 && !isBoss ? "  ·  xuất hiện từ đợt " + e.minWave : ""));
            });

            var back = UIK.Btn(o, "Quay Lại", ShowMenu, UIK.BtnGrey, 26);
            back.GetComponent<RectTransform>().Place(0, 0, 240, 64, 40, 36);
        }

        // ================================================================== Stats & synergy panels
        void StatsPanel(Transform parent, RectTransform place)
        {
            var run = Run;
            var p = UIK.Img(parent, UIK.Panel, "stats");
            var rt = p.rectTransform; rt.anchorMin = place.anchorMin; rt.anchorMax = place.anchorMax; rt.pivot = place.pivot; rt.sizeDelta = place.sizeDelta; rt.anchoredPosition = place.anchoredPosition;
            Destroy(place.gameObject);
            UIK.VL(p.gameObject, 2, 14, TextAnchor.UpperCenter);
            UIK.Txt(p.transform, "<b>CHỈ SỐ</b>", 26, UIK.Gold);
            UIK.Txt(p.transform, "<size=20>" + Content.Realm(run.level) + "</size>", 20, UIK.Cyan);
            for (int i = 0; i < (int)Stat.COUNT; i++)
            {
                var s = (Stat)i;
                float v = run.stats[s];
                var row = UIK.R(p.transform, "row");
                UIK.LE(row.gameObject, -1, 29);
                var n = UIK.Txt(row, StatInfo.Names[i], 21, UIK.Dim, TextAnchor.MiddleLeft, FontStyle.Normal, false);
                n.rectTransform.Fill();
                string val = s == Stat.MaxHP ? Mathf.CeilToInt(run.hp) + "/" + StatInfo.Num(v) : StatInfo.Num(v) + (StatInfo.IsPct(s) ? "%" : "");
                Color c = s == Stat.MaxHP ? Color.white : v > 0.01f ? new Color(0.55f, 0.96f, 0.63f) : v < -0.01f ? new Color(1f, 0.48f, 0.48f) : Color.white;
                var vt = UIK.Txt(row, val, 22, c, TextAnchor.MiddleRight, FontStyle.Bold, false);
                vt.rectTransform.Fill();
                var hov = row.gameObject.AddComponent<Image>(); hov.color = new Color(0, 0, 0, 0);
                int si = i;
                var h = row.gameObject.AddComponent<UIHover>(); h.hoverScale = 1f;
                h.onEnter = () => { if (detailTxt) detailTxt.text = "<b>" + StatInfo.Names[si] + "</b>: " + StatInfo.Help[si]; };
            }
        }

        void SynergyPanel(Transform parent, float x, float y, float w, float h)
        {
            var run = Run;
            var p = UIK.Img(parent, UIK.Panel, "syn");
            p.rectTransform.Place(1, 1, w, h, x, y);
            UIK.VL(p.gameObject, 4, 14, TextAnchor.UpperLeft);
            UIK.Txt(p.transform, "<b>ĐẠO CỘNG HƯỞNG</b>", 24, UIK.Gold);
            foreach (var syn in Content.Synergies)
            {
                int c = run.TagCount(syn.tag);
                int tier = RunState.SynTier(c);
                var sb = new StringBuilder();
                sb.Append("<b><color=").Append(Content.TagHex(syn.tag)).Append(">").Append(Content.TagName[(int)syn.tag]).Append("</color></b>  <color=#FFFFFF>").Append(c).Append("</color>\n<size=17>");
                for (int k = 0; k < 3; k++)
                {
                    bool on = k < tier;
                    sb.Append(on ? "<color=#FFD266>" : "<color=#5E5870>").Append("(").Append(Content.SynergyAt[k]).Append(") ").Append(syn.desc[k]).Append("</color>");
                    if (k < 2) sb.Append('\n');
                }
                sb.Append("</size>");
                UIK.Txt(p.transform, sb.ToString(), 22, UIK.Dim, TextAnchor.UpperLeft);
            }
            var tt = new StringBuilder();
            var sc = run.ch.skillColor;
            tt.Append("<b><color=#").Append(ColorUtility.ToHtmlStringRGB(sc)).Append(">THẦN THÔNG</color></b>  <color=#FFFFFF>").Append(Skills.FormName(run.ch, run.skillStage)).Append("</color> · ").Append(Skills.StageLabel(run.skillStage)).Append("\n<size=17>");
            if (run.skillCards.Count == 0) tt.Append("<color=#5E5870>Chưa ngộ thẻ nào (Đột Phá để chọn)</color>");
            else
            {
                for (int k = 0; k < run.skillCards.Count; k++)
                {
                    if (k > 0) tt.Append(" · ");
                    tt.Append("<color=#FFD266>").Append(Content.SkillCard(run.skillCards[k]).name).Append("</color>");
                }
            }
            tt.Append("</size>");
            UIK.Txt(p.transform, tt.ToString(), 22, UIK.Dim, TextAnchor.UpperLeft);
        }

        string WeaponInfo(WeaponDef d, int tier)
        {
            var run = Run;
            var st = run.stats;
            var tmp = new WeaponInst(d, tier);
            float pct = st[Stat.Damage] + (d.Melee ? st[Stat.Melee] : st[Stat.Spell]) + run.bloodStacks;
            float eff = tmp.Dmg * Mathf.Max(0.1f, 1f + pct / 100f) * run.DamageMult();
            var sb = new StringBuilder();
            sb.Append("Sát thương: <b>").Append(StatInfo.Num(Mathf.Round(eff * 10) / 10)).Append("</b> <color=#8A8499>(gốc ").Append(StatInfo.Num(tmp.Dmg)).Append(")</color>\n");
            sb.Append("Hồi chiêu: ").Append(tmp.Cooldown(st).ToString("0.00")).Append("s · Tầm: ").Append(tmp.Range(st).ToString("0.0"));
            if (d.crit > 0) sb.Append("\nBạo kích riêng: +").Append(d.crit).Append('%');
            if (d.splash > 0) sb.Append("\nVùng nổ: ").Append(tmp.SplashR(st).ToString("0.0"));
            if (d.type == WType.Chain) sb.Append("\nSét nảy: ").Append(d.chains + run.ExtraChains + (tier >= 2 ? 1 : 0));
            if (d.type == WType.Swing && d.chains > 0) sb.Append("\nSét nảy: ").Append(d.chains + run.ExtraChains + (tier >= 2 ? 1 : 0));
            if (d.freeze > 0) sb.Append("\n<color=#9BE3FF>Đóng băng ").Append(Mathf.RoundToInt(d.freeze * 100)).Append("%</color>");
            if (d.burn > 0) sb.Append("\n<color=#FF9A55>Thiêu đốt</color>");
            if (d.poison > 0) sb.Append("\n<color=#88F060>Trúng độc</color>");
            sb.Append("\n<color=#C8C2D8>").Append(d.desc).Append("</color>");
            return sb.ToString();
        }

        static string TagLine(WeaponDef d)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < d.tags.Length; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append("<color=").Append(Content.TagHex(d.tags[i])).Append(">").Append(Content.TagName[(int)d.tags[i]]).Append("</color>");
            }
            return sb.ToString();
        }

        // ================================================================== Level up
        public void ShowLevelUp()
        {
            screen = UIScreen.LevelUp;
            Hint("levelup", "Mỗi lần lên cấp: chọn 1 công pháp (phím 1-4). Cứ 10 cấp là <color=#FFD266>Đột Phá</color> cảnh giới — phần thưởng phẩm Tiên!");
            var run = Run;
            var o = Overlay(0.8f);
            bool brk = run.IsBreakthrough;
            bool ngo = System.Array.Exists(run.levelOpts, q => q.card != null);
            if (ngo) Hint("ngodao", "<color=#FFD266>Ngộ Đạo</color>: mỗi lần Đột Phá bạn được chọn thẻ <color=#FFD266>Thần Thông</color> — thẻ Đặc trưng chỉ dành cho nhân vật này, thẻ Chung dùng được cho mọi người.");
            Title(o, brk ? "ĐỘT PHÁ " + Content.RealmOnly(run.ChoosingLevel).ToUpper() + (ngo ? " — NGỘ ĐẠO" : "!") : "LÊN CẤP — CHỌN CÔNG PHÁP", 70, brk ? (ngo ? 52 : 76) : 60,
                brk ? new Color(1f, 0.75f, 0.3f) : UIK.Cyan);
            if (brk)
            {
                string ua = Game.UyApText(run.ChoosingLevel).Replace(" · ", "");
                if (ua.Length > 0) UIK.Txt(o, ua, 24, new Color(1f, 0.85f, 0.4f)).rectTransform.Place(0.5f, 1, 1200, 36, 0, -208);
            }
            var info = UIK.Txt(o, Content.Realm(run.ChoosingLevel) + "    ·    còn " + run.pendingLevels + " lần chọn    ·    phím 1-4", 26, UIK.Dim);
            info.rectTransform.Place(0.5f, 1, 1200, 40, 0, -170);

            var place = UIK.R(o, "p").Place(0, 0.5f, 380, 720, 30, 0);
            StatsPanel(o, place);

            var row = UIK.R(o, "opts").Place(0.5f, 0.5f, 1140, 360, 150, 0);
            UIK.HL(row.gameObject, 22, 0, TextAnchor.MiddleCenter, false);
            for (int i = 0; i < run.levelOpts.Length; i++)
            {
                int oi = i;
                var opt = run.levelOpts[i];
                var card = UIK.Img(row, UIK.Panel2, "opt");
                UIK.LE(card.gameObject, 265, 340);
                UIK.Border(card.gameObject, opt.card != null ? run.ch.skillColor : Content.TierColor[opt.tier], opt.tier >= 2 || opt.card != null ? 3 : 2);
                UIK.VL(card.gameObject, 10, 18, TextAnchor.MiddleCenter);
                UIK.Txt(card.transform, "[" + (i + 1) + "]", 20, UIK.Dim);
                if (opt.card != null)
                {   // Ngộ Đạo card (a proper design comes with part E)
                    UIK.Txt(card.transform, opt.card.charId != null ? "Thần Thông · Đặc trưng" : "Thần Thông · Chung", 19, run.ch.skillColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                    UIK.Spacer(card.transform);
                    UIK.Txt(card.transform, opt.card.name, 30, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter, FontStyle.Bold);
                    UIK.Txt(card.transform, opt.card.desc, 22, Color.white);
                }
                else
                {
                    UIK.Txt(card.transform, Content.TierName[opt.tier] + " phẩm", 28, Content.TierColor[opt.tier], TextAnchor.MiddleCenter, FontStyle.Bold);
                    UIK.Spacer(card.transform);
                    UIK.Txt(card.transform, StatInfo.Fmt(opt.mods).Replace("\n", "\n\n"), 30, Color.white);
                }
                UIK.Spacer(card.transform);
                var b = card.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => Game.I.ChooseLevel(oi));
                card.gameObject.AddComponent<UIHover>().hoverScale = 1.06f;
            }
            int cost = run.LevelRerollCost;
            var rr = UIK.Btn(o, "Đổi lựa chọn  (" + cost + ")", () => { if (Run.RerollLevel()) { Sfx.Play("reroll", 0.6f); ShowLevelUp(); } else Sfx.Play("error", 0.5f); },
                run.gold >= cost ? UIK.BtnBlue : UIK.BtnGrey, 26);
            rr.GetComponent<RectTransform>().Place(0.5f, 0, 360, 64, 40, 150);
            rr.interactable = run.gold >= cost;
            var gold = UIK.Txt(o, "Linh thạch: <color=#FFD266>" + run.gold + "</color>", 26, UIK.Dim);
            gold.rectTransform.Place(0.5f, 0, 400, 40, 40, 100);
        }

        // ================================================================== Shop
        public void ShowShop()
        {
            screen = UIScreen.Shop;
            Hint("shop", "Linh Thị: mua vũ khí/pháp bảo. <color=#FFD266>2 vũ khí cùng loại cùng phẩm → hợp nhất</color> lên phẩm cao hơn. Gom 2/4/6 vũ khí cùng Đạo để kích hoạt cộng hưởng (bảng phải).");
            if (Run.routeOffer > 0) Hint("route", "Lộ Tuyến: <color=#FF8A70>Đường Hiểm</color> khó hơn nhưng thưởng lớn (thêm linh khí, rương). " + GameInput.K("Bấm R để làm mới shop.", "") + "");
            var run = Run;
            var o = Overlay(0.84f);
            Title(o, "LINH THỊ", 24, 56);
            var sub = UIK.Txt(o, "Chuẩn bị cho đợt " + (run.wave + 1) + ((run.wave + 1) % 5 == 0 ? "  —  <color=#FF7070>BOSS: " + run.Boss((run.wave + 1) / 5 - 1).name + "</color>" : ""), 26, UIK.Dim);
            sub.rectTransform.Place(0.5f, 1, 1200, 40, 0, -98);

            // gold
            var gbox = UIK.R(o, "gold").Place(0.5f, 1, 300, 50, 0, -140);
            UIK.HL(gbox.gameObject, 10, 0, TextAnchor.MiddleCenter, false);
            var gi = UIK.Img(gbox, new Color(1f, 0.85f, 0.35f), "d"); gi.sprite = Res.Diamond; UIK.LE(gi.gameObject, 30, 30);
            var gt = UIK.Txt(gbox, run.gold.ToString(), 40, UIK.Gold, TextAnchor.MiddleLeft, FontStyle.Bold); UIK.LE(gt.gameObject, 160, 50);

            var place = UIK.R(o, "p").Place(0, 1, 360, 760, 24, -24);
            StatsPanel(o, place);
            SynergyPanel(o, -24, -24, 380, 800);

            // cards
            var row = UIK.R(o, "cards").Place(0.5f, 1, 1120, 430, -10, -200);
            UIK.HL(row.gameObject, 16, 0, TextAnchor.UpperCenter, false);
            for (int i = 0; i < run.shop.Length; i++) ShopCard(row, i);

            // reroll
            int rc = run.RerollCost;
            var rr = UIK.Btn(o, "Làm mới [R]  (" + rc + ")", DoReroll, run.gold >= rc ? UIK.BtnBlue : UIK.BtnGrey, 24);
            rr.GetComponent<RectTransform>().Place(0.5f, 1, 300, 54, -10, -645);
            rr.interactable = run.gold >= rc;

            // inventory: weapons
            var inv = UIK.Img(o, UIK.Panel, "inv");
            inv.rectTransform.Place(0.5f, 0, 1120, 340, -10, 24);
            var wl = UIK.Txt(inv.transform, "<b>VŨ KHÍ</b>  " + run.weapons.Count + "/" + run.maxWeapons + "   <size=19><color=#8A8499>(bấm để bán / hợp nhất — 2 vũ khí cùng loại cùng phẩm → phẩm cao hơn)</color></size>", 23, UIK.Gold, TextAnchor.MiddleLeft);
            wl.rectTransform.Place(0, 1, 1080, 34, 18, -8);
            var wrow = UIK.R(inv.transform, "wrow").Place(0, 1, 1080, 92, 18, -46);
            UIK.HL(wrow.gameObject, 10, 0, TextAnchor.MiddleLeft, false);
            foreach (var w in run.weapons)
            {
                var ww = w;
                bool sel = w == selWeapon;
                var slot = UIK.Img(wrow, sel ? UIK.Panel2 : new Color(0.13f, 0.10f, 0.07f), "w");
                UIK.LE(slot.gameObject, 88, 88);
                UIK.Border(slot.gameObject, sel ? Color.white : Content.TierColor[w.tier], sel ? 3 : 2);
                var ic = UIK.Img(slot.transform, w.def.tint, "i"); ic.sprite = w.Icon; ic.preserveAspect = true; ic.raycastTarget = false;
                ic.rectTransform.Anchor(0, 0, 1, 1, 10, 10, 10, 10);
                if (run.MergePartner(w) != null)
                {
                    var m = UIK.Txt(slot.transform, "▲", 20, UIK.Gold, TextAnchor.UpperRight);
                    m.rectTransform.Anchor(0, 0, 1, 1, 0, 0, 4, 0);
                }
                var b = slot.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { selWeapon = ww == selWeapon ? null : ww; Sfx.Play("click", 0.5f); ShowShop(); });
                var h = slot.gameObject.AddComponent<UIHover>();
                h.onEnter = () => { if (detailTxt) detailTxt.text = "<b><color=" + Content.TierHex(ww.tier) + ">" + ww.def.name + " (" + Content.TierName[ww.tier] + ")</color></b>  " + TagLine(ww.def) + "\n" + WeaponInfo(ww.def, ww.tier); };
            }

            // items
            var il = UIK.Txt(inv.transform, "<b>PHÁP BẢO</b>  " + run.items.Count, 23, UIK.Gold, TextAnchor.MiddleLeft);
            il.rectTransform.Place(0, 1, 400, 34, 18, -146);
            var irow = UIK.R(inv.transform, "irow").Place(0, 1, 1080, 56, 18, -182);
            UIK.HL(irow.gameObject, 6, 0, TextAnchor.MiddleLeft, false);
            var seen = new List<ItemDef>();
            foreach (var it in run.items)
            {
                if (seen.Contains(it)) continue;
                seen.Add(it);
                var itc = it;
                int cnt = run.Count(it.id);
                var slot = UIK.Img(irow, new Color(0.13f, 0.10f, 0.07f), "it");
                UIK.LE(slot.gameObject, 54, 54);
                UIK.Border(slot.gameObject, Content.TierColor[it.tier], 1);
                var ic = UIK.Img(slot.transform, it.tint, "i"); ic.sprite = Res.Tile(it.sprite); ic.preserveAspect = true; ic.raycastTarget = false;
                ic.rectTransform.Anchor(0, 0, 1, 1, 6, 6, 6, 6);
                if (cnt > 1) { var ct = UIK.Txt(slot.transform, "x" + cnt, 16, Color.white, TextAnchor.LowerRight); ct.rectTransform.Anchor(0, 0, 1, 1, 0, 0, 3, 0); }
                var h = slot.gameObject.AddComponent<UIHover>();
                h.onEnter = () => { if (detailTxt) detailTxt.text = "<b><color=" + Content.TierHex(itc.tier) + ">" + itc.name + "</color></b> x" + cnt + "\n" + ItemText(itc); };
            }

            // detail / selected weapon actions
            var det = UIK.R(inv.transform, "detail").Place(0, 0, 760, 86, 18, 4);
            detailTxt = UIK.Txt(det, "<color=#8A8499>Rê chuột lên vũ khí, pháp bảo hoặc chỉ số để xem chi tiết.</color>", 19, Color.white, TextAnchor.UpperLeft);
            detailTxt.rectTransform.Fill();
            if (selWeapon != null && run.weapons.Contains(selWeapon))
            {
                var sw = selWeapon;
                detailTxt.text = "<b><color=" + Content.TierHex(sw.tier) + ">" + sw.def.name + " (" + Content.TierName[sw.tier] + ")</color></b>  " + TagLine(sw.def) + "\n" + WeaponInfo(sw.def, sw.tier);
                var acts = UIK.R(inv.transform, "acts").Place(1, 0, 290, 110, -16, 10);
                UIK.VL(acts.gameObject, 8, 0, TextAnchor.LowerRight);
                if (run.MergePartner(sw) != null)
                    UIK.Btn(acts, "Hợp nhất → " + Content.TierName[sw.tier + 1], () => { Run.Merge(sw); Sfx.Play("breakthrough", 0.7f); ShowShop(); }, UIK.BtnGold, 22, -1, 50);
                var sellB = UIK.Btn(acts, "Bán  +" + run.SellValue(sw), () => { Run.Sell(sw); selWeapon = null; Sfx.Play("coin", 0.7f); ShowShop(); }, UIK.BtnRed, 22, -1, 50);
                sellB.interactable = run.weapons.Count > 1;
            }
            else selWeapon = null;

            // next wave / route choice (Lộ Tuyến)
            int nw = run.wave + 1;
            if (nw % 5 == 0 || run.routeOffer == 0)
            {
                var go = UIK.Btn(o, "BẮT ĐẦU ĐỢT " + nw + "  ▶", () => { selWeapon = null; Game.I.NextWave(); }, nw % 5 == 0 ? UIK.BtnRed : UIK.BtnGold, 30);
                go.GetComponent<RectTransform>().Place(1, 0, 380, 90, -24, 30);
            }
            else
            {
                int ro = run.routeOffer;
                var lt = UIK.Txt(o, "<b>LỘ TUYẾN — ĐỢT " + nw + "</b>", 22, UIK.Gold);
                lt.rectTransform.Place(1, 0, 380, 30, -24, 222);
                var safe = UIK.Btn(o, "", () => { selWeapon = null; Game.I.NextWave(0); }, UIK.BtnGold, 26);
                safe.SetLabel("<b>Đường Bằng ▶</b>\n<size=17>Bình thường</size>");
                safe.GetComponent<RectTransform>().Place(1, 0, 380, 84, -24, 142);
                var risk = UIK.Btn(o, "", () => { selWeapon = null; Game.I.NextWave(ro); }, UIK.BtnRed, 26);
                risk.SetLabel("<b>Đường Hiểm ▶</b>\n<size=20>" + Content.RouteName[ro] + "</size>\n<size=16>" + Content.RouteDesc[ro] + "</size>");
                risk.GetComponent<RectTransform>().Place(1, 0, 380, 116, -24, 22);
            }
        }

        static string ItemText(ItemDef it)
        {
            string s = StatInfo.Fmt(it.mods);
            if (!string.IsNullOrEmpty(it.desc)) s += (s.Length > 0 ? "\n" : "") + "<color=#FFE7A8>" + it.desc + "</color>";
            s += "\n<color=#8A8499>Giới hạn: " + it.Cap + "</color>";
            return s;
        }

        void ShopCard(Transform row, int i)
        {
            var run = Run;
            var s = run.shop[i];
            var card = UIK.Img(row, s.sold ? new Color(0.12f, 0.09f, 0.07f, 0.6f) : UIK.Panel2, "card");
            UIK.LE(card.gameObject, 268, 430);
            UIK.Border(card.gameObject, s.locked ? UIK.Gold : Content.TierColor[s.tier], s.locked ? 4 : (s.tier >= 2 ? 3 : 2));
            if (s.sold)
            {
                var t = UIK.Txt(card.transform, "ĐÃ MUA", 30, UIK.Dim, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.rectTransform.Fill();
                return;
            }
            UIK.VL(card.gameObject, 6, 14, TextAnchor.UpperCenter);
            bool isW = s.w != null;
            var spr = isW ? Res.Tile(s.w.sprite) : Res.Tile(s.it.sprite);
            var iconBg = UIK.Img(card.transform, new Color(0, 0, 0, 0.25f), "iconbg");
            UIK.LE(iconBg.gameObject, -1, 100);
            var ic = UIK.Img(iconBg.transform, isW ? s.w.tint : s.it.tint, "icon"); ic.sprite = spr; ic.preserveAspect = true; ic.raycastTarget = false;
            ic.rectTransform.Anchor(0, 0, 1, 1, 10, 8, 10, 8);
            var glow = UIK.Img(iconBg.transform, new Color(Content.TierColor[s.tier].r, Content.TierColor[s.tier].g, Content.TierColor[s.tier].b, 0.12f), "g");
            glow.rectTransform.Fill(); glow.raycastTarget = false; glow.transform.SetAsFirstSibling();
            UIK.Txt(card.transform, isW ? s.w.name : s.it.name, 26, Content.TierColor[s.tier], TextAnchor.MiddleCenter, FontStyle.Bold);
            UIK.Txt(card.transform, isW ? "Vũ khí " + Content.TierName[s.tier] + "  ·  " + TagLine(s.w) : "Pháp bảo " + Content.TierName[s.tier], 18, UIK.Dim);
            string body = isW ? WeaponInfo(s.w, s.tier) : ItemText(s.it);
            if (isW && run.MergeTarget(s.w, s.tier) != null && run.weapons.Count >= run.maxWeapons) body += "\n<color=#FFD266>Đủ ô: tự hợp nhất lên " + Content.TierName[s.tier + 1] + "!</color>";
            else if (isW && run.MergeTarget(s.w, s.tier) != null) body += "\n<color=#FFD266>▲ Có thể hợp nhất</color>";
            UIK.Txt(card.transform, body, 18, Color.white);
            UIK.Spacer(card.transform);
            var br = UIK.R(card.transform, "buy");
            UIK.LE(br.gameObject, -1, 54, -1, 0);
            UIK.HL(br.gameObject, 8, 0, TextAnchor.MiddleCenter, true);
            UIK.Btn(br, s.locked ? "Mở" : "Khóa", () => { Run.shop[i].locked = !Run.shop[i].locked; Sfx.Play("click", 0.5f); ShowShop(); }, s.locked ? UIK.BtnGold : UIK.BtnGrey, 19, 70, 54);
            string why = run.CantBuy(i);
            var buy = UIK.Btn(br, why == null ? "Mua  " + s.price : s.price + " · " + why, () =>
            {
                if (Run.Buy(i)) { Sfx.Play("coin", 0.8f); Sfx.Play("confirm", 0.4f, 0.05f, 1.3f); ShowShop(); }
                else Sfx.Play("error", 0.5f);
            }, why == null ? new Color(0.2f, 0.45f, 0.3f) : UIK.BtnGrey, why == null ? 24 : 17);
            UIK.LE(buy.gameObject, 166, 54);
            buy.interactable = why == null;
        }

        void DoReroll()
        {
            if (Run == null) return;
            if (Run.Reroll()) { Sfx.Play("reroll", 0.7f); ShowShop(); }
            else Sfx.Play("error", 0.5f);
        }

        // ================================================================== Chest
        public void ShowChest()
        {
            screen = UIScreen.Chest;
            Hint("chest", "Bảo Rương do tinh anh và boss rơi: nhận pháp bảo hoặc bán lấy linh thạch.");
            var run = Run;
            var it = run.chestItem;
            var o = Overlay(0.82f);
            Title(o, "BẢO RƯƠNG", 120, 72);
            var sub = UIK.Txt(o, run.chests > 1 ? "Còn " + run.chests + " rương" : "Tinh anh để lại bảo vật", 26, UIK.Dim);
            sub.rectTransform.Place(0.5f, 1, 1000, 40, 0, -220);
            var glow = UIK.Img(o, new Color(Content.TierColor[it.tier].r, Content.TierColor[it.tier].g, Content.TierColor[it.tier].b, 0.12f), "glow");
            glow.sprite = Res.Glow; glow.rectTransform.Place(0.5f, 0.5f, 760, 760, 0, -10); glow.raycastTarget = false;
            var card = UIK.Img(o, UIK.Panel2, "card");
            card.rectTransform.Place(0.5f, 0.5f, 380, 420, 0, -10);
            UIK.Border(card.gameObject, Content.TierColor[it.tier], 4);
            UIK.VL(card.gameObject, 10, 22, TextAnchor.UpperCenter);
            var ic = UIK.Icon(card.transform, Res.Tile(it.sprite), it.tint, 130);
            UIK.Txt(card.transform, it.name, 34, Content.TierColor[it.tier], TextAnchor.MiddleCenter, FontStyle.Bold);
            UIK.Txt(card.transform, "Pháp bảo " + Content.TierName[it.tier] + " phẩm", 22, UIK.Dim);
            UIK.Txt(card.transform, ItemText(it), 24, Color.white);
            var row = UIK.R(o, "btns").Place(0.5f, 0, 760, 80, 0, 110);
            UIK.HL(row.gameObject, 20, 0, TextAnchor.MiddleCenter, true);
            bool full = run.Count(it.id) >= it.Cap;
            var take = UIK.Btn(row, full ? "Đã đạt giới hạn" : "Nhận", () => Game.I.TakeChest(false), full ? UIK.BtnGrey : UIK.BtnGold, 30, 340, 76);
            take.interactable = !full;
            UIK.Btn(row, "Bán  +" + run.ChestSellValue, () => Game.I.TakeChest(true), UIK.BtnRed, 26, 340, 76);
            Sfx.Play("open", 0.7f);
        }

        // ================================================================== Boss treasure (pick 1 of 3)
        public void ShowRelics()
        {
            screen = UIScreen.Relic;
            var run = Run;
            var o = Overlay(0.86f);
            Title(o, "BẢO KHỐ", 120, 72, new Color(0.95f, 0.62f, 0.42f));
            var sub = UIK.Txt(o, run.Boss(run.wave / 5 - 1).name + " để lại kho báu — chọn một món", 26, UIK.Dim);
            sub.rectTransform.Place(0.5f, 1, 1100, 40, 0, -220);
            if (run.stageUpWave == run.wave)
            {
                Hint("tiengiai", "Mỗi boss hạ gục sẽ <color=#FFD266>Tiến Giai</color> Thần Thông: chiêu đổi dạng, mạnh hơn. Xem tên dạng ở thanh chiêu.");
                var sc = run.ch.skillColor;
                var tg = UIK.Txt(o, "<b>THẦN THÔNG TIẾN GIAI: " + Skills.FormName(run.ch, run.skillStage) + " (" + Skills.StageLabel(run.skillStage) + ")</b>\n<size=21>" + Skills.FormDesc(run.ch, run.skillStage) + "</size>", 28, sc);
                tg.rectTransform.Place(0.5f, 1, 1100, 66, 0, -258);
            }
            var row = UIK.R(o, "relics").Place(0.5f, 0.5f, 1180, 470, 0, -52);
            UIK.HL(row.gameObject, 30, 0, TextAnchor.MiddleCenter, false);
            for (int i = 0; i < run.relicChoices.Length; i++)
            {
                int ri = i;
                var it = run.relicChoices[i];
                var card = UIK.Img(row, UIK.Panel2, "relic");
                UIK.LE(card.gameObject, 360, 460);
                UIK.Border(card.gameObject, Content.TierColor[it.tier], 4);
                UIK.VL(card.gameObject, 8, 18, TextAnchor.UpperCenter);
                UIK.Txt(card.transform, "[" + (i + 1) + "]", 20, UIK.Dim);
                UIK.Icon(card.transform, Res.Tile(it.sprite), it.tint, 110);
                UIK.Txt(card.transform, it.name, 30, Content.TierColor[it.tier], TextAnchor.MiddleCenter, FontStyle.Bold);
                UIK.Txt(card.transform, "Pháp bảo " + Content.TierName[it.tier] + " phẩm", 20, UIK.Dim);
                UIK.Txt(card.transform, ItemText(it), 22, Color.white);
                var b = card.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => Game.I.TakeRelic(ri));
                card.gameObject.AddComponent<UIHover>().hoverScale = 1.05f;
            }
            Sfx.Play("open", 0.8f);
            Sfx.Play("bell", 0.5f, 0f, 0.8f);
        }

        // ================================================================== Event (Kỳ Ngộ)
        public void ShowEvent(EventDef ev)
        {
            screen = UIScreen.Event;
            Hint("event", "Kỳ Ngộ: mỗi lựa chọn là một canh bạc. Đọc kỹ cái giá trước khi chọn (phím 1-3).");
            var run = Run;
            var o = Overlay(0.88f);
            Title(o, "KỲ NGỘ", 70, 48, UIK.Dim);
            Title(o, ev.title.ToUpper(), 130, 70);
            var por = UIK.Img(o, Color.white, "art");
            por.sprite = Res.Tile(ev.sprite); por.preserveAspect = true; por.raycastTarget = false;
            por.rectTransform.Place(0.5f, 1, 160, 160, 0, -240);
            var tx = UIK.Txt(o, "<i>" + ev.text + "</i>", 28, new Color(0.96f, 0.91f, 0.81f));
            tx.rectTransform.Place(0.5f, 1, 1100, 110, 0, -420);
            var col = UIK.R(o, "opts").Place(0.5f, 0, 900, 120 * ev.options.Length, 0, 90);
            UIK.VL(col.gameObject, 14, 0, TextAnchor.LowerCenter);
            for (int i = 0; i < ev.options.Length; i++)
            {
                int oi = i;
                var op = ev.options[i];
                bool can = op.can(run);
                var b = UIK.Btn(col, "", () => Game.I.ChooseEvent(oi), can ? UIK.Panel2 : new Color(0.14f, 0.11f, 0.08f), 26, -1, 100);
                b.SetLabel("<b>[" + (i + 1) + "] " + op.label + "</b>\n<size=21>" + (can ? CostColors(op.desc) : "<color=#706878>" + op.desc + "</color>") + "</size>");
                b.interactable = can;
                UIK.Border(b.gameObject, can ? new Color(1f, 0.82f, 0.4f, 0.5f) : new Color(1, 1, 1, 0.08f), 2);
            }
            var gold = UIK.Txt(o, "Linh thạch: <color=#FFD266>" + run.gold + "</color>", 24, UIK.Dim, TextAnchor.MiddleRight);
            gold.rectTransform.Place(1, 1, 400, 40, -30, -30);
            Sfx.Play("card", 0.7f);
        }

        /// <summary>Gains in green, costs ("-6 Sinh Lực", "Mất 20 linh thạch") in red.</summary>
        static string CostColors(string desc)
        {
            var parts = System.Text.RegularExpressions.Regex.Split(desc, @"(?<=[,.:]) ");
            for (int i = 0; i < parts.Length; i++)
            {
                string t = parts[i];
                bool cost = t.StartsWith("-") || t.StartsWith("Mất");
                parts[i] = "<color=" + (cost ? "#FF8A80" : "#B8F0C0") + ">" + t + "</color>";
            }
            return string.Join(" ", parts);
        }

        // ================================================================== Pause
        public void ShowPause()
        {
            screen = UIScreen.Pause;
            var o = Overlay(0.75f);
            LangButton(o);
            Title(o, "TẠM DỪNG", 200, 72);
            var col = UIK.R(o, "col").Place(0.5f, 0.5f, 420, 440, 0, -60);
            UIK.VL(col.gameObject, 14, 0, TextAnchor.MiddleCenter);
            PadDefault.Mark(UIK.Btn(col, "Tiếp Tục", () => Game.I.SetPause(false), UIK.BtnGold, 30, -1, 70));
            Button sfxB = null, musB = null;
            sfxB = UIK.Btn(col, VolLabel("Âm hiệu", Save.sfxVol), () => { Save.sfxVol = Cycle(Save.sfxVol); Save.Save(); sfxB.SetLabel(VolLabel("Âm hiệu", Save.sfxVol)); }, UIK.BtnGrey, 24, -1, 52);
            musB = UIK.Btn(col, VolLabel("Nhạc", Save.musicVol), () => { Save.musicVol = Cycle(Save.musicVol); Save.Save(); musB.SetLabel(VolLabel("Nhạc", Save.musicVol)); }, UIK.BtnGrey, 24, -1, 52);
            SettingsButtons(col);
            UIK.Btn(col, "Bỏ Cuộc", () => Game.I.Abandon(), UIK.BtnRed, 24, -1, 56);
            var place = UIK.R(o, "p").Place(0, 0.5f, 380, 700, 30, -60);
            StatsPanel(o, place);
            SynergyPanel(o, -24, -180, 380, 800);
        }

        // ================================================================== Game over
        /// <summary>The run's final build: weapons with their tier frame, then the most valuable items with stack counts.</summary>
        static void BuildRecap(Transform parent, RunState run)
        {
            if (run == null) return;
            var wrow = UIK.R(parent, "weapons");
            UIK.LE(wrow.gameObject, 760, 54);
            UIK.HL(wrow.gameObject, 8, 0, TextAnchor.MiddleCenter, false);
            foreach (var w in run.weapons)
            {
                var ic = UIK.Icon(wrow, Res.Tile(w.def.sprite), w.def.tint, 48);
                UIK.Border(ic.gameObject, Content.TierColor[w.tier], 2);
            }
            var counts = new Dictionary<ItemDef, int>();
            foreach (var it in run.items) counts[it] = counts.TryGetValue(it, out var n) ? n + 1 : 1;
            var list = new List<ItemDef>(counts.Keys);
            list.Sort((a, b) => b.tier != a.tier ? b.tier.CompareTo(a.tier) : counts[b].CompareTo(counts[a]));
            var irow = UIK.R(parent, "items");
            UIK.LE(irow.gameObject, 760, 44);
            UIK.HL(irow.gameObject, 6, 0, TextAnchor.MiddleCenter, false);
            for (int i = 0; i < Mathf.Min(16, list.Count); i++)
            {
                var it = list[i];
                var ic = UIK.Icon(irow, Res.Tile(it.sprite), it.tint, 38);
                UIK.Border(ic.gameObject, Content.TierColor[it.tier], 1.5f);
                if (counts[it] > 1)
                {
                    var t = UIK.Txt(ic.transform, "x" + counts[it], 15, Color.white, TextAnchor.LowerRight, FontStyle.Bold);
                    t.rectTransform.Fill();
                }
            }
            if (list.Count > 16) UIK.Txt(irow, "+" + (list.Count - 16), 18, UIK.Dim);
        }

        public void ShowGameOver(RunSummary s)
        {
            screen = UIScreen.GameOver;
            var o = Overlay(0.85f);
            Title(o, s.win ? "PHI THĂNG THÀNH TIÊN!" : "THÂN TỬ ĐẠO TIÊU", 140, 84, s.win ? UIK.Gold : new Color(1f, 0.4f, 0.4f));
            var sub = UIK.Txt(o, s.charName + "   ·   Kiếp " + s.kiep, 30, UIK.Dim, TextAnchor.MiddleCenter, FontStyle.Italic);
            sub.rectTransform.Place(0.5f, 1, 1000, 44, 0, -250);
            var panel = UIK.Img(o, UIK.Panel, "sum");
            panel.rectTransform.Place(0.5f, 0.5f, 820, 500, 0, -50);   // starts below the character · Kiếp subtitle
            UIK.VL(panel.gameObject, 8, 24, TextAnchor.UpperCenter);
            UIK.Txt(panel.transform, (s.win ? "Vượt qua cả 20 đợt" : "Gục ngã ở đợt " + s.wave), 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIK.Txt(panel.transform, "Cảnh giới: <color=#7CF0FF>" + Content.Realm(s.level) + "</color>", 26, UIK.Dim);
            UIK.Txt(panel.transform, "Kẻ địch bị diệt: <color=#FFFFFF>" + s.kills + "</color>", 26, UIK.Dim);
            UIK.Txt(panel.transform, "Tổng sát thương: <color=#FFFFFF>" + Mathf.RoundToInt(s.dmgDealt) + "</color>   ·   Đòn mạnh nhất: <color=#FFFFFF>" + Mathf.RoundToInt(s.bestHit) + "</color>", 24, UIK.Dim);
            UIK.Txt(panel.transform, "Liên Sát cao nhất: <color=#FFD266>x" + s.bestCombo + "</color>", 24, UIK.Dim);
            UIK.Txt(panel.transform, "+" + s.daoHanh + " Đạo Hạnh", 40, UIK.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            foreach (var u in s.unlocks) UIK.Txt(panel.transform, u, 26, new Color(1f, 0.75f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);
            BuildRecap(panel.transform, Game.I.run);

            var row = UIK.R(o, "btns").Place(0.5f, 0, 1100, 80, 0, 90);
            UIK.HL(row.gameObject, 20, 0, TextAnchor.MiddleCenter, true);
            int ci = Game.I.run.charIndex, k = Game.I.run.kiep;
            PadDefault.Mark(UIK.Btn(row, "Tái Chiến", () => Game.I.StartRun(ci, k), UIK.BtnGold, 30, 320, 76));
            UIK.Btn(row, "Tàng Kinh Các", () => { Game.I.GoMenu(); ShowMeta(); }, UIK.BtnBlue, 26, 320, 76);
            UIK.Btn(row, "Về Sơn Môn", () => Game.I.GoMenu(), UIK.BtnGrey, 26, 320, 76);
        }
    }
}

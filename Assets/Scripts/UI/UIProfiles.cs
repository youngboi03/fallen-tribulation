using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VK
{
    /// <summary>Profile screen (3 cards, name entry, delete confirm) and the "erase all progress" dialog of Settings.</summary>
    public partial class UIRoot
    {
        bool resetConfirm;           // Settings is showing its erase dialog
        public int profMode;         // 0 cards, 1 name entry, 2 delete confirm
        int profSlot; bool profCreate;
        public InputField NameField { get; private set; }   // tests
        public bool ResetConfirmUp => resetConfirm;

        public void ShowProfiles() { profMode = 0; BuildProfiles(); }
        public void ProfileBack() { if (profMode != 0) ShowProfiles(); else ShowMenu(); }

        void BuildProfiles()
        {
            screen = UIScreen.Profiles; NameField = null;
            GetComponent<PadNav>()?.Forget();
            UIK.Clear(hud); hpFill = null;
            var o = Overlay(0.92f);
            Title(o, "Hồ Sơ", 50, 64);
            if (profMode == 1) { NameEntry(o); return; }
            if (profMode == 2) { DeleteConfirm(o); return; }
            for (int i = 0; i < Profiles.Max; i++) ProfileCard(o, i);
            var back = UIK.Btn(o, "Quay Lại", ShowMenu, UIK.BtnGrey, 28);
            back.GetComponent<RectTransform>().Place(0.5f, 0, 300, 64, 0, 60);
            PadDefault.Mark(back);
        }

        void ProfileCard(Transform o, int i)
        {
            bool used = Profiles.Used(i), act = i == Profiles.Active;
            var card = UIK.Img(o, UIK.Panel, "card");
            card.rectTransform.Place(0.5f, 1, 560, 640, (i - 1) * 600, -170);
            UIK.VL(card.gameObject, 12, 24, TextAnchor.UpperCenter);
            if (act) UIK.Border(card.gameObject, UIK.Gold, 3);
            if (!used)
            {
                UIK.Spacer(card.transform);
                var e = UIK.Txt(card.transform, "Hồ sơ trống", 32, UIK.Dim); UIK.LE(e.gameObject, -1, 50);
                UIK.Spacer(card.transform);
                UIK.Btn(card.transform, "Tạo hồ sơ", () => { profMode = 1; profSlot = i; profCreate = true; BuildProfiles(); }, UIK.BtnGold, 30, -1, 70);
                return;
            }
            var d = act ? Save : SaveData.Load(Profiles.SlotKey(i));
            var n = UIK.Txt(card.transform, Profiles.Name(i), 42, UIK.Gold, TextAnchor.MiddleCenter, FontStyle.Bold); UIK.LE(n.gameObject, -1, 60);
            var tag = UIK.Txt(card.transform, act ? "Đang dùng" : " ", 24, new Color(0.49f, 0.94f, 1f)); UIK.LE(tag.gameObject, -1, 34);
            var st = UIK.Txt(card.transform, "Đạo Hạnh: <color=#FFD266>" + d.daoHanh + "</color>\nLượt chơi: " + d.runs + "\nĐợt tốt nhất: " + d.bestWave + "\nThắng: " + d.wins, 28, UIK.Dim);
            UIK.LE(st.gameObject, -1, 170);
            UIK.Spacer(card.transform);
            var pick = UIK.Btn(card.transform, "Chọn", () => LoadProfile(Profiles.Switch(i, Save)), UIK.BtnBlue, 28, -1, 62);
            pick.interactable = !act;
            UIK.Btn(card.transform, "Đổi tên", () => { profMode = 1; profSlot = i; profCreate = false; BuildProfiles(); }, UIK.BtnGrey, 26, -1, 58);
            var del = UIK.Btn(card.transform, "Xoá", () => { profMode = 2; profSlot = i; BuildProfiles(); }, UIK.BtnRed, 26, -1, 58);
            del.interactable = !act || Profiles.UsedCount == 1;   // the active profile stays unless it is the only one (then Xoá resets it)
        }

        /// <summary>Makes <paramref name="d"/> the live save: language, display, back to the menu.</summary>
        public void LoadProfile(SaveData d)
        {
            profMode = 0;
            Game.I.save = d;
            UIRoot.ApplyDisplay(d, Game.I.bot == null);
            ApplyLanguage(d.lang < 0 ? Loc.Lang : d.lang);   // swaps the font if needed and rebuilds the menu
        }

        void NameEntry(Transform o)
        {
            var prompt = UIK.Txt(o, profCreate ? "Đặt tên hồ sơ mới" : "Đổi tên hồ sơ", 32, UIK.Dim);
            prompt.rectTransform.Place(0.5f, 1, 1000, 44, 0, -220);
            var bg = UIK.Img(o, new Color(0.08f, 0.06f, 0.05f, 0.96f), "field");
            bg.rectTransform.Place(0.5f, 1, 720, 80, 0, -290);
            UIK.Border(bg.gameObject, UIK.Gold, 2);
            var tr = UIK.R(bg.transform, "text").Anchor(0, 0, 1, 1, 20, 6, 20, 6);
            var tx = tr.gameObject.AddComponent<Text>();   // plain Text: the player's own words must not go through the phrase translator
            tx.font = Res.Font; tx.fontSize = 36; tx.color = new Color(1f, 0.96f, 0.87f); tx.alignment = TextAnchor.MiddleLeft; tx.supportRichText = false;
            tx.horizontalOverflow = HorizontalWrapMode.Overflow;
            var ph = UIK.Txt(bg.transform, "Tên hồ sơ", 32, new Color(0.6f, 0.55f, 0.48f), TextAnchor.MiddleLeft, FontStyle.Italic, false);
            ph.rectTransform.Anchor(0, 0, 1, 1, 20, 6, 20, 6);
            var f = bg.gameObject.AddComponent<InputField>();
            f.textComponent = tx; f.placeholder = ph; f.targetGraphic = bg;
            f.characterLimit = Profiles.NameMax; f.lineType = InputField.LineType.SingleLine;
            f.text = profCreate ? "" : Profiles.Name(profSlot);
            f.onEndEdit.AddListener(_ => { if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) NameOk(); });
            NameField = f;

            var row = UIK.R(o, "row").Place(0.5f, 1, 900, 70, 0, -400);
            UIK.HL(row.gameObject, 20, 0);
            UIK.Btn(row, "Ngẫu nhiên", () => f.text = Loc.Tr(Profiles.RandomNames[Random.Range(0, Profiles.RandomNames.Length)]), UIK.BtnBlue, 26, 260, 64);
            PadDefault.Mark(UIK.Btn(row, "OK", NameOk, UIK.BtnGold, 30, 200, 64));
            UIK.Btn(row, "Huỷ", ShowProfiles, UIK.BtnGrey, 26, 200, 64);
            if (!GameInput.Pad || Platform.Touch) StartCoroutine(Focus(f));   // phones: opens the soft keyboard; a pad user types nothing
        }

        static IEnumerator Focus(InputField f) { yield return null; if (f) f.ActivateInputField(); }

        /// <summary>OK on the name entry (button or Enter): create or rename.</summary>
        public void NameOk()
        {
            if (NameField == null) return;
            string nm = Profiles.Clean(NameField.text, profSlot);
            if (profCreate) LoadProfile(Profiles.Create(profSlot, nm, Save));
            else { Profiles.Rename(profSlot, nm); ShowProfiles(); }
        }

        void DeleteConfirm(Transform o)
        {
            bool only = Profiles.UsedCount == 1;
            var q = UIK.Txt(o, only ? "Đặt lại hồ sơ này?" : "Xoá hồ sơ này?", 46, UIK.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            q.rectTransform.Place(0.5f, 1, 1200, 64, 0, -250);
            var m = UIK.Txt(o, "<b>" + Profiles.Name(profSlot) + "</b>\n" + (only ? "Mọi tiến trình sẽ bị xoá, cài đặt được giữ. Không thể hoàn tác." : "Toàn bộ tiến trình của hồ sơ này sẽ mất vĩnh viễn."), 30, UIK.Dim);
            m.rectTransform.Place(0.5f, 1, 1200, 130, 0, -330);
            var row = UIK.R(o, "row").Place(0.5f, 1, 700, 70, 0, -500);
            UIK.HL(row.gameObject, 30, 0);
            UIK.Btn(row, "Xoá", () =>
            {
                if (only) Game.I.save = Profiles.ResetProgress(Save); else Profiles.Delete(profSlot);
                ShowProfiles();
            }, UIK.BtnRed, 30, 280, 64);
            PadDefault.Mark(UIK.Btn(row, "Huỷ", ShowProfiles, UIK.BtnGrey, 30, 280, 64));
        }

        // ------------------------------------------------------------ Settings: erase all progress
        public void ShowResetConfirm()
        {
            resetConfirm = true; screen = UIScreen.Settings;
            GetComponent<PadNav>()?.Forget();
            UIK.Clear(hud); hpFill = null;
            var o = Overlay(0.92f);
            Title(o, "Xoá Tiến Trình?", 150, 64, new Color(1f, 0.4f, 0.4f));
            var m = UIK.Txt(o, "Xoá toàn bộ tiến trình của hồ sơ <b>" + Profiles.Name(Profiles.Active) + "</b>?\nĐạo Hạnh, nâng cấp, mở khoá, thành tựu, đồ giám và thống kê sẽ mất hết. Cài đặt được giữ. Không thể hoàn tác.", 30, UIK.Dim);
            m.rectTransform.Place(0.5f, 1, 1100, 170, 0, -300);
            var row = UIK.R(o, "row").Place(0.5f, 1, 700, 70, 0, -520);
            UIK.HL(row.gameObject, 30, 0);
            UIK.Btn(row, "Xoá", ResetAllProgress, UIK.BtnRed, 30, 280, 64);
            PadDefault.Mark(UIK.Btn(row, "Huỷ", () => ShowSettings(setFromPause), UIK.BtnGrey, 30, 280, 64));
        }

        public void ResetAllProgress()
        {
            resetConfirm = false;
            Game.I.save = Profiles.ResetProgress(Save);
            Game.I.GoMenu();
            Toast("Đã xoá tiến trình", "Hồ sơ bắt đầu lại từ đầu.");
        }
    }
}

using UnityEngine;

namespace VK
{
    /// <summary>First-launch question about anonymous play statistics (see <see cref="Telemetry"/>). Nothing is sent before the player says yes.</summary>
    public partial class UIRoot
    {
        public const string SupportUrl = "https://youngboi03.itch.io/fallen-tribulation";

        public void ShowPrivacyAsk()
        {
            screen = UIScreen.Menu;
            GetComponent<PadNav>()?.Forget();
            UIK.Clear(hud); hpFill = null;
            var o = Overlay(0.94f);
            LangButton(o);
            Title(o, "Giúp cải thiện game?", 140, 64);
            var m = UIK.Txt(o, "Gửi thống kê chơi ẩn danh cho tác giả?", 36, Color.white);
            m.rectTransform.Place(0.5f, 1, 1200, 60, 0, -280);
            var d = UIK.Txt(o, "Chỉ gửi: nhân vật, độ khó, đợt đạt tới, nguyên nhân thua, lựa chọn build, FPS và hệ điều hành.\nKhông gửi tên, email, địa chỉ hay mã thiết bị.\nBạn đổi ý bất cứ lúc nào trong Cài Đặt.", 28, UIK.Dim);
            d.rectTransform.Place(0.5f, 1, 1300, 190, 0, -390);
            var row = UIK.R(o, "row").Place(0.5f, 1, 800, 70, 0, -650);
            UIK.HL(row.gameObject, 40, 0);
            // equal weight on purpose: declining must be as easy as accepting
            PadDefault.Mark(UIK.Btn(row, "Không, cảm ơn", () => AnswerPrivacy(false), UIK.BtnGrey, 30, 380, 64));
            UIK.Btn(row, "Đồng ý gửi", () => AnswerPrivacy(true), UIK.BtnGrey, 30, 380, 64);
        }

        public void AnswerPrivacy(bool yes)
        {
            Telemetry.SetConsent(yes);
            if (yes) Telemetry.StartSession();
            Sfx.Play("confirm", 0.6f);
            ShowMenu();
        }
    }
}

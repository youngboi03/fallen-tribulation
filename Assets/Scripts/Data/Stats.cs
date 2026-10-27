using System.Text;
using UnityEngine;

namespace VK
{
    public enum Stat { MaxHP, Regen, Lifesteal, Damage, Melee, Spell, AtkSpeed, Crit, CritDmg, Range, Armor, Dodge, Speed, Luck, Harvest, Pickup, SkillPower, SkillHaste, COUNT }

    [System.Serializable]
    public struct Mod
    {
        public Stat s; public float v;
        public Mod(Stat s, float v) { this.s = s; this.v = v; }
    }

    public class StatBlock
    {
        public float[] v = new float[(int)Stat.COUNT];
        public float this[Stat s] { get => v[(int)s]; set => v[(int)s] = value; }
        public void Add(Mod m) => v[(int)m.s] += m.v;
        public void Add(Mod[] ms) { if (ms == null) return; foreach (var m in ms) Add(m); }
        public void Add(StatBlock b) { for (int i = 0; i < v.Length; i++) v[i] += b.v[i]; }
        public void Clear() { System.Array.Clear(v, 0, v.Length); }
    }

    public static class StatInfo
    {
        public static readonly string[] Names = {
            "Sinh Lực", "Hồi Phục", "Hút Máu", "Sát Thương", "Cận Chiến", "Pháp Thuật", "Tốc Đánh", "Bạo Kích",
            "ST Bạo Kích", "Tầm Đánh", "Hộ Giáp", "Né Tránh", "Thân Pháp", "Cơ Duyên", "Thu Hoạch", "Thu Linh", "Thần Lực", "Tĩnh Tâm" };

        public static readonly string[] Help = {
            "Máu tối đa", "Hồi máu mỗi giây", "% cơ hội hồi 1 máu khi đánh trúng", "% sát thương mọi vũ khí",
            "% sát thương vũ khí cận chiến", "% sát thương vũ khí pháp thuật", "% tốc độ tấn công", "% cơ hội bạo kích",
            "% sát thương bạo kích thêm", "% tầm đánh", "Giảm sát thương nhận vào", "% né đòn (tối đa 60%)",
            "% tốc độ di chuyển", "Tăng cơ hội gặp đồ phẩm cao", "Linh thạch nhận thêm mỗi cuối đợt", "% tầm hút linh khí", "% sát thương Thần Thông", "% giảm hồi chiêu Thần Thông" };

        public static bool IsPct(Stat s) =>
            s == Stat.Lifesteal || s == Stat.Damage || s == Stat.Melee || s == Stat.Spell || s == Stat.AtkSpeed ||
            s == Stat.Crit || s == Stat.CritDmg || s == Stat.Range || s == Stat.Dodge || s == Stat.Speed || s == Stat.Pickup || s == Stat.SkillPower || s == Stat.SkillHaste;

        public static string Num(float v) =>
            Mathf.Abs(v - Mathf.Round(v)) < 0.01f ? ((int)Mathf.Round(v)).ToString() : v.ToString("0.#");

        public static string Fmt(Mod m, bool color = true)
        {
            string s = (m.v >= 0 ? "+" : "") + Num(m.v) + (IsPct(m.s) ? "% " : " ") + Names[(int)m.s];
            if (!color) return s;
            return (m.v >= 0 ? "<color=#8CF5A0>" : "<color=#FF7B7B>") + s + "</color>";
        }

        public static string Fmt(Mod[] ms)
        {
            if (ms == null) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < ms.Length; i++) { if (i > 0) sb.Append('\n'); sb.Append(Fmt(ms[i])); }
            return sb.ToString();
        }
    }
}

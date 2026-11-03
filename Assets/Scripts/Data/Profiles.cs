using UnityEngine;

namespace VK
{
    [System.Serializable]
    public class ProfileIndex { public int active; public string[] names = { "", "", "" }; }

    /// <summary>
    /// Up to three player profiles. Slot 0 keeps the original save key so the first player's progress is profile 1;
    /// slots 1 and 2 add "_p1" / "_p2". The tiny index (active slot + names, empty name = unused slot) lives under "vk_profiles".
    /// </summary>
    public static class Profiles
    {
        public const int Max = 3, NameMax = 16;
        public const string RealBase = "vk_save_v1";
        /// <summary>Key of slot 0; the bot and tests move it to vk_save_autotest.</summary>
        public static string Base = RealBase;
        static ProfileIndex idx;

        public static string SlotKey(int i) => i == 0 ? Base : Base + "_p" + i;
        public static string IndexKey => Base == RealBase ? "vk_profiles" : "vk_profiles_" + Base.Substring(8);
        public static string DefaultName(int slot) => slot == 0 ? "Đạo Hữu" : "Đạo Hữu " + (slot + 1);

        public static readonly string[] RandomNames = { "Thanh Phong", "Vân Hạc", "Nguyệt Ảnh", "Hàn Sương", "Tử Điện", "Huyền Minh",
            "Bạch Liên", "Xích Diệm", "Thiên Kiếm", "Mặc Trúc", "Kim Ô", "Lăng Tiêu" };

        public static int Active => Index.active;
        public static string Name(int i) => Index.names[i];
        public static bool Used(int i) => !string.IsNullOrEmpty(Index.names[i]);
        public static int UsedCount { get { int n = 0; for (int i = 0; i < Max; i++) if (Used(i)) n++; return n; } }

        static ProfileIndex Index
        {
            get
            {
                if (idx == null)
                {
                    try { idx = JsonUtility.FromJson<ProfileIndex>(Platform.Services.LoadString(IndexKey)); } catch { idx = null; }
                    if (idx == null || idx.names == null || idx.names.Length != Max) idx = new ProfileIndex();
                    idx.active = Mathf.Clamp(idx.active, 0, Max - 1);
                    if (!Used(idx.active))
                    {   // lost or damaged index: fall back to the first used slot, else slot 0 (the original save)
                        int f = 0; while (f < Max && !Used(f)) f++;
                        if (f == Max) { f = 0; idx.names[0] = DefaultName(0); }
                        idx.active = f;
                    }
                    SaveIndex();
                }
                return idx;
            }
        }

        static void SaveIndex() => Platform.Services.SaveString(IndexKey, JsonUtility.ToJson(idx));

        /// <summary>Reads the index again and points <see cref="SaveData.Key"/> at the active slot. Call once at start-up.</summary>
        public static void Init() { idx = null; SaveData.Key = SlotKey(Index.active); }

        public static string Clean(string s, int slot)
        {
            s = (s ?? "").Trim();
            if (s.Length > NameMax) s = s.Substring(0, NameMax).Trim();
            return s.Length == 0 ? DefaultName(slot) : s;
        }

        public static void Rename(int slot, string name) { Index.names[slot] = Clean(name, slot); SaveIndex(); }

        /// <summary>Writes a save with no progress into <paramref name="slot"/>, language, volumes, display and shake taken from <paramref name="keep"/>.</summary>
        static void WriteFresh(int slot, SaveData keep)
        {
            var d = new SaveData();
            if (keep != null) { d.lang = keep.lang; d.sfxVol = keep.sfxVol; d.musicVol = keep.musicVol; d.shake = keep.shake; d.fullscreen = keep.fullscreen; d.vsyncOff = keep.vsyncOff; d.winSize = keep.winSize; d.dmgNumbers = keep.dmgNumbers; }
            Platform.Services.SaveString(SlotKey(slot), JsonUtility.ToJson(d));
        }

        /// <summary>Same profile, progress wiped, settings and name kept. Returns the new data (the caller makes it the live save).</summary>
        public static SaveData ResetProgress(SaveData cur) { WriteFresh(Active, cur); return SaveData.Load(); }

        /// <summary>Saves <paramref name="cur"/>, makes <paramref name="slot"/> active and returns its data.</summary>
        public static SaveData Switch(int slot, SaveData cur)
        {
            cur?.Save();
            Index.active = slot; SaveIndex();
            SaveData.Key = SlotKey(slot);
            return SaveData.Load();
        }

        /// <summary>Makes a new profile in an empty slot (settings copied from <paramref name="cur"/>, no progress) and switches to it.</summary>
        public static SaveData Create(int slot, string name, SaveData cur)
        {
            Index.names[slot] = Clean(name, slot);
            WriteFresh(slot, cur);
            return Switch(slot, cur);
        }

        /// <summary>Deletes another profile and its save. False for the active one: the only profile is reset instead (ResetProgress).</summary>
        public static bool Delete(int slot)
        {
            if (!Used(slot)) return false;
            if (slot == Active) return false;
            Platform.Services.DeleteKey(SlotKey(slot));
            Index.names[slot] = ""; SaveIndex();
            return true;
        }
    }
}

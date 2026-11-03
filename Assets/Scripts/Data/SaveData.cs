using System.Collections.Generic;
using UnityEngine;

namespace VK
{
    [System.Serializable]
    public class SaveData
    {
        public int daoHanh;
        public int[] meta = new int[6];
        public int totalKills, bestWave, bestLevel, runs, wins, maxKiepUnlocked;
        public List<string> ach = new List<string>();
        public List<string> codex = new List<string>();
        public List<string> bossIntroSeen = new List<string>();   // boss intro cards already shown (null-safe for old saves)
        public List<string> tips = new List<string>();      // tutorial hints already shown   // "w:id", "i:id", "e:id"
        public bool tipsAll;                                // the player asked to see the hints again after the first run
        public int[] charWinKiep = { -1, -1, -1, -1, -1, -1 };   // highest kiếp won per character
        public float sfxVol = 0.8f, musicVol = 0.5f, shake = 1f;
        public bool fullscreen, vsyncOff;   // display options (defaults: windowed, vsync on)
        public int winSize = 1;             // 0 1280x720, 1 1600x900, 2 1920x1080
        public int dmgNumbers;   // 0 all, 1 crits only, 2 off
        public int lastChar, lastKiep;
        public int lang = -1;   // Loc.VI/EN/ZH; -1 = not chosen yet

        public static string Key = "vk_save_v1";

        public static SaveData Load()
        {
            string s = Platform.Services.LoadString(Key);
            SaveData d = null;
            if (!string.IsNullOrEmpty(s)) { try { d = JsonUtility.FromJson<SaveData>(s); } catch { d = null; } }
            if (d == null) d = new SaveData();
            if (d.meta == null || d.meta.Length != 6) { var m = new int[6]; if (d.meta != null) for (int i = 0; i < Mathf.Min(6, d.meta.Length); i++) m[i] = d.meta[i]; d.meta = m; }
            int nc = Content.Chars.Count;   // grow with new characters, keeping old records
            if (d.charWinKiep == null || d.charWinKiep.Length != nc)
            {
                var w = new int[nc];
                for (int i = 0; i < nc; i++) w[i] = d.charWinKiep != null && i < d.charWinKiep.Length ? d.charWinKiep[i] : -1;
                d.charWinKiep = w;
            }
            if (d.ach == null) d.ach = new List<string>();
            if (d.codex == null) d.codex = new List<string>();
            if (d.tips == null) d.tips = new List<string>();
            if (d.bossIntroSeen == null) d.bossIntroSeen = new List<string>();
            return d;
        }

        public void Save()
        {
            Platform.Services.SaveString(Key, JsonUtility.ToJson(this));
        }

        public bool Has(string a) => ach.Contains(a);
        public bool Seen(string key) => codex.Contains(key);
        public void See(string key) { if (!codex.Contains(key)) codex.Add(key); }
        public static int CodexTotal => Content.Weapons.Count + Content.Items.Count + Content.Enemies.Count + Content.Bosses.Length;
        public int CodexSeen()
        {
            int n = 0;
            foreach (var w in Content.Weapons) if (Seen("w:" + w.id)) n++;
            foreach (var i in Content.Items) if (Seen("i:" + i.id)) n++;
            foreach (var e in Content.Enemies) if (Seen("e:" + e.id)) n++;
            foreach (var b in Content.Bosses) if (Seen("e:" + b.id)) n++;
            return n;
        }

        public float Completion()
        {
            int total = Content.Achs.Length + Content.Chars.Count * (Content.MaxKiep + 1) + Content.Meta.Length * Content.MetaMax + CodexTotal;
            int got = CodexSeen();
            foreach (var a in Content.Achs) if (Has(a.id)) got++;
            foreach (var k in charWinKiep) got += Mathf.Max(0, k + 1);
            foreach (var m in meta) got += m;
            return (float)got / total;
        }
    }
}

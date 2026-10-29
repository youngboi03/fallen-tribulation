using System.Collections.Generic;

namespace VK
{
    public class AnimClip
    {
        public string name;
        public int frames;
        public float fps;
        public bool loop;
        public int strikeFrame;
    }

    public static class AnimDefs
    {
        static readonly Dictionary<string, AnimClip> table = new Dictionary<string, AnimClip>();

        static AnimDefs()
        {
            string[] chars = { "kiem_tu", "the_tu", "dan_tu", "phu_tu", "loi_tu", "ma_tu" };
            foreach (var id in chars)
                table[id + "/walk"] = new AnimClip { name = "walk", frames = 8, fps = 10f, loop = true };
        }

        public static AnimClip Get(string id, string clip)
        {
            table.TryGetValue(id + "/" + clip, out var c);
            return c;
        }
    }
}

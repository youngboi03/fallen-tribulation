using UnityEngine;

namespace VK
{
    /// <summary>
    /// Store/OS services behind one seam so a Steam (Steamworks.NET) or Google Play Games
    /// backend can be dropped in later without touching gameplay code. See docs/PORTING.md.
    /// </summary>
    public interface IPlatformServices
    {
        string Name { get; }
        string LoadString(string key);
        void SaveString(string key, string value);
        void UnlockAchievement(string id);
    }

    /// <summary>Default backend: PlayerPrefs storage, achievements kept only in the save file.</summary>
    public class LocalPlatform : IPlatformServices
    {
        public string Name => "Local";
        public string LoadString(string key) => PlayerPrefs.GetString(key, "");
        public void SaveString(string key, string value) { PlayerPrefs.SetString(key, value); PlayerPrefs.Save(); }
        public void UnlockAchievement(string id) { }
    }

    public static class Platform
    {
        static IPlatformServices services;
        public static IPlatformServices Services
        {
            get => services ??= new LocalPlatform();
            set => services = value;
        }

        /// <summary>Tests flip this to look at the touch wording on PC.</summary>
        public static bool? TouchForce;
        /// <summary>Phone/tablet build, or forced with the -touch command line flag for testing on PC.</summary>
        public static bool Touch => TouchForce ?? (Application.isMobilePlatform || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-touch") >= 0);

        /// <summary>HUD / banner scale on phones: small screens read the in-fight text at arm's length.</summary>
        public static float Boost => Touch ? 1.25f : 1f;

        /// <summary>Tests set this to pretend the screen has a notch.</summary>
        public static Rect? FakeSafe;
        static Vector2? argInset;   // "-safe L,R": pixels cut off the left and right edges (debug)
        static bool argRead;

        /// <summary>The part of the screen that is not under a notch, rounded corner or gesture bar (pixels).</summary>
        public static Rect Safe
        {
            get
            {
                if (FakeSafe.HasValue) return FakeSafe.Value;
                if (!argRead)
                {
                    argRead = true;
                    var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-safe");
                    if (i >= 0 && i + 1 < a.Length)
                    {
                        var p = a[i + 1].Split(',');
                        if (p.Length >= 2 && float.TryParse(p[0], out var l) && float.TryParse(p[1], out var r)) argInset = new Vector2(l, r);
                    }
                }
                return argInset.HasValue ? new Rect(argInset.Value.x, 0, Screen.width - argInset.Value.x - argInset.Value.y, Screen.height) : Screen.safeArea;
            }
        }
    }
}

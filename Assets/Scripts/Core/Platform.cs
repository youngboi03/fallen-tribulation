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

        /// <summary>Phone/tablet build, or forced with the -touch command line flag for testing on PC.</summary>
        public static bool Touch => Application.isMobilePlatform || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-touch") >= 0;
    }
}

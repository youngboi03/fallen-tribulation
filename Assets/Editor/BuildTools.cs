using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VK.EditorTools
{
    public static class BuildTools
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string ExePath = "Builds/Windows/FallenTribulation.exe";

        /// <summary>Batch-mode entry: reimport sprites, make sure the scene exists. Used by CI-style checks.</summary>
        public static void Setup()
        {
            AssetDatabase.ImportAsset("Assets/Resources/Tiles", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            EnsureScene();
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[VK] Setup complete");
        }

        [MenuItem("Vạn Kiếp/Build Windows")]
        public static void BuildWindows()
        {
            Setup();
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ExePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[VK] BUILD RESULT: " + report.summary.result + " errors=" + report.summary.totalErrors + " size=" + report.summary.totalSize);
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>Signed ARM64 APK. Keystore path and password come from VK_KEYSTORE / VK_KEYSTORE_PASS (kept outside the repo).</summary>
        [MenuItem("Vạn Kiếp/Build Android APK")]
        public static void BuildAndroid()
        {
            Setup();
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.youngboi03.fallentribulation");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.bundleVersion = "1.1";
            PlayerSettings.Android.bundleVersionCode = 2;
            PlayerSettings.Android.forceInternetPermission = true;   // anonymous statistics (Telemetry)
            EditorUserBuildSettings.buildAppBundle = false;
            string ks = System.Environment.GetEnvironmentVariable("VK_KEYSTORE"), pw = System.Environment.GetEnvironmentVariable("VK_KEYSTORE_PASS");
            PlayerSettings.Android.useCustomKeystore = !string.IsNullOrEmpty(ks);
            if (PlayerSettings.Android.useCustomKeystore)
            {
                PlayerSettings.Android.keystoreName = ks; PlayerSettings.Android.keystorePass = pw;
                PlayerSettings.Android.keyaliasName = "fallen"; PlayerSettings.Android.keyaliasPass = pw;
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Builds/Android/FallenTribulation.apk",
                target = BuildTarget.Android, options = BuildOptions.None
            });
            if (PlayerSettings.Android.useCustomKeystore)   // never leave the password or the local key path in ProjectSettings
            {
                PlayerSettings.Android.keystorePass = PlayerSettings.Android.keyaliasPass = PlayerSettings.Android.keystoreName = "";
                PlayerSettings.Android.useCustomKeystore = false;
                AssetDatabase.SaveAssets();
            }
            Debug.Log("[VK] BUILD RESULT: " + report.summary.result + " errors=" + report.summary.totalErrors + " size=" + report.summary.totalSize);
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cam = new GameObject("Main Camera");
                cam.tag = "MainCamera";
                var c = cam.AddComponent<Camera>();
                c.orthographic = true;
                c.orthographicSize = 7.2f;
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = new Color(0.05f, 0.04f, 0.08f);
                cam.transform.position = new Vector3(0, 0, -10);
                cam.AddComponent<AudioListener>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "MinhIndie";
            PlayerSettings.productName = "Fallen Tribulation";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
        }
    }
}

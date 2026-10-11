using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Unice
{
    /// <summary>
    /// One-time project setup, run automatically the first time the project opens (and from the Unice menu):
    /// installs the multiplayer packages, creates the scene and the unlit material, and sets up the Android build.
    /// </summary>
    [InitializeOnLoad]
    static class UniceSetup
    {
        const string ScenePath = "Assets/Unice/Scenes/Main.unity", MatPath = "Assets/Unice/Resources/UniceUnlit.mat", PanelPath = "Assets/Unice/Resources/UI/UnicePanel.asset";
        static readonly string[] Packages = { "com.unity.netcode.gameobjects", "com.unity.services.multiplayer" };

        static UniceSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                InstallPackages(false);
                if (!File.Exists(ScenePath) || !File.Exists(MatPath) || !File.Exists(PanelPath)) Run();
            };
        }

        [MenuItem("Unice/Configurar projeto")]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[Unice] Saia do Play mode para configurar o projeto."); return; }
            CreateMaterial();
            CreatePanel();
            ConfigurePlayer();
            CreateScene();
            Debug.Log("[Unice] Projeto configurado. Aperte Play para testar, ou File > Build Profiles > Android para gerar o APK.");
        }

        [MenuItem("Unice/Instalar pacotes de multiplayer")]
        static void InstallMenu() => InstallPackages(true);

        /// <summary>Adds the packages through the Package Manager (never by editing manifest.json by hand).</summary>
        static void InstallPackages(bool force)
        {
            if (!force && SessionState.GetBool("unice.pkg", false)) return;
            SessionState.SetBool("unice.pkg", true);
            string manifest = File.ReadAllText("Packages/manifest.json");
            var missing = Packages.Where(package => !manifest.Contains("\"" + package + "\"")).ToArray();
            if (missing.Length == 0) return;
            Debug.Log("[Unice] Instalando pacotes de multiplayer: " + string.Join(", ", missing));
            var req = Client.AddAndRemove(missing, null);
            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                if (!req.IsCompleted) return;
                EditorApplication.update -= poll;
                if (req.Status == StatusCode.Success) Debug.Log("[Unice] Pacotes de multiplayer instalados.");
                else Debug.LogError("[Unice] Não consegui instalar os pacotes: " + req.Error?.message + ". Use o menu Unice > Instalar pacotes de multiplayer para tentar de novo.");
            };
            EditorApplication.update += poll;
        }

        static void CreateMaterial()
        {
            if (File.Exists(MatPath)) return;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            AssetDatabase.CreateAsset(new Material(shader) { name = "UniceUnlit" }, MatPath);
        }

        /// <summary>A saved panel asset keeps references to the UI shaders, so they are not stripped from the Android build.</summary>
        static void CreatePanel()
        {
            if (File.Exists(PanelPath)) return;
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize; panelSettings.referenceResolution = new Vector2Int(960, 540);   // same as Ui.ReferenceResolution (this assembly can't see Core)
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight; panelSettings.match = 1;
            panelSettings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Unice/Resources/UI/UniceTheme.tss");
            AssetDatabase.CreateAsset(panelSettings, PanelPath);
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.productName = "Unice";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.brunocarvalhs.unice");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.runInBackground = true;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Unice/Resources/Art/app_icon.png");
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }

        static void CreateScene()
        {
            if (File.Exists(ScenePath)) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 2.5f; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(17, 12, 18, 255); cam.transform.position = new Vector3(8.6f, -5.9f, -10);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }

    /// <summary>The exported art keeps its exact size and stays sharp (no mipmaps, no power-of-two scaling).</summary>
    sealed class UniceArtImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Unice/Resources/Art/")) return;
            var textureImporter = (TextureImporter)assetImporter;
            textureImporter.textureType = TextureImporterType.Default;
            textureImporter.npotScale = TextureImporterNPOTScale.None;
            textureImporter.mipmapEnabled = false;
            textureImporter.alphaIsTransparency = true;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.maxTextureSize = 4096;
            textureImporter.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}

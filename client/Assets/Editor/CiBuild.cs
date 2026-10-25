using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mmorpg.EditorTools
{
    /// <summary>
    /// Gera o APK de teste. Usado pelo GitHub Actions (.github/workflows/android-apk.yml) e também pelo menu
    /// "Mmorpg > Gerar APK de teste (Android)" no Editor.
    ///
    /// O jogo se cria sozinho (GameBootstrap, RuntimeInitializeOnLoadMethod), então a cena só precisa existir:
    /// este script cria uma cena vazia se ainda não houver nenhuma. As configurações abaixo são as mínimas para
    /// um APK de teste instalável por arquivo (assinado com a chave de debug da Unity). NÃO serve para publicar na Play Store.
    ///
    /// ATENÇÃO: este arquivo nunca foi executado em um Editor Unity.
    /// </summary>
    public static class CiBuild
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string ApkName = "mmorpg-teste.apk";

        [MenuItem("Mmorpg/Gerar APK de teste (Android)")]
        public static void BuildAndroidFromMenu()
        {
            string dir = EditorUtility.SaveFolderPanel("Pasta de saída do APK", "", "");
            if (string.IsNullOrEmpty(dir)) return;
            string error = Build(dir);
            if (error != null) EditorUtility.DisplayDialog("Falha ao gerar o APK", error, "OK");
            else EditorUtility.RevealInFinder(Path.Combine(dir, ApkName));
        }

        /// <summary>Ponto de entrada do CI: <c>-executeMethod Mmorpg.EditorTools.CiBuild.BuildAndroid</c>.</summary>
        public static void BuildAndroid()
        {
            string dir = ReadArg("-customBuildPath");
            if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath("Builds/Android");

            string error = Build(dir);
            if (error != null)
            {
                Debug.LogError("[CiBuild] FALHA: " + error);
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("[CiBuild] APK gerado com sucesso em " + Path.Combine(dir, ApkName));
        }

        /// <summary>Devolve null em caso de sucesso, ou o motivo da falha.</summary>
        private static string Build(string outputDir)
        {
            try
            {
                EnsureScene();
                ConfigureAndroid();

                Directory.CreateDirectory(outputDir);
                string apk = Path.Combine(outputDir, ApkName);

                BuildPlayerOptions options = new BuildPlayerOptions();
                options.scenes = new string[] { ScenePath };
                options.target = BuildTarget.Android;
                options.locationPathName = apk;
                options.options = BuildOptions.None;

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                if (summary.result != BuildResult.Succeeded)
                    return "resultado " + summary.result + ", " + summary.totalErrors + " erro(s) de build (veja as linhas de erro acima no log).";

                Debug.Log("[CiBuild] Tamanho do APK: " + (summary.totalSize / (1024 * 1024)) + " MB; tempo: " + summary.totalTime);
                return null;
            }
            catch (Exception e)
            {
                return e.GetType().Name + ": " + e.Message;
            }
        }

        private static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new Exception("Não foi possível salvar a cena " + ScenePath);
                AssetDatabase.Refresh();
            }
            EditorBuildSettings.scenes = new EditorBuildSettingsScene[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void ConfigureAndroid()
        {
            PlayerSettings.companyName = "Unnce";
            PlayerSettings.productName = "MMORPG Teste";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.unnce.mmorpg");

            // IL2CPP + ARM64: celulares atuais já não executam código só de 32 bits.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.useCustomKeystore = false; // chave de debug: serve para instalar por arquivo

            // Pipeline embutido (sem URP): OpenGL ES 3 é o caminho mais compatível.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new GraphicsDeviceType[] { GraphicsDeviceType.OpenGLES3 });

            // A interface de toque foi desenhada para tela deitada.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.buildAppBundle = false; // APK, não AAB
            EditorUserBuildSettings.development = false;

            EnsureLegacyInput();
        }

        /// <summary>
        /// O jogo lê a entrada pelo Input Manager clássico (UnityEngine.Input). Se o projeto estiver só com o Input System
        /// novo, passa para "Both". Só registra no log o que encontrou e o que mudou.
        /// </summary>
        private static void EnsureLegacyInput()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[CiBuild] ProjectSettings.asset não encontrado; não foi possível conferir a entrada.");
                return;
            }
            SerializedObject so = new SerializedObject(assets[0]);
            SerializedProperty prop = so.FindProperty("activeInputHandler");
            if (prop == null)
            {
                Debug.LogWarning("[CiBuild] Propriedade activeInputHandler não encontrada.");
                return;
            }
            Debug.Log("[CiBuild] activeInputHandler = " + prop.intValue + " (0 = Input Manager, 1 = Input System, 2 = Both)");
            if (prop.intValue == 1)
            {
                prop.intValue = 2;
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log("[CiBuild] activeInputHandler alterado para 2 (Both).");
            }
        }

        private static string ReadArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}

using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine.Rendering;

public static class BuildQuest3
{
    private const string SCENE_PATH = "Assets/Scenes/DemoWeb/CenaDemoWeb.unity";
    private const string BUILD_OUTPUT_DIR = "Builds/Quest3";
    private const string APK_NAME = "PacManSemantico_Quest3.apk";

    [MenuItem("Pac-Man Semântico/Gerar Build Meta Quest 3 (APK)")]
    public static void BuildMetaQuest3APK()
    {
        Debug.Log("================================================================================");
        Debug.Log("[BUILD QUEST 3] Iniciando processo de compilação do APK Standalone para Meta Quest 3/3S...");
        Debug.Log("================================================================================");

        DateTime startTime = DateTime.Now;

        // 1. Garantir configurações do Player
        PlayerSettings.productName = "Pac-Man Semantico VR";
        PlayerSettings.companyName = "Cin-UFPE";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.cinufpe.pacmansemanticovr");
        
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.colorSpace = ColorSpace.Linear;

        // Forçar geração de APK (e não AAB / App Bundle da Google Play Store)
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.Generic;

        // Configurar Ícone Oficial em Alta Definição para o Meta Quest
        try
        {
            string iconPath = "Assets/Textures/AppIcon_PacManVR.png";
            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (icon != null)
            {
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, new Texture2D[] { icon });
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new Texture2D[] { icon });
                Debug.Log("[BUILD QUEST 3] Ícone oficial em alta definição configurado para Android / Meta Quest!");
            }
        }
        catch (Exception exIcon)
        {
            Debug.LogWarning($"[BUILD QUEST 3] Aviso ao configurar ícone: {exIcon.Message}");
        }

        // 2. Garantir configurações de XR Management no EditorBuildSettings
        try
        {
            UnityEngine.Object currentXRSettings;
            if (!EditorBuildSettings.TryGetConfigObject(UnityEngine.XR.Management.XRGeneralSettings.settingsKey, out currentXRSettings) || currentXRSettings == null)
            {
                var xrSettingsAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                if (xrSettingsAsset != null)
                {
                    EditorBuildSettings.AddConfigObject(UnityEngine.XR.Management.XRGeneralSettings.settingsKey, xrSettingsAsset, true);
                    Debug.Log("[BUILD QUEST 3] XRGeneralSettingsPerBuildTarget registrado no EditorBuildSettings com sucesso!");
                }
            }
        }
        catch (Exception exXR)
        {
            Debug.LogWarning($"[BUILD QUEST 3] Aviso ao verificar XR Settings: {exXR.Message}");
        }

        // 3. Garantir diretório de saída
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string outputFolder = Path.Combine(projectRoot, BUILD_OUTPUT_DIR);
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        string fullApkPath = Path.Combine(outputFolder, APK_NAME);

        // 3. Configurar Opções de Build
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = new string[] { SCENE_PATH },
            locationPathName = fullApkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        };

        Debug.Log($"[BUILD QUEST 3] Cena alvo: {SCENE_PATH}");
        Debug.Log($"[BUILD QUEST 3] Destino do APK: {fullApkPath}");
        Debug.Log("[BUILD QUEST 3] Compilando IL2CPP ARM64 + OpenXR... Aguarde...");

        // 4. Executar compilação
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        TimeSpan duration = DateTime.Now - startTime;

        if (summary.result == BuildResult.Succeeded)
        {
            long sizeBytes = (long)summary.totalSize;
            float sizeMB = sizeBytes / (1024f * 1024f);

            Debug.Log("================================================================================");
            Debug.Log($"[BUILD QUEST 3 SUCESSO!] APK gerado em: {duration.TotalMinutes:F1} minutos.");
            Debug.Log($"[BUILD QUEST 3 SUCESSO!] Tamanho do arquivo: {sizeMB:F2} MB");
            Debug.Log($"[BUILD QUEST 3 SUCESSO!] Caminho: {fullApkPath}");
            Debug.Log("================================================================================");

            // Copiar também para a Área de Trabalho (Desktop) para facilitar a vida do usuário
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string desktopApk = Path.Combine(desktopPath, APK_NAME);
                File.Copy(fullApkPath, desktopApk, true);
                Debug.Log($"[BUILD QUEST 3] Cópia de conveniência disponibilizada na Área de Trabalho: {desktopApk}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BUILD QUEST 3] Aviso ao copiar para Desktop: {ex.Message}");
            }
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError($"[BUILD QUEST 3 ERRO] A compilação falhou com {summary.totalErrors} erros em {duration.TotalSeconds:F1}s.");
        }
        else
        {
            Debug.LogWarning($"[BUILD QUEST 3 AVISO] Resultado da compilação: {summary.result}");
        }
    }
}


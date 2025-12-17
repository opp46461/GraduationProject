using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// 资源加载模式选择器
/// 1. 编辑器模式不加载AB，单纯检查资源加载问题，但需要映射表
/// </summary>
public class AssetModeSelector
{
    private const string EditorMode = "Editor";
    private const string SimulationMode = "Simulation";
    private const string ReleaseMode = "Release";
    private static string currentMode = EditorMode;

    [MenuItem("AssetBundle/Asset Mode/Editor Mode", false, 0)]
    private static void SelectEditorMode()
    {
        currentMode = EditorMode;
        UpdateScriptingDefineSymbols();
        Menu.SetChecked("AssetBundle/Asset Mode/Editor Mode", true);
        Menu.SetChecked("AssetBundle/Asset Mode/Simulation Mode", false);
        Menu.SetChecked("AssetBundle/Asset Mode/Release Mode", false);
    }

    [MenuItem("AssetBundle/Asset Mode/Simulation Mode", false, 0)]
    private static void SelectSimulationMode()
    {
        currentMode = SimulationMode;
        UpdateScriptingDefineSymbols();
        Menu.SetChecked("AssetBundle/Asset Mode/Editor Mode", false);
        Menu.SetChecked("AssetBundle/Asset Mode/Simulation Mode", true);
        Menu.SetChecked("AssetBundle/Asset Mode/Release Mode", false);
    }

    [MenuItem("AssetBundle/Asset Mode/Release Mode", false, 0)]
    private static void SelectReleaseMode()
    {
        currentMode = ReleaseMode;
        UpdateScriptingDefineSymbols();
        Menu.SetChecked("AssetBundle/Asset Mode/Editor Mode", false);
        Menu.SetChecked("AssetBundle/Asset Mode/Simulation Mode", false);
        Menu.SetChecked("AssetBundle/Asset Mode/Release Mode", true);
    }

    public static string GetCurrentMode()
    {
        return currentMode;
    }

    private static void UpdateScriptingDefineSymbols()
    {
        BuildTarget activeTarget = EditorUserBuildSettings.activeBuildTarget;
        BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(activeTarget);
        string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
        if (currentMode == EditorMode)
        {
            symbols = symbols.Replace("SIMULATION_MODE", "").Trim();
            symbols = symbols.Replace("RELEASE_MODE", "").Trim();
            if (!symbols.Contains("EDITOR_MODE"))
            {
                symbols += symbols.Length > 0 ? ";EDITOR_MODE" : "EDITOR_MODE";
            }
        }
        else if (currentMode == SimulationMode)
        {
            symbols = symbols.Replace("EDITOR_MODE", "").Trim();
            symbols = symbols.Replace("RELEASE_MODE", "").Trim();
            if (!symbols.Contains("SIMULATION_MODE"))
            {
                symbols += symbols.Length > 0 ? ";SIMULATION_MODE" : "SIMULATION_MODE";
            }
        }
        else if (currentMode == ReleaseMode)
        {
            symbols = symbols.Replace("EDITOR_MODE", "").Trim();
            symbols = symbols.Replace("SIMULATION_MODE", "").Trim();
            if (!symbols.Contains("SIMULATION_MODE"))
            {
                symbols += symbols.Length > 0 ? ";RELEASE_MODE" : "RELEASE_MODE";
            }
        }
        PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, symbols);
    }
}
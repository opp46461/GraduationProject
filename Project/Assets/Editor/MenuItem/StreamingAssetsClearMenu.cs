using UnityEngine;
using UnityEditor;
using System.IO;

// StreamingAssets 清理工具
public class StreamingAssetsClearMenu
{
    private const string MenuRoot = "Tools/StreamingAssets Clear/";

    [MenuItem(MenuRoot + "ALL", false, 1)]
    public static void ClearAllStreamingAssets()
    {
        if (EditorUtility.DisplayDialog("清空确认",
            "确定要清空整个StreamingAssets目录吗？此操作不可恢复！", "确定", "取消"))
        {
            string streamingAssetsPath = Application.streamingAssetsPath;
            ClearDirectory(streamingAssetsPath);
            Debug.Log("StreamingAssets目录已清空完成");
        }
    }

    [MenuItem(MenuRoot + "AssetBundles", false, 2)]
    public static void ClearAssetBundles()
    {
        if (EditorUtility.DisplayDialog("清空确认",
            "确定要清空StreamingAssets/AssetBundles目录吗？此操作不可恢复！", "确定", "取消"))
        {
            string assetBundlesPath = Path.Combine(Application.streamingAssetsPath, "AssetBundles");
            ClearDirectory(assetBundlesPath);
            Debug.Log("AssetBundles目录已清空完成");
        }
    }

    /// <summary>
    /// 清空指定目录，如果目录不存在则创建
    /// </summary>
    /// <param name="directoryPath">要清空的目录路径</param>
    private static void ClearDirectory(string directoryPath)
    {
        try
        {
            // 如果目录不存在则创建
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                Debug.Log($"目录不存在，已创建: {directoryPath}");
                return;
            }

            // 获取目录中的所有文件
            string[] files = Directory.GetFiles(directoryPath);
            foreach (string file in files)
            {
                File.Delete(file);
                Debug.Log($"已删除文件: {file}");
            }

            // 获取目录中的所有子目录
            string[] directories = Directory.GetDirectories(directoryPath);
            foreach (string directory in directories)
            {
                Directory.Delete(directory, true);
                Debug.Log($"已删除目录: {directory}");
            }

            // 刷新AssetDatabase以确保Unity编辑器更新
            AssetDatabase.Refresh();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"清空目录时发生错误: {e.Message}");
            EditorUtility.DisplayDialog("错误", $"清空目录时发生错误: {e.Message}", "确定");
        }
    }

    /// <summary>
    /// 验证菜单项是否可用
    /// </summary>
    [MenuItem(MenuRoot + "ALL", true)]
    [MenuItem(MenuRoot + "AssetBundles", true)]
    public static bool ValidateMenu()
    {
        // 这里可以添加验证逻辑，比如只在编辑模式下可用
        return !Application.isPlaying;
    }
}
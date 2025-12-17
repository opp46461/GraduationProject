using System.IO;
using UnityEngine;

public class PathHelper
{
    /// <summary>
    /// 确保路径存在，如果不存在则创建
    /// </summary>
    /// <param name="fullPath">完整文件路径</param>
    /// <returns>成功与否</returns>
    public static bool EnsureDirectoryExists(string fullPath)
    {
        try
        {
            // 获取目录路径（去掉文件名）
            string directory = Path.GetDirectoryName(fullPath);

            // 如果目录不存在，则创建
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Debug.Log($"创建目录: {directory}");
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"创建目录失败: {e.Message}");
            return false;
        }
    }
}
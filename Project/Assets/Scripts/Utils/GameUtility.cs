using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

//此行用于编码格式变更
public class GameUtility
{
    public const string AssetsFolderName = "Assets";

    /// <summary>
    /// 安全删除文件
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public static bool SafeDeleteFile(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return true;
            }

            if (!File.Exists(filePath))
            {
                return true;
            }
            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Delete(filePath);
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError(string.Format("SafeDeleteFile failed! path = {0} with err: {1}", filePath, ex.Message));
            return false;
        }
    }

    /// <summary>
    /// 检查文件夹，如果不存在则生成
    /// </summary>
    /// <param name="folderPath"></param>
    public static void CheckDirAndCreateWhenNeeded(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            return;
        }

        if (!Directory.Exists(folderPath))
        {
            try
            {
                Directory.CreateDirectory(folderPath);
            }
            catch (System.Exception)
            {
            }
        }
    }


    /// <summary>
    /// 查找同名的数据
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="allConfigs"></param>
    /// <returns></returns>
    public static Dictionary<string, List<T>> FindDuplicateNames<T>(List<T> allConfigs) where T : SharedCode.IAssetName
    {
        // 创建字典存储结果：Key=资源名，Value=同名配置列表
        Dictionary<string, List<T>> nameGroups = new Dictionary<string, List<T>>();

        // 遍历所有配置数据
        foreach (T config in allConfigs)
        {
            // 跳过空包名
            if (string.IsNullOrEmpty(config.AssetNameValue)) continue;

            // 统一使用小写进行比较（可选）
            string normalizedName = config.AssetNameValue.ToLowerInvariant();

            // 添加到字典分组
            if (!nameGroups.ContainsKey(normalizedName))
            {
                nameGroups[normalizedName] = new List<T>();
            }
            nameGroups[normalizedName].Add(config);
        }

        // 筛选出重复的分组（数量 > 1）
        return nameGroups
            .Where(group => group.Value.Count > 1)
            .ToDictionary(group => group.Key, group => group.Value);
    }

    public static string CalculateMD5(string filePath)
    {
        // 创建MD5实例
        using (var md5 = MD5.Create())
        {
            // 打开文件流
            using (var stream = File.OpenRead(filePath))
            {
                // 计算文件流的MD5哈希值
                byte[] hash = md5.ComputeHash(stream);

                // 将字节数组转换为十六进制字符串
                return System.BitConverter.ToString(hash)
                    .Replace("-", "")  // 移除连字符
                    .ToLowerInvariant();  // 转换为小写
            }
        }
    }
}

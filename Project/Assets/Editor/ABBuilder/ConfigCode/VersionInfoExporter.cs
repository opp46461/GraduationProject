using AssetBundles;
using Codice.CM.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

// 版本信息导出器
public class VersionInfoExporter
{
    /// <summary>
    /// 暂时只有一个热更DLL被打包成AB，除非大型项目，否则一般不细分AB
    /// </summary>
    /// <param name="outputPath"></param>
    /// <param name="versions"></param>
    /// <param name="channel"></param>
    /// <param name="platform"></param>
    /// <param name="abPaths"></param>
    public static void ExportCodeABVersionInfo(string outputPath, string versions, string channel, string platform, List<string> abPaths)
    {
        var abHashDict = new Dictionary<string, string>();
        var abSizeDict = new Dictionary<string, int>();

        // 热更代码
        string hotUpdate_abPath = string.Format("{0}/{1}/{2}", outputPath, channel, "HotUpdateDLL".ToLower());
        string hotUpdate_abName = Path.GetFileName(hotUpdate_abPath);
        string hotUpdate_hash = GameUtility.CalculateMD5(hotUpdate_abPath);
        abHashDict[hotUpdate_abName] = hotUpdate_hash;
        FileInfo hotUpdate_fileInfo = new FileInfo(hotUpdate_abPath);
        abSizeDict[hotUpdate_abName] = (int)hotUpdate_fileInfo.Length;
        // AB
        foreach (string abPath in abPaths)
        {
            string abName = Path.GetFileName(abPath);
            string hash = GameUtility.CalculateMD5(abPath);
            abHashDict[abName] = hash;
            FileInfo fileInfo = new FileInfo(abPath);
            abSizeDict[abName] = (int)fileInfo.Length;
        }

        string content = $"Versions : {versions}\n" +
                         $"Platform : {platform}\n" +
                         $"Channel : {channel}\n";

        foreach (var kvp in abHashDict)
        {
            content += $"AssetBundles {kvp.Key} {kvp.Value} {abSizeDict[kvp.Key]}\n";
        }

        string txtPath = string.Format("{0}/codeManifest.txt", outputPath);
        File.WriteAllText(txtPath, content);
        AssetDatabase.Refresh();
        Debug.Log($"代码AB版本信息已导出到 {txtPath}");
    }

    public static void ExportABVersionInfo(string outputPath, string versions, string channel, string platform, List<string> abPaths)
    {
        var abHashDict = new Dictionary<string, string>();
        var abSizeDict = new Dictionary<string, int>();

        // 总包
        string total_abPath = string.Format("{0}/{1}/{2}", outputPath, channel, channel);
        string total_abName = Path.GetFileName(total_abPath);
        string total_hash = GameUtility.CalculateMD5(total_abPath);
        abHashDict[total_abName] = total_hash;
        FileInfo total_fileInfo = new FileInfo(total_abPath);
        abSizeDict[total_abName] = (int)total_fileInfo.Length;
        // 映射AB
        string map_abPath = string.Format("{0}/{1}/{2}", outputPath, channel, ABPathManager.AssetMap.ToLower());
        string map_abName = Path.GetFileName(map_abPath);
        string map_hash = GameUtility.CalculateMD5(map_abPath);
        abHashDict[map_abName] = map_hash;
        FileInfo map_fileInfo = new FileInfo(map_abPath);
        abSizeDict[map_abName] = (int)map_fileInfo.Length;
        // AB
        foreach (string abPath in abPaths)
        {
            string abName = Path.GetFileName(abPath);
            string hash = GameUtility.CalculateMD5(abPath);
            abHashDict[abName] = hash;
            FileInfo fileInfo = new FileInfo(abPath);
            abSizeDict[abName] = (int)fileInfo.Length;
        }

        string content = $"Versions : {versions}\n" +
                         $"Platform : {platform}\n" +
                         $"Channel : {channel}\n";

        foreach (var kvp in abHashDict)
        {
            content += $"AssetBundles {kvp.Key} {kvp.Value} {abSizeDict[kvp.Key]}\n";
        }

        string txtPath = string.Format("{0}/abManifest.txt", outputPath);
        File.WriteAllText(txtPath, content);
        AssetDatabase.Refresh();
        Debug.Log($"AB版本信息已导出到 {txtPath}");
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class ManifestData
{
    public string Versions = string.Empty;
    public string Platform = string.Empty;
    public string Channel = string.Empty;
    public Dictionary<string, ABInfo> abInfoDic = new Dictionary<string, ABInfo>();

    public static ManifestData Deserialization(string filePath)
    {
        ManifestData manifestData = new ManifestData();
        int lineNumber = 0;
        // 使用UTF-8编码打开文件
        using (StreamReader reader = new StreamReader(filePath, Encoding.UTF8))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;

                try
                {
                    // 跳过注释行
                    if (line.TrimStart().StartsWith("//")) continue;

                    // 逐行处理
                    if (line.StartsWith("Versions"))
                    {
                        string[] versions = line.Split(':');
                        manifestData.Versions = versions[1].Trim();
                    }
                    else if (line.StartsWith("Platform"))
                    {
                        string[] platforms = line.Split(':');
                        manifestData.Platform = platforms[1].Trim();
                    }
                    else if (line.StartsWith("Channel"))
                    {
                        string[] channels = line.Split(':');
                        manifestData.Channel = channels[1].Trim();
                    }
                    else if (line.StartsWith("AssetBundles"))
                    {
                        string[] infos = line.Split(' ');
                        ABInfo info = new ABInfo();
                        info.Name = infos[1].Trim();
                        info.Hash = infos[2].Trim();
                        info.Size = int.Parse(infos[3].Trim());
                        bool addSucceed = manifestData.abInfoDic.TryAdd(info.Name, info);
                        if (!addSucceed)
                        {
                            Debug.LogError("检查清单AB是否重名：" + info.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"处理第 {lineNumber} 行时出错: {ex.Message}");
                    Console.WriteLine($"问题行内容: {line}");
                }
            }
        }
        return manifestData;
    }

    public bool IsEmpty()
    {
        if (string.IsNullOrEmpty(Versions)) return true;
        if (string.IsNullOrEmpty(Platform)) return true;
        if (string.IsNullOrEmpty(Channel)) return true;
        if (abInfoDic.Count == 0) return true;

        return false;
    }
}

public class ABInfo
{
    public string Name;
    public string Hash;
    public int Size;
}
using SharedCode;
using System.Collections.Generic;
using System.Xml;
using Newtonsoft.Json;
using UnityEngine;


/// <summary>
/// 用来储存资源映射的数据
/// </summary>
public class ResourceMap : ScriptableObject
{
    // 序列化到属性面板，方便观看。
    // AB包名/资源名 → 具体信息
    public List<ResourceInfo> showMappings = new List<ResourceInfo>();

    public List<IResourceInfo> mappings = new List<IResourceInfo>();

    public List<IResourceInfo> Mappings { get => mappings; set => mappings = value; }

    public void AddSingleMap(string LogicalName, IResourceInfo info)
    {
        bool isDone = Mappings.TryAddElement(info);
        if (isDone)
        {
            showMappings.Add(new ResourceInfo(info));
        }
    }

    public void Clear()
    {
        Mappings.Clear();
    }

    public string ToJson()
    {
        string json = string.Empty;
        return JsonConvert.SerializeObject(mappings, Newtonsoft.Json.Formatting.Indented);

        //// 创建可序列化的数据容器
        //List<SerializableResourceInfo> serializedList = new List<SerializableResourceInfo>();
        //// 转换每个资源信息
        //foreach (var mapping in Mappings)
        //{
        //    if (mapping == null) continue;
        //    serializedList.Add(new SerializableResourceInfo(mapping));
        //}
        //// 使用包装类进行序列化
        //ResourceInfoWrapper wrapper = new ResourceInfoWrapper { items = serializedList };
        //return JsonUtility.ToJson(wrapper, true);
    }
}

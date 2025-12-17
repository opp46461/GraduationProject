using SharedCode;
using UnityEngine;

/// <summary>
/// 资源信息
/// </summary>
[System.Serializable]
//public class ResourceInfo : AssetName, SharedCode.IResourceInfo
public class ResourceInfo : SharedCode.IResourceInfo
{
    // 用途：方便编辑器下查看映射表
    [SerializeField]
    private string showName;

    /// <summary>
    /// 资源的Assets路径（如：Assets/ABResources/Materials/Custom_Respawn.mat）
    /// </summary>
    //public string AssetPath;
    public string assetPath;
    // 所属包名
    //public string BundleName;
    public string bundleName;
    // 资源直接依赖
    //public string[] DirectDependencies;
    public string[] directDependencies;
    // 资源所有直接和间接依赖
    //public string[] AllDependencies;
    public string[] allDependencies;

    [SerializeField]
    private string assetName;

    /// <summary>
    /// 资源唯一标识（用于映射使用加载）
    /// </summary>
    public string AssetNameValue { get => assetName; set => assetName = value.ToLower(); }
    public string AssetPath { get => assetPath; set => assetPath = value.ToLower(); }
    public string BundleName { get => bundleName; set => bundleName = value.ToLower(); }
    public string[] DirectDependencies { get => directDependencies; set => directDependencies = value; }
    public string[] AllDependencies { get => allDependencies; set => allDependencies = value; }

    public ResourceInfo() { }
    public ResourceInfo(IResourceInfo info)
    {
        if (info == null) return;
        AssetNameValue = info.AssetNameValue;
        AssetPath = info.AssetPath;
        BundleName = info.BundleName;
        DirectDependencies = info.DirectDependencies;
        AllDependencies = info.AllDependencies;

        showName = string.Format("AB：{0} → 资源标签：{1}", info.BundleName, info.AssetNameValue);
    }
}

//public interface AssetName
//{
//    public string AssetNameValue { get; set; }
//}

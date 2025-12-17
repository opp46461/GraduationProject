using SharedCode;
using UnityEngine;

public class ResourceRuntimeInfo : IResourceInfo
{
    /// <summary>
    /// 资源的Assets路径（如：Assets/ABResources/Materials/Custom_Respawn.mat）
    /// </summary>
    //public string AssetPath;
    private string assetPath;
    // 所属包名
    //public string BundleName;
    private string bundleName;
    // 资源直接依赖
    //public string[] DirectDependencies;
    private string[] directDependencies;
    // 资源所有直接和间接依赖
    //public string[] AllDependencies;
    private string[] allDependencies;

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
}
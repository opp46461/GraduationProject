using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace AssetBundles
{
    // 过滤器类型
    public enum AssetBundleDispatcherFilterType
    {
        All,  // 过滤当前以及所有子资源
        Optional,  // 可选的过滤方式
    }

    // 分包方式
    public enum AssetBundleDispatcherType
    {
        All,  // 把当前目录以及所有子孙的资源打包成一个包（默认方式）
        OnlyCurrentDirectory,  // 只打包当前目录的资源
    }

    // AssetBundle检查器的过滤器配置
    public class AssetBundleFilter
    {
        // 相对路径（相对于主路径）
        public string RelativePath;
        // 对象过滤规则（Unity搜索语法）
        public string ObjectFilter;

        public AssetBundleFilter(string relativePath, string objectFilter)
        {
            RelativePath = relativePath;
            ObjectFilter = objectFilter;
        }
    }

    // AssetBundle分发器配置类（ScriptableObject）
    public class AssetBundleDispatcherConfig : ScriptableObject, SharedCode.IAssetName
    {
        [SerializeField]
        // AB包名（构建过程中实例化）
        private string packageName = string.Empty;
        /// <summary>
        /// AB包名
        /// </summary>
        public string AssetNameValue { get => packageName; set => packageName = value.ToLower(); }

        // 当前设置数据绝对路径（临时的）
        public string ThisPath = string.Empty;
        // 包路径（相对路径）
        public string PackagePath = string.Empty;
        // 分发器类型
        public AssetBundleDispatcherType Type = AssetBundleDispatcherType.All;
        // 过滤器类型
        public AssetBundleDispatcherFilterType FilterType = AssetBundleDispatcherFilterType.All;
        // 过滤当前目录
        public bool filterCurrent = false;
        // 过滤一级子目录
        public bool filterFirstLevel = false;
        // 过滤二级子目录
        public bool filterSecondLevel = false;
        // 检查器过滤器
        public List<AssetBundleFilter> CheckerFilters = new List<AssetBundleFilter>();


        // 序列化字段（因为自定义类无法直接序列化）
        // 序列化用的，AssetBundleCheckerFilter的字段拆成两个数组
        [SerializeField]
        string[] RelativePaths = null;  // 相对路径数组
        [SerializeField]
        string[] ObjectFilters = null;  // 对象过滤数组


        public void SetThisPathAndCheckPackagePath(string ThisPath)
        {
            if (string.IsNullOrEmpty(ThisPath)) return;
            this.ThisPath = ThisPath;
            //PackagePath = 
        }

        // 从序列化字段加载配置
        public void Load()
        {
            CheckerFilters.Clear();
            // 如果序列化字段存在数据，加载到CheckerFilters
            if (RelativePaths != null && RelativePaths.Length > 0)
            {
                for (int i = 0; i < RelativePaths.Length; i++)
                {
                    CheckerFilters.Add(new AssetBundleFilter(RelativePaths[i], ObjectFilters[i]));
                }
            }
        }

        // 将配置应用到序列化字段
        public void Apply()
        {
            // 无过滤器时清空序列化字段
            if (CheckerFilters.Count <= 0)
            {
                RelativePaths = null;
                ObjectFilters = null;
                return;
            }

            // 初始化序列化数组
            RelativePaths = new string[CheckerFilters.Count];
            ObjectFilters = new string[CheckerFilters.Count];

            // 填充序列化数组
            for (int i = 0; i < CheckerFilters.Count; i++)
            {
                RelativePaths[i] = CheckerFilters[i].RelativePath;
                ObjectFilters[i] = CheckerFilters[i].ObjectFilter;
            }
        }
    }
}
using UnityEngine;
using System.Collections.Generic;

namespace AssetBundles
{
    /// <summary>
    /// 插件打包类别
    /// </summary>
    public enum PluginBundleCategory
    {
        Common,     // 公共包
        Features,   // 功能包
        Standalone  // 独立包
    }

    /// <summary>
    /// 插件配置项
    /// </summary>
    [System.Serializable]
    public class PluginConfig
    {
        public string pluginName;                   // 插件名称
        public bool includeInBuild = true;          // 是否包含在构建中
        public PluginBundleCategory category;       // 打包类别
        public string customBundleName;             // 自定义包名（可选）
    }

    /// <summary>
    /// AB插件配置（ScriptableObject）
    /// </summary>
    [CreateAssetMenu(fileName = "ABPluginConfigs", menuName = "ABBuilder/Plugin Config")]
    public class ABPluginConfig : ScriptableObject
    {
        public List<PluginConfig> plugins = new List<PluginConfig>();

        /// <summary>
        /// 获取插件的打包配置
        /// </summary>
        public PluginConfig GetConfigForPlugin(string pluginName)
        {
            foreach (var plugin in plugins)
            {
                if (plugin.pluginName == pluginName)
                {
                    return plugin;
                }
            }

            // 如果没找到，创建默认配置
            var newConfig = new PluginConfig
            {
                pluginName = pluginName,
                includeInBuild = true,
                category = PluginBundleCategory.Standalone
            };

            plugins.Add(newConfig);
            return newConfig;
        }
    }
}
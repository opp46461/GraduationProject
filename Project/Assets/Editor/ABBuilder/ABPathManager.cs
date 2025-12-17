using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


/*  规则：
 *      1. 主要AB包的资源路径：Assets/ABResources
 *      2. AB包数据路径：Assets/Editor/ABBuilder/ABBuilderSetting
 *      3. 输出路径：Assets/../AssetBundle/平台/渠道，如Assets/../AssetBundle/StandaloneWindows/Test
 *      4. AB包名 = AB包相对路径 = Assets路径下的相对路径，如主要包路径的Assets/ABResources/Case/Case_1，该包名就 = ABResources/Case/Case_1
 *      5. 【后续补充加载和使用资源规则】
 */

namespace AssetBundles
{
    public class ABPathManager
    {
        // 输出路径（后续输出路径改为自动路径，而非手动）
        public static string OutputPath = "D:/SVN/Project/LightgunGame108/AssetBundles";
        //public static string OutputPath = "Assets/StreamingAssets/AssetBundles";
        public const string ABBuilderSettingRoot = "Assets/Editor/ABBuilder/ABBuilderSetting";
        // 本地资源服务器路径（用于模拟模式测试）
        //public const string localSvrAppPath = "Editor/AssetBundle/LocalServer/AssetBundleServer.exe";
        // AssetBundles 输出目录名
        public const string AssetBundlesFolderName = "AssetBundles";
        // AssetBundle 文件后缀
        public const string AssetBundleSuffix = ".assetbundle";
        // 资源根目录名（存放所有打包资源）
        public const string AssetsFolderName = "ABResources";
        // 没有被主动分配到AB的资源，且又被依赖到，则单独打成包
        public const string NotAssignedABName = "NotAssigned";
        // AB资源映射表路径（存放AB资源映射表SO）
        public const string AssetMapPath = "Assets/ABAssetMap/ABAssetMap.asset";
        public const string AssetMap = "ABAssetMap";
        // 渠道配置文件目录名
        //public const string ChannelFolderName = "Channel";
        // 资源服务器URL配置文件名
        //public const string AssetBundleServerUrlFileName = "AssetBundleServerUrl.txt";
        // 变体映射文件名
        //public const string VariantsMapFileName = "VariantsMap.bytes";
        // 变体标识符
        //public const string VariantMapParttren = "Variant";
        // 通用映射分隔符
        //public const string CommonMapPattren = ",";

        public static string GetFullPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError("路径为空");
                return assetPath;
            }
            if (!assetPath.StartsWith("Assets/") && !assetPath.StartsWith(@"Assets\"))
            {
                Debug.LogError("路径有误：" + assetPath);
                return assetPath;
            }
            assetPath = assetPath.Substring("Assets/".Length);
            return @Path.Combine(Application.dataPath, assetPath);
        }

        public static string GetAssetMapOutPutPath()
        {
            return AddAssetsFolderPath(AssetMap);
        }

        public static string AddAssetsFolderPath(string path)
        {
            return @Path.Combine("Assets/", path);
        }

        /// <summary>
        /// 获取ABResources根目录
        /// </summary>
        public static string GetABResourcesRoot()
        {
            return "Assets/ABResources/";
        }

        /// <summary>
        /// 获取AB资源路径的包设置路径
        /// </summary>
        /// <returns>返回ABResources的包设置</returns>
        public static string GetABResourcesPackSettingPath()
        {
            return "Assets/Editor/ABBuilder/ABBuilderSetting/ABResources";
        }

        /// <summary>
        /// 获取文件夹相对路径（相对于ABResources）
        /// </summary>
        public static string GetRelativePath(string fullPath)
        {
            string root = GetABResourcesRoot();

            if (fullPath == root)
                return ""; // 根目录相对路径为空

            if (fullPath.StartsWith(root + "/"))
            {
                return fullPath.Substring(root.Length + 1);
            }
            return fullPath;
        }

        /// <summary>
        /// 将资源路径转换为配置文件路径
        /// 转换规则：
        /// 1. 移出"Assets/"前缀
        /// 2. 附加到DatabaseRoot目录下
        /// 3. 添加".asset"扩展名
        /// </summary>
        /// <param name="assetPath">原始资源路径</param>
        /// <returns>配置文件路径（无效路径返回null）</returns>
        static public string AssetPathToDatabasePath(string assetPath)
        {
            // 先验证是否为AB路径下的资源
            if (!IsPackagePath(assetPath))
            {
                return null;
            }

            // 移除"Assets/"前缀
            assetPath = assetPath.Replace("Assets/", "");
            // 组合成完整配置文件路径：DatabaseRoot + 相对路径 + .asset扩展名
            return Path.Combine(ABBuilderSettingRoot, assetPath + ".asset");
        }

        static public string AddAssetSuffix(string assetPath)
        {
            return string.Format("{0}/{1}.asset", assetPath, AssetMap);
        }

        static public string AddJsonSuffix(string assetPath)
        {
            return string.Format("{0}/{1}.json", assetPath, AssetMap);
        }

        static public string AddBytesSuffix(string assetPath)
        {
            return string.Format("{0}/{1}.bytes", assetPath, AssetMap);
        }

        /// <summary>
        /// 检查路径是否可能是AssetBundle打包路径下的资源
        /// 规则：路径必须位于"Assets/[AssetsFolderName]"目录下
        /// </summary>
        /// <param name="assetPath">资源路径（含"Assets/"前缀）</param>
        /// <returns>是否在打包路径下</returns>
        public static bool IsPackagePath(string assetPath)
        {
            string path = "Assets/" + AssetsFolderName + "/";
            return assetPath.StartsWith(path);
        }


        public static string AssetsPathToPackagePath(string assetPath)
        {
            assetPath.Replace('\\', '/');
            string assetsRoot = "Assets/";
            string path = assetsRoot + AssetsFolderName + "/";
            if (assetPath.StartsWith(path))
            {
                //return assetPath.Substring(path.Length);
                return assetPath.Substring(assetsRoot.Length);
            }
            else
            {
                Debug.LogError("Asset path is not a package path!");
                return assetPath;
            }
        }

        public static string RemoveExtensionManual(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return fileName;

            // 找到最后一个点的位置
            int lastDotIndex = fileName.LastIndexOf('.');

            // 如果没有点或者点是最后一个字符，直接返回原文件名
            if (lastDotIndex <= 0 || lastDotIndex == fileName.Length - 1)
                return fileName;

            // 截取从开始到最后一个点之前的字符串
            return fileName.Substring(0, lastDotIndex);
        }
    }
}
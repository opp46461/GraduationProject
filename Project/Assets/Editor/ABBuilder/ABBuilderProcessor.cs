using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using static ResManager;


namespace AssetBundles
{
    /// <summary>
    /// AB打包处理器
    /// </summary>
    public class ABBuilderProcessor
    {
        // 配置引用
        //private static ABPluginConfig pluginConfig;

        // 版本号
        private static string codeVersion = "1.0.0";
        private static string assetVersion = "1.0.0";

        // 输出路径
        private static string outputPath;

        // 当前构建目标
        private static BuildTarget buildTarget;

        // 当前构建渠道
        private static ABBuilderChannel channel;

        // 压缩选项
        private static BuildAssetBundleOptions buildOptions;

        // 所有被SpriteAtlas管理的精灵路径
        private static HashSet<string> atlasSpritePaths = new HashSet<string>();
        // 所有需要打包的目录下的直接资源
        private static Dictionary<string, ResourceInfo> _allDirectAsset = new();
        // 所有需要打包的目录下的直接资源所依赖的资源【即不在当前所有打包目录下的资源】
        private static Dictionary<string, ResourceInfo> _allDependenciesAsset = new();

        // 解决AB包名/资源重名冲突最大递归深度
        private const int MAX_DEPTH = 20;

        // -------- 循环依赖相关
        // 初始化状态字典
        private static Dictionary<string, NodeState> resourceNodeStates = new Dictionary<string, NodeState>();
        private static Dictionary<string, NodeState> bundleNodeStates = new Dictionary<string, NodeState>();
        // 节点状态枚举
        enum NodeState
        {
            Unvisited,  // 未访问（白色）
            Visiting,   // 访问中（灰色）
            Visited     // 已访问（黑色）
        }


        /// <summary>
        /// 初始化处理器
        /// </summary>
        public static void Initialize(
            string codeVersion,
            string assetVersion,
            string outputPath,
            BuildTarget target,
            ABBuilderChannel channel,
            BuildAssetBundleOptions options)
        {
            ABBuilderProcessor.codeVersion = codeVersion;
            ABBuilderProcessor.assetVersion = assetVersion;
            ABBuilderProcessor.outputPath = outputPath;
            buildTarget = target;
            ABBuilderProcessor.channel = channel;
            buildOptions = options;
        }

        /// <summary>
        /// 执行AB打包流程
        /// </summary>
        public static bool ExecuteBuild(bool isEditor = false)
        {
            AssetDatabase.Refresh();
            // 分发器寄存器
            List<AssetBundleDispatcherConfig> results = new List<AssetBundleDispatcherConfig>();

            // 1. 收集所有资源 + 依赖
            bool canPacked = CollectAll(ref results);
            if (!canPacked) return false;

            // 2. 检查 资源同名冲突
            canPacked = CheckAssetName();
            if (!canPacked) return false;

            // 3. 运行基础检查，是否能打包
            canPacked = BaseCheck();
            if (!canPacked) return false;

            // 4. 检查 基础依赖问题
            Dictionary<string, ResourceInfo> allResources = GetAllResources();
            canPacked = CheckDependencies(allResources);
            if (!canPacked) return false;

            // 5. 检查 资源级循环依赖
            canPacked = CheckResourceCycle(allResources);
            if (!canPacked) return false;

            // 6. 清理输出目录
            CleanOutputDirectory();

            // 7. 重置所有AB标记
            ResetAllAssetBundleNames();

            // 8. 开始AB分包
            foreach (var info in _allDirectAsset.Values)
            {
                AssetImporter importer = AssetImporter.GetAtPath(info.AssetPath);
                importer.assetBundleName = info.BundleName;
            }
            foreach (var info in _allDependenciesAsset.Values)
            {
                AssetImporter importer = AssetImporter.GetAtPath(info.AssetPath);
                importer.assetBundleName = info.BundleName;
            }

            // 9. 检查 AB包的循环依赖问题
            //  获取所有已定义的 AssetBundle
            string[] allBundleNames = AssetDatabase.GetAllAssetBundleNames();
            Dictionary<string, List<string>> dependencyGraph = new Dictionary<string, List<string>>();
            //  遍历所有 AssetBundle
            foreach (string bundleName in allBundleNames)
            {
                // 获取该 Bundle 的直接依赖
                string[] dependencies = AssetDatabase.GetAssetBundleDependencies(bundleName, false);
                // 添加到依赖图
                bool addSucceed = dependencyGraph.TryAdd(bundleName, new List<string>(dependencies));
            }
            if (HasBundleCycle(dependencyGraph))
            {
                Debug.LogError("发现AB循环依赖！");
                return false;
            }

            // 10. 构建资源映射表
            BuildMap();

            if (isEditor) return true;

            // 11. 执行打包
            string ab_output;
            BuildAssetBundles(out ab_output);

            // 12. 生成报告
            GenerateBuildReport();

            // 13. 输出代码版本对比文件
            // 暂时只有一个热更DLL被打包成AB，除非大型项目，否则一般不细分AB
            List<string> codeABPaths = new List<string>();
            // 暂时关闭
            //VersionInfoExporter.ExportCodeABVersionInfo(outputPath, codeVersion, channel.ToString(), buildTarget.ToString(), codeABPaths);

            // 14. 输出资源版本对比文件
            //  14.1 获取AB位置
            List<string> abPaths = new List<string>();
            foreach (string bundleName in allBundleNames)
            {
                abPaths.Add(string.Format("{0}/{1}", ab_output, bundleName));
            }
            VersionInfoExporter.ExportABVersionInfo(outputPath, assetVersion, channel.ToString(), buildTarget.ToString(), abPaths);

            return true;
        }

        /// <summary>
        /// 收集资源和依赖
        /// </summary>
        private static bool CollectAll(ref List<AssetBundleDispatcherConfig> results)
        {
            Debug.LogFormat("开始 收集资源和依赖：{0}", Time.time);
            // 清空下存储器
            _allDirectAsset.Clear();
            atlasSpritePaths.Clear();

            // 收集所有被SpriteAtlas管理的精灵路径
            atlasSpritePaths = CollectAtlasSpritePaths();

            //   1.1 检查 ABResources路径下有没有AB数据
            string[] abconfig_guids = AssetDatabase.FindAssets("t:AssetBundleDispatcherConfig", new[] { ABPathManager.GetABResourcesPackSettingPath() });
            if (abconfig_guids.Length <= 0)
            {
                Debug.LogError("AB打包失败：主要AB路径下没有AB数据！");
                return false;
            }
            //   1.2 获取所有分发器
            foreach (string guid in abconfig_guids)
            {
                // 通过 GUID 获取资源的路径
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                // 加载对应的 ScriptableObject 对象
                AssetBundleDispatcherConfig config = AssetDatabase.LoadAssetAtPath<AssetBundleDispatcherConfig>(assetPath);
                if (config != null)
                {
                    // 记录设置数据路径
                    config.ThisPath = assetPath;
                    results.Add(config);
                }
            }
            //   1.3 检查 是否有AB分发器
            if (results.Count <= 0)
            {
                Debug.LogError("AB打包失败：主要AB路径下没有AB数据！");
                return false;
            }
            //   1.4 收集所有资源
            //   1.4.1 按目录深度倒序排序配置（深度优先）
            var sortedConfigs = results
                .OrderByDescending(c => c.PackagePath.Count(c => c == '/' || c == '\\'))
                .ToList();
            //   1.4.2 包名处理
            //     1.4.2.1 设置包名
            foreach (var config in sortedConfigs)
            {
                if (string.IsNullOrEmpty(config.name))
                {
                    Debug.LogErrorFormat("AB打包失败：包的配置数据名有误：{0}，  包路径：{1}", config.name, config.PackagePath);
                    return false;
                }
                config.AssetNameValue = config.name;
            }
            //     1.4.2.2 检查 包名是否存在冲突，并解决冲突
            bool solveABDuplicateName = CheckABDuplicateName(sortedConfigs);
            if (!solveABDuplicateName) return false;

            //  深度优先遍历AB配置数据
            foreach (var config in sortedConfigs)
            {
                //   1.4.3 检查 路径是否有效
                if (string.IsNullOrEmpty(config.PackagePath))
                {
                    Debug.LogErrorFormat("AB打包失败：没发现AB包：{0}，  包路径：{1}", config.name, config.PackagePath);
                    return false;
                }
                //   1.4.4 检查 数据库中所有AB所指向的路径目录是否存在
                string result_path = ABPathManager.AddAssetsFolderPath(config.PackagePath);
                if (!Directory.Exists(result_path))
                {
                    Debug.LogErrorFormat("AB打包失败：没发现AB包：{0}，  路径：{1}", config.name, result_path);
                    return false;
                }

                //  1.4.5 根据分包类型和过滤情况进行收集
                int count = 0;
                // 是否搜集底下所有资源
                bool includeSubdirectories = config.Type == AssetBundleDispatcherType.All ? true : false;
                // 是否需要用到过滤
                bool isAllFiles = config.CheckerFilters.Count == 0 ? true : false;
                if (isAllFiles)
                {
                    count = FindAssets(config.AssetNameValue, result_path, "", includeSubdirectories);
                }
                else
                {
                    foreach (var filter in config.CheckerFilters)
                    {
                        // 搜索符合过滤条件的资源GUID
                        count = FindAssets(config.AssetNameValue, result_path, filter.ObjectFilter, includeSubdirectories);
                    }
                }
                if (count <= 0)
                {
                    Debug.LogErrorFormat("发现空包：{0}，  路径：{1}", config.name, result_path);
                    return false;
                }
            }

            //  1.4.6 把依赖添加到_allDependenciesAsset，方便后续标记
            foreach (var info in _allDirectAsset.Values)
            {
                foreach (var dep in info.AllDependencies)
                {
                    // 如果不存在于AB目录下的直接资源，则为依赖了其他地方的资源
                    if (!_allDirectAsset.ContainsKey(dep) && IsValidFile(dep, info.AssetNameValue) && !IsSceneFile(dep))
                    {
                        ProcessSingleDependencies(dep, ABPathManager.NotAssignedABName);
                    }
                }
            }

            Debug.LogFormat("完成 收集资源和依赖：{0}", Time.time);
            return true;
        }

        #region 检查相关
        /// <summary>
        /// 检查 资源名是否存在冲突，并解决冲突
        /// </summary>
        /// <returns></returns>
        private static bool CheckAssetName()
        {
            //  检查 直接资源名
            List<ResourceInfo> infos = _allDirectAsset.Values.ToList();
            bool solveAssetDuplicateName = CheckAssetDuplicateName(infos);
            if (!solveAssetDuplicateName) return false;
            //  检查 依赖资源名
            infos.Clear();
            infos = _allDependenciesAsset.Values.ToList();
            solveAssetDuplicateName = CheckAssetDuplicateName(infos);
            if (!solveAssetDuplicateName) return false;

            return true;
        }

        /// <summary>
        /// 基础检查，能否打包
        /// </summary>
        /// <returns></returns>
        private static bool BaseCheck()
        {
            Debug.Log("开始基础检查");
            // 1. 检查是否存在AB数据
            if (!Directory.Exists(ABPathManager.ABBuilderSettingRoot))
            {
                Debug.LogError("AB打包失败：没发现AB数据库目录！");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查AB同名，并解决冲突
        /// </summary>
        /// <param name="sortedConfigs"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private static bool CheckABDuplicateName(List<AssetBundleDispatcherConfig> sortedConfigs)
        {
            int depth = 0;
            bool hasDuplicates;

            do
            {
                hasDuplicates = false;
                Dictionary<string, List<AssetBundleDispatcherConfig>> duplicates = GameUtility.FindDuplicateNames(sortedConfigs);

                // 遍历所有同名
                foreach (var group in duplicates)
                {
                    hasDuplicates = true;

                    foreach (var config in group.Value)
                    {
                        // 达到最大深度仍然有冲突则报错
                        if (depth >= MAX_DEPTH)
                        {
                            throw new InvalidOperationException(
                                $"无法解决AB包名冲突: {config.AssetNameValue}\n" +
                                $"路径: {config.PackagePath}\n");
                        }

                        // -------- 添加父级文件夹前缀
                        // 分割路径为层级组件（深度优先排序）
                        var segments = config.PackagePath.Split('/')
                            .Where(s => !string.IsNullOrEmpty(s))
                            .Reverse()
                            .ToArray();

                        // 根据当前深度决定包含多少父级
                        int includeDepth = Math.Min(depth + 1, segments.Length);

                        // 构建新名称 (从最远父级到最近)
                        var newSegments = segments
                            .Take(includeDepth)
                            .Reverse()
                            .ToArray();

                        //// 添加原始文件夹名
                        //newSegments = newSegments
                        //    .Concat(new[] { segments[0] })
                        //    .ToArray();

                        // 添加父级文件夹前缀
                        config.AssetNameValue = string.Join("_", newSegments).ToLowerInvariant();
                    }
                }

                depth++;
            } while (hasDuplicates && depth < MAX_DEPTH);
            // 最终验证
            if (GameUtility.FindDuplicateNames(sortedConfigs).Count > 0)
            {
                Debug.LogError("未能完全解决所有AB包名冲突");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 自动解决同名资源解决
        /// </summary>
        /// <returns></returns>
        private static bool CheckAssetDuplicateName(List<ResourceInfo> infos)
        {
            int depth = 0;
            bool hasDuplicates;

            do
            {
                hasDuplicates = false;
                Dictionary<string, List<ResourceInfo>> duplicates = GameUtility.FindDuplicateNames(infos);

                // 遍历所有同名
                foreach (var group in duplicates)
                {
                    hasDuplicates = true;

                    foreach (var info in group.Value)
                    {
                        // 达到最大深度仍然有冲突则报错
                        if (depth >= MAX_DEPTH)
                        {
                            throw new InvalidOperationException(
                                $"无法解决资源名冲突: {info.AssetNameValue}\n" +
                                $"路径: {info.AssetPath}\n");
                        }

                        // -------- 添加父级文件夹前缀
                        // 分割路径为层级组件（深度优先排序）
                        var segments = info.AssetPath.Split('/')
                            .Where(s => !string.IsNullOrEmpty(s))
                            .Reverse()
                            .ToArray();

                        // 根据当前深度决定包含多少父级
                        int includeDepth = Math.Min(depth + 1, segments.Length);

                        // 构建新名称 (从最远父级到最近)
                        var newSegments = segments
                            .Take(includeDepth)
                            .Reverse()
                            .ToArray();

                        //// 添加原始文件夹名
                        //newSegments = newSegments
                        //    .Concat(new[] { segments[0] })
                        //    .ToArray();

                        // 添加父级文件夹前缀
                        info.AssetNameValue = string.Join("_", newSegments).ToLowerInvariant();
                    }
                }

                depth++;
            } while (hasDuplicates && depth < MAX_DEPTH);
            // 最终验证
            if (GameUtility.FindDuplicateNames(infos).Count > 0)
            {
                Debug.LogError("未能完全解决所有AB包名冲突");
                return false;
            }

            return true;
        }

        // -------- 资源 级
        // 合并所有资源节点
        private static Dictionary<string, ResourceInfo> GetAllResources()
        {
            var allResources = new Dictionary<string, ResourceInfo>();
            foreach (var kvp in _allDirectAsset) allResources[kvp.Key] = kvp.Value;
            foreach (var kvp in _allDependenciesAsset)
            {
                if (!allResources.ContainsKey(kvp.Key))
                    allResources[kvp.Key] = kvp.Value;
            }
            return allResources;
        }



        // 检查 依赖
        private static bool CheckDependencies(Dictionary<string, ResourceInfo> assets)
        {
            List<string> missingResources = new List<string>();
            Debug.LogFormat("开始 检查依赖：{0}", Time.time);
            foreach (var assetPath in assets.Keys)
            {
                foreach (string dep in assets[assetPath].AllDependencies)
                {
                    // 3.1 检查 是否有资源缺失
                    if (!System.IO.File.Exists(dep))
                    {
                        missingResources.Add(dep);
                        Debug.LogError($"{assetPath}资源缺失: {dep}");
                    }

                    // 3.2 检查 是否有资源不在打包资源根目录
                    if (!dep.StartsWith(ABPathManager.GetABResourcesRoot()))
                    {
                        Debug.LogWarningFormat("{0}包中资源{1}的依赖资源{2}不在资源根目录下",
                            assets[assetPath].BundleName, assets[assetPath].AssetNameValue, dep);
                    }
                }
            }
            if (missingResources.Count != 0) return false;

            // 3.4 检查 是否有资源被多个资源依赖/地方引用
            Debug.LogFormat("完成 依赖检查：{0}", Time.time);
            return true;
        }

        // 资源级 循环依赖检测
        private static bool CheckResourceCycle(Dictionary<string, ResourceInfo> allResources)
        {
            Debug.Log("开始资源级循环依赖检测");
            resourceNodeStates.Clear();

            // 初始化所有节点状态
            foreach (var path in allResources.Keys)
            {
                resourceNodeStates[path] = NodeState.Unvisited;
            }

            // 检测循环
            foreach (var path in allResources.Keys)
            {
                if (resourceNodeStates[path] == NodeState.Unvisited)
                {
                    if (HasResourceCycle(allResources, path, new Stack<string>()))
                    {
                        return false;
                    }
                }
            }

            Debug.Log("资源级循环依赖检测完成");
            return true;
        }

        // DFS检测资源循环
        private static bool HasResourceCycle(Dictionary<string, ResourceInfo> allResources,
            string currentPath, Stack<string> pathStack)
        {
            // 检查是否已在栈中（循环）
            if (pathStack.Contains(currentPath))
            {
                LogResourceCycle(pathStack, currentPath);
                return true;
            }

            // 标记为访问中
            resourceNodeStates[currentPath] = NodeState.Visiting;
            pathStack.Push(currentPath);

            // 处理所有依赖
            if (allResources.TryGetValue(currentPath, out ResourceInfo currentInfo))
            {
                foreach (string depPath in currentInfo.DirectDependencies)
                {
                    // 只处理有效资源节点
                    if (!allResources.ContainsKey(depPath)) continue;

                    // 递归检查依赖
                    if (HasResourceCycle(allResources, depPath, pathStack))
                    {
                        return true;
                    }
                }
            }

            // 回溯
            pathStack.Pop();
            resourceNodeStates[currentPath] = NodeState.Visited;
            return false;
        }

        // 记录资源循环路径
        private static void LogResourceCycle(Stack<string> pathStack, string cycleEnd)
        {
            List<string> cyclePath = new List<string>();
            bool recording = false;

            // 从栈中提取循环路径
            foreach (string path in pathStack.Reverse())
            {
                if (path == cycleEnd) recording = true;
                if (recording) cyclePath.Add(path);
            }
            cyclePath.Add(cycleEnd); // 闭合循环

            Debug.LogError("资源循环依赖路径: " + string.Join(" → ", cyclePath));

            // 输出详细资源信息
            foreach (string path in cyclePath)
            {
                if (_allDirectAsset.TryGetValue(path, out ResourceInfo info))
                {
                    Debug.LogError($"资源: {info.AssetNameValue} | 包: {info.BundleName} | 路径: {path}");
                }
                else if (_allDependenciesAsset.TryGetValue(path, out info))
                {
                    Debug.LogError($"依赖资源: {info.AssetNameValue} | 包: {info.BundleName} | 路径: {path}");
                }
            }
        }

        // -------- AB 级
        // AB包循环检测
        public static bool HasBundleCycle(Dictionary<string, List<string>> graph)
        {
            bundleNodeStates.Clear();
            List<List<string>> allCycles = new List<List<string>>();

            // 初始化状态
            foreach (string bundle in graph.Keys)
            {
                bundleNodeStates[bundle] = NodeState.Unvisited;
            }

            // 检测循环
            foreach (string bundle in graph.Keys)
            {
                if (bundleNodeStates[bundle] == NodeState.Unvisited)
                {
                    FindBundleCycles(graph, bundle, new Stack<string>(), allCycles);
                }
            }

            // 输出所有循环
            if (allCycles.Count > 0)
            {
                foreach (var cycle in allCycles)
                {
                    Debug.LogError("AB包循环依赖: " + string.Join(" → ", cycle));
                    AnalyzeBundleCycle(cycle);
                }
                return true;
            }

            return false;
        }

        // 查找AB包循环
        private static void FindBundleCycles(Dictionary<string, List<string>> graph, string currentBundle,
            Stack<string> pathStack, List<List<string>> allCycles)
        {
            // 检查是否已在栈中（循环）
            if (pathStack.Contains(currentBundle))
            {
                RecordBundleCycle(pathStack, currentBundle, allCycles);
                return;
            }

            // 标记为访问中
            bundleNodeStates[currentBundle] = NodeState.Visiting;
            pathStack.Push(currentBundle);

            // 处理依赖
            if (graph.TryGetValue(currentBundle, out List<string> dependencies))
            {
                foreach (string depBundle in dependencies)
                {
                    // 只处理图中的bundle
                    if (!graph.ContainsKey(depBundle)) continue;

                    // 递归检查
                    FindBundleCycles(graph, depBundle, pathStack, allCycles);
                }
            }

            // 回溯
            pathStack.Pop();
            bundleNodeStates[currentBundle] = NodeState.Visited;
        }

        // 记录AB包循环
        private static void RecordBundleCycle(Stack<string> pathStack, string cycleEnd, List<List<string>> allCycles)
        {
            List<string> cyclePath = new List<string>();
            bool recording = false;

            // 从栈中提取循环路径
            foreach (string bundle in pathStack.Reverse())
            {
                if (bundle == cycleEnd) recording = true;
                if (recording) cyclePath.Add(bundle);
            }
            cyclePath.Add(cycleEnd); // 闭合循环

            // 添加到结果集
            allCycles.Add(cyclePath);
        }

        // 增强循环分析
        private static void AnalyzeBundleCycle(List<string> cycle)
        {
            Debug.LogError("开始分析AB包循环依赖原因...");

            for (int i = 0; i < cycle.Count; i++)
            {
                string currentBundle = cycle[i];
                string nextBundle = cycle[(i + 1) % cycle.Count]; // 循环中的下一个包

                // 获取两个包中的所有资源
                string[] currentAssets = AssetDatabase.GetAssetPathsFromAssetBundle(currentBundle);
                string[] nextAssets = AssetDatabase.GetAssetPathsFromAssetBundle(nextBundle);

                // 检查currentBundle中的资源是否直接依赖nextBundle中的资源
                foreach (string assetPath in currentAssets)
                {
                    string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
                    foreach (string dep in dependencies)
                    {
                        AssetImporter importer = AssetImporter.GetAtPath(dep);
                        if (importer != null &&
                            !string.IsNullOrEmpty(importer.assetBundleName) &&
                            importer.assetBundleName == nextBundle)
                        {
                            Debug.LogError($"⚠️ 循环依赖源头: " +
                                $"\n资源: {assetPath} (在 {currentBundle})" +
                                $"\n依赖: {dep} (在 {nextBundle})");
                        }
                    }
                }
            }

            // 额外：输出每个AB包的内容
            Debug.LogError("涉及AB包内容详情:");
            foreach (string bundle in cycle)
            {
                string[] assets = AssetDatabase.GetAssetPathsFromAssetBundle(bundle);
                Debug.LogError($"--- {bundle} ({assets.Length}个资源) ---");
                foreach (string asset in assets)
                {
                    Debug.LogError($"  {asset}");
                }
            }
        }

        #endregion

        /// <summary>
        /// 清理输出目录
        /// </summary>
        private static void CleanOutputDirectory()
        {
            string finalOutputPath = string.Format("{0}/{1}", outputPath, channel.ToString());

            if (Directory.Exists(finalOutputPath))
            {
                Directory.Delete(finalOutputPath, true);
            }
            Directory.CreateDirectory(finalOutputPath);

            Debug.Log($"清理输出目录: {finalOutputPath}");
        }

        /// <summary>
        /// 重置所有AB标记
        /// </summary>
        private static void ResetAllAssetBundleNames()
        {
            // 获取所有已标记的AB名称
            string[] allBundles = AssetDatabase.GetAllAssetBundleNames();

            // 移除所有AB标记
            foreach (string bundle in allBundles)
            {
                AssetDatabase.RemoveAssetBundleName(bundle, true);
            }

            Debug.Log("已重置所有AssetBundle标记");
        }

        /// <summary>
        /// 处理ABResources目录
        /// </summary>
        private static void ProcessABResourcesFolderAB()
        {
            string rootPath = ABPathManager.GetABResourcesRoot();

            ProcessFolder(rootPath);

            Debug.Log("ABResources目录处理完成");
        }

        /// <summary>
        /// 处理文件夹（修复递归逻辑）
        /// </summary>
        private static void ProcessFolder(string folderPath)
        {
            // 获取文件夹相对路径
            string relativePath = ABPathManager.GetRelativePath(folderPath);

            ProcessSubFolders(folderPath);
        }

        /// <summary>
        /// 递归处理子文件夹
        /// </summary>
        private static void ProcessSubFolders(string parentFolder)
        {
            // 获取所有子文件夹
            string[] subFolders = Directory.GetDirectories(parentFolder);

            foreach (string subFolder in subFolders)
            {
                // 处理当前子文件夹
                ProcessFolder(subFolder);
            }
        }

        /// <summary>
        /// 递归处理文件夹
        /// </summary>
        private static void ProcessFolderRecursive(string folderPath)
        {
            // 获取文件夹相对路径
            string relativePath = ABPathManager.GetRelativePath(folderPath);

            // 递归处理子文件夹
            string[] subFolders = Directory.GetDirectories(folderPath);
            foreach (string subFolder in subFolders)
            {
                ProcessFolderRecursive(subFolder);
            }
        }

        private static void BuildMap()
        {
            //  9.1 生成基于ScriptableObject的资源映射表
            string dir = ABPathManager.GetAssetMapOutPutPath();
            //   确保目录存在
            string fullPath = ABPathManager.GetFullPath(dir);
            GameUtility.CheckDirAndCreateWhenNeeded(fullPath);
            //   创建配置文件实例
            ResourceMap rMap = ScriptableObject.CreateInstance<ResourceMap>();
            rMap.Clear();
            foreach (var info in _allDirectAsset.Values)
            {
                rMap.AddSingleMap(info.AssetNameValue, info);
            }
            foreach (var info in _allDependenciesAsset.Values)
            {
                rMap.AddSingleMap(info.AssetNameValue, info);
            }
            //   保存可视化数据结构
            string mapDataPath = ABPathManager.AddAssetSuffix(dir);
            AssetDatabase.CreateAsset(rMap, mapDataPath);
            //   保存到Json文件
            string mapJsonPath = ABPathManager.AddJsonSuffix(dir);
            string mapJson = rMap.ToJson();
            Debug.Log(mapJson);
            File.WriteAllText(mapJsonPath, mapJson);
            //   刷新数据库
            AssetDatabase.Refresh();
            Debug.LogFormat("成功生成资源映射表：{0}", mapDataPath);
            //  9.2 配置映射表AB
            //AssetImporter mapImporter = AssetImporter.GetAtPath(mapDataPath);
            //    此处改成Json

            AssetImporter mapImporter = AssetImporter.GetAtPath(mapJsonPath);
            mapImporter.assetBundleName = ABPathManager.AssetMap;
        }

        /// <summary>
        /// 执行AB打包
        /// </summary>
        private static void BuildAssetBundles(out string ab_output)
        {
            ab_output = string.Format("{0}/{1}", outputPath, channel);
            // 确保输出目录存在
            if (!Directory.Exists(ab_output))
            {
                Directory.CreateDirectory(ab_output);
            }

            // 执行打包
            BuildPipeline.BuildAssetBundles(
                ab_output,
                buildOptions,
                buildTarget
            );

            Debug.Log($"AB打包完成! 输出目录: {ab_output}");
        }

        /// <summary>
        /// 生成打包报告
        /// </summary>
        private static void GenerateBuildReport()
        {
            // 获取所有AB包
            string[] bundles = AssetDatabase.GetAllAssetBundleNames();

            // 创建报告
            string report = $"AB打包报告\n生成时间: {System.DateTime.Now}\n\n";
            report += $"目标平台: {buildTarget}\n";
            report += $"目标渠道: {channel}\n";
            report += $"压缩选项: {buildOptions}\n";
            report += $"输出目录: {outputPath}\n\n";
            report += $"AB包总数: {bundles.Length}\n";

            // 添加每个包的详细信息
            foreach (string bundle in bundles)
            {
                report += $"\n--- {bundle} ---\n";

                // 获取包内资源
                string[] assets = AssetDatabase.GetAssetPathsFromAssetBundle(bundle);
                report += $"资源数量: {assets.Length}\n";

                // 计算包大小
                string bundlePath = Path.Combine(outputPath, bundle);
                if (File.Exists(bundlePath))
                {
                    FileInfo fileInfo = new FileInfo(bundlePath);
                    report += $"包大小: {fileInfo.Length / 1024} KB\n";
                }

                // 获取依赖
                string[] dependencies = AssetDatabase.GetAssetBundleDependencies(bundle, false);
                if (dependencies.Length > 0)
                {
                    report += "依赖包: " + string.Join(", ", dependencies) + "\n";
                }
            }

            // 保存报告到文件
            string reportPath = Path.Combine(outputPath, "ABBuildReport.txt");
            File.WriteAllText(reportPath, report);

            Debug.Log($"打包报告已生成: {reportPath}");
        }

        #region 部分工具接口
        /// <summary>
        /// 是否为有效文件
        /// </summary>
        /// <returns></returns>
        public static bool IsValidFile(string assetPath, string source = "")
        {
            // 如果是文件夹，则表明不是文件
            if (AssetDatabase.IsValidFolder(assetPath)) return false;
            // 非 Asset 路径的不打包
            if (!assetPath.ToLower().Replace("\\", "/").StartsWith("assets/"))
            {
                Debug.LogWarningFormat("Asset isnot 'Assets' path. source : {0} \n path : {1}", source, assetPath);
                return false;
            }
            // assets/packages 路径的不打包
            if (assetPath.ToLower().Replace("\\", "/").StartsWith("assets/packages"))
            {
                Debug.LogWarningFormat("Asset is 'Assets/Packages' path. source : {0} \n path : {1}", source, assetPath);
                return false;
            }
            // Resources 路径的不打包
            if (assetPath.ToLower().Replace("\\", "/").Contains("/resources/"))
            {
                Debug.LogWarningFormat("Asset is 'Resources' path. source : {0} \n path : {1}", source, assetPath);
                return false;
            }
            // 跳过.meta文件和代码
            //if (assetPath.EndsWith(".spriteatlas")) return false;
            if (assetPath.EndsWith(".meta") || assetPath.EndsWith(".cs")) return false;
            if (assetPath.EndsWith(".asset"))
            {
                // 跳过光照贴图
                Type type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (type == typeof(LightingDataAsset)) return false;
            }
            // 跳过图集资源
            // 如果这个资源是被图集管理的精灵，清除其AB标记
            if (atlasSpritePaths.Contains(assetPath))
            {
                Debug.Log($"清理图集精灵AB标记: {assetPath}");
                return false;
            }

            // 此处可额外跳过非资源文件
            return true;
        }

        public static bool IsSceneFile(string assetPath)
        {
            if (!assetPath.EndsWith(".unity")) return false;
            return true;
        }

        private static ResourceInfo CreateResourceInfo(string assetPath, string bundleName)
        {
            // 所有场景资源单独成包【注意：需要在编辑器主动限制唯一场景名，暂时不做处理】
            if (IsSceneFile(assetPath))
            {
                bundleName = Path.GetFileName(assetPath);
                bundleName = bundleName.Replace(".unity", "-unityscene");
            }

            var segments = assetPath.Split('/');
            string assetFullName = segments[segments.Length - 1];
            var assetInfo = new ResourceInfo
            {
                // 移除后缀名
                AssetNameValue = ABPathManager.RemoveExtensionManual(assetFullName),
                AssetPath = assetPath,
                BundleName = bundleName,
                // 获取直接依赖
                DirectDependencies = AssetDatabase.GetDependencies(assetPath, false),
                // 获取所有直接和间接依赖
                AllDependencies = AssetDatabase.GetDependencies(assetPath, true)
            };

            return assetInfo;
        }

        /// <summary>
        /// 处理单个资源
        /// </summary>
        /// <param name="assetPath"></param>
        /// <param name="config"></param>
        private static void ProcessAsset(string assetPath, string bundleName)
        {
            var assetInfo = CreateResourceInfo(assetPath, bundleName);
            _allDirectAsset.TryAddElement(assetPath, assetInfo);
        }

        /// <summary>
        /// 处理资源的单个依赖
        /// </summary>
        /// <param name="assetPath"></param>
        /// <param name="config"></param>
        private static void ProcessSingleDependencies(string assetPath, string bundleName)
        {

            var assetInfo = CreateResourceInfo(assetPath, bundleName);
            _allDependenciesAsset.TryAddElement(assetPath, assetInfo);
        }

        /// <summary>
        /// 查找资源
        /// </summary>
        /// <param name="bundleName">包名</param>
        /// <param name="result_path">在哪个文件夹下找</param>
        /// <param name="searchPattern">过滤条件</param>
        /// <param name="includeSubdirectories">是否需要递归找儿孙资源</param>
        /// <returns></returns>
        private static int FindAssets(string bundleName, string result_path, string searchPattern, bool includeSubdirectories)
        {
            int count = 0;

            if (string.IsNullOrEmpty(result_path)) result_path = "";
            if (string.IsNullOrEmpty(searchPattern)) searchPattern = "";

            Debug.LogFormat("{0} 开始 查找资源。包名：{1}，路径：{2}，过滤条件：{3}，是否递归全部：{4}",
                Time.time, bundleName, result_path, searchPattern, includeSubdirectories);
            string[] asset_guids = AssetDatabase.FindAssets(searchPattern, new[] { result_path });

            foreach (string guid in asset_guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                // 如果不包含子目录，检查路径深度
                if (!includeSubdirectories)
                {
                    string parentDir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
                    if (parentDir != result_path.TrimEnd('/'))
                        continue;
                }

                if (IsValidFile(assetPath))
                {
                    if (IsSceneFile(assetPath))
                    {
                        // 所有场景资源单独成包【注意：需要在编辑器主动限制唯一场景名，暂时不做处理】
                        string bn = Path.GetFileName(assetPath);
                        ProcessAsset(assetPath, bn.Substring(".unity".Length));
                    }
                    else
                    {
                        ProcessAsset(assetPath, bundleName);
                    }
                    count++;
                }
            }

            Debug.LogFormat("{0} 完成 查找资源。共找到{1}个资源", Time.time, count, result_path);

            return count;
        }

        private static HashSet<string> CollectAtlasSpritePaths()
        {
            HashSet<string> spritePaths = new HashSet<string>();

            // 查找所有的SpriteAtlas
            string[] atlasGuids = AssetDatabase.FindAssets("t:SpriteAtlas");
            foreach (string guid in atlasGuids)
            {
                string atlasPath = AssetDatabase.GUIDToAssetPath(guid);
                SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);

                if (atlas != null)
                {
                    // 获取图集包含的所有精灵
                    UnityEngine.Object[] packedAssets = atlas.GetPackables();
                    foreach (UnityEngine.Object packedAsset in packedAssets)
                    {
                        if (packedAsset != null)
                        {
                            string assetPath = AssetDatabase.GetAssetPath(packedAsset);

                            // 如果是精灵，直接添加
                            if (packedAsset is UnityEngine.Sprite)
                            {
                                spritePaths.Add(assetPath);
                            }
                            // 如果是纹理，获取其生成的精灵
                            else if (packedAsset is UnityEngine.Texture2D)
                            {
                                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                                foreach (UnityEngine.Object asset in assets)
                                {
                                    if (asset is UnityEngine.Sprite)
                                    {
                                        spritePaths.Add(assetPath);
                                        break;
                                    }
                                }
                            }
                            // 如果是文件夹，递归获取所有精灵
                            else if (packedAsset.GetType() == typeof(UnityEditor.DefaultAsset))
                            {
                                string folderPath = AssetDatabase.GetAssetPath(packedAsset);
                                string[] folderSpriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
                                foreach (string spriteGuid in folderSpriteGuids)
                                {
                                    string spritePath = AssetDatabase.GUIDToAssetPath(spriteGuid);
                                    spritePaths.Add(spritePath);
                                }
                            }
                        }
                    }
                }
            }

            return spritePaths;
        }
        #endregion
    }
}
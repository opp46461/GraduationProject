using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System.IO;
using SharedCode;
using Newtonsoft.Json;


// 添加必要的命名空间
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif


/// <summary>
/// 基于AB的资源管理
/// 1. 双模式均依赖映射表
/// 2. 编辑器模式主要目的是为了快速测试代码和资源
/// 3. 模拟模式正常应该配备服务器支持，暂时没有
/// </summary>
public class ResManager : UnitySingleton<ResManager>
{
    /*
     * 编辑器模式：StreamingAssets 加载映射表，编辑器数据库加载资源
     * 模拟模式：纯 StreamingAssets 加载，即 资源通过AB进行加载
     * 发布模式：AB路径改为实际路径
     */
    public enum AssetLoadMode
    {
        Editor,
        Simulation,
        Release,
    }

    private static AssetLoadMode assetLoadMode = AssetLoadMode.Editor;
    public static AssetLoadMode AssetLoadModel { get => assetLoadMode; }

    private static string releaseAssetPath;

    // 路径相关
    public const string AssetBundlesFolderName = "AssetBundles";
    // AB资源映射表路径（存放AB资源映射表SO）
    //public const string AssetMapPath = "Assets/ABAssetMap/ABAssetMap.asset";
    public const string AssetMapPath = "Assets/ABAssetMap/ABAssetMap.json";


    // 远程加载地址【用于远程加载，后续再考虑】
    private string _remoteBaseUrl = "http://your-server.com/assetbundles";
    private int MAX_RETRY = 3;

    // 资源清单
    ManifestData manifestData;
    private AssetBundleManifest _assetBundleManifest;

    // 映射表：运行时快速查询的字典 (Key: LogicalName, Value: ResourceMapping)
    public Dictionary<string, ResourceRuntimeInfo> _resourceMap = new Dictionary<string, ResourceRuntimeInfo>();
    //public Dictionary<string, IResourceInfo> _resourceMap = new Dictionary<string, IResourceInfo>();

    // AB和Asset缓存
    private readonly Dictionary<string, AssetBundle> _loadedBundles = new Dictionary<string, AssetBundle>();
    // 格式：{bundleName}/{assetName}
    private readonly Dictionary<string, Object> _assetCache = new Dictionary<string, Object>();

    // 引用计数
    private readonly Dictionary<string, int> _bundleRefCount = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _assetRefCount = new Dictionary<string, int>();

    // 防止循环依赖卡死（虽然在打包时已经确保了没有循环依赖，在这做个双保险）
    private readonly HashSet<string> _loadingSet = new HashSet<string>();
    private readonly HashSet<string> _unloadingSet = new HashSet<string>();

    private bool _isInitialized = false;
    public bool IsInitialized { get => _isInitialized; }
    private bool _isInitializing = false;

    // 场景管理相关字段
    private readonly Dictionary<string, SceneLoadInfo> _loadedScenes = new Dictionary<string, SceneLoadInfo>();
    private readonly Dictionary<string, AsyncOperation> _sceneLoadingOperations = new Dictionary<string, AsyncOperation>();
    private readonly Dictionary<string, List<UnityAction<float>>> _sceneProgressCallbacks = new Dictionary<string, List<UnityAction<float>>>();

    public static void InitializeAssetLoadMode()
    {
#if EDITOR_MODE
        assetLoadMode = AssetLoadMode.Editor;
#elif SIMULATION_MODE
        assetLoadMode = AssetLoadMode.Simulation;
#elif RELEASE_MODE
        assetLoadMode = AssetLoadMode.Release;
        releaseAssetPath = string.Format("{0}/{1}", ServerDataHandle.Instance.GetABPath(), AssetBundlesFolderName);
#endif
    }

    public override void Awake()
    {
        base.Awake();
    }

    public void StartCoroutineInitialize(ManifestData localManifestData, UnityAction<float> progress = null)
    {
        StartCoroutine(Initialize(localManifestData, progress));
    }

    /// <summary>
    /// 初始化资源管理器
    /// </summary>
    public IEnumerator Initialize(ManifestData localManifestData, UnityAction<float> progress = null)
    {
        if (_isInitialized || _isInitializing) yield break;

        Debug.Log("初始化资源管理器");

        _isInitializing = true;
        manifestData = localManifestData;

        // 编辑器模式特殊初始化
        if (AssetLoadModel == AssetLoadMode.Editor)
        {
            yield return EditorInitialize(progress);
        }
        //else if (AssetLoadMode1 == AssetLoadMode.Simulation)
        else
        {
            // 1. 特殊加载主清单AssetBundle（无依赖）
            yield return LoadMainManifestBundle(manifestData.Channel);

            // 2. 获取AssetBundleManifest
            _assetBundleManifest = _loadedBundles[manifestData.Channel]
                .LoadAsset<AssetBundleManifest>("AssetBundleManifest");

            // 发布前测试时再检查
            //CheckCoreBundleDependencies();

            // 3. 加载 Loading 场景【暂时不加载，后面再加进去】
            //yield return LoadLoadingScene(progress);

            // 4. 加载核心资源（映射表AB、公共AB等）
            yield return PreloadCoreBundles(progress);

            // 5. 序列化映射表内容
            yield return LoadAndSerializeMapping();

        }

        _isInitialized = true;
        _isInitializing = false;

        progress?.Invoke(1f);
    }
    #region 编辑器模式初始化
    /// <summary>
    /// 编辑器模式初始化
    /// </summary>
    private IEnumerator EditorInitialize(UnityAction<float> progress)
    {
        // 1. 直接加载映射表
        yield return LoadMappingInEditor(progress);

        // 2. 加载Loading场景
        //yield return EditorLoadLoadingScene(progress);

        //progress?.Invoke(1f);
    }

    /// <summary>
    /// 编辑器模式下加载映射表
    /// </summary>
    private IEnumerator LoadMappingInEditor(UnityAction<float> progress)
    {
#if UNITY_EDITOR
        // 在编辑器模式下直接从AssetDatabase加载映射表
        string mappingPath = AssetMapPath;
        //ResourceMap mapData = AssetDatabase.LoadAssetAtPath<ResourceMap>(mappingPath);
        // 从 .json 文件加载
        TextAsset jsonData = AssetDatabase.LoadAssetAtPath<TextAsset>(mappingPath);
        List<ResourceRuntimeInfo> mapData = JsonConvert.DeserializeObject<List<ResourceRuntimeInfo>>(jsonData.text);

        if (mapData == null)
        {
            Debug.LogError("Failed to load asset mapping table in Editor mode!");
            yield break;
        }

        // 序列化映射表数据
        //Debug.Log($"Asset mapping table loaded with {mapData.Mappings.Count} entries (Editor Mode)");
        //foreach (var item in mapData.Mappings)
        //{
        //    _resourceMap.TryAdd(item.AssetNameValue, item);
        //}
        Debug.Log($"Asset mapping table loaded with {mapData.Count} entries (Editor Mode)");
        SerializedMapping(mapData);
#else
        yield return null;
#endif
        progress?.Invoke(0.5f);
    }

    /// <summary>
    /// 编辑器模式加载Loading场景
    /// </summary>
    private IEnumerator EditorLoadLoadingScene(UnityAction<float> progress)
    {
        // 进度从0.5开始
        float baseProgress = 0.5f;
        AsyncOperation asyncLoad = null;
        string loadingSceneName = "Loading";
        string scenePath = "Assets\\ABResources\\Common\\Base\\Scene\\Loading/Loading.unity";
#if UNITY_EDITOR
        // 创建加载参数（附加模式）
        LoadSceneParameters parameters = new LoadSceneParameters
        {
            loadSceneMode = LoadSceneMode.Additive
        };
        // 启动异步加载
        asyncLoad = EditorSceneManager.LoadSceneAsyncInPlayMode(
            scenePath,
            parameters
        );
#endif
        // 确保异步操作有效
        if (asyncLoad == null)
        {
            Debug.LogError("Scene loading operation failed to start");
            progress?.Invoke(1f);
            yield break;
        }

        // 禁用自动激活（允许控制进度）
        asyncLoad.allowSceneActivation = false;

        // 进度监控循环
        while (!asyncLoad.isDone)
        {
            // 计算实际进度（0-0.9范围映射到0.5-0.95）
            float calculatedProgress = baseProgress + (asyncLoad.progress * 0.5f);

            // 更新进度回调
            progress?.Invoke(calculatedProgress);

            // 当加载达到90%时（Unity异步加载上限）
            if (asyncLoad.progress >= 0.9f)
            {
                // 允许场景激活（完成最后10%的加载）
                asyncLoad.allowSceneActivation = true;

                // 调整基础进度，使最后阶段平滑过渡到1.0
                baseProgress = 0.95f;
            }

            yield return null;
        }

        // 确保最终进度为1.0
        progress?.Invoke(1f);

        // 场景加载完成后可选的后续操作
        Scene loadedScene = SceneManager.GetSceneByName(loadingSceneName);
        if (loadedScene.IsValid())
        {
            SceneManager.SetActiveScene(loadedScene);
        }
    }
    #endregion

    #region 初始化流程
    /// <summary>
    /// 特殊加载主清单AssetBundle（无依赖）
    /// </summary>
    private IEnumerator LoadMainManifestBundle(string bundleName)
    {
        if (_loadedBundles.ContainsKey(bundleName)) yield break;

        string path = GetABPath(bundleName);

        // 同步加载主清单（必须同步加载，因为后续操作依赖它）
        AssetBundle bundle = AssetBundle.LoadFromFile(path);

        if (bundle == null)
        {
            Debug.LogError($"Failed to load main manifest bundle: {bundleName}");
            yield break;
        }

        _loadedBundles.Add(bundleName, bundle);
        // 主清单永不卸载
        _bundleRefCount.Add(bundleName, int.MaxValue);

        yield return null;
    }

    /// <summary>
    /// 检查关键AB是否存在循环依赖问题
    /// </summary>
    private void CheckCoreBundleDependencies()
    {
        string[] coreBundles = { "abassetmap", "common",  "textmeshproab",
            GetSceneABName("Loading"), GetSceneABName("Case_1"), GetSceneABName("Case_2") };

        foreach (string bundle in coreBundles)
        {
            if (HasCircularDependency(bundle))
            {
                Debug.LogError($"发现循环依赖: {bundle}");
            }
        }
    }

    /// <summary>
    /// 加载Loading场景
    /// </summary>
    private IEnumerator LoadLoadingScene(UnityAction<float> progress)
    {
        string loadingSceneName = "Loading";
        string loadingSceneBundle = GetSceneABName("Loading");

        // 确保Loading场景Bundle已加载
        if (!_loadedBundles.ContainsKey(loadingSceneBundle))
        {
            // 加载依赖项
            string[] dependencies = _assetBundleManifest.GetAllDependencies(loadingSceneBundle);
            float totalDependencies = dependencies.Length;
            float loadedDependencies = 0;

            foreach (string dep in dependencies)
            {
                if (!_loadedBundles.ContainsKey(dep))
                {
                    // 使用内部加载方法（不检查初始化状态）
                    yield return InternalLoadBundleAsync(dep, p =>
                    {
                        progress?.Invoke((loadedDependencies + p) / (totalDependencies + 1) * 0.5f);
                    });
                }
                loadedDependencies++;
            }

            // 加载主Bundle
            string path = GetABPath(loadingSceneBundle);
            AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(path);

            while (!request.isDone)
            {
                progress?.Invoke(0.5f + request.progress * 0.5f);
                yield return null;
            }

            if (request.assetBundle == null)
            {
                Debug.LogError($"Failed to load loading scene bundle: {loadingSceneBundle}");
                yield break;
            }

            _loadedBundles.Add(loadingSceneBundle, request.assetBundle);
            _bundleRefCount.Add(loadingSceneBundle, 1);
        }

        // 异步加载Loading场景
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Additive);

        while (!asyncLoad.isDone)
        {
            progress?.Invoke(1f);
            yield return null;
        }

        // 场景加载完成，重置进度回调
        progress?.Invoke(0f);
    }

    /// <summary>
    /// 预加载核心Bundle（常驻内存）
    /// </summary>
    private IEnumerator PreloadCoreBundles(UnityAction<float> progress)
    {
        string[] coreBundles = { "abassetmap", "common" };
        float total = coreBundles.Length;
        float current = 0;

        foreach (string bundle in coreBundles)
        {
            if (!_loadedBundles.ContainsKey(bundle))
            {
                // 使用内部加载方法（不检查初始化状态）
                yield return InternalLoadBundleAsync(bundle, p =>
                {
                    progress?.Invoke((current + p) / total);
                });

                // 设置为永久引用
                if (_bundleRefCount.ContainsKey(bundle))
                    _bundleRefCount[bundle] = int.MaxValue;
            }

            current++;
            progress?.Invoke(current / total);
        }
    }

    /// <summary>
    /// 加载并序列化映射表
    /// </summary>
    private IEnumerator LoadAndSerializeMapping()
    {
        // 确保映射表Bundle已加载
        string mappingBundle = "abassetmap";

        if (!_loadedBundles.ContainsKey(mappingBundle))
        {
            Debug.LogError("Mapping table bundle not loaded!");
            yield break;
        }

        // 异步加载映射表资源
        //AssetBundleRequest request = _loadedBundles[mappingBundle].LoadAssetAsync<ResourceMap>("ABAssetMap");
        AssetBundleRequest request = _loadedBundles[mappingBundle].LoadAssetAsync<TextAsset>("ABAssetMap");

        yield return request;

        if (request.asset == null)
        {
            Debug.LogError("Failed to load asset mapping table!");
            yield break;
        }

        //ResourceMap mapData = (ResourceMap)request.asset;
        TextAsset jsonData = (TextAsset)request.asset;
        List<ResourceRuntimeInfo> mapData = JsonConvert.DeserializeObject<List<ResourceRuntimeInfo>>(jsonData.text);

        // 序列化映射表数据
        //Debug.Log($"Asset mapping table loaded with {mapData.Mappings.Count} entries");
        //foreach (var item in mapData.Mappings)
        //{
        //    _resourceMap.TryAdd(item.AssetNameValue, item);
        //}
        Debug.Log($"Asset mapping table loaded with {mapData.Count} entries");
        SerializedMapping(mapData);

        // 已经加载到字典里了，就给它卸载掉
        UnloadBundle(mappingBundle);
    }
    #endregion

    #region AB管理
    /// <summary>
    /// 内部使用的Bundle异步加载方法（不检查初始化状态）
    /// </summary>
    private IEnumerator InternalLoadBundleAsync(string bundleName, UnityAction<float> progress = null)
    {
        if (_loadedBundles.ContainsKey(bundleName))
        {
            _bundleRefCount[bundleName]++;
            progress?.Invoke(1f);
            yield break;
        }

        // 循环依赖检测
        if (_loadingSet.Contains(bundleName))
        {
            Debug.LogError($"Detected circular dependency: {bundleName}");
            progress?.Invoke(1f);
            yield break;
        }

        // 添加到加载中集合
        _loadingSet.Add(bundleName);

        try
        {
            // 加载依赖项
            string[] dependencies = _assetBundleManifest.GetAllDependencies(bundleName);
            int loadedDependencies = 0;

            for (int i = 0; i < dependencies.Length; i++)
            {
                string dep = dependencies[i];

                if (!_loadedBundles.ContainsKey(dep))
                {
                    // 使用闭包捕获当前索引
                    int currentIndex = i;
                    yield return InternalLoadBundleAsync(dep, p =>
                    {
                        // 计算依赖项综合进度
                        float depProgress = (currentIndex + p) / dependencies.Length;
                        progress?.Invoke(depProgress * 0.5f);
                    });
                }
                loadedDependencies++;
            }

            // 异步加载主Bundle
            string path = GetABPath(bundleName);
            // 远程加载
            if (path.StartsWith("http"))
            {
                yield return RemoteLoadBundleAsync(bundleName, progress);
                yield break;
            }
            // 本地加载
            AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(path);

            while (!request.isDone)
            {
                // 主Bundle加载占50%权重
                progress?.Invoke(0.5f + request.progress * 0.5f);
                yield return null;
            }

            if (request.assetBundle == null)
            {
                Debug.LogError($"Failed to load bundle async: {bundleName}");
                yield break;
            }

            _loadedBundles.Add(bundleName, request.assetBundle);
            _bundleRefCount.Add(bundleName, 1);
            progress?.Invoke(1f);
        }
        finally
        {
            // 从加载中集合移除
            _loadingSet.Remove(bundleName);
        }
    }

    private IEnumerator RemoteLoadBundleAsync(string bundleName, UnityAction<float> progress = null)
    {
        string url = $"{_remoteBaseUrl}/{bundleName}";
        int retryCount = 0;
        float retryDelay = 1f;

        while (retryCount < MAX_RETRY)
        {
            using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(url))
            {
                // 发送请求并跟踪进度
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    progress?.Invoke(operation.progress);
                    yield return null;
                }

                // 成功处理
                if (request.result == UnityWebRequest.Result.Success)
                {
                    AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(request);
                    if (bundle != null)
                    {
                        _loadedBundles.Add(bundleName, bundle);
                        _bundleRefCount.Add(bundleName, 1);
                        yield break;
                    }
                }

                // 失败处理
                Debug.LogWarning($"Bundle load failed (attempt {retryCount + 1}/{MAX_RETRY}): {request.error}");
                retryCount++;
                yield return new WaitForSeconds(retryDelay);
                retryDelay *= 2; // 指数退避
            }
        }

        Debug.LogError($"Failed to load bundle after {MAX_RETRY} attempts: {bundleName}");
    }

    /// <summary>
    /// 同步加载AssetBundle（带依赖处理）
    /// </summary>
    /// <param name="bundleName"></param>
    public void LoadBundleSync(string bundleName)
    {
        if (!_isInitialized)
        {
            Debug.LogError("ResourceManager not initialized!");
            return;
        }

        bundleName = bundleName.ToLower();

        // 编辑器模式下不需要加载AB
        if (assetLoadMode == AssetLoadMode.Editor) return;

        if (_loadedBundles.ContainsKey(bundleName))
        {
            _bundleRefCount[bundleName]++;
            return;
        }

        // 循环依赖检测
        if (_loadingSet.Contains(bundleName))
        {
            Debug.LogError($"Detected circular dependency: {bundleName}");
            return;
        }

        // 添加到加载中集合
        _loadingSet.Add(bundleName);

        try
        {
            // 加载依赖项
            string[] dependencies = _assetBundleManifest.GetAllDependencies(bundleName);
            foreach (string dep in dependencies)
            {
                // 如果依赖项尚未加载，则加载它
                if (!_loadedBundles.ContainsKey(dep))
                {
                    LoadBundleSync(dep);
                }
            }

            // 加载主Bundle
            string path = GetABPath(bundleName);
            AssetBundle bundle = AssetBundle.LoadFromFile(path);

            if (bundle == null)
            {
                Debug.LogError($"Failed to load bundle: {bundleName}");
                return;
            }

            _loadedBundles.Add(bundleName, bundle);
            _bundleRefCount.Add(bundleName, 1);
        }
        finally
        {
            // 从加载中集合移除
            _loadingSet.Remove(bundleName);
        }
    }

    /// <summary>
    /// 异步加载AssetBundle（带依赖处理）
    /// </summary>
    /// <param name="bundleName"></param>
    /// <param name="progress"></param>
    /// <returns></returns>
    public IEnumerator LoadBundleAsync(string bundleName, UnityAction<float> progress = null)
    {
        if (!_isInitialized)
        {
            Debug.LogError("ResourceManager not initialized!");
            yield break;
        }

        // 编辑器模式下不需要加载AB
        if (assetLoadMode == AssetLoadMode.Editor) yield break;

        // 如果已加载，则引用计数加1
        if (_loadedBundles.ContainsKey(bundleName))
        {
            _bundleRefCount[bundleName]++;
            progress?.Invoke(1f);
            yield break;
        }

        // 加载依赖项
        string[] dependencies = _assetBundleManifest.GetAllDependencies(bundleName);
        int loadedDependencies = 0;

        foreach (string dep in dependencies)
        {
            if (!_loadedBundles.ContainsKey(dep))
            {
                yield return InternalLoadBundleAsync(dep, p =>
                {
                    // 计算依赖项综合进度
                    float depProgress = (loadedDependencies + p) / dependencies.Length;
                    progress?.Invoke(depProgress * 0.5f);
                });
            }
            loadedDependencies++;
        }

        // 异步加载主Bundle
        string path = GetABPath(bundleName);
        AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(path);

        while (!request.isDone)
        {
            // 主Bundle加载占50%权重
            progress?.Invoke(0.5f + request.progress * 0.5f);
            yield return null;
        }

        if (request.assetBundle == null)
        {
            Debug.LogError($"Failed to load bundle async: {bundleName}");
            yield break;
        }

        _loadedBundles.Add(bundleName, request.assetBundle);
        _bundleRefCount.Add(bundleName, 1);
        progress?.Invoke(1f);
    }

    /// <summary>
    /// 安全卸载AssetBundle（递归处理依赖）
    /// </summary>
    /// <param name="bundleName"></param>
    /// <param name="unloadAllObjects"></param>
    public void UnloadBundle(string bundleName, bool unloadAllObjects = false)
    {
        bundleName = bundleName.ToLower();

        if (!_loadedBundles.ContainsKey(bundleName))
        {
            Debug.LogWarning($"Attempted to unload non-loaded bundle: {bundleName}");
            return;
        }

        // 防止循环依赖导致的无限递归
        if (_unloadingSet.Contains(bundleName))
        {
            Debug.LogWarning($"Detected circular dependency while unloading {bundleName}");
            return;
        }

        _unloadingSet.Add(bundleName);

        try
        {
            // 1. 减少当前Bundle的引用计数
            _bundleRefCount[bundleName]--;

            // 2. 仅当引用计数归零时执行卸载
            if (_bundleRefCount[bundleName] <= 0)
            {
                // 3. 获取所有依赖项（直接和间接）
                string[] dependencies = _assetBundleManifest.GetAllDependencies(bundleName);

                // 4. 先卸载所有依赖项（递归）
                foreach (string dep in dependencies)
                {
                    // 重要：只卸载引用计数归零的依赖项
                    if (_loadedBundles.ContainsKey(dep) && _bundleRefCount[dep] > 0)
                    {
                        UnloadBundle(dep, unloadAllObjects);
                    }
                }

                // 5. 卸载主Bundle（最后一步）
                Debug.Log($"Unloading bundle: {bundleName}, UnloadAssets: {unloadAllObjects}");
                _loadedBundles[bundleName].Unload(unloadAllObjects);

                // 6. 从管理系统中移除
                _loadedBundles.Remove(bundleName);
                _bundleRefCount.Remove(bundleName);

                // 7. 清理相关资源缓存
                CleanAssetCacheForBundle(bundleName);
            }
        }
        finally
        {
            _unloadingSet.Remove(bundleName);
        }
    }

    /// <summary>
    /// 清理指定Bundle的资源缓存
    /// </summary>
    private void CleanAssetCacheForBundle(string bundleName)
    {
        List<string> keysToRemove = new List<string>();

        foreach (var key in _assetCache.Keys)
        {
            if (key.StartsWith($"{bundleName}/"))
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _assetCache.Remove(key);
            _assetRefCount.Remove(key);
        }
    }
    #endregion

    #region 资源加载
    /// <summary>
    /// 同步加载资源（使用映射表）
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="assetName">唯一资源标识</param>
    /// <param name="isToLower">资源名是否需要小写处理</param>
    /// <returns></returns>
    public T LoadAsset<T>(string assetName, bool isToLower = false) where T : Object
    {
        if (!_isInitialized)
        {
            Debug.LogError("ResourceManager not initialized!");
            return null;
        }
        if (string.IsNullOrEmpty(assetName))
        {
            Debug.LogError("assetName can not be empty.");
            return null;
        }
        ResourceRuntimeInfo info;

        if (isToLower) assetName = assetName.ToLower();

        // 编辑器模式特殊处理
        if (assetLoadMode == AssetLoadMode.Editor)
        {
#if UNITY_EDITOR
            // 使用映射表获取资源信息
            if (!_resourceMap.TryGetValue(assetName, out info))
            {
                if (!assetName.Equals("null", System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError($"Asset not found in mapping table: {assetName}");
                }
                return null;
            }

            // 直接从AssetDatabase加载
            T asset = AssetDatabase.LoadAssetAtPath<T>(info.AssetPath);
            if (asset == null)
            {
                Debug.LogError($"Editor: Failed to load asset at path: {info.AssetPath}");
                return null;
            }
            return asset;
#else
            return null;
#endif
        }

        // 使用映射表获取资源信息
        if (_resourceMap == null)
        {
            Debug.LogError("Asset mapping table not loaded!");
            return null;
        }

        if (!_resourceMap.TryGetValue(assetName, out info))
        {
            if (!assetName.Equals("null", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"Asset not found in mapping table: {assetName}");
            }
            return null;
        }

        return LoadAsset<T>(info.BundleName, info.AssetNameValue);
    }

    /// <summary>
    /// 同步加载资源（直接指定Bundle）
    /// </summary>
    public T LoadAsset<T>(string bundleName, string assetName) where T : Object
    {
        // 编辑器模式不支持直接Bundle加载
        if (assetLoadMode == AssetLoadMode.Editor)
        {
            Debug.LogWarning("Editor mode doesn't support bundle-specified loading");
            return null;
        }
        if (string.IsNullOrEmpty(bundleName))
        {
            Debug.LogError("bundleName can not be empty.");
            return null;
        }
        if (string.IsNullOrEmpty(assetName))
        {
            Debug.LogError("assetName can not be empty.");
            return null;
        }

        string assetKey = GetAssetKey(bundleName, assetName);

        // 资源缓存检查
        if (_assetCache.TryGetValue(assetKey, out Object cachedAsset))
        {
            _assetRefCount[assetKey]++;
            return (T)cachedAsset;
        }

        // 确保Bundle已加载
        if (!_loadedBundles.ContainsKey(bundleName))
        {
            LoadBundleSync(bundleName);
        }

        // 加载资源
        T asset = _loadedBundles[bundleName].LoadAsset<T>(assetName);
        if (asset == null)
        {
            Debug.LogError($"Failed to load asset: {assetName} from {bundleName}");
            return null;
        }

        // 缓存资源
        _assetCache.Add(assetKey, asset);
        _assetRefCount.Add(assetKey, 1);
        return asset;
    }

    /// <summary>
    /// 异步加载资源（使用映射表）
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="assetName">资源唯一标识</param>
    /// <param name="onComplete"></param>
    /// <param name="isToLower">资源名是否需要小写处理</param>
    /// <returns></returns>
    public IEnumerator LoadAssetAsync<T>(string assetName, UnityAction<T> onComplete, bool isToLower = false) where T : Object
    {
        if (!_isInitialized)
        {
            Debug.LogError("ResourceManager not initialized!");
            onComplete?.Invoke(null);
            yield break;
        }

        if (string.IsNullOrEmpty(assetName))
        {
            Debug.LogError("assetName can not be empty.");
            yield break;
        }

        if (isToLower) assetName = assetName.ToLower();

        // 编辑器模式特殊处理
        if (assetLoadMode == AssetLoadMode.Editor)
        {
#if UNITY_EDITOR
            if (!_resourceMap.TryGetValue(assetName, out ResourceRuntimeInfo info))
            {
                if (!assetName.Equals("null", System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError($"Asset not found in mapping table: {assetName}");
                }
                onComplete?.Invoke(null);
                yield break;
            }

            // 模拟异步延迟
            yield return null;

            T asset = AssetDatabase.LoadAssetAtPath<T>(info.AssetPath);
            if (asset == null)
            {
                Debug.LogError($"Editor: Failed to load asset at path: {info.AssetPath}");
            }
            onComplete?.Invoke(asset);
#else
            onComplete?.Invoke(null);
            yield break;
#endif
        }
        //else if (assetLoadMode == AssetLoadMode.Simulation)
        else
        {
            // 使用映射表获取资源信息
            if (_resourceMap == null)
            {
                Debug.LogError("Asset mapping table not loaded!");
                onComplete?.Invoke(null);
                yield break;
            }

            if (!_resourceMap.TryGetValue(assetName, out ResourceRuntimeInfo info))
            {
                if (!assetName.Equals("null", System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError($"Asset not found in mapping table: {assetName}");
                }
                onComplete?.Invoke(null);
                yield break;
            }

            yield return LoadAssetAsync<T>(info.BundleName, info.AssetNameValue, onComplete);
        }
    }

    /// <summary>
    /// 异步加载资源（直接指定Bundle）
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bundleName"></param>
    /// <param name="assetName">资源唯一标识</param>
    /// <param name="onComplete"></param>
    /// <returns></returns>
    public IEnumerator LoadAssetAsync<T>(string bundleName, string assetName, UnityAction<T> onComplete) where T : Object
    {
        string assetKey = GetAssetKey(bundleName, assetName);

        // 资源缓存检查
        if (_assetCache.TryGetValue(assetKey, out Object cachedAsset))
        {
            _assetRefCount[assetKey]++;
            onComplete?.Invoke((T)cachedAsset);
            yield break;
        }

        if (string.IsNullOrEmpty(bundleName))
        {
            Debug.LogError("bundleName can not be empty.");
            yield break;
        }
        if (string.IsNullOrEmpty(assetName))
        {
            Debug.LogError("assetName can not be empty.");
            yield break;
        }

        // 确保Bundle已加载
        if (!_loadedBundles.ContainsKey(bundleName))
        {
            yield return LoadBundleAsync(bundleName);
        }

        // 异步加载资源
        AssetBundleRequest request = _loadedBundles[bundleName].LoadAssetAsync<T>(assetName);
        yield return request;

        if (request.asset == null)
        {
            Debug.LogError($"Failed to load asset async: {assetName} from {bundleName}");
            onComplete?.Invoke(null);
            yield break;
        }

        T asset = (T)request.asset;
        _assetCache.Add(assetKey, asset);
        _assetRefCount.Add(assetKey, 1);
        onComplete?.Invoke(asset);
    }

    /// <summary>
    /// 卸载资源（自动处理Bundle引用）
    /// </summary>
    /// <param name="assetName">资源唯一标识</param>
    /// <param name="isToLower">资源名是否需要小写处理</param>
    public void UnloadAsset(string assetName, bool isToLower = false)
    {
        if (string.IsNullOrEmpty(assetName))
        {
            Debug.LogError("assetName can not be empty.");
            return;
        }


        if (isToLower) assetName = assetName.ToLower();

        if (_resourceMap == null ||
            !_resourceMap.TryGetValue(assetName, out ResourceRuntimeInfo info))
        {
            if (!assetName.Equals("null", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"Asset not found in mapping table: {assetName}");
            }
            return;
        }

        UnloadAsset(info.BundleName, info.AssetNameValue);
    }

    /// <summary>
    /// 卸载资源（直接指定Bundle）
    /// </summary>
    /// <param name="bundleName"></param>
    /// <param name="assetName">资源唯一标识</param>
    public void UnloadAsset(string bundleName, string assetName)
    {
        if (string.IsNullOrEmpty(bundleName))
        {
            Debug.LogError("bundleName can not be empty.");
            return;
        }
        if (string.IsNullOrEmpty(assetName))
        {
            Debug.LogError("assetName can not be empty.");
            return;
        }

        string assetKey = GetAssetKey(bundleName, assetName);

        if (!_assetCache.ContainsKey(assetKey)) return;

        // 减少引用计数
        _assetRefCount[assetKey]--;

        // 引用计数为0时卸载
        if (_assetRefCount[assetKey] <= 0)
        {
            _assetCache.Remove(assetKey);
            _assetRefCount.Remove(assetKey);
            UnloadBundle(bundleName); // 减少Bundle引用
        }
    }
    #endregion

    #region 场景管理
    /// <summary>
    /// 异步加载场景（使用映射表）
    /// </summary>
    /// <param name="sceneName">场景名称</param>
    /// <param name="loadMode">加载模式</param>
    /// <param name="activateOnLoad">加载完成后是否立即激活</param>
    /// <param name="progress">进度回调</param>
    /// <param name="onComplete">完成回调</param>
    /// <returns></returns>
    public IEnumerator LoadSceneAsync(string sceneName, LoadSceneMode loadMode = LoadSceneMode.Single,
        bool activateOnLoad = true, UnityAction<float> progress = null, UnityAction<bool> onComplete = null)
    {
        if (!_isInitialized)
        {
            Debug.LogError("ResourceManager not initialized!");
            onComplete?.Invoke(false);
            yield break;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("Scene name cannot be empty!");
            onComplete?.Invoke(false);
            yield break;
        }

        // 检查场景是否已加载
        if (_loadedScenes.ContainsKey(sceneName))
        {
            _loadedScenes[sceneName].RefCount++;
            progress?.Invoke(1f);
            onComplete?.Invoke(true);
            yield break;
        }

        string assetName = sceneName.ToLower();

        // 使用映射表获取场景信息
        if (!_resourceMap.TryGetValue(assetName, out ResourceRuntimeInfo info))
        {
            Debug.LogError($"Scene not found in mapping table: {assetName}");
            onComplete?.Invoke(false);
            yield break;
        }

        // 编辑器模式特殊处理
        if (assetLoadMode == AssetLoadMode.Editor)
        {
            yield return EditorLoadSceneAsync(sceneName, info.AssetPath, loadMode, activateOnLoad, progress, onComplete);
        }
        else
        {
            yield return LoadSceneAsyncByBundle(info.BundleName, sceneName, loadMode, activateOnLoad, progress, onComplete);
        }
    }

    /// <summary>
    /// 异步加载场景（直接指定Bundle）
    /// </summary>
    /// <param name="bundleName">Bundle名称</param>
    /// <param name="sceneName">场景名称</param>
    /// <param name="loadMode">加载模式</param>
    /// <param name="activateOnLoad">加载完成后是否立即激活</param>
    /// <param name="progress">进度回调</param>
    /// <param name="onComplete">完成回调</param>
    /// <returns></returns>
    public IEnumerator LoadSceneAsyncByBundle(string bundleName, string sceneName, LoadSceneMode loadMode = LoadSceneMode.Single,
        bool activateOnLoad = true, UnityAction<float> progress = null, UnityAction<bool> onComplete = null)
    {
        if (assetLoadMode == AssetLoadMode.Editor)
        {
            Debug.LogWarning("Editor mode should use mapping table for scene loading");
            onComplete?.Invoke(false);
            yield break;
        }

        // 确保场景Bundle已加载
        if (!_loadedBundles.ContainsKey(bundleName))
        {
            yield return LoadBundleAsync(bundleName, progress);
        }
        else
        {
            _bundleRefCount[bundleName]++;
            progress?.Invoke(1f);
        }

        // 注册进度回调
        if (progress != null)
        {
            if (!_sceneProgressCallbacks.ContainsKey(sceneName))
                _sceneProgressCallbacks[sceneName] = new List<UnityAction<float>>();
            _sceneProgressCallbacks[sceneName].Add(progress);
        }

        // 开始异步加载场景
        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName, loadMode);

        if (asyncOp == null)
        {
            Debug.LogError($"Failed to start loading scene: {sceneName}");
            onComplete?.Invoke(false);
            yield break;
        }

        // 配置加载参数
        asyncOp.allowSceneActivation = activateOnLoad;
        _sceneLoadingOperations[sceneName] = asyncOp;

        // 监控加载进度
        while (!asyncOp.isDone)
        {
            float sceneProgress = asyncOp.progress;

            // Unity的progress在0.9时停止，需要手动处理最后10%
            if (!activateOnLoad && sceneProgress >= 0.9f)
            {
                sceneProgress = 0.9f;
            }

            // 调用所有注册的进度回调
            if (_sceneProgressCallbacks.ContainsKey(sceneName))
            {
                foreach (var callback in _sceneProgressCallbacks[sceneName])
                {
                    callback?.Invoke(sceneProgress);
                }
            }

            yield return null;
        }

        // 加载完成处理
        _sceneLoadingOperations.Remove(sceneName);

        if (_sceneProgressCallbacks.ContainsKey(sceneName))
        {
            _sceneProgressCallbacks[sceneName].Clear();
        }

        // 记录已加载场景（在AB模式下，ScenePath为空）
        _loadedScenes[sceneName] = new SceneLoadInfo(sceneName, bundleName, "", loadMode, activateOnLoad);

        // 最终进度
        progress?.Invoke(1f);
        onComplete?.Invoke(true);
    }

    /// <summary>
    /// 编辑器模式下加载场景（使用映射表路径）
    /// </summary>
    private IEnumerator EditorLoadSceneAsync(string sceneName, string scenePath, LoadSceneMode loadMode, bool activateOnLoad,
        UnityAction<float> progress, UnityAction<bool> onComplete)
    {
#if UNITY_EDITOR
        // 模拟异步加载进度
        float simulatedProgress = 0f;
        while (simulatedProgress < 0.9f)
        {
            simulatedProgress += 0.1f;
            progress?.Invoke(simulatedProgress);
            yield return new WaitForSeconds(0.05f);
        }

        // 使用映射表中的路径加载场景
        AsyncOperation asyncOp = null;

        if (loadMode == LoadSceneMode.Single)
        {
            // 单模式直接加载
            asyncOp = EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(loadMode));
        }
        else
        {
            // 附加模式
            asyncOp = EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(loadMode));
            // （暂不做特殊处理）
            //asyncOp = EditorSceneManager.LoadSceneAsync(scenePath, loadMode);
        }

        // 确保异步操作有效
        if (asyncOp == null)
        {
            Debug.LogError($"Failed to load scene at path: {scenePath}");
            onComplete?.Invoke(false);
            yield break;
        }

        asyncOp.allowSceneActivation = activateOnLoad;
        _sceneLoadingOperations[sceneName] = asyncOp;

        // 监控加载进度
        while (!asyncOp.isDone)
        {
            float currentProgress = 0.9f + asyncOp.progress * 0.1f;
            progress?.Invoke(currentProgress);

            // 调用所有注册的进度回调
            if (_sceneProgressCallbacks.ContainsKey(sceneName))
            {
                foreach (var callback in _sceneProgressCallbacks[sceneName])
                {
                    callback?.Invoke(currentProgress);
                }
            }

            yield return null;
        }

        // 加载完成处理
        _sceneLoadingOperations.Remove(sceneName);

        if (_sceneProgressCallbacks.ContainsKey(sceneName))
        {
            _sceneProgressCallbacks[sceneName].Clear();
        }

        // 记录已加载场景（在编辑器模式下，BundleName为空，使用ScenePath）
        _loadedScenes[sceneName] = new SceneLoadInfo(sceneName, "", scenePath, loadMode, activateOnLoad);

        progress?.Invoke(1f);
        onComplete?.Invoke(true);
#else
        onComplete?.Invoke(false);
        yield break;
#endif
    }

    /// <summary>
    /// 异步卸载场景
    /// </summary>
    /// <param name="sceneName">场景名称</param>
    /// <param name="progress">进度回调</param>
    /// <param name="onComplete">完成回调</param>
    /// <returns></returns>
    public IEnumerator UnloadSceneAsync(string sceneName, UnityAction<float> progress = null, UnityAction<bool> onComplete = null)
    {
        if (!_loadedScenes.ContainsKey(sceneName))
        {
            Debug.LogWarning($"Scene not loaded: {sceneName}");
            onComplete?.Invoke(false);
            yield break;
        }

        SceneLoadInfo sceneInfo = _loadedScenes[sceneName];

        // 减少引用计数
        sceneInfo.RefCount--;

        // 如果还有引用，不卸载场景
        if (sceneInfo.RefCount > 0)
        {
            progress?.Invoke(1f);
            onComplete?.Invoke(true);
            yield break;
        }

        // 开始异步卸载
        AsyncOperation asyncOp = null;

        // 编辑器模式使用EditorSceneManager卸载场景
        if (assetLoadMode == AssetLoadMode.Editor)
        {
#if UNITY_EDITOR
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid())
            {
                asyncOp = UnityEditor.SceneManagement.EditorSceneManager.UnloadSceneAsync(scene);
            }
#else
            asyncOp = SceneManager.UnloadSceneAsync(sceneName);
#endif
        }
        else
        {
            asyncOp = SceneManager.UnloadSceneAsync(sceneName);
        }

        if (asyncOp == null)
        {
            Debug.LogError($"Failed to start unloading scene: {sceneName}");
            onComplete?.Invoke(false);
            yield break;
        }

        // 监控卸载进度
        while (!asyncOp.isDone)
        {
            progress?.Invoke(asyncOp.progress);
            yield return null;
        }

        // 卸载完成处理
        _loadedScenes.Remove(sceneName);

        // 在非编辑器模式下，减少Bundle引用计数
        if (assetLoadMode != AssetLoadMode.Editor &&
            !string.IsNullOrEmpty(sceneInfo.BundleName) &&
            _loadedBundles.ContainsKey(sceneInfo.BundleName))
        {
            UnloadBundle(sceneInfo.BundleName);
        }

        progress?.Invoke(1f);
        onComplete?.Invoke(true);
    }

    /// <summary>
    /// 获取场景加载状态
    /// </summary>
    /// <param name="sceneName">场景名称</param>
    /// <returns></returns>
    public SceneLoadStatus GetSceneLoadStatus(string sceneName)
    {
        if (!_loadedScenes.ContainsKey(sceneName))
            return SceneLoadStatus.NotLoaded;

        if (_sceneLoadingOperations.ContainsKey(sceneName))
            return SceneLoadStatus.Loading;

        return SceneLoadStatus.Loaded;
    }

    /// <summary>
    /// 获取场景加载进度
    /// </summary>
    /// <param name="sceneName">场景名称</param>
    /// <returns>0-1的进度值，-1表示场景未在加载中</returns>
    public float GetSceneLoadProgress(string sceneName)
    {
        if (_sceneLoadingOperations.TryGetValue(sceneName, out AsyncOperation asyncOp))
        {
            return asyncOp.progress;
        }

        return -1f;
    }

    /// <summary>
    /// 激活已加载的场景（用于延迟激活）
    /// </summary>
    /// <param name="sceneName">场景名称</param>
    /// <returns></returns>
    public bool ActivateLoadedScene(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            SceneManager.SetActiveScene(scene);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 检查场景是否存在
    /// </summary>
    /// <param name="sceneName">场景名称</param>
    /// <returns></returns>
    public bool SceneExists(string sceneName)
    {
        // 通过映射表检查场景是否存在
        return _resourceMap.ContainsKey(sceneName);
    }

    /// <summary>
    /// 获取所有已加载场景
    /// </summary>
    /// <returns></returns>
    public List<string> GetLoadedScenes()
    {
        return new List<string>(_loadedScenes.Keys);
    }

    /// <summary>
    /// 获取场景引用计数
    /// </summary>
    /// <param name="sceneName">场景名称</param>
    /// <returns></returns>
    public int GetSceneRefCount(string sceneName)
    {
        if (_loadedScenes.TryGetValue(sceneName, out SceneLoadInfo info))
        {
            return info.RefCount;
        }
        return 0;
    }

    /// <summary>
    /// 强制卸载所有场景（用于清理）
    /// </summary>
    public void ForceUnloadAllScenes()
    {
        foreach (var sceneName in new List<string>(_loadedScenes.Keys))
        {
            // 编辑器模式使用EditorSceneManager卸载场景
            if (assetLoadMode == AssetLoadMode.Editor)
            {
#if UNITY_EDITOR
                Scene scene = SceneManager.GetSceneByName(sceneName);
                if (scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.UnloadSceneAsync(scene);
                }
#else
                SceneManager.UnloadSceneAsync(sceneName);
#endif
            }
            else
            {
                SceneManager.UnloadSceneAsync(sceneName);
            }

            // 在非编辑器模式下，减少Bundle引用计数
            var sceneInfo = _loadedScenes[sceneName];
            if (assetLoadMode != AssetLoadMode.Editor &&
                !string.IsNullOrEmpty(sceneInfo.BundleName) &&
                _loadedBundles.ContainsKey(sceneInfo.BundleName))
            {
                UnloadBundle(sceneInfo.BundleName);
            }
        }

        _loadedScenes.Clear();
        _sceneLoadingOperations.Clear();
        _sceneProgressCallbacks.Clear();
    }
    #endregion


    #region 工具
    /// <summary>
    /// 检查循环依赖【考虑到性能开销问题，只在必要时调用检查】
    /// </summary>
    /// <param name="bundleName"></param>
    /// <returns></returns>
    public bool HasCircularDependency(string bundleName)
    {
        var visited = new HashSet<string>();
        var stack = new HashSet<string>();
        return CheckDependency(bundleName, visited, stack);
    }

    private bool CheckDependency(string current, HashSet<string> visited, HashSet<string> stack)
    {
        // 循环依赖检测
        if (stack.Contains(current))
            return true;

        // 已访问过
        if (visited.Contains(current))
            return false;

        // 标记访问
        visited.Add(current);
        stack.Add(current);

        // 检查所有依赖
        string[] dependencies = _assetBundleManifest.GetAllDependencies(current);
        foreach (string dep in dependencies)
        {
            if (CheckDependency(dep, visited, stack))
                return true;
        }

        // 回溯
        stack.Remove(current);
        return false;
    }

    private string GetAssetKey(string bundleName, string assetName)
    {
        return $"{bundleName}/{assetName}";
    }
    /// <summary>
    /// 获取场景AB名（无后缀）
    /// </summary>
    /// <param name="sceneName">场景名</param>
    /// <returns></returns>
    public string GetSceneABName(string sceneName)
    {
        return $"{sceneName.ToLower()}-unityscene";
    }

    public void SerializedMapping(List<ResourceRuntimeInfo> mapData)
    {
        foreach (var info in mapData)
        {
            _resourceMap.TryAdd(info.AssetNameValue, info);
        }
    }
    #endregion

    #region 路径管理
    public string GetABPath(string abName)
    {
        string path = abName;

        // 不同模式用不同的加载方式
        if (assetLoadMode == AssetLoadMode.Editor)
        {
            Debug.LogWarning("Editor mode doesn't support bundle-specified loading");
        }
        //else if (assetLoadMode == AssetLoadMode.Simulation)
        else
        {
            path = GetRuntimeABResourcePath(abName);

        }
        return path;
    }

    /// <summary>
    /// 运行时的AB文件路径 → 运行时资源数据目录/AssetBundles/Channel
    /// </summary>
    /// <param name="abName">AB名</param>
    /// <returns></returns>
    public string GetRuntimeABResourcePath(string abName)
    {
        if (manifestData == null) return null;
        return string.Format("{0}/{1}/{2}", GetRuntimeAssetBundlesFolder(), manifestData.Channel, abName);
    }


    public static string GetLocalManifestDataPath(string fileName)
    {
        string manifestDataPath = fileName;
        // StreamingAssets
        if (assetLoadMode == AssetLoadMode.Editor)
            manifestDataPath = GetStreamingAssetsAssetBundlesFolder();
        else
            manifestDataPath = GetRuntimeAssetBundlesFolder();
        return string.Format("{0}/{1}", manifestDataPath, fileName);
    }
    public static string GetStreamingAssetsAssetBundlesFolder()
    {
        return Path.Combine(Application.streamingAssetsPath, AssetBundlesFolderName);
    }
    public static string GetRuntimeAssetBundlesFolder()
    {
        // 模拟模式下，StreamingAssets 加载
        if (assetLoadMode == AssetLoadMode.Simulation)
            return GetStreamingAssetsAssetBundlesFolder();
        // 发布模式下，persistentDataPath 加载【不是很确定】
        else
            //return Path.Combine(Application.persistentDataPath, AssetBundlesFolderName);
            return releaseAssetPath;
    }
    #endregion
}

#region 场景加载数据结构
/// <summary>
/// 场景加载信息
/// </summary>
public class SceneLoadInfo
{
    public string SceneName;
    public string BundleName;
    public string ScenePath; // 编辑器模式使用
    public LoadSceneMode LoadMode;
    public bool ActivateOnLoad;
    public int RefCount;

    public SceneLoadInfo(string sceneName, string bundleName, string scenePath, LoadSceneMode loadMode, bool activateOnLoad = true)
    {
        SceneName = sceneName;
        BundleName = bundleName;
        ScenePath = scenePath;
        LoadMode = loadMode;
        ActivateOnLoad = activateOnLoad;
        RefCount = 1;
    }
}

/// <summary>
/// 场景加载状态
/// </summary>
public enum SceneLoadStatus
{
    NotLoaded,
    Loading,
    Loaded
}
#endregion

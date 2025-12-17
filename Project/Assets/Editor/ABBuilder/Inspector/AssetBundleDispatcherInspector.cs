using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;


namespace AssetBundles
{
    // 声明自定义编辑器，作用于所有DefaultAsset类型
    [CustomEditor(typeof(DefaultAsset), true)]
    public class AssetBundleDispatcherInspector : Editor
    {
        // 分发器配置对象
        AssetBundleDispatcherConfig dispatcherConfig = null;
        // 资源包路径（无Assets前缀）
        string packagePath = null;
        // 当前选中资源的完整路径（含Assets）
        string targetAssetPath = null;
        // 配置文件在数据库中的路径
        string databaseAssetPath = null;

        // 存储折叠面板状态
        static Dictionary<string, bool> inspectorSate = new Dictionary<string, bool>();
        // 当前分发器类型（分包方式）
        AssetBundleDispatcherType dispatcherType = AssetBundleDispatcherType.All;
        // 当前过滤器类型
        AssetBundleDispatcherFilterType filterType = AssetBundleDispatcherFilterType.All;
        // 过滤当前目录
        private bool filterCurrent = false;
        // 过滤一级子目录
        private bool filterFirstLevel = false;
        // 过滤二级子目录
        private bool filterSecondLevel = false;  
        // 标记配置是否被修改
        bool configChanged = false;


        void OnEnable()
        {
            // 初始化方法
            Initialize();
        }

        // 初始化编辑器状态
        void Initialize()
        {
            configChanged = false;
            dispatcherType = AssetBundleDispatcherType.All;
            filterType = AssetBundleDispatcherFilterType.All;
            // 获取当前选中资源的路径
            targetAssetPath = AssetDatabase.GetAssetPath(target);
            // 检查是否为有效资源包路径
            if (!ABPathManager.IsPackagePath(targetAssetPath))
            {
                return;
            }

            // 转换路径格式
            packagePath = ABPathManager.AssetsPathToPackagePath(targetAssetPath);
            databaseAssetPath = ABPathManager.AssetPathToDatabasePath(targetAssetPath);

            // 加载配置文件
            dispatcherConfig = AssetDatabase.LoadAssetAtPath<AssetBundleDispatcherConfig>(databaseAssetPath);
            if (dispatcherConfig != null)
            {
                // 加载配置数据
                dispatcherConfig.Load();
                // 同步分包方式
                dispatcherType = dispatcherConfig.Type;
                // 同步过滤器类型
                filterType = dispatcherConfig.FilterType;
                filterCurrent = dispatcherConfig.filterCurrent;
                filterFirstLevel = dispatcherConfig.filterCurrent;
                filterSecondLevel = dispatcherConfig.filterCurrent;

            }
        }

        // 应用配置更改
        void Apply()
        {
            dispatcherConfig.PackagePath = packagePath;
            dispatcherConfig.Type = dispatcherType;
            dispatcherConfig.FilterType = filterType;
            dispatcherConfig.filterCurrent = filterCurrent;
            dispatcherConfig.filterCurrent = filterFirstLevel;
            dispatcherConfig.filterCurrent = filterSecondLevel;
            // 应用配置
            dispatcherConfig.Apply();
            // 标记为脏数据
            EditorUtility.SetDirty(dispatcherConfig);
            // 保存资源
            AssetDatabase.SaveAssets();

            // 重新初始化
            Initialize();
            // 重绘界面
            Repaint();
            // 重置修改标记
            configChanged = false;
        }

        // 删除配置文件
        void Remove()
        {
            // 弹出确认对话框
            bool checkRemove = EditorUtility.DisplayDialog("Remove Warning",
                "Sure to remove the AssetBundle dispatcher ?",
                "Confirm", "Cancel");
            if (!checkRemove)
            {
                return;
            }
            // 安全删除文件
            GameUtility.SafeDeleteFile(databaseAssetPath);
            // 刷新数据库
            AssetDatabase.Refresh();

            // 重新初始化
            Initialize();
            // 重绘界面
            Repaint();
            // 重置修改标记
            configChanged = false;
        }

        void OnDisable()
        {
            // 检查未保存的修改
            if (configChanged)
            {
                bool checkApply = EditorUtility.DisplayDialog("Modify Warning",
                    "You have modified the AssetBundle dispatcher setting, Apply it ?",
                    "Confirm", "Cancel");
                if (checkApply)
                {
                    // 确认则应用更改
                    Apply();
                }
            }
            // 清理引用
            dispatcherConfig = null;
            // 清空状态字典
            inspectorSate.Clear();
        }

        #region 绘制
        // 重写Inspector绘制
        public override void OnInspectorGUI()
        {
            // 绘制默认Inspector
            base.OnInspectorGUI();
            // 检查是否为有效资源包资源（即专属的AB打包文件夹下的资源）
            if (!ABPathManager.IsPackagePath(targetAssetPath))
            {
                return;
            }
            // 确保GUI可用
            GUI.enabled = true;

            // 根据配置状态绘制不同界面
            if (dispatcherConfig == null)
            {
                // 无配置时显示创建按钮
                DrawCreateAssetBundleDispatcher();
            }
            else
            {
                // 有配置时显示完整面板
                DrawAssetBundleDispatcherInspector();
            }
        }

        // 主绘制方法：分发器面板
        void DrawAssetBundleDispatcherInspector()
        {
            // 绘制可折叠的标题栏
            if (GUILayoutUtils.DrawHeader("AssetBundle Dispatcher : ", inspectorSate, "DispatcherConfig", true, false))
            {
                // 展开时绘制配置面板
                DrawAssetDispatcherConfig();
            }
        }

        // 绘制创建分发器按钮
        void DrawCreateAssetBundleDispatcher()
        {
            // 创建按钮
            if (GUILayout.Button("生成AB分发器"))
            {
                var dir = Path.GetDirectoryName(databaseAssetPath);
                // 确保目录存在
                string fullPath = ABPathManager.GetFullPath(dir);
                GameUtility.CheckDirAndCreateWhenNeeded(fullPath);

                // 创建配置文件实例
                var instance = CreateInstance<AssetBundleDispatcherConfig>();
                // 锁死包路径
                instance.PackagePath = ABPathManager.AssetsPathToPackagePath(targetAssetPath);
                // 保存到数据库
                AssetDatabase.CreateAsset(instance, databaseAssetPath);
                // 刷新数据库
                AssetDatabase.Refresh();

                // 重新初始化
                Initialize();
                // 重绘界面
                Repaint();
            }
        }

        // 绘制分发器配置面板
        void DrawAssetDispatcherConfig()
        {
            // 开始内容区域
            GUILayoutUtils.BeginContents(false);

            // 显示资源包路径（只读）
            GUILayoutUtils.DrawProperty("Path:", ABPathManager.AssetsPathToPackagePath(targetAssetPath), 300f, 80f);

            // 分包方式选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("DispatcherType:", GUILayout.MaxWidth(80f));
            var selectDispatcherType = (AssetBundleDispatcherType)EditorGUILayout.EnumPopup(dispatcherType);
            // 类型变更检测
            if (selectDispatcherType != dispatcherType)
            {
                dispatcherType = selectDispatcherType;
                configChanged = true;

                // 当切换到第二种分包方式时，强制过滤类型为All
                if (dispatcherType == AssetBundleDispatcherType.OnlyCurrentDirectory)
                {
                    filterType = AssetBundleDispatcherFilterType.All;
                }
            }
            EditorGUILayout.EndHorizontal();

            // 根据分包方式显示不同选项
            if (dispatcherType == AssetBundleDispatcherType.All)
            {
                // 过滤器类型选择
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("FilterType:", GUILayout.MaxWidth(100f));
                var selectFilterType = (AssetBundleDispatcherFilterType)EditorGUILayout.EnumPopup(filterType);
                if (selectFilterType != filterType)
                {
                    filterType = selectFilterType;
                    configChanged = true;
                }
                EditorGUILayout.EndHorizontal();
            }
            // OnlyCurrentDirectory 模式
            else if (dispatcherType == AssetBundleDispatcherType.OnlyCurrentDirectory)
            {
                // 显示固定提示（过滤类型被锁定）
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("FilterType:", GUILayout.MaxWidth(100f));
                var selectFilterType = AssetBundleDispatcherFilterType.All;
                if (selectFilterType != filterType)
                {
                    filterType = selectFilterType;
                    configChanged = true;
                }
                EditorGUILayout.LabelField("ALL (Locked)", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();
            }

            // 根据过滤类型显示不同选项
            if (filterType == AssetBundleDispatcherFilterType.Optional)
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField("Filter Options:", EditorStyles.boldLabel);

                // 过滤当前目录选项
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("   Filter Current:", GUILayout.MaxWidth(100f));
                var newFilterCurrent = EditorGUILayout.Toggle(filterCurrent);
                if (newFilterCurrent != filterCurrent)
                {
                    filterCurrent = newFilterCurrent;
                    configChanged = true;
                }
                EditorGUILayout.EndHorizontal();

                // 过滤一级子目录选项
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("   Filter First Level:", GUILayout.MaxWidth(100f));
                var newFilterFirstLevel = EditorGUILayout.Toggle(filterFirstLevel);
                if (newFilterFirstLevel != filterFirstLevel)
                {
                    filterFirstLevel = newFilterFirstLevel;
                    configChanged = true;
                }
                EditorGUILayout.EndHorizontal();

                // 过滤二级子目录选项
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("   Filter Second Level:", GUILayout.MaxWidth(100f));
                var newFilterSecondLevel = EditorGUILayout.Toggle(filterSecondLevel);
                if (newFilterSecondLevel != filterSecondLevel)
                {
                    filterSecondLevel = newFilterSecondLevel;
                    configChanged = true;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Separator();
            var filtersCount = dispatcherConfig.CheckerFilters.Count;
            // 绘制可折叠的过滤器列表标题
            if (GUILayoutUtils.DrawSubHeader(0, "CheckerFilters:", inspectorSate, "CheckerFilters", filtersCount.ToString()))
            {
                // 展开时绘制列表
                DrawFilterTypesList(dispatcherConfig.CheckerFilters);
            }

            // 保存原始GUI颜色
            Color color = GUI.color;
            if (configChanged)
            {
                GUI.color = color * new Color(1, 1, 0.5f);
            }
            EditorGUILayout.Separator();

            GUILayout.BeginHorizontal();
            // 应用更改按钮
            if (GUILayout.Button("Apply"))
            {
                Apply();
            }
            // 红色Remove按钮
            GUI.color = new Color(1, 0.5f, 0.5f);
            // 移除配置按钮
            if (GUILayout.Button("Remove"))
            {
                Remove();
            }
            // 恢复原始颜色
            GUI.color = color;
            GUILayout.EndHorizontal();
            EditorGUILayout.Separator();

            // 结束内容区域
            GUILayoutUtils.EndContents(false);
        }

        // 绘制过滤器列表
        void DrawFilterTypesList(List<AssetBundleFilter> checkerFilters)
        {
            // 垂直区域
            GUILayout.BeginVertical(EditorStyles.textField);
            GUILayout.Space(3);

            // 分隔线
            EditorGUILayout.Separator();
            for (int i = 0; i < checkerFilters.Count; i++)
            {
                var curFilter = checkerFilters[i];
                // 格式化显示文本
                var relativePath = string.IsNullOrEmpty(curFilter.RelativePath) ? "root" : curFilter.RelativePath;
                var objectFilter = string.IsNullOrEmpty(curFilter.ObjectFilter) ? "all" : curFilter.ObjectFilter;
                var filterType = relativePath + ": <" + objectFilter + ">";
                // 状态唯一键
                var stateKey = "CheckerFilters" + i.ToString();
                // 绘制可移除的折叠面板项
                if (GUILayoutUtils.DrawRemovableSubHeader(1, filterType, inspectorSate, stateKey, () =>
                {
                    configChanged = true;
                    // 移除回调
                    checkerFilters.RemoveAt(i);
                    i--;
                }))
                {
                    // 展开时绘制详细项
                    DrawFilterItem(curFilter);
                }
                EditorGUILayout.Separator();
            }
            // 添加新过滤器
            if (GUILayout.Button("Add"))
            {
                configChanged = true;
                // 默认添加Prefab过滤器
                checkerFilters.Add(new AssetBundleFilter("", "t:prefab"));
            }
            EditorGUILayout.Separator();

            GUILayout.Space(3);
            GUILayout.EndVertical();
        }

        // 绘制单个过滤器项
        void DrawFilterItem(AssetBundleFilter checkerFilter)
        {
            GUILayout.BeginVertical();
            // 绘制相对路径输入框
            var relativePath = GUILayoutUtils.DrawInputField("RelativePath:", checkerFilter.RelativePath, 300f, 80f);
            // 绘制对象过滤规则输入框
            var objectFilter = GUILayoutUtils.DrawInputField("ObjectFilter:", checkerFilter.ObjectFilter, 300f, 80f);
            // 检查修改
            if (relativePath != checkerFilter.RelativePath)
            {
                configChanged = true;
                checkerFilter.RelativePath = relativePath;
            }
            if (objectFilter != checkerFilter.ObjectFilter)
            {
                configChanged = true;
                checkerFilter.ObjectFilter = objectFilter;
            }
            GUILayout.EndVertical();
        }
        #endregion
    }
}

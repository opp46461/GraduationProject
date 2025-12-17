using System.IO;
using UnityEditor;
using UnityEngine;
using static ResManager;

namespace AssetBundles
{
    /// <summary>
    /// AB打包工具主窗口
    /// </summary>
    public class ABBuilderWindow : EditorWindow
    {
        // 配置引用
        //private ABPluginConfig pluginConfig;

        // 版本号
        private string codeVersion = "1.0.0";
        private string assetVersion = "1.0.0";

        // 平台选项
        private BuildTarget buildTarget = BuildTarget.StandaloneWindows;
        private readonly BuildTarget[] supportedPlatforms = {
            BuildTarget.StandaloneWindows,
            BuildTarget.Android,
            BuildTarget.WebGL
        };

        private ABBuilderChannel channel = ABBuilderChannel.Test;
        private readonly ABBuilderChannel[] supportedChannels = {
            ABBuilderChannel.Test,
            ABBuilderChannel.Channel_1,
            ABBuilderChannel.Channel_2
        };

        // 压缩选项
        private BuildAssetBundleOptions buildOptions = BuildAssetBundleOptions.ChunkBasedCompression;

        // 滚动位置
        private Vector2 scrollPosition;

        [MenuItem("AssetBundle/AB打包工具")]
        public static void ShowWindow()
        {
            GetWindow<ABBuilderWindow>("AB打包工具");
        }

        private void OnEnable()
        {
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // 标题
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AssetBundle打包工具", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("版本设置：", EditorStyles.boldLabel);
            codeVersion = EditorGUILayout.TextField("代码版本：", codeVersion);
            assetVersion = EditorGUILayout.TextField("资源版本：", assetVersion);

            // 配置部分
            DrawConfigSection();

            // 平台选择
            DrawPlatformSection();

            // 输出设置
            DrawOutputSection();

            // 打包按钮
            DrawBuildButton();

            EditorGUILayout.EndScrollView();
        }

        private void DrawConfigSection()
        {
            EditorGUILayout.LabelField("配置", EditorStyles.boldLabel);

            // AB数据库路径
            EditorGUILayout.LabelField("AB数据库路径", ABPathManager.ABBuilderSettingRoot);

            EditorGUILayout.Space();
        }

        private void DrawPlatformSection()
        {
            EditorGUILayout.LabelField("目标平台", EditorStyles.boldLabel);

            // 平台选择
            int currentIndex = System.Array.IndexOf(supportedPlatforms, buildTarget);
            if (currentIndex < 0) currentIndex = 0;

            int newIndex = EditorGUILayout.Popup("平台", currentIndex, GetPlatformNames());
            buildTarget = supportedPlatforms[newIndex];

            // 渠道选择
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("目标渠道", EditorStyles.boldLabel);

            int currentChannelIndex = System.Array.IndexOf(supportedChannels, channel);
            if (currentChannelIndex < 0) currentChannelIndex = 0;

            int newChannelIndex = EditorGUILayout.Popup("渠道", currentChannelIndex, GetChannelNames());
            channel = supportedChannels[newChannelIndex];

            // 压缩选项
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("压缩选项", EditorStyles.boldLabel);

            bool lzmaSelected = (buildOptions & BuildAssetBundleOptions.None) != 0;
            bool lz4Selected = (buildOptions & BuildAssetBundleOptions.ChunkBasedCompression) != 0;
            bool uncompressed = (buildOptions & BuildAssetBundleOptions.UncompressedAssetBundle) != 0;

            lzmaSelected = EditorGUILayout.Toggle("LZMA (高压缩)", lzmaSelected);
            lz4Selected = EditorGUILayout.Toggle("LZ4 (快速加载)", lz4Selected);
            uncompressed = EditorGUILayout.Toggle("未压缩", uncompressed);

            // 更新选项
            buildOptions = BuildAssetBundleOptions.None;
            if (lzmaSelected) buildOptions |= BuildAssetBundleOptions.None;
            if (lz4Selected) buildOptions |= BuildAssetBundleOptions.ChunkBasedCompression;
            if (uncompressed) buildOptions |= BuildAssetBundleOptions.UncompressedAssetBundle;

            EditorGUILayout.Space();
        }

        private string[] GetPlatformNames()
        {
            string[] names = new string[supportedPlatforms.Length];
            for (int i = 0; i < supportedPlatforms.Length; i++)
            {
                names[i] = supportedPlatforms[i].ToString();
            }
            return names;
        }

        private string[] GetChannelNames()
        {
            string[] names = new string[supportedChannels.Length];
            for (int i = 0; i < supportedChannels.Length; i++)
            {
                names[i] = supportedChannels[i].ToString();
            }
            return names;
        }

        private void DrawOutputSection()
        {
            EditorGUILayout.LabelField("输出设置", EditorStyles.boldLabel);

            // 输出路径
            EditorGUILayout.BeginHorizontal();
            ABPathManager.OutputPath = EditorGUILayout.TextField("输出路径", ABPathManager.OutputPath);
            if (GUILayout.Button("浏览", GUILayout.Width(60)))
            {
                string newPath = EditorUtility.SaveFolderPanel("选择输出目录", ABPathManager.OutputPath, "");
                if (!string.IsNullOrEmpty(newPath))
                {
                    // 转换为相对路径
                    if (newPath.StartsWith(Application.dataPath))
                    {
                        ABPathManager.OutputPath = "Assets" + newPath.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        ABPathManager.OutputPath = newPath;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            // 附加选项
            bool appendHash = (buildOptions & BuildAssetBundleOptions.AppendHashToAssetBundleName) != 0;
            bool forceRebuild = (buildOptions & BuildAssetBundleOptions.ForceRebuildAssetBundle) != 0;
            bool disableTypeTree = (buildOptions & BuildAssetBundleOptions.DisableWriteTypeTree) != 0;

            appendHash = EditorGUILayout.Toggle("添加Hash到文件名", appendHash);
            forceRebuild = EditorGUILayout.Toggle("强制重新构建", forceRebuild);
            disableTypeTree = EditorGUILayout.Toggle("禁用TypeTree", disableTypeTree);

            // 更新选项
            if (appendHash) buildOptions |= BuildAssetBundleOptions.AppendHashToAssetBundleName;
            else buildOptions &= ~BuildAssetBundleOptions.AppendHashToAssetBundleName;

            if (forceRebuild) buildOptions |= BuildAssetBundleOptions.ForceRebuildAssetBundle;
            else buildOptions &= ~BuildAssetBundleOptions.ForceRebuildAssetBundle;

            if (disableTypeTree) buildOptions |= BuildAssetBundleOptions.DisableWriteTypeTree;
            else buildOptions &= ~BuildAssetBundleOptions.DisableWriteTypeTree;

            EditorGUILayout.Space();
        }

        private void DrawBuildButton()
        {
            EditorGUILayout.Space(20);
            if (GUILayout.Button("强制移除所有AB标记", GUILayout.Height(40)))
            {
                // 防止误点
                var buildTargetName = buildTarget;
                var channelName = channel.ToString();
                // 确认对话框
                bool checkCopy = EditorUtility.DisplayDialog("强制移除所有标记", $"请注意，此操作无法撤销!", "确定");
                if (!checkCopy)
                {
                    return;
                }

                // 获取所有当前定义的AssetBundle名称
                string[] abNames = AssetDatabase.GetAllAssetBundleNames();

                for (int i = 0; i < abNames.Length; i++)
                {
                    // 强制移除每个AssetBundle名称
                    AssetDatabase.RemoveAssetBundleName(abNames[i], true);
                }

                AssetDatabase.Refresh();

                // 刷新资源数据库
                AssetDatabase.Refresh();
                Debug.Log("已强制清空所有资源的AssetBundle标记！");
            }
            if (GUILayout.Button("根据当前模式构建（非发布模式）", GUILayout.Height(40)))
            {
                // 防止误点
                var buildTargetName = buildTarget;
                var channelName = channel.ToString();
                string mode = "";
#if EDITOR_MODE
                mode = "Editor";
#elif SIMULATION_MODE
                mode = "Simulation";
#elif RELEASE_MODE
                mode = "Release";
#endif
                if (mode == "Release")
                {
                    EditorUtility.DisplayDialog("Build Error", "Current AssetBundleMode is Release！", "Confirm");
                    return;
                }
                // 确认对话框
                bool checkCopy = EditorUtility.DisplayDialog("Build Warning",
                    string.Format("Build for : \n\nmode : {0} \nchannel : {1} \n\nContinue ?", mode, channelName),
                    "Confirm", "Cancel");
                if (!checkCopy)
                {
                    return;
                }

                // 初始化处理器
                ABBuilderProcessor.Initialize(codeVersion, assetVersion, "Assets/StreamingAssets/AssetBundles", BuildTarget.StandaloneWindows, channel, buildOptions);
                // 执行打包
                bool succeed = ABBuilderProcessor.ExecuteBuild(mode == "Editor");

                if (succeed)
                    // 提示完成
                    EditorUtility.DisplayDialog("Construction completed",
                        string.Format("Build for : \n\nmode : {0} \nchannel : {1} \n\nCompleted!", mode, channelName), "Confirm");
                else
                    // 提示失败
                    EditorUtility.DisplayDialog("Construction fail",
                        string.Format("Build for : \n\nmode : {0} \nchannel : {1} \n\nFail!", mode, channelName), "Confirm");


                // 刷新资源数据库
                AssetDatabase.Refresh();
            }

            if (GUILayout.Button("构建发布模式AB", GUILayout.Height(40)))
            {
                // 防止误点
                var buildTargetName = buildTarget;
                var channelName = channel.ToString();
                // 确认对话框
                bool checkCopy = EditorUtility.DisplayDialog("Build AssetBundles Warning",
                    string.Format("Build AssetBundles for : \n\nplatform : {0} \nchannel : {1} \n\nContinue ?", buildTargetName, channelName),
                    "Confirm", "Cancel");
                if (!checkCopy)
                {
                    return;
                }

                // 初始化处理器
                ABBuilderProcessor.Initialize(codeVersion, assetVersion, ABPathManager.OutputPath, buildTarget, channel, buildOptions);
                // 执行打包
                bool succeed = ABBuilderProcessor.ExecuteBuild();

                if (succeed)
                    // 提示完成
                    EditorUtility.DisplayDialog("打包完成", "AssetBundle打包已完成！", "确定");
                else
                    // 提示失败
                    EditorUtility.DisplayDialog("打包失败", "请查看控制台输出！", "确定");


                // 刷新资源数据库
                AssetDatabase.Refresh();
            }

            if (GUILayout.Button("将AB复制到 StreamingAssets", GUILayout.Height(40)))
            {
                CopyFolderToStreamingAssetsFunction(ABPathManager.AssetBundlesFolderName);
            }

            EditorGUILayout.Space();
        }

        public static void CopyFolderToStreamingAssetsFunction(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
            {
                EditorUtility.DisplayDialog("错误", "请输入文件夹名称", "确定");
                return;
            }

            try
            {
                // 获取项目根目录（Assets的父目录）
                string projectRoot = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
                // 构建源文件夹路径
                string sourceFolderPath = Path.Combine(projectRoot, folderName);

                // 检查源文件夹是否存在
                if (!Directory.Exists(sourceFolderPath))
                {
                    EditorUtility.DisplayDialog("错误", $"源文件夹不存在: {sourceFolderPath}", "确定");
                    return;
                }

                // 获取StreamingAssets路径
                string streamingAssetsPath = Application.streamingAssetsPath;

                // 如果StreamingAssets文件夹不存在，则创建
                if (!Directory.Exists(streamingAssetsPath))
                {
                    Directory.CreateDirectory(streamingAssetsPath);
                }

                // 目标文件夹路径
                string targetFolderPath = Path.Combine(streamingAssetsPath, folderName);

                // 显示确认对话框
                bool shouldProceed = EditorUtility.DisplayDialog(
                    "确认复制",
                    $"将从:\n{sourceFolderPath}\n复制到:\n{targetFolderPath}\n\n如果目标存在将被覆盖。继续吗？",
                    "继续",
                    "取消"
                );

                if (!shouldProceed)
                {
                    return;
                }

                FileUtil.ReplaceDirectory(sourceFolderPath, targetFolderPath);

                // 刷新AssetDatabase以确保Unity识别新文件
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("成功", $"文件夹 '{folderName}' 已成功复制到 StreamingAssets", "确定");
                Debug.Log($"成功将 '{sourceFolderPath}' 复制到 '{targetFolderPath}'");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("错误", $"复制过程中出现错误: {e.Message}", "确定");
                Debug.LogError($"复制错误: {e}");
            }
        }
    }
}
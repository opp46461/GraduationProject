using SharedCode;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

public class Launcher : MonoBehaviour, IRun
{
    private const string LocalManifestFile = "abManifest.txt";

    // 本地清单寄存器
    private ManifestData localManifestData;


    private void Start()
    {
        // 锁死分辨率
        Screen.SetResolution(1280, 720, true);

        Run();
    }

    public void Run()
    {
        // 主线程调度器（用于子线程回调回到主线程执行）
        UnityMainThreadDispatcher.Instance.Initialize();

        StartCoroutine(Wait_ServerDataHandle_InitializeComplete());
    }

    IEnumerator Wait_ServerDataHandle_InitializeComplete()
    {
        // 初始化资源加载模式
        ResManager.InitializeAssetLoadMode();
        // 加载本地清单
        if (ResManager.AssetLoadModel != ResManager.AssetLoadMode.Editor)
        {
            LoadLocalManifest();
            if (localManifestData.IsEmpty())
            {
                Debug.LogError("加载本地清单失败！检查路径及清单格式！");
                yield break;
            }
        }

        // 先等待图形设备初始化完成
        //GraphicsSafetyManager.Instance.ExecuteSafelyCoroutine(
        //    ResManager.Instance.Initialize(localManifestData, InitializationModule), "Initialize");
        StartCoroutine(ResManager.Instance.Initialize(localManifestData, InitializationModule));
    }

    /// <summary>
    /// 初始化各模块 → 运行游戏全局控制器
    /// </summary>
    /// <param name="progress"></param>
    void InitializationModule(float progress)
    {
        if (progress >= 1 && ResManager.Instance.IsInitialized)
        {
            Debug.Log("开始模块初始化！");

            // 初始化UI框架（加载UI框架预设、初始化UI管理器）
            UI_Manager.Initialize();

            // 打印下分辨率
            //LogCurrentResolution();

            // 初始化计时器对象池
            TimerManager.Instance.Initialize();

            // 初始化 本地配置数据管理
            LocalDataManager.Instance.Initialize();
        }
    }

    private void LoadLocalManifest()
    {
        localManifestData = new ManifestData();
        string path = ResManager.GetLocalManifestDataPath(LocalManifestFile);
        Debug.Log("加载AB本地清单：" + path);
        if (!File.Exists(path))
        {
            Debug.LogError("加载AB本地清单失败：" + path);
            return;
        }

        // 获取数据
        localManifestData = ManifestData.Deserialization(path);
    }

    public void LogCurrentResolution()
    {
        // 方法1：使用Screen.currentResolution（全屏时的分辨率）
        Resolution currentRes = Screen.currentResolution;
        Debug.Log($"当前分辨率（Screen.currentResolution）: {currentRes.width} x {currentRes.height} @ {currentRes.refreshRateRatio}Hz");

        // 方法2：使用Screen.width和Screen.height（当前窗口/屏幕的实际尺寸）
        Debug.Log($"当前窗口尺寸: {Screen.width} x {Screen.height}");

        // 方法3：获取全屏状态
        Debug.Log($"全屏模式: {Screen.fullScreen}");

        // 方法4：获取所有支持的分辨率
        Resolution[] resolutions = Screen.resolutions;
        Debug.Log($"系统支持 {resolutions.Length} 种分辨率");

        // 显示最大的分辨率
        if (resolutions.Length > 0)
        {
            Resolution maxRes = resolutions[resolutions.Length - 1];
            Debug.Log($"最大支持分辨率: {maxRes.width} x {maxRes.height} @ {maxRes.refreshRateRatio}Hz");
        }
    }
}

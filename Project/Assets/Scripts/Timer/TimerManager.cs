using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 计时器管理器
/// 基于对象池的线程安全计时器管理系统
/// 支持单次计时、循环计时和无限循环计时
/// 提供完整的生命周期追踪和状态监控
/// 计时器在完成或取消后自动回收
/// </summary>
public class TimerManager : UnitySingleton<TimerManager>
{
    #region 私有字段

    /// <summary>
    /// 计时器对象池
    /// </summary>
    private IObjectPool<ThreadSafePooledTimer> _timerPool;

    /// <summary>
    /// 活跃计时器列表
    /// </summary>
    private List<ThreadSafePooledTimer> _activeTimers = new List<ThreadSafePooledTimer>();

    /// <summary>
    /// ID到活跃计时器的映射（用于快速查找）
    /// </summary>
    private Dictionary<string, ThreadSafePooledTimer> _activeTimersById = new Dictionary<string, ThreadSafePooledTimer>();

    /// <summary>
    /// 列表访问锁（确保线程安全）
    /// </summary>
    private readonly object _listLock = new object();

    /// <summary>
    /// 管理器初始化状态
    /// </summary>
    private bool _isInitialized = false;

    // 添加主线程回调队列
    private Queue<Action> _mainThreadCallbacks = new Queue<Action>();
    private readonly object _callbackLock = new object();

    #endregion

    #region 公共属性

    /// <summary>
    /// 活跃计时器数量
    /// </summary>
    public int ActiveTimerCount
    {
        get
        {
            lock (_listLock)
            {
                return _activeTimers.Count;
            }
        }
    }

    /// <summary>
    /// 对象池中可用计时器数量
    /// </summary>
    public int AvailableTimerCount => _timerPool?.Count ?? 0;

    /// <summary>
    /// 管理器是否已初始化
    /// </summary>
    public bool IsInitialized => _isInitialized;

    #endregion

    #region 初始化方法

    /// <summary>
    /// 初始化计时器管理器
    /// 创建计时器对象池并配置参数
    /// </summary>
    /// <param name="config">对象池配置（可选，使用默认配置如果为null）</param>
    public void Initialize(PoolConfig config = null)
    {
        if (_isInitialized)
        {
            Debug.LogWarning("计时器管理器已经初始化，跳过重复初始化");
            return;
        }

        // 使用默认配置或自定义配置
        config = config ?? new PoolConfig();
        config.InitialSize = 0;

        // 从PoolManager获取计时器对象池
        _timerPool = PoolManager.Instance.GetPool<ThreadSafePooledTimer>(
            config: config
        );

        _isInitialized = true;
        Debug.Log("计时器管理器初始化完成");
    }

    #endregion

    #region 主线程回调
    private void Update()
    {
        // 每帧执行所有主线程回调
        lock (_callbackLock)
        {
            while (_mainThreadCallbacks.Count > 0)
            {
                try
                {
                    var callback = _mainThreadCallbacks.Dequeue();
                    callback?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"主线程回调执行错误: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 添加主线程回调到执行队列
    /// </summary>
    public void EnqueueMainThreadCallback(Action callback)
    {
        if (callback == null) return;

        lock (_callbackLock)
        {
            _mainThreadCallbacks.Enqueue(callback);
        }
    }
    #endregion

    #region 计时器创建方法

    /// <summary>
    /// 从对象池获取计时器实例
    /// 包含重试机制，确保返回可用的计时器
    /// </summary>
    /// <returns>可用的计时器实例</returns>
    public ThreadSafePooledTimer Get()
    {
        if (!_isInitialized)
        {
            Debug.LogError("计时器管理器未初始化，请先调用Initialize()");
            return null;
        }

        ThreadSafePooledTimer timer = _timerPool.Get();
        if (timer == null)
        {
            Debug.LogError("计时器管理器 获取到空的计时器，请检查代码！");
            return null;
        }
        _activeTimers.Add(timer);
        _activeTimersById.Add(timer.ID, timer);
        return timer;
    }

    /// <summary>
    /// 创建单次计时器【确保在主线程调用该接口】
    /// 计时器完成后会自动回收
    /// </summary>
    /// <param name="onAutoRecycle">非临时计时器，一律需要在自动回收时，设置为空</param>
    /// <param name="duration">计时时长（秒）</param>
    /// <param name="onComplete">完成回调</param>
    /// <param name="onUpdate">更新回调（每帧调用，参数为剩余时间）</param>
    /// <param name="useUnscaledTime">是否使用非缩放时间（忽略Time.timeScale）</param>
    /// <returns>计时器实例</returns>
    public ThreadSafePooledTimer CreateTimer(Action<ThreadSafePooledTimer> onAutoRecycle, float duration, Action<ThreadSafePooledTimer> onComplete = null, Action<ThreadSafePooledTimer, float> onUpdate = null, bool useUnscaledTime = false)
    {
        var timer = Get();
        if (timer == null) return null;

        if (!timer.Initialize(onAutoRecycle, duration, onComplete, onUpdate, useUnscaledTime, Recycle))
        {
            Debug.LogError("创建单次计时器失败");
            //SafeRecycleTimer(timer);
            return null;
        }
         
        return timer;
    }

    /// <summary>
    /// 创建循环计时器
    /// 计时器完成后会自动回收
    /// </summary>
    /// <param name="onAutoRecycle">非临时计时器，一律需要在自动回收时，设置为空</param>
    /// <param name="interval">循环间隔（秒）</param>
    /// <param name="loopCount">循环次数（0表示无限循环）</param>
    /// <param name="onLoop">每次循环回调（参数为当前循环次数）</param>
    /// <param name="onComplete">最终完成回调</param>
    /// <param name="onUpdate">更新回调</param>
    /// <param name="useUnscaledTime">是否使用非缩放时间</param>
    /// <returns>计时器实例</returns>
    public ThreadSafePooledTimer CreateLoopTimer(Action<ThreadSafePooledTimer> onAutoRecycle, float interval, int loopCount, Action<ThreadSafePooledTimer, int> onLoop, Action<ThreadSafePooledTimer> onComplete = null, Action<ThreadSafePooledTimer, float> onUpdate = null, bool useUnscaledTime = false)
    {
        var timer = Get();
        if (timer == null) return null;

        // 直接使用用户提供的回调，计时器完成后会自动回收
        if (!timer.InitializeLoop(onAutoRecycle, interval, loopCount, onLoop, onComplete, onUpdate, useUnscaledTime, Recycle))
        {
            Debug.LogError("创建循环计时器失败");
            //SafeRecycleTimer(timer);
            return null;
        }

        return timer;
    }

    /// <summary>
    /// 创建无限循环计时器
    /// 需要手动停止，停止后会自动回收
    /// </summary>
    /// <param name="onAutoRecycle">非临时计时器，一律需要在自动回收时，设置为空</param>
    /// <param name="interval">循环间隔（秒）</param>
    /// <param name="onLoop">每次循环回调（参数为当前循环次数）</param>
    /// <param name="onUpdate">更新回调</param>
    /// <param name="useUnscaledTime">是否使用非缩放时间</param>
    /// <returns>计时器实例</returns>
    public ThreadSafePooledTimer CreateInfiniteLoopTimer(Action<ThreadSafePooledTimer> onAutoRecycle, float interval, Action<ThreadSafePooledTimer, int> onLoop, Action<ThreadSafePooledTimer, float> onUpdate = null, bool useUnscaledTime = false)
    {
        // 无限循环：loopCount = 0，需要手动停止
        return CreateLoopTimer(onAutoRecycle, interval, 0, onLoop, null, onUpdate, useUnscaledTime);
    }

    #endregion

    #region 计时器管理和控制

    /// <summary>
    /// 通过ID获取计时器实例
    /// </summary>
    /// <param name="timerId">计时器ID</param>
    /// <returns>计时器实例，如果未找到返回null</returns>
    public ThreadSafePooledTimer GetTimerById(string timerId)
    {
        if (string.IsNullOrEmpty(timerId))
        {
            Debug.LogWarning("提供的计时器ID为空");
            return null;
        }

        lock (_listLock)
        {
            return _activeTimersById.TryGetValue(timerId, out var timer) ? timer : null;
        }
    }

    /// <summary>
    /// 停止指定计时器
    /// 停止后计时器会自动回收
    /// </summary>
    /// <param name="timer">要停止的计时器实例</param>
    /// <returns>是否成功发送停止信号</returns>
    public bool StopTimer(ThreadSafePooledTimer timer)
    {
        if (timer != null)
        {
            return timer.Stop();
            // 注意：停止后计时器会自动进入Cancelling状态，最终变为Cancelled并请求回收
        }
        return false;
    }

    /// <summary>
    /// 通过ID停止计时器
    /// 停止后计时器会自动回收
    /// </summary>
    /// <param name="timerId">要停止的计时器ID</param>
    /// <returns>是否成功找到并停止计时器</returns>
    public bool StopTimerById(string timerId)
    {
        var timer = GetTimerById(timerId);
        if (timer != null)
        {
            return StopTimer(timer);
        }

        Debug.LogWarning($"未找到ID为 {timerId} 的计时器");
        return false;
    }

    /// <summary>
    /// 计时器会自动回调该函数
    /// </summary>
    /// <param name="timer"></param>
    private void Recycle(ThreadSafePooledTimer timer)
    {
        if (timer == null) return;
        lock (_listLock)
        {
            if (_activeTimers.Contains(timer)) _activeTimers.Remove(timer);
            if (_activeTimersById.ContainsKey(timer.ID)) _activeTimersById.Remove(timer.ID);

            _timerPool.Recycle(timer);
        }
    }

    #endregion

    #region 清理和资源管理
    /// <summary>
    /// 强制清理所有计时器（目前只考虑在停止运行程序后执行）
    /// 立即停止并回收所有计时器，包括Faulted状态的计时器
    /// </summary>
    private void ForceClear()
    {
        List<ThreadSafePooledTimer> allTimers;
        lock (_listLock)
        {
            allTimers = new List<ThreadSafePooledTimer>(_activeTimers);
        }

        int recycledCount = 0;
        foreach (var timer in allTimers)
        {
            // 强制回收，不考虑状态限制
            timer.Dispose();
            //SafeRecycleTimer(timer);
            recycledCount++;
        }

        Debug.Log($"强制回收了 {recycledCount} 个计时器");
    }

    /// <summary>
    /// Unity对象销毁时的清理
    /// </summary>
    private void OnDestroy()
    {
        // 强制清理所有计时器
        ForceClear();

        Debug.Log("TimerManager 已销毁");
    }
    #endregion
}
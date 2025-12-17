using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 线程安全的可池化计时器
/// 支持单次计时、循环计时和无限循环计时
/// 包含完整的生命周期追踪和严格的状态转换验证
/// </summary>
public class ThreadSafePooledTimer : IDisposable
{
    #region 静态字段和ID生成

    // 静态ID计数器，确保每个计时器都有唯一ID
    private static long _idCounter = 0;
    private static readonly object _idLock = new object();

    #endregion

    #region 公共属性

    /// <summary>
    /// 计时器的唯一标识符
    /// </summary>
    public string ID { get; private set; }

    /// <summary>
    /// 计时器总时长（秒）
    /// </summary>
    public float Duration { get; private set; }

    /// <summary>
    /// 剩余时间（秒）
    /// </summary>
    public float RemainingTime { get; private set; }

    /// <summary>
    /// 进度（0到1）
    /// </summary>
    public float Progress => Duration > 0 ? 1f - (RemainingTime / Duration) : 0f;

    /// <summary>
    /// 当前运行状态
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// 是否为循环模式
    /// </summary>
    public bool IsLooping { get; private set; }

    /// <summary>
    /// 当前循环次数
    /// </summary>
    public int CurrentLoopCount { get; private set; }

    /// <summary>
    /// 计时器当前状态
    /// </summary>
    public TimerStatus Status { get; private set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; private set; }

    /// <summary>
    /// 开始运行时间
    /// </summary>
    public DateTime? StartTime { get; private set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; private set; }

    /// <summary>
    /// 最后状态更新时间
    /// </summary>
    public DateTime? LastUpdateTime { get; private set; }

    /// <summary>
    /// 总循环次数统计
    /// </summary>
    public int TotalLoopsCompleted { get; private set; }

    /// <summary>
    /// 总运行时间统计
    /// </summary>
    public TimeSpan TotalRunningTime { get; private set; }

    #endregion

    #region 私有字段

    // 线程安全的运行状态标志
    private volatile bool _isRunning;

    // 回调函数
    private Action<ThreadSafePooledTimer> _onRecycle;
    private Action<ThreadSafePooledTimer> _onComplete;
    private Action<ThreadSafePooledTimer, float> _onUpdate;
    private Action<ThreadSafePooledTimer, int> _onLoop;
    private Action<ThreadSafePooledTimer> _onAutoRecycle;

    // 异步任务控制
    private CancellationTokenSource _cancellationTokenSource;
    private float _startTime;
    private bool _useUnscaledTime;
    private int _maxLoopCount;

    // 线程同步锁
    private readonly object _lockObject = new object();

    // 用来确保只执行一次取消回调
    private bool cancelCallbackEntered = false;

    #endregion

    #region 状态枚举和转换规则

    /// <summary>
    /// 计时器状态枚举
    /// </summary>
    public enum TimerStatus
    {
        /// <summary>默认状态，一出生就是该状态</summary>
        Default,
        /// <summary>已创建但未初始化</summary>
        Created,
        /// <summary>已初始化参数但未开始</summary>
        Initialized,
        /// <summary>正在运行中</summary>
        Running,
        /// <summary>正在完成（执行最终回调）</summary>
        Completing,
        /// <summary>正常完成</summary>
        Completed,
        /// <summary>正在取消</summary>
        Cancelling,
        /// <summary>已被取消</summary>
        Cancelled,
        /// <summary>运行出错（调试状态，不可回收）</summary>
        Faulted,
        /// <summary>已回收至对象池</summary>
        Recycled
    }

    /// <summary>
    /// 检查状态转换是否合法
    /// 基于严格的状态机规则
    /// </summary>
    private bool IsValidTransition(TimerStatus from, TimerStatus to)
    {
        // 定义合法的状态转换规则
        switch (from)
        {
            case TimerStatus.Default:
                // 一出来必定需要被实例化生成
                return to == TimerStatus.Created;
            case TimerStatus.Created:
                // 已经生成后，只能走向初始化
                return to == TimerStatus.Initialized;

            case TimerStatus.Initialized:
                // 初始化后，要么没用，要么运行，要么回收
                return to == TimerStatus.Running || to == TimerStatus.Recycled;

            case TimerStatus.Running:
                return to == TimerStatus.Faulted
                    // 正常走到运行中
                    || to == TimerStatus.Completing
                    // 外部调用取消后
                    || to == TimerStatus.Cancelling;

            case TimerStatus.Completing:
                return to == TimerStatus.Faulted 
                    // 正常完成
                    || to == TimerStatus.Completed
                    // 外部调用取消后，主线程执行之前
                    || to == TimerStatus.Cancelling;

            case TimerStatus.Cancelling:
                return to == TimerStatus.Faulted
                    // 只会走到取消完成
                    || to == TimerStatus.Cancelled;

            case TimerStatus.Completed:
                // Completed 在主线程 同一帧 回到 池子
                return to == TimerStatus.Recycled;

            case TimerStatus.Cancelled:
                // Cancelled 取消成功后，不允许有任何操作，自然过渡到回收
                return to == TimerStatus.Recycled;

            case TimerStatus.Faulted:
                // Faulted 是调试状态，不能转换到任何其他状态
                return false;

            case TimerStatus.Recycled:
                // 从池子中拿出来，必定需要初始化
                return to == TimerStatus.Initialized;

            default:
                return false;
        }
    }

    /// <summary>
    /// 检查当前状态是否为中间状态（不允许重置的状态）
    /// </summary>
    private bool IsIntermediateState(TimerStatus status)
    {
        return status == TimerStatus.Initialized ||
               status == TimerStatus.Completed ||
               status == TimerStatus.Cancelled;
    }

    /// <summary>
    /// 检查状态是否允许回收
    /// </summary>
    private bool IsStateRecyclable(TimerStatus status)
    {
        return status == TimerStatus.Created ||
               status == TimerStatus.Initialized ||
               status == TimerStatus.Completed ||
               status == TimerStatus.Cancelled;
    }

    #endregion

    #region 构造函数和初始化

    /// <summary>
    /// 构造函数
    /// 自动生成唯一ID并初始化基础状态
    /// </summary>
    public ThreadSafePooledTimer()
    {
        // 生成唯一ID
        lock (_idLock)
        {
            ID = $"Timer_{Interlocked.Increment(ref _idCounter):D8}";
        }

        // 初始化基础属性
        CreatedTime = DateTime.UtcNow;
        SafeSetStatus(TimerStatus.Created);

        Debug.Log($"[{ID}] 计时器实例已创建，时间：{DateTime.UtcNow}");
    }

    /// <summary>
    /// 检查计时器是否可安全重用
    /// 必须处于Recycled状态
    /// </summary>
    public bool IsSafeToReuse
    {
        get
        {
            lock (_lockObject)
            {
                return Status == TimerStatus.Recycled;
            }
        }
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 初始化单次计时器
    /// </summary>
    /// <param name="onAutoRecycle">自动回收前回调</param>
    /// <param name="duration">计时时长（秒）</param>
    /// <param name="onComplete">完成回调</param>
    /// <param name="onUpdate">更新回调（每帧调用）</param>
    /// <param name="useUnscaledTime">是否使用非缩放时间（忽略Time.timeScale）</param>
    /// <returns>是否初始化成功</returns>
    public bool Initialize(Action<ThreadSafePooledTimer> onAutoRecycle, float duration, Action<ThreadSafePooledTimer> onComplete = null, Action<ThreadSafePooledTimer, float> onUpdate = null, bool useUnscaledTime = false, Action<ThreadSafePooledTimer> onRecycle = null)
    {
        lock (_lockObject)
        {
            // 检查当前状态是否允许初始化（必须是Created/Recycled状态）
            if (Status != TimerStatus.Created && Status != TimerStatus.Recycled)
            {
                Debug.LogError($"[{ID}] 无法在状态 {Status} 下初始化循环计时器，必须为Created或者Recycled状态");
                return false;
            }

            // 设置新参数
            Duration = duration;
            RemainingTime = duration;
            _onComplete = onComplete;
            // 自动回收
            _onAutoRecycle = onAutoRecycle;
            _onComplete += AutoRecycle;
            _onUpdate = onUpdate;
            _useUnscaledTime = useUnscaledTime;
            _onRecycle = onRecycle;
            IsLooping = false;
            _maxLoopCount = 0;

            if (!SafeSetStatus(TimerStatus.Initialized))
            {
                Debug.LogError($"[{ID}] 设置Initialized状态失败");
                return false;
            }

            // 刷新取消回调进入判断
            cancelCallbackEntered = false;

            Debug.Log($"[{ID}] 单次计时器初始化: {duration}s");
            return true;
        }
    }

    /// <summary>
    /// 初始化循环计时器
    /// </summary>
    /// <param name="onAutoRecycle">自动回收前回调</param>
    /// <param name="interval">循环间隔（秒）</param>
    /// <param name="maxLoopCount">最大循环次数（0表示无限循环）</param>
    /// <param name="onLoop">每次循环回调</param>
    /// <param name="onComplete">最终完成回调</param>
    /// <param name="onUpdate">更新回调</param>
    /// <param name="useUnscaledTime">是否使用非缩放时间</param>
    /// <returns>是否初始化成功</returns>
    public bool InitializeLoop(Action<ThreadSafePooledTimer> onAutoRecycle, float interval, int maxLoopCount, Action<ThreadSafePooledTimer, int> onLoop, Action<ThreadSafePooledTimer> onComplete = null, Action<ThreadSafePooledTimer, float> onUpdate = null, bool useUnscaledTime = false, Action<ThreadSafePooledTimer> onRecycle = null)
    {
        lock (_lockObject)
        {
            // 检查当前状态是否允许初始化（必须是Created/Recycled状态）
            if (Status != TimerStatus.Created && Status != TimerStatus.Recycled)
            {
                Debug.LogError($"[{ID}] 无法在状态 {Status} 下初始化循环计时器，必须为Created或者Recycled状态");
                return false;
            }

            Duration = interval;
            RemainingTime = interval;
            _onComplete = onComplete;
            // 自动回收
            _onAutoRecycle = onAutoRecycle;
            _onComplete += AutoRecycle;
            _onUpdate = onUpdate;
            _onLoop = onLoop;
            _onRecycle = onRecycle;
            _useUnscaledTime = useUnscaledTime;
            IsLooping = true;
            CurrentLoopCount = 0;
            _maxLoopCount = maxLoopCount;

            if (!SafeSetStatus(TimerStatus.Initialized))
            {
                Debug.LogError($"[{ID}] 设置Initialized状态失败");
                return false;
            }

            // 刷新取消回调进入判断
            cancelCallbackEntered = false;

            Debug.Log($"[{ID}] 循环计时器初始化: 间隔{interval}s, 循环{maxLoopCount}次");
            return true;
        }
    }

    /// <summary>
    /// 开始计时器
    /// 异步运行，不会阻塞调用线程
    /// </summary>
    /// <returns>是否成功启动</returns>
    public bool Start()
    {
        CancellationToken token;
        bool shouldStart = false;

        Debug.Log($"[{ID}] 开始 {Status} ");
        lock (_lockObject)
        {
            // 检查当前状态是否允许启动
            if (Status != TimerStatus.Initialized)
            {
                Debug.LogError($"[{ID}] 无法在状态 {Status} 下启动计时器");
                return false;
            }

            // 如果已经在运行，直接返回
            if (_isRunning)
            {
                Debug.LogWarning($"[{ID}] 计时器已在运行状态");
                return false;
            }

            // 确保取消令牌源有效
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
            }

            // 设置运行状态
            _isRunning = true;
            RemainingTime = Duration;

            // 记录开始时间（Unity引擎时间）
            _startTime = _useUnscaledTime ? Time.unscaledTime : Time.time;

            token = _cancellationTokenSource.Token;
            shouldStart = true;

            if (!SafeSetStatus(TimerStatus.Running))
            {
                _isRunning = false;
                return false;
            }
        }

        // 如果条件不满足，不启动
        if (!shouldStart) return false;

        // 异步启动计时器逻辑（_ =：这是一个"丢弃运算符"，表示我们不关心这个异步任务的返回值）
        // 使用 _ = 避免编译器警告"未等待的异步调用"
        _ = RunTimerAsync(token);
        return true;
    }

    /// <summary>
    /// 停止计时器（非即时停止和回收）
    /// </summary>
    /// <returns>是否成功停止，以及走回收流程？</returns>
    public bool Stop()
    {
        lock (_lockObject)
        {
            if (Status == TimerStatus.Recycled)
            {
                Debug.LogWarning($"[{ID}] Recycled 状态下，应该在池子中，这里想 Stop，请检查代码！");
                return false;
            }
            if (Status == TimerStatus.Cancelling)
            {
                Debug.Log($"[{ID}] Cancelling 状态表示正在被取消，不需要再次 Stop，请确保调用无误！");
                return false;
            }

            // 如果当前状态允许被回收
            if (IsStateRecyclable(Status))
            {
                if (Status == TimerStatus.Completed)
                {
                    Debug.LogWarning($"[{ID}] Completed 状态下，应该会在同一帧自动回收，这里想 Stop，请检查代码！");
                    return false;
                }
                if (Status == TimerStatus.Cancelled)
                {
                    Debug.LogWarning($"[{ID}] Cancelled 状态下，会自动回收，不需要再额外停止！");
                    return false;
                }
                if (Status == TimerStatus.Created)
                {
                    if (_onComplete != null)
                    {
                        Debug.LogError($"[{ID}] 状态为 Created ，但 _onComplete 不为空，有异常！");
                        return false;
                    }
                    if (_onUpdate != null)
                    {
                        Debug.LogError($"[{ID}] 状态为 Created ，但 _onUpdate 不为空，有异常！");
                        return false;
                    }
                    if (_onLoop != null)
                    {
                        Debug.LogError($"[{ID}] 状态为 Created ，但 _onLoop 不为空，有异常！");
                        return false;
                    }
                    if (_cancellationTokenSource != null)
                    {
                        Debug.LogError($"[{ID}] 状态为 Created ，但 _cancellationTokenSource 不为空，有异常！");
                        return false;
                    }

                    Debug.Log($"[{ID}] 状态为 Created ，还没初始化就被停止了！");
                    Recycle();
                    return true;
                }
                if (Status == TimerStatus.Initialized)
                {
                    // 释放
                    SafeDispose();
                    Debug.Log($"[{ID}] 状态为 Initialized ，还没开始就被停止了！");
                    Recycle();
                }
                return true;
            }


            // 设置取消状态并发送取消信号
            if (!SafeSetStatus(TimerStatus.Cancelling))
            {
                Debug.LogError($"[{ID}] 设置Cancelling状态失败");
                return false;
            }

            _isRunning = false;
            _cancellationTokenSource?.Cancel();

            // 广播事件出去（带信息），如果回调函数中有走协程，而又需要停止协程，则需要用到
            TimerInfo timerInfo = GetTimerInfo();
            SimplifyEventMgr.Emit(10041, timerInfo);

            Debug.Log($"[{ID}] 计时器停止请求已发送");
            return true;
        }
    }

    /// <summary>
    /// 获取计时器详细信息
    /// 用于调试和监控
    /// </summary>
    public TimerInfo GetTimerInfo()
    {
        lock (_lockObject)
        {
            return new TimerInfo
            {
                Id = ID,
                Status = Status,
                CreatedTime = CreatedTime,
                StartTime = StartTime,
                EndTime = EndTime,
                Duration = Duration,
                RemainingTime = RemainingTime,
                Progress = Progress,
                IsLooping = IsLooping,
                CurrentLoopCount = CurrentLoopCount,
                MaxLoopCount = _maxLoopCount,
                TotalLoopsCompleted = TotalLoopsCompleted,
                TotalRunningTime = TotalRunningTime,
                LastUpdateTime = LastUpdateTime
            };
        }
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 安全设置计时器状态（线程安全）
    /// 包含状态转换验证，只允许合法的状态转换
    /// </summary>
    private bool SafeSetStatus(TimerStatus newStatus)
    {
        lock (_lockObject)
        {
            var oldStatus = Status;

            // 检查状态转换是否合法
            if (!IsValidTransition(oldStatus, newStatus))
            {
                Debug.LogError($"[{ID}] 非法的状态转换: {oldStatus} -> {newStatus}");
                return false;
            }

            Status = newStatus;
            LastUpdateTime = DateTime.UtcNow;

            // 记录重要时间点
            if (newStatus == TimerStatus.Running)
            {
                StartTime = DateTime.UtcNow;
                EndTime = null;
            }
            else if (newStatus == TimerStatus.Completed ||
                     newStatus == TimerStatus.Cancelled ||
                     newStatus == TimerStatus.Faulted)
            {
                EndTime = DateTime.UtcNow;
                if (StartTime.HasValue)
                {
                    TotalRunningTime += EndTime.Value - StartTime.Value;
                }
            }

            // 调试日志
            Debug.Log($"[{ID}] 状态变更: {oldStatus} -> {newStatus}，时间：{DateTime.UtcNow}，_isRunning = {_isRunning}");
            return true;
        }
    }

    /// <summary>
    /// 异常取消回调
    /// </summary>
    private void ExceptionCancelCallback()
    {
        Debug.Log($"[{ID}] 计时器操作被取消 → 通过异常捕获 → OperationCanceledException");
        CancelCallback();
    }
    /// <summary>
    /// 一般取消回调
    /// </summary>
    private void CancelCallback()
    {
        Debug.Log($"[{ID}] 计时器取消回调 → CancelCallback ， 回调前的状态：{Status} ， 是否已进入过回调：{cancelCallbackEntered}");
        // 确保只执行一次该回调
        if (cancelCallbackEntered) return;
        // 以下状态不允许取消：Completed,Cancelling,Cancelled,Faulted,Recycled


        // 确保只执行一次该回调
        cancelCallbackEntered = true;
        SafeSetStatus(TimerStatus.Cancelled);

        // 成功取消后，回到主线程调用自动回收
        _ = ExecuteOnMainThread(() =>
        {
            AutoRecycle(this);
        });
    }

    /// <summary>
    /// 需要手动确保在主线程中执行（会在最终执行）
    /// </summary>
    private void AutoRecycle(ThreadSafePooledTimer threadSafePooledTimer)
    {
        _onAutoRecycle?.Invoke(threadSafePooledTimer);
        Debug.Log($"[{ID}] 开始自动回收， 时间：{DateTime.UtcNow}");
        // 释放资源
        if (!SafeDispose())
        {
            Debug.LogError($"[{ID}] 自动回收失败， 时间：{DateTime.UtcNow}");
            return;
        }
        // 回收
        Recycle();
    }

    private void Recycle()
    {
        // 变更状态
        SafeSetStatus(TimerStatus.Recycled);
        // 最后再回到池
        _onRecycle?.Invoke(this);
    }

    #region Task 相关
    /// <summary>
    /// 运行计时器的主要异步逻辑
    /// </summary>
    private async Task RunTimerAsync(CancellationToken token)
    {
        // 主循环：检查是否应该继续运行
        while (!token.IsCancellationRequested)
        {
            bool shouldContinue;
            lock (_lockObject)
            {
                // 检查运行条件：
                // 1. 仍在运行状态
                // 2. 循环模式：无限循环或未达到最大循环次数
                // 3. 单次模式：还有剩余时间
                shouldContinue = (_isRunning &&
                    ((IsLooping && (_maxLoopCount == 0 || CurrentLoopCount < _maxLoopCount)) ||
                     (!IsLooping && RemainingTime > 0)));

                if (!shouldContinue) break;
            }

            // 等待下一帧（关键：让出控制权，允许其他任务执行，即不阻塞线程）
            await Task.Yield();

            // 再次检查取消令牌
            if (token.IsCancellationRequested)
            {
                Debug.Log($"[{ID}] 计时器操作被取消 → RunTimerAsync while");
                CancelCallback();
                return;
            }

            // 更新时间
            float currentTime;
            lock (_lockObject)
            {
                // 再次检查运行状态
                if (!_isRunning)
                {
                    Debug.Log($"[{ID}] 计时器不在 Running ，请检查代码流程！");
                    break;
                }

                currentTime = _useUnscaledTime ? Time.unscaledTime : Time.time;
                float elapsed = currentTime - _startTime;
                RemainingTime = Mathf.Max(0f, Duration - elapsed);
            }

            // 调用更新回调（确保在主线程执行）
            if (_onUpdate != null)
            {
                await ExecuteOnMainThread(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        _onUpdate?.Invoke(this, RemainingTime);
                    }
                    else
                    {
                        Debug.Log($"[{ID}] 计时器操作被取消 → RunTimerAsync _onUpdate");
                        CancelCallback();
                    }
                });
            }

            // 检查是否到达间隔时间
            if (RemainingTime <= 0)
            {
                // 等待 HandleTimerCompletion 执行完后，再继续后续逻辑
                await HandleTimerCompletion(currentTime, token);
                // 确保及时走取消回调
                if (token.IsCancellationRequested)
                {
                    Debug.Log($"[{ID}] 计时器操作被取消 → RunTimerAsync RemainingTime");
                    CancelCallback();
                    break;
                }
            }
        }

        // 确保必定会走取消回调
        if (token.IsCancellationRequested)
        {
            Debug.Log($"[{ID}] 计时器操作被取消 → RunTimerAsync end");
            CancelCallback();
            return;
        }
        try
        {

        }
        catch (OperationCanceledException)
        {
            // 触发条件：当使用支持 CancellationToken 的异步API时，如果token被取消，这些API会主动抛出 OperationCanceledException
            //          只有在 HandleTimerCompletion 或其他地方使用了，类似 Task.Delay(..., token) 这样的API时才会进入这里
            ExceptionCancelCallback();
        }
        catch (Exception ex)
        {
            // 其他异常，记录错误并设置错误状态
            Debug.LogError($"[{ID}] 计时器运行错误: {ex.Message}");
            SafeSetStatus(TimerStatus.Faulted);
        }
    }

    /// <summary>
    /// 处理计时器完成逻辑
    /// </summary>
    private async Task HandleTimerCompletion(float currentTime, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            Debug.Log($"[{ID}] 计时器操作被取消 → HandleTimerCompletion");
            CancelCallback();
            return;
        }

        if (IsLooping)
        {
            // 执行循环回调
            if (_onLoop != null)
            {
                await ExecuteOnMainThread(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        lock (_lockObject)
                        {
                            CurrentLoopCount++;
                            TotalLoopsCompleted++;
                            Debug.Log($"[{ID}] 循环回调: 第{CurrentLoopCount}次");
                            _onLoop?.Invoke(this, CurrentLoopCount);
                        }
                    }
                    else
                    {
                        Debug.Log($"[{ID}] 计时器操作被取消 → HandleTimerCompletion _onLoop");
                        CancelCallback();
                    }
                });
            }

            // 检查是否还有下一次循环
            bool hasNextLoop;
            lock (_lockObject)
            {
                hasNextLoop = (_maxLoopCount == 0 || CurrentLoopCount < _maxLoopCount);
                if (hasNextLoop)
                {
                    // 重置时间为下一次循环
                    RemainingTime = Duration;
                    _startTime = currentTime;
                }
            }

            // 确保执行下面代码前是非取消的
            if (token.IsCancellationRequested)
            {
                Debug.Log($"[{ID}] 计时器操作被取消 → HandleTimerCompletion IsLooping");
                CancelCallback();
                return;
            }

            // 如果没有下一次循环，完成计时器
            if (!hasNextLoop)
            {
                // 设置Completing状态
                if (!SafeSetStatus(TimerStatus.Completing))
                {
                    Debug.LogError($"[{ID}] 设置Completing状态失败");
                    return;
                }

                // 执行完成回调
                if (_onComplete != null)
                {
                    await ExecuteOnMainThread(() =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            // 设置Completed状态
                            SafeSetStatus(TimerStatus.Completed);

                            Debug.Log($"[{ID}] 循环计时器最终完成");
                            _onComplete?.Invoke(this);
                        }
                        else
                        {
                            Debug.Log($"[{ID}] 计时器操作被取消 → HandleTimerCompletion loop _onComplete");
                            CancelCallback();
                        }
                    });
                }
                else
                {
                    // 设置Completed状态
                    SafeSetStatus(TimerStatus.Completed);
                }
            }
        }
        else
        {
            // 设置Completing状态
            if (!SafeSetStatus(TimerStatus.Completing))
            {
                Debug.LogError($"[{ID}] 设置Completing状态失败");
                return;
            }

            // 单次计时器完成
            if (_onComplete != null)
            {
                await ExecuteOnMainThread(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        // 设置Completed状态
                        SafeSetStatus(TimerStatus.Completed);
                        Debug.Log($"[{ID}] 单次计时器完成");
                        _onComplete?.Invoke(this);
                    }
                    else
                    {
                        Debug.Log($"[{ID}] 计时器操作被取消 → HandleTimerCompletion once _onComplete");
                        CancelCallback();
                    }
                });
            }
            else
            {
                // 设置Completed状态
                SafeSetStatus(TimerStatus.Completed);
            }
        }
    }

    /// <summary>
    /// 在主线程执行操作
    /// 使用UnityMainThreadDispatcher或降级方案
    /// </summary>
    private async Task ExecuteOnMainThread(Action action)
    {
        if (action == null) return;

        // 设置异步任务成功回调结果
        var completionSource = new TaskCompletionSource<bool>();

        // 将回调包装并提交到TimerManager的主线程队列
        Action wrappedAction = () =>
        {
            action();
            completionSource.SetResult(true);
            try
            {

            }
            catch (Exception ex)
            {
                completionSource.SetException(ex);
                Debug.LogError($"计时器在主线程执行操作错误：{ex}");
            }
        };

        // 通过TimerManager在主线程执行
        TimerManager.Instance?.EnqueueMainThreadCallback(wrappedAction);

        // 等待回调完成
        await completionSource.Task;
    }
    #endregion

    #endregion

    #region IDisposable实现

    /// <summary>
    /// 释放计时器资源
    /// 强制释放，不考虑状态限制（程序停止运行后需调用）
    /// </summary>
    public void Dispose()
    {
        lock (_lockObject)
        {
            // 强制停止运行
            _isRunning = false;
            _cancellationTokenSource?.Cancel();

            // 强制释放资源
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            // 清理回调引用
            _onComplete = null;
            _onUpdate = null;
            _onLoop = null;

            // 记录旧状态
            var oldStatus = Status;

            // 设置回收状态
            LastUpdateTime = DateTime.UtcNow;

            Debug.Log($"[{ID}] 计时器资源已安全释放，时间：{DateTime.UtcNow}");
        }
    }

    /// <summary>
    /// 安全释放计时器资源
    /// 在成功释放后调用回调
    /// </summary>
    /// <returns>是否释放成功</returns>
    public bool SafeDispose()
    {
        bool disposed = false;

        lock (_lockObject)
        {
            Debug.Log($"[{ID}] 安全释放 {Status} ，_isRunning = {_isRunning}");
            // 检查当前状态是否允许释放：非指定状态不允许释放资源
            if (!IsIntermediateState(Status))
            {
                Debug.LogError($"[{ID}] 无法在中间状态 {Status} 下释放计时器");
                return false;
            }

            _isRunning = false;
            // 清理取消令牌
            _cancellationTokenSource = null;

            // 清理回调引用
            _onComplete = null;
            _onUpdate = null;
            _onLoop = null;

            // 记录旧状态
            var oldStatus = Status;

            // 设置回收状态
            LastUpdateTime = DateTime.UtcNow;

            disposed = true;
            Debug.Log($"[{ID}] 计时器资源已安全释放，时间：{DateTime.UtcNow}");
        }
        return disposed;
    }
    #endregion
}

/// <summary>
/// 计时器信息结构
/// 用于调试、监控和序列化
/// </summary>
public struct TimerInfo : IEventData
{
    public string Id { get; set; }
    public ThreadSafePooledTimer.TimerStatus Status { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public float Duration { get; set; }
    public float RemainingTime { get; set; }
    public float Progress { get; set; }
    public bool IsLooping { get; set; }
    public int CurrentLoopCount { get; set; }
    public int MaxLoopCount { get; set; }
    public int TotalLoopsCompleted { get; set; }
    public TimeSpan TotalRunningTime { get; set; }
    public DateTime? LastUpdateTime { get; set; }

    /// <summary>
    /// 转换为可读字符串
    /// </summary>
    public override string ToString()
    {
        return $"Timer {Id}: {Status}, 进度: {Progress:P1}, 循环: {CurrentLoopCount}/{MaxLoopCount}, 运行: {TotalRunningTime.TotalSeconds:F2}s";
    }
}
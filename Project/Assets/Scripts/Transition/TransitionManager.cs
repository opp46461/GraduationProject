using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 异步操作步骤
[System.Serializable]
public class TransitionStep
{
    public string stepName;
    // Unity 协程操作
    public IEnumerator operation;
    // 权重，用于进度计算
    public float weight; 

    public TransitionStep(string name, IEnumerator op, float weight = 1f)
    {
        stepName = name;
        operation = op;
        this.weight = weight;
    }
}

// 跳转配置
public class TransitionConfig
{
    public List<TransitionStep> asyncSteps = new List<TransitionStep>();
    /// <summary>
    /// 是否启用增量GC
    /// </summary>
    public bool enableGCCollection = false;
    /// <summary>
    /// 默认3帧收集一次增量GC
    /// </summary>
    public int framesBetweenGC = 3;
    /// <summary>
    /// 完成后是否调用一次完整GC？
    /// </summary>
    public bool forceGCOnComplete = true;
}

// 跳转管理器
public class TransitionManager : UnitySingleton<TransitionManager>
{
    // 仅保留必要的事件
    private event Action<float> OnProgressUpdate;
    private event Action OnTransitionComplete;

    // 内部状态
    private bool isTransitioning = false;
    private TransitionConfig currentConfig;
    private Coroutine currentTransitionCoroutine;
    private float currentProgress = 0f;
    // 跟踪正在执行的步骤协程
    private List<Coroutine> currentStepCoroutines = new List<Coroutine>();

    // 公共属性
    public bool IsTransitioning => isTransitioning;
    public float CurrentProgress => currentProgress;


    #region StartTransition → 外部启动开始跳转业务
    /// <summary>
    /// 开始跳转流程
    /// </summary>
    /// <param name="onProgressUpdate">进度更新事件</param>
    /// <param name="onTransitionComplete">跳转完成事件</param>
    /// <param name="asyncSteps">异步操作步骤列表</param>
    /// <param name="config">跳转配置</param>
    public void StartTransition(Action<float> onProgressUpdate = null, Action onTransitionComplete = null, List<TransitionStep> asyncSteps = null, TransitionConfig config = null)
    {
        if (isTransitioning)
        {
            Debug.LogWarning("Transition is already in progress!");
            return;
        }

        // 清空事件，确保没有旧委托
        ClearEvents();

        // 注册新事件
        this.OnProgressUpdate += onProgressUpdate;
        this.OnTransitionComplete += onTransitionComplete;

        currentConfig = config ?? new TransitionConfig();

        // 设置异步步骤
        if (asyncSteps != null)
        {
            currentConfig.asyncSteps = asyncSteps;
        }

        isTransitioning = true;
        currentProgress = 0f;

        if (currentTransitionCoroutine != null)
        {
            StopCoroutine(currentTransitionCoroutine);
        }

        currentTransitionCoroutine = StartCoroutine(TransitionRoutine());
    }

    /// <summary>
    /// 开始跳转流程（AsyncOperation 版本）
    /// </summary>
    /// <param name="operations">Unity AsyncOperation 列表</param>
    /// <param name="onProgressUpdate">进度更新事件</param>
    /// <param name="onTransitionComplete">跳转完成事件</param>
    public void StartTransition(List<AsyncOperation> operations, Action<float> onProgressUpdate = null, Action onTransitionComplete = null)
    {
        var steps = new List<TransitionStep>();

        if (operations != null)
        {
            for (int i = 0; i < operations.Count; i++)
            {
                steps.Add(new TransitionStep($"Operation_{i}", WaitForAsyncOperation(operations[i]), 1f));
            }
        }

        StartTransition(onProgressUpdate, onTransitionComplete, steps);
    }
    // 等待 Unity AsyncOperation 的协程
    private IEnumerator WaitForAsyncOperation(AsyncOperation asyncOp)
    {
        while (!asyncOp.isDone)
        {
            yield return null;
        }
    }
    /// <summary>
    /// 开始跳转流程（IEnumerator 版本）
    /// </summary>
    /// <param name="coroutines">协程列表</param>
    /// <param name="onProgressUpdate">进度更新事件</param>
    /// <param name="onTransitionComplete">跳转完成事件</param>
    public void StartTransition(List<IEnumerator> coroutines, Action<float> onProgressUpdate = null, Action onTransitionComplete = null)
    {
        var steps = new List<TransitionStep>();

        if (coroutines != null)
        {
            for (int i = 0; i < coroutines.Count; i++)
            {
                steps.Add(new TransitionStep($"Step_{i}", coroutines[i], 1f));
            }
        }

        StartTransition(onProgressUpdate, onTransitionComplete, steps);
    }
    /// <summary>
    /// 开始跳转流程（混合 IEnumerator 、 AsyncOperation 、TransitionStep 版本）
    /// </summary>
    /// <param name="operations">混合操作列表</param>
    /// <param name="onProgressUpdate">进度更新事件</param>
    /// <param name="onTransitionComplete">跳转完成事件</param>
    public void StartTransition(List<object> operations, Action<float> onProgressUpdate = null, Action onTransitionComplete = null)
    {
        var steps = ConvertMixedOperationsToSteps(operations);
        StartTransition(onProgressUpdate, onTransitionComplete, steps);
    }
    // 转换混合操作到步骤列表
    private List<TransitionStep> ConvertMixedOperationsToSteps(List<object> operations)
    {
        var steps = new List<TransitionStep>();

        if (operations != null)
        {
            for (int i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                TransitionStep step = null;

                switch (operation)
                {
                    case IEnumerator coroutine:
                        step = new TransitionStep($"Coroutine_Step_{i}", coroutine, 1f);
                        break;

                    case AsyncOperation asyncOp:
                        step = new TransitionStep($"AsyncOperation_{i}", WaitForAsyncOperation(asyncOp), 1f);
                        break;

                    case TransitionStep existingStep:
                        // 如果已经是 TransitionStep，直接使用
                        step = existingStep;
                        break;

                    default:
                        Debug.LogWarning($"Unsupported operation type at index {i}: {operation?.GetType().Name}");
                        break;
                }

                if (step != null)
                {
                    steps.Add(step);
                }
            }
        }

        return steps;
    }
    #endregion

    #region 核心跳转流程
    // 核心跳转协程
    private IEnumerator TransitionRoutine()
    {
        Debug.Log("Transition: Starting transition routine");

        // 执行异步操作
        if (currentConfig.asyncSteps.Count > 0)
        {
            Debug.Log($"Transition: Executing {currentConfig.asyncSteps.Count} async operations");
            yield return StartCoroutine(ExecuteAsyncOperations());
        }
        else
        {
            Debug.Log("Transition: No async operations, completing immediately");
        }

        // 最终清理
        Debug.Log("Transition: Complete");

        if (currentConfig.forceGCOnComplete)
        {
            yield return StartCoroutine(PerformFinalGC());
        }

        OnTransitionComplete?.Invoke();
        isTransitioning = false;
        currentTransitionCoroutine = null;
    }

    // 执行所有异步操作
    private IEnumerator ExecuteAsyncOperations()
    {
        int totalSteps = currentConfig.asyncSteps.Count;

        if (totalSteps == 0)
        {
            UpdateProgress(1f);
            yield break;
        }

        float totalWeight = CalculateTotalWeight();
        float completedWeight = 0f;
        int frameCount = 0;

        for (int i = 0; i < totalSteps; i++)
        {
            // 检查是否被强制停止
            if (!isTransitioning)
            {
                Debug.LogWarning("Transition was force completed, stopping step execution");
                yield break;
            }

            var step = currentConfig.asyncSteps[i];
            Debug.Log($"Transition: Starting step {i + 1}/{totalSteps}: {step.stepName}");

            float stepStartTime = Time.realtimeSinceStartup;

            // 执行异步操作
            if (step.operation != null)
            {
                // 启动步骤协程并跟踪它
                Coroutine stepCoroutine = StartCoroutine(ExecuteSingleStep(step.operation, step.stepName));
                currentStepCoroutines.Add(stepCoroutine);

                // 等待步骤完成
                yield return stepCoroutine;

                // 从跟踪列表中移除
                currentStepCoroutines.Remove(stepCoroutine);
            }
            else
            {
                Debug.LogWarning($"Transition: Step {step.stepName} has no operation, skipping");
                yield return null;
            }

            float stepDuration = Time.realtimeSinceStartup - stepStartTime;
            Debug.Log($"Transition: Step {step.stepName} took {stepDuration:F2} seconds");

            // 更新进度
            completedWeight += step.weight;
            UpdateProgress(completedWeight / totalWeight);

            // 增量GC收集（ 每 framesBetweenGC 帧执行一次轻量级GC）
            if (currentConfig.enableGCCollection && frameCount % currentConfig.framesBetweenGC == 0)
            {
                TriggerIncrementalGC();
            }

            frameCount++;

            Debug.Log($"Transition: Completed step {i + 1}/{totalSteps}: {step.stepName}");
        }

        // 最终进度更新
        UpdateProgress(1f);

        // 清空步骤跟踪
        currentStepCoroutines.Clear();
    }

    // 执行单个步骤，包含强制停止检查
    private IEnumerator ExecuteSingleStep(IEnumerator operation, string stepName)
    {
        if (operation == null)
            yield break;

        // 手动迭代协程，以便在每一步检查是否被强制停止
        while (operation.MoveNext())
        {
            // 检查是否被强制停止
            if (!isTransitioning)
            {
                Debug.LogWarning($"Step {stepName} was interrupted by force complete");
                yield break;
            }

            yield return operation.Current;
        }
    }

    // 计算总权重
    private float CalculateTotalWeight()
    {
        float total = 0f;
        foreach (var step in currentConfig.asyncSteps)
        {
            total += step.weight;
        }
        return total;
    }

    // 更新进度
    private void UpdateProgress(float progress)
    {
        currentProgress = Mathf.Clamp01(progress);
        OnProgressUpdate?.Invoke(currentProgress);
        Debug.Log($"Transition: Progress updated to {currentProgress:P0}");
    }
    #endregion

    #region GC 相关
    // 触发增量GC
    private void TriggerIncrementalGC()
    {
        try
        {
            // 尝试进入"无GC区域"（申请1MB的无GC内存）
            if (System.GC.TryStartNoGCRegion(1024 * 1024))
            {
                // 如果成功进入无GC区域，立即结束（一个轻量级的GC操作）
                System.GC.EndNoGCRegion();
            }
            else
            {
                // 如果无法进入无GC区域，执行第0代GC
                // 第0代包含最新创建的对象，回收速度最快
                System.GC.Collect(0, System.GCCollectionMode.Forced, false);
            }

            Debug.Log("Transition: Incremental GC performed");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Incremental GC failed: {e.Message}");
        }
    }

    // 执行最终GC清理（跳转完成后执行彻底清理）
    private IEnumerator PerformFinalGC()
    {
        Debug.Log("Transition: Performing final GC collection");

        // 第一步：完整GC收集
        bool gcSuccess = PerformGCOperation(() =>
        {
            // 完整GC，回收所有代
            System.GC.Collect();

            // 等待挂起的终结器（后续检查内存使用情况时，再考虑）
            //System.GC.WaitForPendingFinalizers();
        }, "First GC collection");

        yield return null;  // 等待一帧，让GC工作完成

        // 第二步：再次GC收集，清理终结器产生的垃圾
        gcSuccess = PerformGCOperation(() =>
        {
            System.GC.Collect();  // 再次回收
        }, "Second GC collection");

        Debug.Log("Transition: Final GC collection completed");
    }

    // 执行GC操作，不包含yield（try 不允许用 yield，所以作为单独的查错操作）
    private bool PerformGCOperation(Action gcAction, string operationName)
    {
        try
        {
            gcAction?.Invoke();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"{operationName} failed: {e.Message}");
            return false;
        }
    }
    #endregion


    /// <summary>
    /// 强制完成当前跳转（谨慎调用，可能会出现报错，需要外部捕获异常）
    /// </summary>
    /// <param name="skipRemainingSteps">是否跳过剩余的异步步骤</param>
    public void ForceComplete(bool skipRemainingSteps = true)
    {
        if (!isTransitioning) return;

        Debug.Log("Transition: Force completing transition");

        // 停止主跳转协程
        if (currentTransitionCoroutine != null)
        {
            StopCoroutine(currentTransitionCoroutine);
            currentTransitionCoroutine = null;
        }

        // 停止所有步骤协程
        if (skipRemainingSteps)
        {
            foreach (var coroutine in currentStepCoroutines)
            {
                if (coroutine != null)
                {
                    StopCoroutine(coroutine);
                }
            }
            currentStepCoroutines.Clear();
        }

        // 更新进度为100%（如果跳过剩余步骤）
        if (skipRemainingSteps)
        {
            UpdateProgress(1f);
        }

        // 直接执行最终GC
        if (currentConfig != null && currentConfig.forceGCOnComplete)
        {
            StartCoroutine(PerformFinalGC());
        }

        OnTransitionComplete?.Invoke();
        isTransitioning = false;
    }


    // 清空事件
    private void ClearEvents()
    {
        OnProgressUpdate = null;
        OnTransitionComplete = null;
        Debug.Log("Transition: Events cleared");
    }
}
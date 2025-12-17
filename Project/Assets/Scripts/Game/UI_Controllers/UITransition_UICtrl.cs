using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using DG.Tweening;

/* 【说明】
 *    转场界面控制类：
 *      1. 完整转场事件业务发起者；
 *      2. 流程：外部执行转场前的操作 → 发送转场事件信号 → 该脚本收到信号后 播放进场动画 → 进场动画完毕执行 onEnterComplete
 *			→ 开启转场异步流程 → 异步流程结束后 播放退场动画 → 退场动画完毕执行 onExitComplete
 */

public class UITransition_UICtrl : UI_Ctrl 
{
	Image mask;
    TransitionData transitionData = null;
    ProhibitAllInteractionsData prohibitAllInteractionsData = new ProhibitAllInteractionsData();
    bool isTransitioning = false;

    public override void Awake() 
	{
		base.Awake();

		mask = GetT<Image>("Mask");

        SimplifyEventMgr.AddListener<TransitionData>(10026, Transition);
	}

    private void OnDestroy()
    {
		SimplifyEventMgr.RemoveListener<TransitionData>(10026, Transition);
    }

    void Start() 
	{

	}

    private void Transition(TransitionData data)
    {
        if (isTransitioning) return;
        isTransitioning = true;
        // 禁止所有交互
        prohibitAllInteractionsData.canInteractions = false;
        prohibitAllInteractionsData.TimeOut = false;
        SimplifyEventMgr.Emit(10027, prohibitAllInteractionsData);

        this.transitionData = data;
        // 播放转场动画
        mask.DOFade(1, 0.5f).OnComplete(OnEnterComplete);
    }

    /// <summary>
    /// 进场完成事件
    /// </summary>
    private void OnEnterComplete()
	{
        // 开启一个异步操作
        if (transitionData == null) transitionData = new TransitionData();
        //if (transitionData.transitionConfig == null) transitionData.transitionConfig = new TransitionConfig();
        // 播放呼吸动画

        // 转场动画完成后，立马需要执行的事
        transitionData.onEnterComplete?.Invoke();
        // 开始走转场异步操作
        TransitionManager.Instance.StartTransition(transitionData.operations, OnProgressUpdate, OnExitEvent);
    }

    /// <summary>
    /// 退场事件（用于播放退场动画）
    /// </summary>
    private void OnExitEvent()
    {
        // 立马执行转场异步操作结束事件
        transitionData?.onTransitionComplete?.Invoke();
        // 将退场事件放到到最后执行
        mask.DOFade(0, 0.5f).OnComplete(OnExitComplete);
    }

    /// <summary>
    /// 退场完成事件
    /// </summary>
    private void OnExitComplete()
    {
        if (transitionData != null)
        {
            transitionData.onExitComplete?.Invoke();
            transitionData = null;
        }

        // 允许交互
        prohibitAllInteractionsData.canInteractions = true;
        prohibitAllInteractionsData.TimeOut = false;
        SimplifyEventMgr.Emit(10027, prohibitAllInteractionsData);
        // 状态变更
        isTransitioning = false;
    }

    /// <summary>
    /// 进度更新事件（用于显示进度条/显示进度）
    /// </summary>
    /// <param name="currentProgress"></param>
    private void OnProgressUpdate(float currentProgress)
    {

    }
}

public class TransitionData : IEventData
{
    ///// <summary>
    ///// 转场时的配置（操作为异步）
    ///// </summary>
    //public TransitionConfig transitionConfig;

    /// <summary>
    /// 暂时确定就这么用，后续再考虑拓展 TransitionStep 接口来优化
    /// </summary>
    public List<object> operations;
    /// <summary>
    /// 进场动画完成时立刻执行的事件，同步操作
    /// </summary>
    public Action onEnterComplete;
    /// <summary>
    /// 转场异步操作完成后的事件
    /// </summary>
    public Action onTransitionComplete;
    /// <summary>
    /// 退场动画完成时立刻执行的事件，同步操作
    /// </summary>
    public Action onExitComplete;
}

public class ProhibitAllInteractionsData : IEventData
{
    /// <summary>
    /// 能交互吗？
    /// </summary>
    public bool canInteractions = false;
    /// <summary>
    /// 是否暂停时间？
    /// </summary>
    public bool TimeOut = false;
}

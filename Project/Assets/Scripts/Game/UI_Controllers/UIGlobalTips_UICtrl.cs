using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIGlobalTips_UICtrl : UI_Ctrl 
{
    // 使用条件提示
	TextMeshProUGUI[] ucText;
    // 中间横屏的提示
    TextMeshProUGUI middleText;
    // 游戏关卡/回合倒计时
    TextMeshProUGUI gameCountDownText;

    // 输入名称计时器
    TextMeshProUGUI top_timer_withBG_text;

    Dictionary<string, TextMeshProUGUI> noBGCountDown = new Dictionary<string, TextMeshProUGUI>();
    Dictionary<string, CanvasGroup> noBGCountDownCG = new Dictionary<string, CanvasGroup>();

    public override void Awake() 
	{
		base.Awake();

        ucText = new TextMeshProUGUI[2];
        ucText[0] = GetT<TextMeshProUGUI>("1P_text");
        ucText[1] = GetT<TextMeshProUGUI>("2P_text");
        middleText = GetT<TextMeshProUGUI>("GameMidTips/text");
        gameCountDownText = GetT<TextMeshProUGUI>("GameCountDown/text");
        top_timer_withBG_text = GetT<TextMeshProUGUI>("top_timer_withBG/text");

        CanvasGroup top_timer_withBG_CG = GetT<CanvasGroup>("top_timer_withBG");
        top_timer_withBG_CG.alpha = 1.0f;
        CanvasGroup GameCountDown_CG = GetT<CanvasGroup>("GameCountDown");
        GameCountDown_CG.alpha = 1.0f;
        CanvasGroup GameMidTips_CG = GetT<CanvasGroup>("GameMidTips");
        GameMidTips_CG.alpha = 1.0f;
        top_timer_withBG_CG.gameObject.SetActive(false);
        GameCountDown_CG.gameObject.SetActive(false);
        GameMidTips_CG.gameObject.SetActive(false);

        noBGCountDown.Add("bottom", GetT<TextMeshProUGUI>("bottom_timerText"));
        noBGCountDownCG.Add("bottom", GetT<CanvasGroup>("bottom_timerText"));
        noBGCountDown.Add("rightTop", GetT<TextMeshProUGUI>("rightTop_timerText"));
        noBGCountDownCG.Add("rightTop", GetT<CanvasGroup>("rightTop_timerText"));

        PlayUseConditionTween();

        SimplifyEventMgr.AddListener<SingleTipsData>(10015, GameCountDownTips);
        SimplifyEventMgr.AddListener<SingleTipsData>(10014, MiddleTips);
        SimplifyEventMgr.AddListener<CountDownTipsData>(10009, HideNoBGCountDown);
        SimplifyEventMgr.AddListener<CountDownTipsData>(10008, NoBGCountDown);
        SimplifyEventMgr.AddListener<InputNameCountDownTipsData>(10031, InputNameCountDownTips);
    }


    private void OnDestroy()
    {
        SimplifyEventMgr.RemoveListener<SingleTipsData>(10015, GameCountDownTips);
        SimplifyEventMgr.RemoveListener<SingleTipsData>(10014, MiddleTips);
        SimplifyEventMgr.RemoveListener<CountDownTipsData>(10009, HideNoBGCountDown);
        SimplifyEventMgr.RemoveListener<CountDownTipsData>(10008, NoBGCountDown);
        SimplifyEventMgr.RemoveListener<InputNameCountDownTipsData>(10031, InputNameCountDownTips);
    }


    /// <summary>
    /// 输入名称倒计时
    /// </summary>
    /// <param name="data"></param>
    private void InputNameCountDownTips(InputNameCountDownTipsData data)
    {
        if (top_timer_withBG_text == null) return;

        if (string.IsNullOrEmpty(data.content))
        {
            top_timer_withBG_text.transform.parent.gameObject.SetActive(false);
            top_timer_withBG_text.text = "";
            return;
        }
        if (!top_timer_withBG_text.transform.parent.gameObject.activeInHierarchy) top_timer_withBG_text.transform.parent.gameObject.SetActive(true);
        top_timer_withBG_text.text = data.content;
    }

    /// <summary>
    ///  游戏中的回合倒计时提示
    /// </summary>
    /// <param name="data"></param>
    private void GameCountDownTips(SingleTipsData data)
    {
        if (string.IsNullOrEmpty(data.content))
        {
            gameCountDownText.transform.parent.gameObject.SetActive(false);
            gameCountDownText.text = "";
            return;
        }
        if (!gameCountDownText.transform.parent.gameObject.activeInHierarchy) gameCountDownText.transform.parent.gameObject.SetActive(true);
        gameCountDownText.text = data.content;
    }
    /// <summary>
    /// 中间横屏的提示
    /// </summary>
    /// <param name="data"></param>
    private void MiddleTips(SingleTipsData data)
    {
        if (string.IsNullOrEmpty(data.content))
        {
            middleText.transform.parent.gameObject.SetActive(false);
            middleText.text = "";
            return;
        }
        if (!middleText.transform.parent.gameObject.activeInHierarchy) middleText.transform.parent.gameObject.SetActive(true);
        middleText.text = data.content;

        middleText.transform.parent.localScale = new Vector3(0f, 0f, 1f);
        middleText.transform.parent.DOScaleX(1f, 0.2f);
        middleText.transform.parent.DOScaleY(1f, 0.6f);
    }

    // 暂时与倒计时提示共用一个数据结构
    private void HideNoBGCountDown(CountDownTipsData data)
    {
        if (string.IsNullOrEmpty(data.showPos)) return;
        if (!noBGCountDown.ContainsKey(data.showPos))
        {
            Debug.LogError($"无背景倒计时更新有误，没有发现该倒计时UI：{data.showPos}");
            return;
        }
        CanvasGroup goT = noBGCountDownCG[data.showPos];
        goT.alpha = 0;
    }
    // 无背景倒计时
    private void NoBGCountDown(CountDownTipsData data)
    {
        if (string.IsNullOrEmpty(data.showPos)) return;
        if (!noBGCountDown.ContainsKey(data.showPos))
        {
            Debug.LogError($"无背景倒计时更新有误，没有发现该倒计时UI：{data.showPos}");
            return;
        }
        TextMeshProUGUI goT = noBGCountDown[data.showPos];
        if (goT == null) return;
        CanvasGroup goCG = noBGCountDownCG[data.showPos];
        goT.text = Mathf.FloorToInt(data.remainingTime).ToString();
        if (goCG != null)
        {
            if (goCG.alpha == 0) goCG.alpha = 1;
        }
    }

    /// <summary>
    /// 为了让左右播放频率一致，即看着一块播一块停
    /// </summary>
    private void PlayUseConditionTween()
    {
        foreach (var t in ucText)
        {
            if (t != null)
            {
                if (t.gameObject.activeInHierarchy)
                {
                    t.DOFade(0.01f, 0.5f).SetLoops(-1, LoopType.Yoyo);
                }
            }
        }
    }
}

/// <summary>
/// 倒计时提示
/// </summary>
public struct CountDownTipsData: IEventData
{
    /// <summary>
    /// 暂时是固定的几个位置，不够再加：上下左右中
    /// </summary>
    public string showPos;
    public float remainingTime;
}

public struct SingleTipsData : IEventData
{
    public string content;
}

public struct BoolData : IEventData
{
    public bool isTrue;
}

public struct InputNameCountDownTipsData : IEventData
{
    public string content;
}

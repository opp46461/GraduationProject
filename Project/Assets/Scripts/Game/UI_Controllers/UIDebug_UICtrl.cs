using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIDebug_UICtrl : UI_Ctrl 
{
    [SerializeField]
    TextMeshProUGUI text_fps;

    private string fps = "60";

    [Header("FPS显示设置")]
    [SerializeField] private bool showFPS = true;
    [SerializeField] private float updateInterval = 0.5f; // 每0.5秒更新一次FPS显示

    // FPS计算相关
    private float accum = 0.0f;
    private int frames = 0;
    private float timeLeft; // 当前间隔剩余时间
    private float currentFPS = 0.0f;

    public override void Awake() 
	{
		base.Awake();

        text_fps = GetT<TextMeshProUGUI>("text_fps");
        SetFrameRate();
    }

	void Start() 
	{

    }

    private void Update()
    {
        // 累积时间和帧数
        timeLeft -= Time.deltaTime;
        accum += Time.timeScale / Time.deltaTime;
        frames++;

        // 间隔时间到达，计算FPS
        if (timeLeft <= 0.0f)
        {
            // 计算FPS
            CalculateFPS();
            if (text_fps != null) text_fps.text = fps;
        }
    }


    /// <summary>
    /// 设置目标帧率
    /// </summary>
    public void SetFrameRate()
    {
        //Application.targetFrameRate = ServerDataHandle.Instance.TargetFrameRate;
        //Debug.Log($"帧率锁定为: {ServerDataHandle.Instance.TargetFrameRate} FPS");
    }

    /// <summary>
    /// 计算当前FPS
    /// </summary>
    private void CalculateFPS()
    {
        // 计算这段时间的平均FPS
        currentFPS = accum / frames;

        // 重置计数器
        timeLeft = updateInterval;
        accum = 0.0f;
        frames = 0;

        fps = string.Format("FPS:{0}", Mathf.FloorToInt(currentFPS));
    }
}

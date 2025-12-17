using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowFPS : UnitySingleton<ShowFPS>
{
    private float m_StartUpdateShowTime = 0f;    
    private float m_LastUpdateShowTime = 0f;    // Time of the last FPS update;

    private float m_UpdateShowDeltaTime = 1.00f;// Time interval for updating FPS;

    private int m_FrameUpdate = 0;// Number of frames since last FPS calculation;
    private int m_FrameUpdateTotal = 0;// Total number of frames since the start;

    private float m_FPS = 0;
    private float m_FPS_AVG = 0;


    public override void Awake()
    {
        base.Awake();
        //Application.targetFrameRate = 30;
    }

    public void Initialize()
    {
        m_StartUpdateShowTime = Time.realtimeSinceStartup;
        m_LastUpdateShowTime = m_StartUpdateShowTime;
    }

    // Update is called once per frame
    void Update()
    {
        m_FrameUpdate++;
        m_FrameUpdateTotal++;   
        if (Time.realtimeSinceStartup - m_LastUpdateShowTime >= m_UpdateShowDeltaTime)
        {
            m_FPS = m_FrameUpdate / (Time.realtimeSinceStartup - m_LastUpdateShowTime);
            m_FrameUpdate = 0;
            m_LastUpdateShowTime = Time.realtimeSinceStartup;

            m_FPS_AVG = m_FrameUpdateTotal / (Time.realtimeSinceStartup - m_StartUpdateShowTime);
        }
    }

    void OnGUI()
    {
        GUIStyle bb = new GUIStyle();
        bb.normal.background = null;    // Set the background to be empty
        bb.normal.textColor = new Color(0xff, 0xff, 0xff);   // Set the text color
        bb.fontSize = 48;
        GUI.Label(new Rect(10, /*Screen.height - */130, 300, 300), "FPS: " + m_FPS, bb);
        //GUI.Label(new Rect(10, /*Screen.height - */200, 300, 300), "FPS_AVG: " + m_FPS_AVG, bb);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;



/*
 * GeoffreyChu 2021.01.12
 * 
 * 说明：
 *      每个UIView/视图都有一个控制脚本
 *      每个UIView/视图都有一个对应的 存储器 用于存储该视图下所有的子物体
 *      view 存储器规则 ----> key = path（UI视图路径） + gameObject.name（存储对象名：即根节点儿子名 ， 如果有更多的儿子则用/分割，例如 root.son.name , root.son.name/root.son.son.name）
 *      view 内部使用规则 ----> view[对象名]，例如view[bg]，view[bg/start_bg]
 *      view 外部使用规则 ----> 通过自定义事件驱动/引用传递
 */
//UI控制
//用于被继承
public class UI_Ctrl : MonoBehaviour
{
    public Dictionary<string, GameObject> view = new Dictionary<string, GameObject>();//该UI预制体的引用存储器

    /// <summary>
    /// 加载全部对象
    /// </summary>
    /// <param name="root">根节点</param>
    /// <param name="path"></param>
    private void Load_all_object(GameObject root, string path)
    {
        foreach (Transform tf in root.transform)//查找根节点路径下的所有子物体
        {
            // 规则：path + 根节点下儿子的名 为存储key
            if (this.view.ContainsKey(path + tf.gameObject.name))//如果已经存有该物体引用，则结束此次循环，并且进入下一次循环
            {
                // Debugger.LogWarning("Warning object is exist:" + path + tf.gameObject.name + "!");
                continue;
            }
            this.view.Add(path + tf.gameObject.name, tf.gameObject);//如果没有存，则 把该子物体添加到存储器中
            Load_all_object(tf.gameObject, path + tf.gameObject.name + "/");//递归下去
        }

    }

    public bool IsContain(string path)
    {
        return view.ContainsKey(path);
    }

    public virtual void RemoveSelf()
    {
        GameObject.Destroy(this.gameObject);//销毁该UI视图
    }

    public virtual void Awake()
    {
        this.Load_all_object(this.gameObject, "");//初始化存储器
    }

    /// <summary>
    /// 添加 Button onclick 监听
    /// </summary>
    /// <param name="Button_path">Button在UI视图中的路径</param>
    /// <param name="onclick">委托事件（当被点击时执行）</param>
    public void Add_Button_listener(string Button_path, UnityAction onclick)
    {
        UnityEngine.UI.Button bt = this.view[Button_path].GetComponent<UnityEngine.UI.Button>();
        if (bt == null)
        {
            Debug.LogError("UI_manager add_Button_listener: not Button Component!");//没找到
            return;
        }

        bt.onClick.AddListener(onclick);//添加监听
    }

    /// <summary>
    /// 添加 Slider on_value_changle 监听
    /// </summary>
    /// <param name="slider_path">Slider在UI视图中的路径</param>
    /// <param name="on_value_changle">委托事件（当值改变时执行）</param>
    public void Add_slider_listener(string slider_path, UnityAction<float> on_value_changle)
    {
        Slider s = this.view[slider_path].GetComponent<Slider>();
        if (s == null)
        {
            Debug.LogError("UI_manager add_slider_listener: not Slider Component!");//没找到该Slider
            return;
        }

        s.onValueChanged.AddListener(on_value_changle);//添加监听
    }

    /// <summary>
    /// 添加 Toggle.onValueChanged 监听
    /// </summary>
    /// <param name="path"></param>
    /// <param name="on_value_changle"></param>
    public void Add_Toggle_listener(string path, UnityAction<bool> on_value_changle)
    {
        Toggle toggle = this.view[path].GetComponent<Toggle>();
        if (toggle == null)
        {
            Debug.LogError("UI_manager add_Toggle_listener: not Toggle Component!");//没找到
            return;
        }

        toggle.onValueChanged.AddListener(on_value_changle);//添加监听
    }

    /// <summary>
    /// 添加 Dropdown.onValueChanged 监听
    /// </summary>
    /// <param name="path"></param>
    /// <param name="on_value_changle"></param>
    public void Add_Dropdown_listener(string path, UnityAction<int> on_value_changle)
    {
        Dropdown dropdown = this.view[path].GetComponent<Dropdown>();
        if (dropdown == null)
        {
            Debug.LogError("UI_manager add_Dropdown_listener: not Dropdown Component!");//没找到
            return;
        }

        dropdown.onValueChanged.AddListener(on_value_changle);//添加监听
    }

    /// <summary>
    /// 根据view上的路径获取你想要的组件类型
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="path"></param>
    /// <returns></returns>
    public T GetT<T>(string path) where T : Component
    {
        if (!IsContain(path)) return null;
        return view[path].GetComponent<T>();
    }

    #region 工具封装

    /// <summary>
    /// 替换 Image 精灵
    /// </summary>
    /// <param name="obejctPath">对象在视窗的路径</param>
    /// <param name="spiritePath">精灵路径</param>
    public void SetImageSpirite(string obejctPath, string spiritePath)
    {
        //view[obejctPath].GetComponent<Image>().sprite = ResMgr.Instance.GetAssetCache<Sprite>(spiritePath, ResMgr.LoadType.UI_Spirite);
    }

    #endregion
}

//UI管理器
public class UI_Manager : UnitySingleton<UI_Manager>
{
    private Dictionary<string, GameObject> canvas = new Dictionary<string, GameObject>();//所有的画布
    private static bool isInitialized = false;
    public GameObject eventSystem;

    public override void Awake()
    {
        base.Awake();
    }

    /// <summary>
    /// 初始化 UI 管理器
    /// </summary>
    public static void Initialize()
    {
        if (isInitialized) return;

        GameObject root = GameObject.Instantiate(ResManager.Instance.LoadAsset<GameObject>("ui_manager"));
        root.name = "UI_Manager";
        root.AddComponent<UI_Manager>();


        Instance.eventSystem = root.GetComponentInChildren<EventSystem>().gameObject;

        foreach (var item in root.GetComponentsInChildren<Canvas>())
        {
            if (Instance.canvas.ContainsKey(item.name))//如果已包含，则进入下一循环
            {
                break;
            }
            Instance.canvas.Add(item.name, item.gameObject);
        }

        isInitialized = true;
    }

    #region Canvas_Default
    public UI_Ctrl ShowView_DefaultTop(string view_Name)
    {
        return ShowView_Default(view_Name, "Top");
    }
    public UI_Ctrl ShowView_DefaultHigh(string view_Name)
    {
        return ShowView_Default(view_Name, "High");
    }
    public UI_Ctrl ShowView_DefaultMiddle(string view_Name)
    {
        return ShowView_Default(view_Name, "Middle");
    }
    public UI_Ctrl ShowView_DefaultBottom(string view_Name)
    {
        return ShowView_Default(view_Name, "Bottom");
    }
    public UI_Ctrl ShowView_Default(string view_Name, string layer = null)
    {
        return ShowViewNowOrDelay(view_Name, "Canvas_Default", layer);
    }

    public void RemoveView_DefaultTop(string view_Name)
    {
        string view_PathToCanvas = string.Format("Top/{0}", view_Name);
        RemoveUIView(view_PathToCanvas, "Canvas_Default");
    }
    public void RemoveView_DefaultHigh(string view_Name)
    {
        string view_PathToCanvas = string.Format("High/{0}", view_Name);
        RemoveUIView(view_PathToCanvas, "Canvas_Default");
    }
    public void RemoveView_DefaultMiddle(string view_Name)
    {
        string view_PathToCanvas = string.Format("Middle/{0}", view_Name);
        RemoveUIView(view_PathToCanvas, "Canvas_Default");
    }
    public void RemoveView_DefaultBottom(string view_Name)
    {
        string view_PathToCanvas = string.Format("Bottom/{0}", view_Name);
        RemoveUIView(view_PathToCanvas, "Canvas_Default");
    }

    #endregion

    #region Canvas_Popup
    public UI_Ctrl ShowPopup(string view_Name)
    {
        return ShowViewNowOrDelay(view_Name, "Canvas_Popup");
    }
    public void RemovePopup(string view_Name)
    {
        RemoveUIView(view_Name, "Canvas_Popup");
    }
    #endregion

    #region Canvas_Effects
    public UI_Ctrl ShowEffects(string view_Name)
    {
        return ShowViewNowOrDelay(view_Name, "Canvas_Effects");
    }
    public void RemoveEffects(string view_Name)
    {
        RemoveUIView(view_Name, "Canvas_Effects");
    }
    #endregion

    /// <summary>
    /// 显示 UI视图 预制体
    /// </summary>
    /// <param name="view_Name">UI视图名</param>
    /// <param name="canvas_Name">层名</param>
    /// <returns>返回该UI视图的 UI_ctrl 对象</returns>
    public UI_Ctrl ShowViewNowOrDelay(string view_Name, string canvas_Name, string layer = null)
    {
        if (string.IsNullOrEmpty(view_Name))
        {
            Debug.LogError("ui_view_Name is null.");
            return null;
        }
        //if (GraphicsSafetyManager.Instance.IsNull())
        //{
        //    Debug.Log("ShowViewNow is fail : GraphicsDeviceType is Null. Delay show instead.");
        //    string operationName = $"ShowView{view_Name}";
        //    // 开启延迟Show
        //    GraphicsSafetyManager.Instance.ExecuteSafelyCoroutine(SafeShowView(view_Name, canvas_Name, layer), operationName);
        //    return null;
        //}
        // 如果允许，则 Show now
        return ShowViewNow(view_Name, canvas_Name, layer);
    }
    public IEnumerator SafeShowView(string view_Name, string canvas_Name, string layer = null)
    {
        yield return true;
        ShowViewNow(view_Name, canvas_Name, layer);
    }
    public UI_Ctrl ShowViewNow(string view_Name, string canvas_Name, string layer = null)
    {
        GameObject ui_prefab = (GameObject)ResManager.Instance.LoadAsset<GameObject>(view_Name, true);
        GameObject ui_view = GameObject.Instantiate(ui_prefab);//生成该UI视图到场景中
        ui_view.name = view_Name;
        if (layer == null)
            ui_view.transform.SetParent(canvas[canvas_Name].transform, false);
        else
            ui_view.transform.SetParent(canvas[canvas_Name].transform.Find(layer), false);
        Type type = Type.GetType(string.Format("{0}{1}", view_Name, "_UICtrl"));//创建类型实例，类型规则：UI视图名 + _UICtrl ----> 必须存在该类型
        UI_Ctrl ctrl = (UI_Ctrl)ui_view.AddComponent(type);//添加该控制组件
        return ctrl;
    }


    /// <summary>
    /// 移除UI视图
    /// </summary>
    /// <param name="view_PathToCanvas">UI视图相对于画布的路径</param>
    /// <param name="canvas_Name">画布名</param>
    /// <param name="isRemoveCanvas">是否同时移除该画布</param>
    public void RemoveUIView(string view_PathToCanvas, string canvas_Name)
    {
        if (!canvas.ContainsKey(canvas_Name))
        {
            Debug.LogError("没找到名为：" + canvas_Name + "的画布!!!!!");//如果没找到，则报错
            return;
        }

        Transform view = canvas[canvas_Name].transform.Find(view_PathToCanvas);//从根目录往下找找对应的UI视图变换组件

        if (view)//如果该UI视图不为空
        {
            GameObject.Destroy(view.gameObject);//销毁该UI视图
        }
        else
        {
            Debug.LogError("没找到名为：" + canvas_Name + "/" + view_PathToCanvas + "的视图!!!!!");//如果没找到，则报错
        }
    }

    public Vector2 GetCanvasResolutions(string canvas_Name)
    {
        if (!canvas.ContainsKey(canvas_Name))
        {
            Debug.LogError("没找到名为：" + canvas_Name + "的画布!!!!!");
            return Vector2.one;
        }
        RectTransform canvasRectTransform = canvas[canvas_Name].GetComponent<RectTransform>();
        Vector2 resolutions;
        resolutions.x = canvasRectTransform.rect.width;
        resolutions.y = canvasRectTransform.rect.height;

        return resolutions;
    }
}
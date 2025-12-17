using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 实现普通的单例模式
// where 限制模板的类型, new()指的是这个类型必须要能被实例化
public abstract class Singleton<T> where T : new()
{
    private static T _instance;
    //锁对象，保证单例实例化时不出错
    private static object mutex = new object();
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                // 保证我们的单例，是线程安全的;
                lock (mutex)
                {
                    if (_instance == null)
                    {
                        _instance = new T();
                    }
                }
            }
            return _instance;
        }
    }
}

// Monobeavior: 声音, 网络
// Unity单例
// 需要继承行为类的单例

public class UnitySingleton<T> : MonoBehaviour
where T : Component
{
    private static T _instance = null;
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                //找到已加载的T类型对象
                _instance = FindObjectOfType(typeof(T)) as T;
                //如果尚未有加载的T则继续
                if (_instance == null)
                {
                    GameObject obj = new GameObject();
                    //创建游戏对象，手动挂载T脚本
                    _instance = (T)obj.AddComponent(typeof(T));
                    //设置该对象为不随场景保存（即不保存到场景中），当加载新场景时不被销毁
                    obj.hideFlags = HideFlags.DontSave;
                    // obj.hideFlags = HideFlags.HideAndDontSave;
                    obj.name = typeof(T).Name;
                }
            }
            return _instance;
        }
    }

    //需要初始化脚本对象
    public virtual void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
        if (_instance == null)
        {
            _instance = this as T;
        }
        else
        {
            GameObject.Destroy(this.gameObject);
        }
    }
}






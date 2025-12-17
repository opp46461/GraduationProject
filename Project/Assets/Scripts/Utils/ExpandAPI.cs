using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//  拓展接口
public static class ExpandAPI
{
    /// <summary>
    /// 如果已经有了，则不再添加
    /// </summary>
    /// <typeparam name="TK"></typeparam>
    /// <typeparam name="TV"></typeparam>
    /// <param name="dic"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool TryAddElement<TK, TV>(this Dictionary<TK, TV> dic, TK key, TV value)
    {
        if (dic.ContainsKey(key)) return false;
        dic.Add(key, value);
        return true;
    }
    /// <summary>
    /// 如果已经有了，则会替换掉
    /// </summary>
    /// <typeparam name="TK"></typeparam>
    /// <typeparam name="TV"></typeparam>
    /// <param name="dic"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public static void AddOrReplaceElement<TK, TV>(this Dictionary<TK, TV> dic, TK key, TV value)
    {
        if (dic.ContainsKey(key))
        {
            dic[key] = value;
            return;
        }
        dic.Add(key, value);
    }

    /// <summary>
    /// 如果已经有了，则不再添加
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    public static bool TryAddElement<T>(this List<T> list, T t)
    {
        if (t == null) return false;
        if (list.Contains(t)) return false;
        list.Add(t);
        return true;
    }


    /// <summary>
    /// 随机返回一个 list 中的元素
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    public static T GetRandomElement<T>(this List<T> list, int min = 0, int max = 0)
    {
        if (list == null || list.Count == 0)
        {
            Debug.LogWarning("列表为空或为null");
            return default(T);
        }
        if (max == 0) max = list.Count;

        int randomIndex = Random.Range(min, max);
        return list[randomIndex];
    }

    public static T GetOrAddComponent<T>(this GameObject go) where T : Component
    {
        return GetOrAddComponent<T>(go.transform);
    }
    public static T GetOrAddComponent<T>(this Transform transform) where T : Component
    {
        T t = transform.GetComponent<T>();
        if (t == null) t = transform.gameObject.AddComponent<T>();
        return t;
    }
    public static Component GetOrAddComponent(this GameObject go, System.Type type)
    {
        return GetOrAddComponent(go.transform, type);
    }
    public static Component GetOrAddComponent(this Transform transform, System.Type type)
    {
        Component component = transform.GetComponent(type);
        if (component == null) component = transform.gameObject.AddComponent(type);
        return component;
    }
}

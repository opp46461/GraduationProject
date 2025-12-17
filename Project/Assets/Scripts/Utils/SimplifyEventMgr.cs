using System;
using UnityEngine;

/// <summary>
/// 事件系统入口，简化自定义事件接口调用
/// </summary>
public static class SimplifyEventMgr
{
    /// <summary>
    /// 注册事件监听
    /// </summary>
    public static void AddListener<T>(int eventID, Action<T> handler) where T : IEventData
    {
        EventMgr.Instance?.AddListener(eventID, handler);
    }

    /// <summary>
    /// 取消事件监听
    /// </summary>
    public static void RemoveListener<T>(int eventID, Action<T> handler) where T : IEventData
    {
        EventMgr.Instance?.RemoveListener(eventID, handler);
    }

    /// <summary>
    /// 触发事件
    /// </summary>
    public static void Emit<T>(int eventID, T data) where T : IEventData
    {
        EventMgr.Instance?.Emit(eventID, data);
    }
}

// 具体事件数据类示例
public class HealthChangeEventData : IEventData
{
    public int CurrentHealth;
    public int MaxHealth;
    public int ChangeAmount;
}

// 值类型事件数据
public struct HealthEventData : IEventData
{
    public int Current;
    public int Max;
    public int Change;
}
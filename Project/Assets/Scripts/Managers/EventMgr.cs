using System;
using System.Collections.Generic;
using UnityEngine;

// 数据基础接口
public interface IEventData { }

// 不支持同一事件ID不同类型的委托
public class EventMgr : UnitySingleton<EventMgr>
{
    private readonly Dictionary<int, Delegate> _eventHandlers = new Dictionary<int, Delegate>();

    public void Init()
    {
        _eventHandlers.Clear();
    }

    /// <summary>
    /// 添加事件监听
    /// </summary>
    public void AddListener<T>(int eventID, Action<T> handler) where T : IEventData
    {
        if (handler == null) return;

        if (!_eventHandlers.ContainsKey(eventID))
        {
            _eventHandlers[eventID] = handler;
        }
        else
        {
            var existingHandler = _eventHandlers[eventID];
            if (existingHandler is Action<T> existingAction)
            {
                _eventHandlers[eventID] = existingAction + handler;
            }
            else
            {
                Debug.LogError($"事件 {eventID} 的委托类型不匹配。已存在: {existingHandler.GetType()}，尝试添加: {typeof(Action<T>)}");
            }
        }
    }

    /// <summary>
    /// 移除事件监听
    /// </summary>
    public void RemoveListener<T>(int eventID, Action<T> handler) where T : IEventData
    {
        if (handler == null || !_eventHandlers.ContainsKey(eventID)) return;

        if (!(_eventHandlers[eventID] is Action<T>))
        {
            Debug.Log("事件委托类型异常，无法移除");
            return;
        }

        var existingHandler = _eventHandlers[eventID];
        if (existingHandler is Action<T> existingAction)
        {
            existingAction -= handler;

            if (existingAction == null)
            {
                _eventHandlers.Remove(eventID);
            }
            else
            {
                _eventHandlers[eventID] = existingAction;
            }
        }
        else
        {
            Debug.LogError($"事件 {eventID} 的委托类型异常，无法移除。期望: {typeof(Action<T>)}, 实际: {existingHandler.GetType()}");
        }
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    public void Emit<T>(int eventID, T data) where T : IEventData
    {
        if (!_eventHandlers.ContainsKey(eventID))
        {
            return;
        }
        var handler = _eventHandlers[eventID];
        if (handler is Action<T> action)
        {
            action?.Invoke(data);
        }
        else
        {
            Debug.LogError($"事件 {eventID} 的委托类型异常，无法发布。期望: {typeof(Action<T>)}, 实际: {handler.GetType()}");
        }
    }

    /// <summary>
    /// 移除所有监听
    /// </summary>
    public void RemoveAll()
    {
        _eventHandlers.Clear();
    }
}
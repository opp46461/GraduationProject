using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 需要确保该池的操作是在主线程中执行的
/// </summary>
/// <typeparam name="T"></typeparam>
public class ThreadSafeObjectPool<T> : IObjectPool<T>, IDisposable where T : class, new()
{
    private readonly Stack<T> _pool = new Stack<T>();
    private readonly object _lockObject = new object();
    private readonly Action<T> _onGet;
    private readonly Action<T> _onRecycle;
    private readonly Action<T> _onCreate;

    public int Count
    {
        get
        {
            lock (_lockObject)
            {
                return _pool.Count;
            }
        }
    }

    public int ActiveCount { get; private set; }

    public ThreadSafeObjectPool(Action<T> onCreate = null, Action<T> onGet = null,
                               Action<T> onRecycle = null)
    {
        _onCreate = onCreate;
        _onGet = onGet;
        _onRecycle = onRecycle;
    }

    public T Get()
    {
        lock (_lockObject)
        {
            T obj = null;
            if (_pool.Count > 0)
            {
                obj = _pool.Pop();
            }

            // 如果池中没有可用对象，创建新对象
            if (obj == null)
            {
                obj = CreateNewObject();
            }

            ActiveCount++;
            _onGet?.Invoke(obj);
            return obj;
        }
    }

    public void Recycle(T obj)
    {
        if (obj == null) return;

        lock (_lockObject)
        {
            // 立即执行回收操作
            ExecuteRecycle(obj);
        }
    }

    private void ExecuteRecycle(T obj)
    {
        _onRecycle?.Invoke(obj);
        ActiveCount--;

        _pool.Push(obj);
    }

    private T CreateNewObject()
    {
        T obj = new T();
        _onCreate?.Invoke(obj);
        return obj;
    }

    public void Clear()
    {
        lock (_lockObject)
        {
            _pool.Clear();
            ActiveCount = 0;
        }
    }

    public void Dispose()
    {
        Clear();
    }
}
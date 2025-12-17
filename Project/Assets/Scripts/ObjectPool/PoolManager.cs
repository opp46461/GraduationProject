using System;
using System.Collections.Generic;
using UnityEngine;


public class PoolManager : UnitySingleton<PoolManager>
{
    // 线程安全的对象池字典
    private Dictionary<Type, object> _classPools = new Dictionary<Type, object>();
    private Dictionary<string, ThreadSafeGameObjectPool> _gameObjectPools = new Dictionary<string, ThreadSafeGameObjectPool>();

    /// <summary>
    /// 获取或创建线程安全的Class对象池
    /// </summary>
    public IObjectPool<T> GetPool<T>(PoolConfig config = null, Action<T> onCreate = null, Action<T> onGet = null, Action<T> onRecycle = null) where T : class, new()
    {
        config = config ?? PoolConfig.Default;
        var type = typeof(T);

        if (!_classPools.ContainsKey(type))
        {
            IObjectPool<T> pool;
            pool = new ThreadSafeObjectPool<T>(onCreate, onGet, onRecycle);
            _classPools[type] = pool;
        }
        return (IObjectPool<T>)_classPools[type];
    }

    /// <summary>
    /// 获取或创建线程安全的GameObject对象池
    /// </summary>
    public ThreadSafeGameObjectPool GetGameObjectPool(string poolKey, Transform parent = null, string prefabAssetName = null, PoolConfig config = null)
    {
        config = config ?? PoolConfig.Default;

        if (!_gameObjectPools.ContainsKey(poolKey))
        {
            if (string.IsNullOrEmpty(prefabAssetName))
                prefabAssetName = poolKey;

            try
            {
                var pool = new ThreadSafeGameObjectPool(poolKey, prefabAssetName, parent,
                    config.InitialSize);
                _gameObjectPools[poolKey] = pool;
            }
            catch (ArgumentException ex)
            {
                Debug.LogError($"创建GameObject对象池失败: {ex.Message}");
                return null;
            }
        }
        return _gameObjectPools[poolKey];
    }

    /// <summary>
    /// 清理指定类型的对象池
    /// </summary>
    public void ClearPool<T>() where T : class, new()
    {
        var type = typeof(T);
        if (_classPools.TryGetValue(type, out var poolObj))
        {
            if (poolObj is IDisposable disposable)
                disposable.Dispose();
            _classPools.Remove(type);
        }
    }

    /// <summary>
    /// 清理指定键的GameObject对象池
    /// </summary>
    public void ClearGameObjectPool(string poolKey)
    {
        if (_gameObjectPools.TryGetValue(poolKey, out var pool))
        {
            pool.Dispose();
            _gameObjectPools.Remove(poolKey);
        }
    }

    /// <summary>
    /// 清理所有对象池
    /// </summary>
    public void ClearAllPools()
    {
        // 清理Class对象池
        foreach (var poolObj in _classPools.Values)
        {
            if (poolObj is IDisposable disposable)
                disposable.Dispose();
        }
        _classPools.Clear();

        // 清理GameObject对象池
        foreach (var pool in _gameObjectPools.Values)
        {
            pool.Dispose();
        }
        _gameObjectPools.Clear();
    }

    /// <summary>
    /// 获取对象池统计信息（用于调试）
    /// </summary>
    public void GetPoolStats(out int classPoolCount, out int gameObjectPoolCount, out int totalActiveObjects)
    {
        classPoolCount = _classPools.Count;
        gameObjectPoolCount = _gameObjectPools.Count;

        totalActiveObjects = 0;
        foreach (var poolObj in _classPools.Values)
        {
            if (poolObj is IObjectPool<object> pool)
            {
                totalActiveObjects += pool.ActiveCount;
            }
        }

        foreach (var pool in _gameObjectPools.Values)
        {
            totalActiveObjects += pool.ActiveCount;
        }
    }

    protected void OnDestroy()
    {
        ClearAllPools();
    }
}
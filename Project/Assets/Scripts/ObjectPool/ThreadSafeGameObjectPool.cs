using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ThreadSafeGameObjectPool : IObjectPool<GameObject>, IDisposable
{
    private Stack<GameObject> _pool = new Stack<GameObject>();
    private readonly object _lockObject = new object();
    private readonly string _poolKey;
    private readonly string _prefabAssetName;
    private GameObject _prefab;
    private Transform _parent;
    private TransformInfo transformInfo;


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
    public GameObject Prefab { get => _prefab; }

    public ThreadSafeGameObjectPool(string poolKey, string prefabAssetName,
                                  Transform parent = null, int initialSize = 0)
    {
        _poolKey = poolKey;
        _prefabAssetName = prefabAssetName;
        _parent = parent;

        _prefab = ResManager.Instance.LoadAsset<GameObject>(prefabAssetName, true);
        if (_prefab == null)
        {
            Debug.LogErrorFormat("对象池生成失败，加载预设有错：{0}", prefabAssetName);
        }
        else transformInfo = TransformInfo.GetInfo(_prefab.transform);

        for (int i = 0; i < initialSize; i++)
        {
            GameObject obj = CreateNewObject();
            if (obj != null)
            {
                obj.SetActive(false);
                lock (_lockObject)
                {
                    _pool.Push(obj);
                }
            }
        }
    }

    private GameObject CreateNewObject()
    {
        if (_prefab == null) return null;

        GameObject obj = UnityEngine.Object.Instantiate(_prefab, _parent);
        if (string.IsNullOrEmpty(obj.name))
        {
            obj.name = $"{_prefab.name}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }
        return obj;
    }

    public GameObject Get()
    {
        lock (_lockObject)
        {
            GameObject obj = null;

            // 从池中获取不在冷却期的对象
            var tempStack = new Stack<GameObject>();
            while (_pool.Count > 0)
            {
                var candidate = _pool.Pop();
                if (candidate != null)
                {
                    obj = candidate;
                    break;
                }
                if (candidate != null)
                {
                    tempStack.Push(candidate);
                }
            }

            // 将临时栈中的对象放回池中
            while (tempStack.Count > 0)
            {
                var item = tempStack.Pop();
                if (item != null)
                {
                    _pool.Push(item);
                }
            }

            // 如果池中没有可用对象，创建新对象
            if (obj == null)
            {
                obj = CreateNewObject();
            }

            if (obj != null)
            {
                ActiveCount++;
            }

            // 每次Get都会重置原来的变换信息，外部去更改
            transformInfo.ApplyToTransform(obj.transform, obj.transform.parent);

            return obj;
        }
    }

    public void Recycle(GameObject obj)
    {
        if (obj == null) return;

        lock (_lockObject)
        {
            // 立即执行回收操作
            ExecuteRecycle(obj);
        }
    }

    /// <summary>
    /// 执行回收操作
    /// </summary>
    private void ExecuteRecycle(GameObject obj)
    {
        if (obj != null)
        {
            // 立即执行回收操作
            obj.SetActive(false);
            if (_parent != null)
            {
                obj.transform.SetParent(_parent);
            }
            ActiveCount--;

            _pool.Push(obj);
        }
    }

    public void Clear()
    {
        lock (_lockObject)
        {
            foreach (GameObject obj in _pool)
            {
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            }
            _pool.Clear();
            ActiveCount = 0;
        }
    }

    public void Dispose()
    {
        Clear();
        _prefab = null;
        _parent = null;

        if (ResManager.Instance != null)
        {
            ResManager.Instance.UnloadAsset(_prefabAssetName, true);
        }
    }

    public void RemoveSelfManager()
    {
        PoolManager.Instance?.ClearGameObjectPool(_poolKey);
    }
}


public struct TransformInfo
{
    public float pos_x;
    public float pos_y;
    public float pos_z;
    public float eulerAngles_x;
    public float eulerAngles_y;
    public float eulerAngles_z;
    public float scale_x;
    public float scale_y;
    public float scale_z;

    public static TransformInfo GetInfo(Transform target)
    {
        TransformInfo transformInfo = new TransformInfo();
        if (target == null) return transformInfo;

        transformInfo.pos_x = target.position.x;
        transformInfo.pos_y = target.position.y;
        transformInfo.pos_z = target.position.z;
        transformInfo.eulerAngles_x = target.eulerAngles.x;
        transformInfo.eulerAngles_y = target.eulerAngles.y;
        transformInfo.eulerAngles_z = target.eulerAngles.z;
        transformInfo.scale_x = target.lossyScale.x;
        transformInfo.scale_y = target.lossyScale.y;
        transformInfo.scale_z = target.lossyScale.z;
        return transformInfo;
    }

    public void ApplyToTransform(Transform target, Transform parent = null)
    {
        if (target == null) return;
        target.position = new Vector3(pos_x, pos_y, pos_z);
        target.eulerAngles = new Vector3(eulerAngles_x, eulerAngles_y, eulerAngles_z);
        target.SetParent(null);
        target.localScale = new Vector3(scale_x, scale_y, scale_z);
        if (parent != null) target.SetParent(parent);
    }
}
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;


public class UnityMainThreadDispatcher : UnitySingleton<UnityMainThreadDispatcher>
{
    // 消息队列
    //private readonly Queue<System.Action> _actionQueue = new Queue<System.Action>();
    private readonly List<System.Action> _actionQueue = new List<System.Action>();
    // 单帧最多执行一次事件
    private readonly Queue<System.Action> _inputKeyOnceActionQueue = new Queue<System.Action>();

    private readonly object _lockObject = new object();

    public void Initialize()
    {

    }

    private void Update()
    {
        lock (_lockObject)
        {
            int count = _actionQueue.Count;
            for (int i = 0; i < count; i++)
            {
                var temp = _actionQueue[0];
                _actionQueue.RemoveAt(0);
                try
                {
                    temp?.Invoke();
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            //while (_actionQueue.Count > 0)
            //{
            //    var temp = _actionQueue.Dequeue();
            //    try
            //    {
            //        temp?.Invoke();
            //        //StartCoroutine(ExecuteAction(temp));
            //    }
            //    catch (System.Exception ex)
            //    {
            //        Debug.LogException(ex);
            //    }
            //}
        }
    }

    /// <summary>
    /// 在主线程执行Action
    /// </summary>
    public void EnqueueAction(System.Action action)
    {
        lock (_lockObject)
        {
            //_actionQueue.Enqueue(action);
            _actionQueue.Add(action);
        }
    }

    public void EnqueueInputKeyOnceAction(System.Action action)
    {
        lock (_lockObject)
        {
            bool isContains = _actionQueue.Contains(action);
            if (isContains)
            {
                _actionQueue.Remove(action);
                _actionQueue.Add(action);
            }
            else
            {
                _actionQueue.Add(action);
            }
        }
    }
}

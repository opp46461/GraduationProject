/* 【说明】
 *     用于Class的对象池，GO在GameObjectPool
 */

// 对象池接口
public interface IObjectPool<T> where T : class
{
    T Get();
    void Recycle(T obj);
    void Clear();
    int Count { get; }
    int ActiveCount { get; }
}
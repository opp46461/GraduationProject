
[System.Serializable]
public class PoolConfig
{
    public int InitialSize { get; set; } = 0;
    public int MaxSize { get; set; } = int.MaxValue;
    public static PoolConfig Default => new PoolConfig()
    {
        InitialSize = 0
    };

    public static PoolConfig WithSize(int initialSize, int maxSize = int.MaxValue)
    {
        return new PoolConfig { InitialSize = initialSize, MaxSize = maxSize };
    }
}
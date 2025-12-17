using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class UniversalAPI
{
    /// <summary>
    /// 根据百分比随机，中了为真
    /// </summary>
    /// <param name="percentage"></param>
    /// <returns></returns>
    public static bool RandomBingoByPercentage(float percentage)
    {
        if (percentage < 0) return false;
        if (percentage >= 1) return true;

        int index = UnityEngine.Random.Range(0, 100);

        return index <= Mathf.CeilToInt(percentage * 100);
    }

    public static Vector3 GetRandomPoint(Bounds bounds)
    {
        return GetRandomPoint(bounds.center, bounds.size);
    }
    public static Vector3 GetRandomPoint(Vector3 center, Vector3 size)
    {
        Vector3 randomPoint = new Vector3(
            center.x + Random.Range(-size.x / 2f, size.x / 2f),
            center.y + Random.Range(-size.y / 2f, size.y / 2f),
            center.z + Random.Range(-size.z / 2f, size.z / 2f)
        );
        return randomPoint;
    }

    public static Vector3 GetCenter(Vector3 start, Vector3 end)
    {
        // 计算方向
        Vector3 dir = (end - start).normalized;
        // 计算距离
        float dis = Mathf.Abs(Vector3.Distance(start, end));
        return start + dis / 2 * dir;
    }

    /// <summary>
    /// 生成指定范围的随机顺序列表（Fisher-Yates洗牌算法）
    /// </summary>
    /// <param name="start">起始值（包含）</param>
    /// <param name="end">结束值（包含）</param>
    /// <returns>打乱顺序的列表</returns>
    public static List<int> CreateShuffledList(int start, int end)
    {
        // 创建顺序列表
        List<int> list = new List<int>();
        for (int i = start; i <= end; i++)
        {
            list.Add(i);
        }

        // Fisher-Yates洗牌算法
        for (int i = list.Count - 1; i > 0; i--)
        {
            // 随机选择一个索引（0到i之间）
            int randomIndex = Random.Range(0, i + 1);

            // 交换元素
            int temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }

        return list;
    }
}

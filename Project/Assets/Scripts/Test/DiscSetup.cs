using UnityEngine;

/// <summary>
/// 圆盘初始化脚本：自动排列棍子到圆周
/// </summary>
public class DiscSetup : MonoBehaviour
{
    [Header("棍子配置")]
    public GameObject stickPrefab; // 棍子预制体（拖入场景中的Stick）
    public int stickCount = 5;     // 棍子数量（固定为5）
    public float stickRadius = 4.5f; // 棍子摆放半径（比圆盘小，避免超出）
    public float stickYOffset = 2f;  // 棍子Y轴偏移（底部贴圆盘上表面）

    private Rigidbody _discRigidbody;

    void Start()
    {
        _discRigidbody = GetComponent<Rigidbody>();
        if (_discRigidbody == null)
        {
            Debug.LogError("圆盘缺少Rigidbody组件！");
            return;
        }

        // 初始化预制体（若未指定，自动找场景中的Stick）
        InitStickPrefab();

        // 均匀排列棍子
        ArrangeSticks();
    }

    /// <summary>
    /// 初始化棍子预制体
    /// </summary>
    private void InitStickPrefab()
    {
        if (stickPrefab == null)
        {
            stickPrefab = GameObject.Find("Stick");
            if (stickPrefab != null)
            {
                stickPrefab.SetActive(false); // 隐藏原始棍子，用实例化的
            }
            else
            {
                Debug.LogError("未找到棍子预制体，请在Inspector中指定！");
                enabled = false; // 关闭脚本
            }
        }
    }

    /// <summary>
    /// 均匀排列棍子到圆盘圆周
    /// </summary>
    private void ArrangeSticks()
    {
        float angleStep = 360f / stickCount; // 每根棍子的角度间隔
        float currentAngle = 0f;

        //Vector3 initalPos = this.transform.position - this.transform.forward;
        Vector3 initalPos = this.transform.position;
        Quaternion quaternion = this.transform.rotation;

        for (int i = 0; i < stickCount; i++)
        {
            // 1. 计算圆周位置（弧度转换）
            float radian = currentAngle * Mathf.Deg2Rad;
            //float x = Mathf.Cos(radian) * stickRadius;
            float x = Mathf.Sin(radian) * stickRadius;
            //float z = Mathf.Sin(radian) * stickRadius;
            float z = -Mathf.Cos(radian) * stickRadius;
            Vector3 pos = quaternion * new Vector3(x, 0, z);
            Vector3 stickPos = pos + initalPos;

            Vector3 eulerAngles = Vector3.forward * -angleStep * i;

            // 2. 实例化棍子
            GameObject stick = Instantiate(stickPrefab, stickPos, Quaternion.identity);
            stick.transform.eulerAngles = eulerAngles;
            stick.name = $"Stick_{i + 1}";
            stick.SetActive(true);

            // 3. 绑定FixedJoint到圆盘
            FixedJoint joint = stick.GetComponent<FixedJoint>();
            if (joint == null) joint = stick.AddComponent<FixedJoint>();
            joint.connectedBody = _discRigidbody;

            // 4. 下一根棍子角度
            currentAngle += angleStep;
        }
    }
}
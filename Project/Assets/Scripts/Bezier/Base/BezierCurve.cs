using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class BezierCurve : MonoBehaviour
{
    [Header("控制点（世界坐标）")]
    public Transform[] controlPointTransforms = new Transform[3];
    public Vector3[] controlPoints = new Vector3[3];

    [Header("曲线设置")]
    [Range(2, 100)]
    public int resolution = 50; // 曲线采样点数量
    public Color curveColor = Color.green;
    public float pointSize = 0.1f;

    [Header("预览物体")]
    public GameObject previewObject;
    [Range(0, 1)]
    public float previewPosition = 0;

    // 计算二阶贝塞尔曲线点
    public Vector3 CalculateBezierPointByTransform(float t)
    {
        if (controlPointTransforms == null || controlPointTransforms.Length < 3)
            return Vector3.zero;

        foreach (Transform ct in controlPointTransforms)
        {
            if (ct == null) return Vector3.zero;
        }

        Vector3 p0 = controlPointTransforms[0].position;
        Vector3 p1 = controlPointTransforms[1].position;
        Vector3 p2 = controlPointTransforms[2].position;

        // 二阶贝塞尔公式：B(t) = (1-t)²P0 + 2(1-t)tP1 + t²P2
        float u = 1 - t;
        float uu = u * u;
        float tt = t * t;

        Vector3 point = uu * p0;
        point += 2 * u * t * p1;
        point += tt * p2;

        return point;
    }
    public Vector3 CalculateBezierPointByPoint(float t)
    {
        if (controlPoints == null || controlPoints.Length < 3)
            return Vector3.zero;

        Vector3 p0 = controlPoints[0];
        Vector3 p1 = controlPoints[1];
        Vector3 p2 = controlPoints[2];

        // 二阶贝塞尔公式：B(t) = (1-t)²P0 + 2(1-t)tP1 + t²P2
        float u = 1 - t;
        float uu = u * u;
        float tt = t * t;

        Vector3 point = uu * p0;
        point += 2 * u * t * p1;
        point += tt * p2;

        return point;
    }

    // 获取曲线上的所有点
    public List<Vector3> GetCurvePointsByTransform()
    {
        List<Vector3> points = new List<Vector3>();

        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            points.Add(CalculateBezierPointByTransform(t));
        }

        return points;
    }
    public List<Vector3> GetCurvePointsByPoint()
    {
        List<Vector3> points = new List<Vector3>();

        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            points.Add(CalculateBezierPointByPoint(t));
        }

        return points;
    }

    // 获取曲线总长度（近似）
    public float GetCurveLengthByTransform()
    {
        List<Vector3> points = GetCurvePointsByTransform();
        float length = 0;

        for (int i = 1; i < points.Count; i++)
        {
            length += Vector3.Distance(points[i - 1], points[i]);
        }

        return length;
    }
    public float GetCurveLengthByPoint()
    {
        List<Vector3> points = GetCurvePointsByPoint();
        float length = 0;

        for (int i = 1; i < points.Count; i++)
        {
            length += Vector3.Distance(points[i - 1], points[i]);
        }

        return length;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (controlPointTransforms == null || controlPointTransforms.Length < 3)
            return;

        foreach (Transform ct in controlPointTransforms)
        {
            if (ct == null) return;
        }

        // 绘制控制点
        Gizmos.color = Color.red;
        for (int i = 0; i < controlPointTransforms.Length; i++)
        {
            if (controlPointTransforms[i] != null)
            {
                Gizmos.DrawSphere(controlPointTransforms[i].position, pointSize);
                if (i > 0 && controlPointTransforms[i - 1] != null)
                {
                    Gizmos.DrawLine(controlPointTransforms[i - 1].position, controlPointTransforms[i].position);
                }
            }
        }

        // 绘制贝塞尔曲线
        Gizmos.color = curveColor;
        List<Vector3> curvePoints = GetCurvePointsByTransform();

        for (int i = 1; i < curvePoints.Count; i++)
        {
            Gizmos.DrawLine(curvePoints[i - 1], curvePoints[i]);
        }

        // 绘制预览位置
        if (previewObject != null)
        {
            Vector3 previewPos = CalculateBezierPointByTransform(previewPosition);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(previewPos, pointSize * 1.5f);
        }
    }

    void OnValidate()
    {
        if (previewObject != null)
        {
            previewObject.transform.position = CalculateBezierPointByTransform(previewPosition);
        }
    }
#endif
}
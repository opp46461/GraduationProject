using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public static class RaycastTool
{
    #region UI元素射线检测

    /// <summary>
    /// 从UI元素发射射线，返回第一个命中的UI元素
    /// </summary>
    /// <param name="uiElement">发射射线的UI元素</param>
    /// <returns>命中的UI元素，如果没有命中则返回null</returns>
    public static GameObject RaycastUIFromUI(Graphic uiElement)
    {
        if (uiElement == null) return null;

        Vector2 screenPoint = GetUIScreenPosition(uiElement);
        return RaycastUI(screenPoint);
    }

    /// <summary>
    /// 从屏幕位置发射射线检测UI元素
    /// </summary>
    /// <param name="screenPosition">屏幕位置</param>
    /// <returns>命中的UI元素，如果没有命中则返回null</returns>
    public static GameObject RaycastUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null || !EventSystem.current.IsActive())
            return null;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        if (results.Count > 0)
        {
            return results[0].gameObject;
        }

        return null;
    }

    /// <summary>
    /// 从UI元素发射射线，返回所有命中的UI元素
    /// </summary>
    /// <param name="uiElement">发射射线的UI元素</param>
    /// <returns>所有命中的UI元素列表</returns>
    public static List<GameObject> RaycastAllUIFromUI(Graphic uiElement)
    {
        List<GameObject> hitObjects = new List<GameObject>();

        if (uiElement == null) return hitObjects;

        Vector2 screenPoint = GetUIScreenPosition(uiElement);
        return RaycastAllUI(screenPoint);
    }

    /// <summary>
    /// 从屏幕位置发射射线检测所有UI元素
    /// </summary>
    /// <param name="screenPosition">屏幕位置</param>
    /// <returns>所有命中的UI元素列表</returns>
    public static List<GameObject> RaycastAllUI(Vector2 screenPosition)
    {
        List<GameObject> hitObjects = new List<GameObject>();
        if (EventSystem.current == null || !EventSystem.current.IsActive())
            return hitObjects;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            hitObjects.Add(result.gameObject);
        }

        return hitObjects;
    }

    #endregion


    #region 3D物体射线检测

    /// <summary>
    /// 从UI元素发射射线，返回第一个命中的3D游戏对象
    /// </summary>
    /// <param name="uiElement">发射射线的UI元素</param>
    /// <param name="camera">用于射线检测的摄像机</param>
    /// <param name="layerMask">射线层掩码</param>
    /// <param name="maxDistance">最大检测距离</param>
    /// <returns>命中的3D游戏对象，如果没有命中则返回null</returns>
    public static GameObject Raycast3DFromUI(Graphic uiElement, Camera camera, LayerMask layerMask, float maxDistance = Mathf.Infinity)
    {
        if (uiElement == null || camera == null) return null;

        Vector2 screenPoint = GetUIScreenPosition(uiElement);
        return Raycast3D(screenPoint, camera, layerMask, maxDistance);
    }

    /// <summary>
    /// 从屏幕位置发射射线检测3D物体
    /// </summary>
    /// <param name="screenPosition">屏幕位置</param>
    /// <param name="camera">用于射线检测的摄像机</param>
    /// <param name="layerMask">射线层掩码</param>
    /// <param name="maxDistance">最大检测距离</param>
    /// <returns>命中的3D游戏对象，如果没有命中则返回null</returns>
    public static GameObject Raycast3D(Vector2 screenPosition, Camera camera, LayerMask layerMask, float maxDistance = Mathf.Infinity)
    {
        if (camera == null) return null;

        Ray ray = camera.ScreenPointToRay(screenPosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxDistance, layerMask))
        {
            return hit.collider.gameObject;
        }

        return null;
    }

    /// <summary>
    /// 从UI元素发射射线，返回所有命中的3D游戏对象
    /// </summary>
    /// <param name="uiElement">发射射线的UI元素</param>
    /// <param name="camera">用于射线检测的摄像机</param>
    /// <param name="layerMask">射线层掩码</param>
    /// <param name="maxDistance">最大检测距离</param>
    /// <returns>所有命中的3D游戏对象数组</returns>
    public static RaycastHit[] RaycastAll3DFromUI(Graphic uiElement, Camera camera, LayerMask layerMask, float maxDistance = Mathf.Infinity)
    {
        if (uiElement == null || camera == null) return new RaycastHit[0];

        Vector2 screenPoint = GetUIScreenPosition(uiElement);
        return RaycastAll3D(screenPoint, camera, layerMask, maxDistance);
    }

    /// <summary>
    /// 从屏幕位置发射射线检测所有3D物体
    /// </summary>
    /// <param name="screenPosition">屏幕位置</param>
    /// <param name="camera">用于射线检测的摄像机</param>
    /// <param name="layerMask">射线层掩码</param>
    /// <param name="maxDistance">最大检测距离</param>
    /// <returns>所有命中的3D游戏对象数组</returns>
    public static RaycastHit[] RaycastAll3D(Vector2 screenPosition, Camera camera, LayerMask layerMask, float maxDistance = Mathf.Infinity)
    {
        if (camera == null) return new RaycastHit[0];

        Ray ray = camera.ScreenPointToRay(screenPosition);
        return Physics.RaycastAll(ray, maxDistance, layerMask);
    }

    #endregion


    #region 辅助方法

    /// <summary>
    /// 获取UI元素在屏幕上的位置（考虑不同的Canvas渲染模式）
    /// </summary>
    /// <param name="uiElement">UI元素</param>
    /// <returns>屏幕位置</returns>
    private static Vector2 GetUIScreenPosition(Graphic uiElement)
    {
        RectTransform rectTransform = uiElement.rectTransform;
        Canvas canvas = uiElement.canvas;

        if (canvas == null) return rectTransform.position;

        // 根据Canvas的渲染模式处理不同的坐标转换
        switch (canvas.renderMode)
        {
            case RenderMode.ScreenSpaceOverlay:
                // 在Overlay模式下，UI位置已经是屏幕坐标
                return rectTransform.position;

            case RenderMode.ScreenSpaceCamera:
            case RenderMode.WorldSpace:
                // 在Camera和WorldSpace模式下，需要通过摄像机转换到屏幕坐标
                Camera canvasCamera = canvas.worldCamera;
                if (canvasCamera == null)
                {
                    canvasCamera = Camera.main;
                }

                if (canvasCamera != null)
                {
                    return canvasCamera.WorldToScreenPoint(rectTransform.position);
                }
                else
                {
                    Debug.LogWarning("无法找到有效的摄像机来转换UI位置");
                    return rectTransform.position;
                }

            default:
                return rectTransform.position;
        }
    }

    /// <summary>
    /// 检查UI元素是否在指定的画布中
    /// </summary>
    /// <param name="uiElement">UI元素</param>
    /// <param name="targetCanvas">目标画布</param>
    /// <returns>是否在指定画布中</returns>
    public static bool IsUIElementInCanvas(Graphic uiElement, Canvas targetCanvas)
    {
        if (uiElement == null || targetCanvas == null) return false;
        return uiElement.canvas == targetCanvas;
    }

    /// <summary>
    /// 获取UI元素在世界坐标系中的2D位置（专门用于2D物理检测）
    /// </summary>
    /// <param name="uiElement">UI元素</param>
    /// <param name="camera">用于检测的摄像机</param>
    /// <returns>世界坐标系中的2D位置</returns>
    private static Vector2 GetUIWorldPosition2D(Graphic uiElement, Camera camera)
    {
        if (uiElement == null || camera == null) return Vector2.zero;

        RectTransform rectTransform = uiElement.rectTransform;
        Canvas canvas = uiElement.canvas;

        Vector3 worldPosition;

        // 根据Canvas的渲染模式处理不同的坐标转换
        switch (canvas.renderMode)
        {
            case RenderMode.ScreenSpaceOverlay:
                // 在Overlay模式下，需要将屏幕坐标转换为世界坐标
                Vector2 screenPos = GetUIScreenPosition(uiElement);
                worldPosition = camera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, camera.nearClipPlane));
                break;

            case RenderMode.ScreenSpaceCamera:
            case RenderMode.WorldSpace:
                // 在Camera和WorldSpace模式下，UI已经有世界坐标
                worldPosition = rectTransform.position;
                break;

            default:
                worldPosition = rectTransform.position;
                break;
        }

        return new Vector2(worldPosition.x, worldPosition.y);
    }

    /// <summary>
    /// 获取UI元素在世界坐标系中的矩形区域（专门用于2D物理区域检测）
    /// </summary>
    /// <param name="uiElement">UI元素</param>
    /// <param name="camera">用于检测的摄像机</param>
    /// <returns>世界坐标系中的矩形区域</returns>
    private static Rect GetUIWorldRect2D(Graphic uiElement, Camera camera)
    {
        if (uiElement == null || camera == null) return new Rect();

        RectTransform rectTransform = uiElement.rectTransform;
        Canvas canvas = uiElement.canvas;

        Vector2 center;
        Vector2 size;

        // 根据Canvas的渲染模式处理不同的坐标转换
        switch (canvas.renderMode)
        {
            case RenderMode.ScreenSpaceOverlay:
                // 在Overlay模式下，需要将UI矩形转换为世界坐标
                Vector3[] corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);

                // 将四个角转换为世界坐标
                for (int i = 0; i < 4; i++)
                {
                    corners[i] = camera.ScreenToWorldPoint(new Vector3(corners[i].x, corners[i].y, camera.nearClipPlane));
                }

                // 计算矩形的中心和大小
                Vector2 min = new Vector2(Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x),
                                         Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y));
                Vector2 max = new Vector2(Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x),
                                         Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y));

                center = (min + max) * 0.5f;
                size = max - min;
                break;

            case RenderMode.ScreenSpaceCamera:
            case RenderMode.WorldSpace:
                // 在Camera和WorldSpace模式下，UI已经有世界坐标
                center = rectTransform.position;
                size = rectTransform.rect.size;
                // 考虑缩放
                size.x *= rectTransform.lossyScale.x;
                size.y *= rectTransform.lossyScale.y;
                break;

            default:
                center = rectTransform.position;
                size = rectTransform.rect.size;
                break;
        }

        return new Rect(center - size * 0.5f, size);
    }
    #endregion
}
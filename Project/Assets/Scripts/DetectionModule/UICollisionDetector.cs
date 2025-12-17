using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UICollisionDetector : UnitySingleton<UICollisionDetector>
{
    #region 主要接口

    /// <summary>
    /// 获取第一个被击中的UI元素（带详细信息的重载）
    /// </summary>
    public RaycastResult? GetFirstHitUIWithDetails(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return null;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        if (results.Count > 0)
        {
            return results[0];
        }

        return null;
    }

    /// <summary>
    /// 返回第一个与UI元素有交集的UI游戏对象
    /// </summary>
    public GameObject GetFirstOverlappingUIElement(RectTransform rectTransform, bool includeInactive = false)
    {
        Rect worldRect = GetWorldRect(rectTransform);

        // 按渲染顺序排序，返回最上层的UI元素
        Graphic[] allGraphics = includeInactive ?
            FindObjectsOfType<Graphic>(true) :
            FindObjectsOfType<Graphic>();

        // 按深度排序（粗略排序，基于transform的Z位置和SiblingIndex）
        var sortedGraphics = allGraphics
            .Where(g => g.rectTransform != rectTransform && g.raycastTarget)
            .Where(g => includeInactive || g.gameObject.activeInHierarchy)
            .OrderByDescending(g => GetUIDepth(g.rectTransform))
            .ToArray();

        foreach (var graphic in sortedGraphics)
        {
            Rect otherWorldRect = GetWorldRect(graphic.rectTransform);
            if (worldRect.Overlaps(otherWorldRect))
            {
                return graphic.gameObject;
            }
        }

        return null;
    }

    /// <summary>
    /// 返回第一个与UI元素有交集的2D游戏对象
    /// </summary>
    public GameObject GetFirstOverlapping2DObject(RectTransform rectTransform, LayerMask layerMask, bool includeInactive = false)
    {
        Rect worldRect = GetWorldRect(rectTransform);

        // 获取所有在指定层级的2D碰撞体
        Collider2D[] allColliders = includeInactive ?
            FindObjectsOfType<Collider2D>(true) :
            FindObjectsOfType<Collider2D>();

        // 按Z轴排序（2D中Z轴较小的物体在前面）
        var sortedColliders = allColliders
            .Where(c => includeInactive || c.gameObject.activeInHierarchy)
            .Where(c => ((1 << c.gameObject.layer) & layerMask) != 0 && c.enabled)
            .OrderBy(c => c.transform.position.z)
            .ToArray();

        foreach (var collider in sortedColliders)
        {
            if (Is2DColliderOverlapping(collider, worldRect, rectTransform))
            {
                return collider.gameObject;
            }
        }

        return null;
    }

    /// <summary>
    /// 返回第一个与UI元素有交集的3D游戏对象
    /// </summary>
    public GameObject GetFirstOverlapping3DObject(out Vector3 hitPos, RectTransform rectTransform, LayerMask layerMask, bool includeInactive = false)
    {
        List<Ray> detectionRays = CreateDetectionRays(rectTransform);
        RaycastHit closestHit = new RaycastHit();
        bool hasHit = false;
        float closestDistance = Mathf.Infinity;

        foreach (var ray in detectionRays)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, layerMask)
                .Where(hit => includeInactive || hit.collider.gameObject.activeInHierarchy)
                .OrderBy(hit => hit.distance)
                .ToArray();

            foreach (var hit in hits)
            {
                if (hit.distance < closestDistance)
                {
                    closestHit = hit;
                    closestDistance = hit.distance;
                    hasHit = true;
                }
            }
        }
        hitPos = hasHit ? closestHit.point : Vector3.zero;
        return hasHit ? closestHit.collider.gameObject : null;
    }

    /// <summary>
    /// 获取所有与UI元素有交集的UI元素
    /// </summary>
    public List<GameObject> GetOverlappingUIElements(RectTransform rectTransform, bool includeInactive = false)
    {
        List<GameObject> results = new List<GameObject>();

        // 获取目标UI的世界空间矩形
        Rect worldRect = GetWorldRect(rectTransform);

        // 获取场景中所有可交互的UI元素
        Graphic[] allGraphics = includeInactive ?
            FindObjectsOfType<Graphic>(true) :
            FindObjectsOfType<Graphic>();

        foreach (var graphic in allGraphics)
        {
            if (graphic.rectTransform == rectTransform) continue;
            if (!graphic.raycastTarget) continue;
            if (!includeInactive && !graphic.gameObject.activeInHierarchy) continue;

            Rect otherWorldRect = GetWorldRect(graphic.rectTransform);

            if (worldRect.Overlaps(otherWorldRect))
            {
                results.Add(graphic.gameObject);
            }
        }

        return results;
    }

    /// <summary>
    /// 获取所有与UI元素有交集的2D物体
    /// </summary>
    public List<GameObject> GetOverlapping2DObjects(RectTransform rectTransform, LayerMask layerMask, bool includeInactive = false)
    {
        List<GameObject> results = new List<GameObject>();
        Rect worldRect = GetWorldRect(rectTransform);

        // 获取所有在指定层级的2D碰撞体
        Collider2D[] allColliders = includeInactive ?
            FindObjectsOfType<Collider2D>(true) :
            FindObjectsOfType<Collider2D>();

        foreach (var collider in allColliders)
        {
            if (!includeInactive && !collider.gameObject.activeInHierarchy) continue;
            if (((1 << collider.gameObject.layer) & layerMask) == 0) continue;
            if (!collider.enabled) continue;

            // 将UI矩形转换到2D空间进行检测
            if (Is2DColliderOverlapping(collider, worldRect, rectTransform))
            {
                results.Add(collider.gameObject);
            }
        }

        return results;
    }

    /// <summary>
    /// 获取所有与UI元素有交集的3D物体
    /// </summary>
    public List<GameObject> GetOverlapping3DObjects(RectTransform rectTransform, LayerMask layerMask, bool includeInactive = false)
    {
        List<GameObject> results = new List<GameObject>();

        // 根据画布渲染模式创建检测射线
        List<Ray> detectionRays = CreateDetectionRays(rectTransform);

        foreach (var ray in detectionRays)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, layerMask);

            foreach (var hit in hits)
            {
                if (!includeInactive && !hit.collider.gameObject.activeInHierarchy) continue;
                if (!results.Contains(hit.collider.gameObject))
                {
                    results.Add(hit.collider.gameObject);
                }
            }
        }

        return results;
    }

    #endregion

    #region 核心算法实现

    /// <summary>
    /// 获取UI元素的世界空间矩形
    /// </summary>
    private Rect GetWorldRect(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Vector2 min = corners[0];
        Vector2 max = corners[2];

        for (int i = 1; i < 4; i++)
        {
            min = Vector2.Min(min, corners[i]);
            max = Vector2.Max(max, corners[i]);
        }

        return new Rect(min, max - min);
    }

    /// <summary>
    /// 计算UI元素的深度（用于排序）
    /// </summary>
    private float GetUIDepth(RectTransform rectTransform)
    {
        Canvas canvas = GetCanvas(rectTransform);
        if (canvas == null) return 0;

        float depth = 0;

        // 考虑画布排序顺序
        if (canvas.overrideSorting)
        {
            depth += canvas.sortingOrder * 1000;
        }

        // 考虑SiblingIndex（同层级内的顺序）
        depth += rectTransform.GetSiblingIndex();

        // 考虑Z轴位置（WorldSpace模式）
        if (canvas.renderMode == RenderMode.WorldSpace)
        {
            depth -= rectTransform.position.z * 0.001f;
        }

        return depth;
    }

    /// <summary>
    /// 检测2D碰撞体与UI矩形的重叠
    /// </summary>
    private bool Is2DColliderOverlapping(Collider2D collider, Rect uiWorldRect, RectTransform uiTransform)
    {
        // 对于不同的2D碰撞器类型使用不同的检测方法
        switch (collider)
        {
            case BoxCollider2D boxCollider:
                return IsBoxCollider2DOverlapping(boxCollider, uiWorldRect);

            case CircleCollider2D circleCollider:
                return IsCircleCollider2DOverlapping(circleCollider, uiWorldRect);

            case PolygonCollider2D polygonCollider:
                return IsPolygonCollider2DOverlapping(polygonCollider, uiWorldRect);

            default:
                // 对于其他类型，使用边界框检测
                Bounds colliderBounds = collider.bounds;
                Rect colliderRect = new Rect(
                    colliderBounds.min,
                    colliderBounds.size
                );
                return uiWorldRect.Overlaps(colliderRect);
        }
    }

    private bool IsBoxCollider2DOverlapping(BoxCollider2D collider, Rect uiWorldRect)
    {
        Vector2 center = collider.bounds.center;
        Vector2 size = collider.bounds.size;
        Rect colliderRect = new Rect(center - size * 0.5f, size);

        return uiWorldRect.Overlaps(colliderRect);
    }

    private bool IsCircleCollider2DOverlapping(CircleCollider2D collider, Rect uiWorldRect)
    {
        Vector2 center = collider.bounds.center;
        float radius = collider.radius * Mathf.Max(
            collider.transform.lossyScale.x,
            collider.transform.lossyScale.y
        );

        // 找到矩形上距离圆心最近的点
        Vector2 closestPoint = new Vector2(
            Mathf.Clamp(center.x, uiWorldRect.xMin, uiWorldRect.xMax),
            Mathf.Clamp(center.y, uiWorldRect.yMin, uiWorldRect.yMax)
        );

        float distance = Vector2.Distance(center, closestPoint);
        return distance <= radius;
    }

    private bool IsPolygonCollider2DOverlapping(PolygonCollider2D collider, Rect uiWorldRect)
    {
        // 简化的多边形检测 - 实际项目中可能需要更精确的检测
        Bounds colliderBounds = collider.bounds;
        Rect colliderRect = new Rect(
            colliderBounds.min,
            colliderBounds.size
        );

        return uiWorldRect.Overlaps(colliderRect);
    }

    /// <summary>
    /// 根据画布渲染模式创建检测射线
    /// </summary>
    private List<Ray> CreateDetectionRays(RectTransform rectTransform)
    {
        List<Ray> rays = new List<Ray>();
        Canvas canvas = GetCanvas(rectTransform);

        if (canvas == null) return rays;

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        switch (canvas.renderMode)
        {
            case RenderMode.ScreenSpaceOverlay:
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    // 对于ScreenSpaceOverlay，从屏幕点创建射线
                    foreach (var corner in corners)
                    {
                        // 暂时只考虑用中心射线
                        //rays.Add(mainCamera.ScreenPointToRay(corner));
                    }
                    // 添加中心点射线
                    Vector3 center = (corners[0] + corners[2]) * 0.5f;
                    rays.Add(mainCamera.ScreenPointToRay(center));
                }
                break;

            case RenderMode.ScreenSpaceCamera:
                Camera canvasCamera = canvas.worldCamera;
                if (canvasCamera == null) canvasCamera = Camera.main;

                if (canvasCamera != null)
                {
                    foreach (var corner in corners)
                    {
                        rays.Add(canvasCamera.ScreenPointToRay(
                            RectTransformUtility.WorldToScreenPoint(canvasCamera, corner)
                        ));
                    }
                }
                break;

            case RenderMode.WorldSpace:
                // 对于WorldSpace，从画布相机或主相机创建射线
                Camera worldCamera = canvas.worldCamera ?? Camera.main;
                if (worldCamera != null)
                {
                    foreach (var corner in corners)
                    {
                        rays.Add(new Ray(worldCamera.transform.position,
                            (corner - worldCamera.transform.position).normalized));
                    }
                }
                break;
        }

        return rays;
    }

    /// <summary>
    /// 获取UI元素所在的画布
    /// </summary>
    private Canvas GetCanvas(RectTransform rectTransform)
    {
        return rectTransform.GetComponentInParent<Canvas>();
    }

    #endregion
}
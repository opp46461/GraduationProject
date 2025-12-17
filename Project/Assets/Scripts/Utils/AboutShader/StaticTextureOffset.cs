using UnityEngine;

/// <summary>
/// 静态设置材质纹理偏移
/// </summary>
[RequireComponent(typeof(Renderer))] // 自动挂载Renderer组件
public class StaticTextureOffset : MonoBehaviour
{
    [Header("纹理偏移量")]
    public Vector2 offset = new Vector2(0.2f, 0.1f); // 自定义偏移值

    private Renderer _renderer;
    private Material _material;

    void Start()
    {
        // 获取渲染组件（MeshRenderer/SpriteRenderer均适用）
        _renderer = GetComponent<Renderer>();
        // 获取材质实例（避免修改原始材质）
        _material = _renderer.material;

        // 设置主纹理偏移
        _material.mainTextureOffset = offset * Mathf.Sin(Time.time);

        // 拓展：如果需要设置纹理平铺（配合偏移）
        // _material.mainTextureScale = new Vector2(2f, 2f); // 纹理缩放2倍
    }

    private void Update()
    {
        _material.mainTextureOffset = offset * Mathf.Sin(Time.time);
    }

    // 可选：重置偏移
    [ContextMenu("Reset Offset")]
    void ResetOffset()
    {
        if (_material != null)
        {
            _material.mainTextureOffset = Vector2.zero;
        }
    }
}
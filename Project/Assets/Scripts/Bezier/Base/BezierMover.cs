using UnityEngine;
using DG.Tweening;
using System.Collections;
using System;

public class BezierMover : MonoBehaviour
{
    [Header("曲线引用")]
    public BezierCurve bezierCurve;

    [Header("运动设置")]
    public float duration = 3f;
    //public Ease easeType = Ease.InOutSine;
    public Ease easeType = Ease.Linear;
    public bool playOnStart = false;
    public bool loop = false;
    public LoopType loopType = LoopType.Yoyo;
    public bool lookForward = false;
    public float lookAhead = 0.01f;

    private Tween moveTween;
    private Coroutine moveCoroutine;

    public Action onPathUpdate;
    public Action onPathComplete;

    void Start()
    {
        if (playOnStart)
        {
            InitializeMovement();
        }
    }

    public Tween InitializeMovement()
    {
        if (bezierCurve == null)
        {
            Debug.LogError("未分配贝塞尔曲线！");
            return null;
        }

        // 使用DOTween的路径方法
        Vector3[] path = bezierCurve.GetCurvePointsByPoint().ToArray();

        if (lookForward)
        {
            moveTween = transform.DOPath(path, duration, PathType.CatmullRom)
                .SetEase(easeType)
                .SetLookAt(lookAhead)
                .OnUpdate(() => OnPathUpdate())
                .OnComplete(() => OnPathComplete());
        }
        else
        {
            moveTween = transform.DOPath(path, duration, PathType.CatmullRom)
                .SetEase(easeType)
                .OnComplete(() => OnPathComplete());
        }

        if (loop)
        {
            moveTween.SetLoops(-1, loopType);
        }

        moveTween.Pause();
        return moveTween;
    }
    public void StartSport()
    {
        if (moveTween != null) moveTween.Restart();
    }

    // 使用自定义缓动控制（更精确的贝塞尔运动）
    public void StartBezierMovement()
    {
        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);

        moveCoroutine = StartCoroutine(MoveAlongBezier());
    }

    IEnumerator MoveAlongBezier()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            t = DOVirtual.EasedValue(0, 1, t, easeType);

            Vector3 targetPos = bezierCurve.CalculateBezierPointByTransform(t);
            transform.position = targetPos;

            // 如果需要朝向运动方向
            if (lookForward && elapsed > 0.01f)
            {
                Vector3 nextPos = bezierCurve.CalculateBezierPointByTransform(Mathf.Min(t + lookAhead, 1));
                Vector3 direction = (nextPos - transform.position).normalized;
                if (direction != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(direction);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 确保到达终点
        transform.position = bezierCurve.CalculateBezierPointByTransform(1);
        OnPathComplete();
    }

    /// <summary>
    /// 每帧更新
    /// </summary>
    void OnPathUpdate()
    {
        onPathUpdate?.Invoke();
        // Debug.Log("当前位置: " + transform.position);
    }

    void OnPathComplete()
    {
        onPathComplete?.Invoke();
        //Debug.Log("运动完成！");
    }

    public void PauseMovement()
    {
        if (moveTween != null && moveTween.IsPlaying())
            moveTween.Pause();
    }

    public void ResumeMovement()
    {
        if (moveTween != null && !moveTween.IsPlaying())
            moveTween.Play();
    }

    public void StopMovement()
    {
        if (moveTween != null)
        {
            moveTween.Pause();
            moveTween.Kill();
        }

        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);
    }

    void OnDestroy()
    {
        StopMovement();
    }
}
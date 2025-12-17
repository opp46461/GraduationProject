using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OnTriggerEvent2D : MonoBehaviour
{
    public Collider2D _collider2D;

    public Action<Collider2D> onTriggerEnter;
    public Action<Collider2D> onTriggerExit;
    public Action<Collider2D> onTriggerStay;

    private void Awake()
    {
        _collider2D = GetComponent<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        onTriggerEnter?.Invoke(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        onTriggerExit?.Invoke(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        onTriggerStay?.Invoke(other);
    }

    public void ColliderEnable(bool _enable)
    {
        if (_collider2D != null) _collider2D.enabled = _enable;
    }
}

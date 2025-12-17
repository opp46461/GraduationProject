using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OnTriggerEvent3D : MonoBehaviour
{
    public Collider _collider3D;

    public Action<Collider, Collider> onTriggerEnter;
    public Action<Collider, Collider> onTriggerExit;
    public Action<Collider, Collider> onTriggerStay;

    private void Awake()
    {
        _collider3D = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        onTriggerEnter?.Invoke(_collider3D, other);
    }

    private void OnTriggerExit(Collider other)
    {
        onTriggerExit?.Invoke(_collider3D, other);
    }

    private void OnTriggerStay(Collider other)
    {
        onTriggerStay?.Invoke(_collider3D, other);
    }

    public void ColliderEnable(bool _enable)
    {
        if (_collider3D != null) _collider3D.enabled = _enable;
    }
}

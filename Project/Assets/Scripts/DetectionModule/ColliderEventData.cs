using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderEventData : IEventData
{
    public Collider thisCollider;
    public Collider other;
}

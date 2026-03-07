using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AICannonFireController : FireController
{
    public sealed override void FireProjectile(Vector3 dir, AmmoType type)
    {
        base.FireProjectile(dir, type);
    }
}

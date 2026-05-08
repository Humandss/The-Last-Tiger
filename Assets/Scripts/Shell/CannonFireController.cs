using UnityEngine;

public class CannonFireController : FireController
{
    protected override bool IsPlayerShell => true;

    [Header("Refs")]
    [SerializeField] private CameraController cameraShotShake;

    [SerializeField, Range(0f, 2f)] private float cameraShakeIntensity = 1.0f;
    [SerializeField, Range(0f, 2f)] private float turretShakeIntensity = 0.35f;

    private void TriggerShotShake()
    {
        if (cameraShotShake) cameraShotShake.TriggerShake(cameraShakeIntensity);
    }

    public sealed override void FireProjectile(Vector3 dir, AmmoType type)
    {
        base.FireProjectile(dir, type);
        TriggerShotShake();
        // 포신 반동은 base FireController에서 TankRecoil이 처리
    }
}

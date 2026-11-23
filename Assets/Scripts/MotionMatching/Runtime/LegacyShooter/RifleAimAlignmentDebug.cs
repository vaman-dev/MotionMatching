using UnityEngine;

public class RifleAimAlignmentDebug : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MouseWorldSphereTest aimSystem;
    [SerializeField] private Transform muzzlePoint;

    [Header("Debug")]
    [SerializeField] private float debugLength = 100f;

    private void Update()
    {
        if (aimSystem == null)
            return;

        if (muzzlePoint == null)
            return;

        if (!aimSystem.HasAimPoint)
            return;

        Vector3 aimPoint =
            aimSystem.AimPoint;

        Vector3 direction =
            aimPoint -
            muzzlePoint.position;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        Debug.DrawRay(
            muzzlePoint.position,
            direction * debugLength,
            Color.green
        );

        Debug.DrawRay(
    muzzlePoint.position,
    muzzlePoint.forward * 100f,
    Color.blue
);
    }
}
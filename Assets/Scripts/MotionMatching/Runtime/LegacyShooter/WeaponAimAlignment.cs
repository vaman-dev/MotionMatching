using UnityEngine;

[DefaultExecutionOrder(10000)]
public class WeaponAimAlignment : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private PlayerAimTargetProvider aimSystem;

    [SerializeField]
    private Transform weaponAimPivot;

    [SerializeField]
    private Transform muzzlePoint;


    // =========================================================
    // SETTINGS
    // =========================================================

    [Header("Settings")]
    [SerializeField]
    private float rotationSharpness = 20f;

    [SerializeField]
    private float returnSharpness = 20f;

    [SerializeField]
    private bool instantAlignmentForDebug = true;


    // =========================================================
    // DEBUG
    // =========================================================

    [Header("Debug")]
    [SerializeField]
    private bool drawDebug = true;


    // =========================================================
    // INTERNAL STATE
    // =========================================================

    private Quaternion baseLocalRotation;
    private bool hasCachedBaseRotation;


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        if (playerController == null)
        {
            playerController =
                GetComponentInParent<PlayerController>();
        }

        if (aimSystem == null)
        {
            aimSystem =
                GetComponentInParent<PlayerAimTargetProvider>();
        }

        CacheBaseRotation();
    }


    private void CacheBaseRotation()
    {
        if (weaponAimPivot == null)
            return;

        baseLocalRotation =
            weaponAimPivot.localRotation;

        hasCachedBaseRotation =
            true;
    }


    // =========================================================
    // LATE UPDATE
    // =========================================================

    private void LateUpdate()
    {
        if (weaponAimPivot == null)
            return;

        if (muzzlePoint == null)
            return;

        if (aimSystem == null)
            return;


        if (!hasCachedBaseRotation)
        {
            CacheBaseRotation();
        }


        bool isAiming =
            playerController != null &&
            playerController.IsAiming;


        // =====================================================
        // RMB NOT HELD
        // =====================================================

        if (!isAiming)
        {
            RestoreBaseRotation();
            return;
        }


        // =====================================================
        // NO VALID AIM POINT
        // =====================================================

        if (!aimSystem.HasAimPoint)
        {
            RestoreBaseRotation();
            return;
        }


        // =====================================================
        // RMB HELD
        // =====================================================

        AlignWeaponToAimPoint();
    }


    // =========================================================
    // AIM ALIGNMENT
    // =========================================================

    private void AlignWeaponToAimPoint()
    {
        Vector3 aimPoint =
            aimSystem.AimPoint;


        // -----------------------------------------------------
        // Always start this frame's calculation from the
        // original local pivot orientation.
        // -----------------------------------------------------

        Quaternion previousLocalRotation =
            weaponAimPivot.localRotation;

        weaponAimPivot.localRotation =
            baseLocalRotation;


        // -----------------------------------------------------
        // Calculate direction AFTER resetting the pivot,
        // because resetting can also change MuzzlePoint.
        // -----------------------------------------------------

        Vector3 desiredDirection =
            aimPoint -
            muzzlePoint.position;


        if (desiredDirection.sqrMagnitude < 0.001f)
        {
            weaponAimPivot.localRotation =
                previousLocalRotation;

            return;
        }


        desiredDirection.Normalize();


        // Current world rotation of the clean/base pivot.
        Quaternion basePivotWorldRotation =
            weaponAimPivot.rotation;


        // Current muzzle direction after:
        //
        // Animator
        // + Rifle animation
        // + SpineRig
        // + Right arm / hand
        // + WeaponSocket
        //
        // but BEFORE our WeaponAimPivot correction.
        Vector3 baseMuzzleForward =
            muzzlePoint.forward;


        Quaternion correction =
            Quaternion.FromToRotation(
                baseMuzzleForward,
                desiredDirection
            );


        Quaternion targetWorldRotation =
            correction *
            basePivotWorldRotation;


        // Restore last visual rotation before smoothing.
        weaponAimPivot.localRotation =
            previousLocalRotation;


        // -----------------------------------------------------
        // APPLY CORRECTION
        // -----------------------------------------------------

        if (instantAlignmentForDebug)
        {
            weaponAimPivot.rotation =
                targetWorldRotation;
        }
        else
        {
            float t =
                1f -
                Mathf.Exp(
                    -rotationSharpness *
                    Time.deltaTime
                );


            weaponAimPivot.rotation =
                Quaternion.Slerp(
                    weaponAimPivot.rotation,
                    targetWorldRotation,
                    t
                );
        }


        // -----------------------------------------------------
        // DEBUG
        // -----------------------------------------------------

        if (drawDebug)
        {
            // BLUE = actual barrel direction
            Debug.DrawRay(
                muzzlePoint.position,
                muzzlePoint.forward * 30f,
                Color.blue
            );


            // GREEN = desired barrel direction
            Debug.DrawRay(
                muzzlePoint.position,
                desiredDirection * 30f,
                Color.green
            );


            // RED = muzzle -> shared aim point
            Debug.DrawLine(
                muzzlePoint.position,
                aimPoint,
                Color.red
            );
        }
    }


    // =========================================================
    // RETURN TO BASE POSE
    // =========================================================

    private void RestoreBaseRotation()
    {
        if (
            weaponAimPivot == null ||
            !hasCachedBaseRotation
        )
        {
            return;
        }


        if (instantAlignmentForDebug)
        {
            weaponAimPivot.localRotation =
                baseLocalRotation;

            return;
        }


        float t =
            1f -
            Mathf.Exp(
                -returnSharpness *
                Time.deltaTime
            );


        weaponAimPivot.localRotation =
            Quaternion.Slerp(
                weaponAimPivot.localRotation,
                baseLocalRotation,
                t
            );
    }


    // =========================================================
    // PUBLIC
    // =========================================================

    public void ReCacheBaseRotation()
    {
        CacheBaseRotation();
    }


    public void Configure(
        PlayerController controller,
        PlayerAimTargetProvider provider,
        Transform pivot,
        Transform muzzle
    )
    {
        playerController = controller;
        aimSystem = provider;
        weaponAimPivot = pivot;
        muzzlePoint = muzzle;

        hasCachedBaseRotation = false;
        CacheBaseRotation();
    }
}

using UnityEngine;
using UnityEngine.Animations.Rigging;

[DefaultExecutionOrder(-1)]
public class PlayerAimRigController : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private PlayerAimTargetProvider aimProvider;

    [SerializeField]
    private Transform spineAimTarget;

    [SerializeField]
    private Rig spineRig;


    // =========================================================
    // AIM SETTINGS
    // =========================================================

    [Header("Aim Settings")]

    [SerializeField]
    private float rigBlendSpeed = 10f;

    [SerializeField]
    [Range(0f, 1f)]
    private float maxSpineRigWeight = 0.20f;


    // =========================================================
    // DEBUG
    // =========================================================

    [Header("Debug")]
    [SerializeField]
    private bool drawAimRay = true;


    // =========================================================
    // INTERNAL
    // =========================================================

    private float currentRigWeight;


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        if (playerController == null)
        {
            playerController =
                GetComponent<PlayerController>();
        }


        if (aimProvider == null)
        {
            aimProvider =
                GetComponent<PlayerAimTargetProvider>();
        }


        // Start with the procedural spine rig disabled.
        if (spineRig != null)
        {
            spineRig.weight = 0f;
        }


        currentRigWeight = 0f;
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (playerController == null)
            return;


        bool shouldAim =
            playerController.IsAiming;


        // -----------------------------------------------------
        // UPDATE SHARED SPINE TARGET
        // -----------------------------------------------------

        if (
            shouldAim &&
            aimProvider != null &&
            aimProvider.HasAimPoint &&
            spineAimTarget != null
        )
        {
            UpdateSpineAimTarget();
        }


        // -----------------------------------------------------
        // UPDATE RIG WEIGHT
        // -----------------------------------------------------

        UpdateSpineRigWeight(
            shouldAim
        );
    }


    // =========================================================
    // SPINE TARGET
    // =========================================================

    private void UpdateSpineAimTarget()
    {
        spineAimTarget.position =
            aimProvider.AimPoint;


        if (drawAimRay)
        {
            Debug.DrawLine(
                transform.position,
                aimProvider.AimPoint,
                Color.red
            );
        }
    }


    // =========================================================
    // RIG WEIGHT
    // =========================================================

    private void UpdateSpineRigWeight(
        bool shouldAim
    )
    {
        // While aiming, blend only up to the configured
        // maximum spine influence instead of forcing 1.0.
        float targetWeight =
            shouldAim
                ? maxSpineRigWeight
                : 0f;


        currentRigWeight =
            Mathf.MoveTowards(
                currentRigWeight,
                targetWeight,
                rigBlendSpeed *
                Time.deltaTime
            );


        if (spineRig != null)
        {
            spineRig.weight =
                currentRigWeight;
        }
    }
}
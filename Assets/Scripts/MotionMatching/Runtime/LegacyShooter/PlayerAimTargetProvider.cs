using UnityEngine;

[DefaultExecutionOrder(-10)]
public class PlayerAimTargetProvider : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField]
    private Camera aimCamera;


    // =========================================================
    // AIM SETTINGS
    // =========================================================

    [Header("Aim Settings")]
    [SerializeField]
    private float maxAimDistance = 200f;

    [SerializeField]
    private LayerMask aimLayerMask = ~0;

    [SerializeField]
    private QueryTriggerInteraction triggerInteraction =
        QueryTriggerInteraction.Ignore;

    [SerializeField]
    private Transform ignoredRoot;

    private readonly RaycastHit[] raycastResults =
        new RaycastHit[16];


    // =========================================================
    // DEBUG
    // =========================================================

    [Header("Debug")]
    [SerializeField]
    private bool drawDebugRay = true;

    [SerializeField]
    private float debugSphereRadius = 0.08f;


    // =========================================================
    // PUBLIC DATA
    // =========================================================

    public Vector3 AimPoint { get; private set; }

    public Vector3 AimDirection { get; private set; }

    public Ray AimRay { get; private set; }

    public bool HasAimPoint { get; private set; }

    public bool HitSomething { get; private set; }

    public RaycastHit HitInfo { get; private set; }


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        if (
            aimCamera == null &&
            Camera.main != null
        )
        {
            aimCamera =
                Camera.main;
        }

        if (ignoredRoot == null)
        {
            ignoredRoot = transform;
        }
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        CalculateAimPoint();
    }


    // =========================================================
    // AIM CALCULATION
    // =========================================================

    private void CalculateAimPoint()
    {
        if (aimCamera == null)
        {
            HasAimPoint = false;
            HitSomething = false;
            return;
        }


        // Center of rendered camera:
        //
        // X = 0.5
        // Y = 0.5
        //
        // This becomes the single authoritative
        // reticle / camera aim ray.
        AimRay =
            aimCamera.ViewportPointToRay(
                new Vector3(
                    0.5f,
                    0.5f,
                    0f
                )
            );


        AimDirection =
            AimRay.direction.normalized;


        // -----------------------------------------------------
        // CAMERA RAYCAST
        // -----------------------------------------------------

        int hitCount =
            Physics.RaycastNonAlloc(
                AimRay,
                raycastResults,
                maxAimDistance,
                aimLayerMask,
                triggerInteraction
            );

        bool foundValidHit = false;
        RaycastHit closestHit = default;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastResults[i];

            if (
                hit.collider == null ||
                IsIgnoredCollider(hit.collider)
            )
            {
                continue;
            }

            if (hit.distance >= closestDistance)
                continue;

            closestDistance = hit.distance;
            closestHit = hit;
            foundValidHit = true;
        }

        if (foundValidHit)
        {
            AimPoint =
                closestHit.point;

            HitInfo =
                closestHit;

            HitSomething =
                true;
        }
        else
        {
            AimPoint =
                AimRay.origin +
                AimRay.direction *
                maxAimDistance;

            HitInfo =
                default;

            HitSomething =
                false;
        }


        HasAimPoint =
            true;


        // -----------------------------------------------------
        // DEBUG
        // -----------------------------------------------------

        if (drawDebugRay)
        {
            Debug.DrawLine(
                AimRay.origin,
                AimPoint,
                HitSomething
                    ? Color.green
                    : Color.yellow
            );
        }
    }


    private bool IsIgnoredCollider(Collider collider)
    {
        Transform hitTransform = collider.transform;

        return
            ignoredRoot != null &&
            hitTransform.IsChildOf(ignoredRoot);
    }


    // =========================================================
    // SCENE DEBUG
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (
            !Application.isPlaying ||
            !HasAimPoint
        )
        {
            return;
        }


        Gizmos.DrawWireSphere(
            AimPoint,
            debugSphereRadius
        );
    }
}

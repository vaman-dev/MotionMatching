using UnityEngine;

public class PlayerInteractionSystem : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerAnimator playerAnimator;
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private Transform interactionPoint;


    // =========================================================
    // INTERACTION SETTINGS
    // =========================================================

    [Header("Interaction Settings")]
    [SerializeField] private float interactionRadius = 1.5f;
    [SerializeField] private LayerMask weaponLayer;


    // =========================================================
    // DEBUG SETTINGS
    // =========================================================

    [Header("Debug")]
    [SerializeField] private bool printDetectionLogs = true;


    // =========================================================
    // DETECTION
    // =========================================================

    private readonly Collider[] interactionResults =
        new Collider[10];

    private WeaponPickupItem nearbyWeapon;
    private WeaponPickupItem equippedWeapon;

    private WeaponPickupItem previouslyDetectedWeapon;


    // =========================================================
    // PUBLIC ACCESS
    // =========================================================

    public WeaponPickupItem NearbyWeapon =>
        nearbyWeapon;

    public WeaponPickupItem EquippedWeapon =>
        equippedWeapon;

    public bool HasWeapon =>
        equippedWeapon != null;

    public bool HasDetectedWeapon =>
        nearbyWeapon != null;


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        if (inputReader == null)
        {
            inputReader =
                GetComponent<PlayerInputReader>();
        }

        if (playerAnimator == null)
        {
            playerAnimator =
                GetComponentInChildren<PlayerAnimator>();
        }

        if (interactionPoint == null)
        {
            interactionPoint = transform;
        }
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        FindNearbyWeapon();

        HandleDetectionDebug();

        HandleInteraction();
    }


    // =========================================================
    // WEAPON DETECTION
    // =========================================================

    private void FindNearbyWeapon()
    {
        nearbyWeapon = null;

        Vector3 center =
            interactionPoint != null
                ? interactionPoint.position
                : transform.position;

        int hitCount =
            Physics.OverlapSphereNonAlloc(
                center,
                interactionRadius,
                interactionResults,
                weaponLayer,
                QueryTriggerInteraction.Collide
            );

        float closestDistance =
            float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit =
                interactionResults[i];

            if (hit == null)
                continue;

            WeaponPickupItem weapon =
                hit.GetComponentInParent<WeaponPickupItem>();

            if (weapon == null)
                continue;

            if (weapon.IsEquipped)
                continue;

            float distance =
                Vector3.SqrMagnitude(
                    weapon.transform.position -
                    center
                );

            if (distance < closestDistance)
            {
                closestDistance =
                    distance;

                nearbyWeapon =
                    weapon;
            }
        }
    }


    // =========================================================
    // DETECTION DEBUG
    // =========================================================

    private void HandleDetectionDebug()
    {
        // No change in detected weapon.
        if (nearbyWeapon == previouslyDetectedWeapon)
            return;


        // New weapon detected.
        if (nearbyWeapon != null)
        {
            if (printDetectionLogs)
            {
                Debug.Log(
                    $"Weapon Detected: {nearbyWeapon.name}",
                    nearbyWeapon
                );
            }
        }


        // Weapon left the interaction radius.
        else if (
            previouslyDetectedWeapon != null &&
            printDetectionLogs
        )
        {
            Debug.Log(
                $"Weapon Left Interaction Radius: {previouslyDetectedWeapon.name}",
                previouslyDetectedWeapon
            );
        }


        previouslyDetectedWeapon =
            nearbyWeapon;
    }


    // =========================================================
    // INTERACTION INPUT
    // =========================================================

    private void HandleInteraction()
    {
        if (inputReader == null)
            return;

        if (!inputReader.InteractPressedThisFrame)
            return;


        if (nearbyWeapon == null)
        {
            if (printDetectionLogs)
            {
                Debug.Log(
                    "Interact pressed, but no weapon is inside the interaction radius.",
                    this
                );
            }

            return;
        }


        if (equippedWeapon != null)
        {
            if (printDetectionLogs)
            {
                Debug.Log(
                    "Player already has a weapon equipped.",
                    this
                );
            }

            return;
        }


        EquipWeapon(
            nearbyWeapon
        );
    }


    // =========================================================
    // EQUIP WEAPON
    // =========================================================

    private void EquipWeapon(
        WeaponPickupItem weapon
    )
    {
        if (weapon == null)
        {
            Debug.LogError(
                "EquipWeapon received NULL weapon.",
                this
            );

            return;
        }


        if (weaponSocket == null)
        {
            Debug.LogError(
                "WeaponSocket is NOT assigned on PlayerInteractionSystem.",
                this
            );

            return;
        }


        if (printDetectionLogs)
        {
            Debug.Log(
                $"Trying to equip {weapon.name} to {weaponSocket.name}",
                this
            );
        }


        // -----------------------------------------
        // Attach weapon to right-hand socket
        // -----------------------------------------

        weapon.AttachToWeaponSocket(
            weaponSocket
        );


        // -----------------------------------------
        // Store equipped weapon
        // -----------------------------------------

        equippedWeapon =
            weapon;

        nearbyWeapon =
            null;

        previouslyDetectedWeapon =
            null;


        // -----------------------------------------
        // Switch Animator to Rifle Locomotion
        // -----------------------------------------

        if (playerAnimator != null)
        {
            playerAnimator.SetHasRifle(
                true
            );
        }
        else
        {
            Debug.LogWarning(
                "PlayerAnimator is not assigned.",
                this
            );
        }


        // -----------------------------------------
        // Debug
        // -----------------------------------------

        if (printDetectionLogs)
        {
            Debug.Log(
                $"Weapon Equipped Successfully: {weapon.name}",
                weapon
            );
        }
    }


    // =========================================================
    // DEBUG GIZMO
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Vector3 center =
            interactionPoint != null
                ? interactionPoint.position
                : transform.position;


        // Yellow = no weapon detected.
        // Green  = weapon detected.
        Gizmos.color =
            nearbyWeapon != null
                ? Color.green
                : Color.yellow;


        Gizmos.DrawWireSphere(
            center,
            interactionRadius
        );
    }
}
using UnityEngine;

public class WeaponPickupItem : MonoBehaviour
{
    // =========================================================
    // WEAPON ROOT
    // =========================================================

    [Header("Weapon Root")]
    [SerializeField] private Transform weaponRoot;


    // =========================================================
    // EQUIPPED TRANSFORM
    // =========================================================

    [Header("Equipped Local Transform")]
    [SerializeField] private Vector3 equippedLocalPosition;

    [SerializeField] private Vector3 equippedLocalEulerAngles;

    [SerializeField] private Vector3 equippedLocalScale = Vector3.one;


    // =========================================================
    // WEAPON POINTS
    // =========================================================

    [Header("Weapon Points")]
    [SerializeField] private Transform muzzlePoint;

    // We are not using IK yet,
    // but having this point ready on the rifle is useful
    // for the next phase.
    [SerializeField] private Transform leftHandGripPoint;


    // =========================================================
    // PICKUP SETTINGS
    // =========================================================

    [Header("Pickup Settings")]
    [SerializeField] private bool disablePhysicsOnPickup = true;

    [SerializeField] private bool disableCollidersOnPickup = true;


    // =========================================================
    // INTERNAL REFERENCES
    // =========================================================

    private Rigidbody[] rigidbodies;

    private Collider[] colliders;

    private bool isEquipped;


    // =========================================================
    // PUBLIC ACCESS
    // =========================================================

    public Transform WeaponRoot =>
        weaponRoot != null
            ? weaponRoot
            : transform;

    public Transform MuzzlePoint =>
        muzzlePoint;

    public Transform LeftHandGripPoint =>
        leftHandGripPoint;

    public bool IsEquipped =>
        isEquipped;


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        if (weaponRoot == null)
        {
            weaponRoot = transform;
        }

        rigidbodies =
            WeaponRoot.GetComponentsInChildren<Rigidbody>(
                true
            );

        colliders =
            WeaponRoot.GetComponentsInChildren<Collider>(
                true
            );
    }


    // =========================================================
    // EQUIP
    // =========================================================

    public void AttachToWeaponSocket(Transform weaponSocket)
    {
        if (weaponSocket == null)
        {
            Debug.LogWarning(
                "WeaponPickupItem: Weapon Socket is null.",
                this
            );

            return;
        }

        Transform root = WeaponRoot;


        // Disable world physics before attaching.
        if (disablePhysicsOnPickup)
        {
            DisablePhysics();
        }

        if (disableCollidersOnPickup)
        {
            DisableColliders();
        }


        // Parent rifle to the player's right-hand socket.
        root.SetParent(
            weaponSocket,
            false
        );


        // Apply weapon-specific hand placement.
        root.localPosition =
            equippedLocalPosition;

        root.localEulerAngles =
            equippedLocalEulerAngles;

        root.localScale =
            equippedLocalScale;


        isEquipped = true;
    }


    // =========================================================
    // PHYSICS
    // =========================================================

    private void DisablePhysics()
    {
        if (rigidbodies == null)
            return;

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb =
                rigidbodies[i];

            if (rb == null)
                continue;

            rb.linearVelocity =
                Vector3.zero;

            rb.angularVelocity =
                Vector3.zero;

            rb.useGravity =
                false;

            rb.isKinematic =
                true;
        }
    }


    // =========================================================
    // COLLIDERS
    // =========================================================

    private void DisableColliders()
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled =
                    false;
            }
        }
    }
}
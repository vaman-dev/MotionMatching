using UnityEngine;
using UnityEngine.Animations.Rigging;

public class PlayerLeftHandIK : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField] private PlayerInteractionSystem interactionSystem;

    [SerializeField] private Rig handRig;

    [SerializeField] private Transform leftHandTarget;


    // =========================================================
    // SETTINGS
    // =========================================================

    [Header("Settings")]
    [SerializeField] private float blendSpeed = 10f;


    // =========================================================
    // INTERNAL
    // =========================================================

    private float currentWeight;


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        if (interactionSystem == null)
        {
            interactionSystem =
                GetComponentInParent<PlayerInteractionSystem>();
        }

        if (handRig != null)
        {
            handRig.weight = 0f;
        }
    }


    // =========================================================
    // LATE UPDATE
    // =========================================================

    private void LateUpdate()
    {
        UpdateLeftHandTarget();

        UpdateRigWeight();
    }


    // =========================================================
    // TARGET UPDATE
    // =========================================================

    private void UpdateLeftHandTarget()
    {
        if (interactionSystem == null)
            return;

        WeaponPickupItem weapon =
            interactionSystem.EquippedWeapon;

        if (weapon == null)
            return;

        Transform grip =
            weapon.LeftHandGripPoint;

        if (grip == null)
            return;

        if (leftHandTarget == null)
            return;


        leftHandTarget.SetPositionAndRotation(
            grip.position,
            grip.rotation
        );
    }


    // =========================================================
    // WEIGHT
    // =========================================================

    private void UpdateRigWeight()
    {
        bool shouldUseIK =
            interactionSystem != null &&
            interactionSystem.EquippedWeapon != null &&
            interactionSystem.EquippedWeapon.LeftHandGripPoint != null;


        float targetWeight =
            shouldUseIK
                ? 1f
                : 0f;


        currentWeight =
            Mathf.MoveTowards(
                currentWeight,
                targetWeight,
                blendSpeed * Time.deltaTime
            );


        if (handRig != null)
        {
            handRig.weight =
                currentWeight;
        }
    }
}
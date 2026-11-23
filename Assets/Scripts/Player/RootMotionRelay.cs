using UnityEngine;

[RequireComponent(typeof(Animator))]
public class RootMotionRelay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;

    [Header("Root Motion")]
    [SerializeField] private bool useRootMotionMovement = true;
    [SerializeField] private bool useRootMotionRotation = false;
    [SerializeField] private bool ignoreRootMotionY = true;
    [SerializeField] private bool ignoreMovementRootMotionWhenIdle = true;
    [SerializeField] private string speedParameter = "Speed";

    private int speedHash;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();

        if (animator != null)
        {
            animator.applyRootMotion = true;
            speedHash = Animator.StringToHash(speedParameter);
        }
    }

    private void OnAnimatorMove()
    {
        if (!useRootMotionMovement)
            return;

        if (animator == null || playerController == null)
            return;

        Vector3 deltaPosition = animator.deltaPosition;

        // Idle clips can contain tiny authored root offsets. Keep gravity
        // running, but do not let those offsets drift the player.
        if (
            ignoreMovementRootMotionWhenIdle &&
            Mathf.Abs(animator.GetFloat(speedHash)) < 0.01f
        )
        {
            deltaPosition = Vector3.zero;
        }

        if (ignoreRootMotionY)
            deltaPosition.y = 0f;

        playerController.ApplyRootMotionMovement(deltaPosition);

        if (useRootMotionRotation)
            playerController.ApplyRootMotionRotation(animator.deltaRotation);
    }
}

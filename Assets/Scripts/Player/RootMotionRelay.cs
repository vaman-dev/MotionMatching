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
    [Tooltip("Tagged authored actions keep root travel when the locomotion Speed is zero.")]
    [SerializeField] private string rootMotionActionTag = "RootMotionAction";

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
        if (!isActiveAndEnabled || !useRootMotionMovement)
            return;

        if (animator == null || playerController == null || !playerController.isActiveAndEnabled)
            return;

        Vector3 deltaPosition = animator.deltaPosition;

        // In aim mode, input owns the horizontal direction while the selected
        // animation clip only supplies stride length.  This prevents clips
        // with forward-authored root motion from making S move forward.
        if (playerController.IsAiming)
        {
            Vector3 moveDirection =
                playerController.GetCameraRelativeMoveDirection(
                    playerController.InputReader.Move
                );

            float horizontalDistance =
                new Vector2(
                    deltaPosition.x,
                    deltaPosition.z
                ).magnitude;

            deltaPosition.x =
                moveDirection.x * horizontalDistance;

            deltaPosition.z =
                moveDirection.z * horizontalDistance;
        }

        // Idle clips can contain tiny authored root offsets. Gravity is
        // scheduled independently by PlayerController.LateUpdate.
        if (
            ignoreMovementRootMotionWhenIdle &&
            Mathf.Abs(animator.GetFloat(speedHash)) < 0.01f &&
            !HasAuthoredActionRootMotion()
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

    private bool HasAuthoredActionRootMotion()
    {
        if (string.IsNullOrEmpty(rootMotionActionTag)) return false;
        return animator.GetCurrentAnimatorStateInfo(0).IsTag(rootMotionActionTag) ||
            (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag(rootMotionActionTag));
    }
}

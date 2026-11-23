using UnityEngine;

/// <summary>
/// Handles the core Phase 1 player systems:
/// - Locomotion
/// - Character rotation
/// - Ground detection
/// - Jump
/// - Dive roll
/// - Gravity
/// - Root motion movement
///
/// Weapon, aiming, IK and rifle systems are intentionally
/// NOT handled in this phase.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerAnimator playerAnimator;
    [SerializeField] private Transform cameraTransform;


    // =========================================================
    // ROTATION
    // =========================================================

    [Header("Rotation")]
    [SerializeField] private float rotationSharpness = 18f;


    // =========================================================
    // GROUND CHECK
    // =========================================================

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private float groundCheckVerticalOffset = 0.05f;
    [SerializeField] private LayerMask groundLayer;


    // =========================================================
    // JUMP
    // =========================================================

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.4f;

    [SerializeField] private float coyoteTime = 0.12f;

    [SerializeField] private float minJumpStateTime = 0.15f;

    [SerializeField] private float groundIgnoreTimeAfterJump = 0.12f;

    [SerializeField] private float jumpRetriggerBlockTime = 0.25f;

    [SerializeField] private float jumpForwardForce = 2.5f;

    [SerializeField] private float jumpForwardDamping = 8f;


    // =========================================================
    // DIVE ROLL
    // =========================================================

    [Header("Dive Roll")]
    [SerializeField] private float diveRollInputWindow = 0.5f;

    [SerializeField] private float minDiveRollInputDelay = 0.08f;

    [SerializeField] private float minDiveRollStateTime = 0.9f;

    [SerializeField] private float diveStartDownwardVelocity = -3f;


    // =========================================================
    // GRAVITY
    // =========================================================

    [Header("Gravity")]
    [SerializeField] private float gravity = -25f;

    [SerializeField] private float groundedGravity = -2f;


    // =========================================================
    // COMPONENTS
    // =========================================================

    private CharacterController characterController;
    private PlayerStateMachine stateMachine;


    // =========================================================
    // JUMP / GROUND STATE
    // =========================================================

    private float verticalVelocity;

    private float coyoteTimer;

    private float groundIgnoreTimer;

    private float nextAllowedJumpTime;

    private float lastJumpStartTime;


    // =========================================================
    // STATE FLAGS
    // =========================================================

    private bool isGrounded;

    private bool jumpConsumed;

    private bool jumpInputLocked;

    private bool diveRollConsumed;


    // =========================================================
    // ROOT MOTION
    // =========================================================

    private int ignoreHorizontalRootMotionFrames;

    private Vector3 jumpForwardVelocity;


    // =========================================================
    // PUBLIC ACCESS
    // =========================================================

    public PlayerInputReader InputReader => inputReader;

    public PlayerAnimator PlayerAnimator => playerAnimator;

    public CharacterController CharacterController => characterController;

    public Transform CameraTransform => cameraTransform;

    public bool IsGrounded => isGrounded;

    public float MinJumpStateTime => minJumpStateTime;

    public float MinDiveRollStateTime => minDiveRollStateTime;


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (inputReader == null)
        {
            inputReader = GetComponent<PlayerInputReader>();
        }

        if (playerAnimator == null)
        {
            playerAnimator = GetComponentInChildren<PlayerAnimator>();
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        stateMachine = new PlayerStateMachine();

        stateMachine.RegisterState(
            PlayerStateId.Locomotion,
            new PlayerLocomotionState(this)
        );

        stateMachine.RegisterState(
            PlayerStateId.Jump,
            new PlayerJumpState(this)
        );

        stateMachine.RegisterState(
            PlayerStateId.DiveRoll,
            new PlayerDiveRollState(this)
        );
    }


    private void Start()
    {
        RefreshGroundCheck();

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
        }

        if (playerAnimator != null)
        {
            playerAnimator.SetGrounded(isGrounded);
        }

        stateMachine.ChangeState(PlayerStateId.Locomotion);
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        stateMachine.Tick();
    }


    private void LateUpdate()
    {
        UpdateGroundIgnoreTimer();

        RefreshGroundCheck();

        UpdateCoyoteTimer();

        UpdateJumpInputLock();

        if (playerAnimator != null)
        {
            playerAnimator.SetGrounded(isGrounded);
        }
    }


    // =========================================================
    // STATE MACHINE
    // =========================================================

    public void ChangeState(PlayerStateId stateId)
    {
        stateMachine.ChangeState(stateId);
    }


    // =========================================================
    // JUMP VALIDATION
    // =========================================================

    public bool CanJump()
    {
        if (Time.time < nextAllowedJumpTime)
            return false;

        if (jumpInputLocked)
            return false;

        if (jumpConsumed)
            return false;

        if (coyoteTimer <= 0f)
            return false;

        return true;
    }


    // =========================================================
    // DIVE ROLL VALIDATION
    // =========================================================

    public bool CanStartDiveRoll()
    {
        if (diveRollConsumed)
            return false;

        // Ground dive roll.
        if (isGrounded)
            return true;

        // Air dive roll is only allowed after a jump.
        if (!jumpConsumed)
            return false;

        float timeSinceJump = Time.time - lastJumpStartTime;

        if (timeSinceJump < minDiveRollInputDelay)
            return false;

        if (timeSinceJump > diveRollInputWindow)
            return false;

        return true;
    }


    // =========================================================
    // START JUMP
    // =========================================================

    public void StartJump()
    {
        jumpConsumed = true;

        jumpInputLocked = true;

        nextAllowedJumpTime =
            Time.time + jumpRetriggerBlockTime;

        lastJumpStartTime = Time.time;

        coyoteTimer = 0f;

        groundIgnoreTimer =
            groundIgnoreTimeAfterJump;

        verticalVelocity =
            Mathf.Sqrt(
                jumpHeight * -2f * gravity
            );

        Vector2 moveInput =
            inputReader != null
                ? inputReader.Move
                : Vector2.zero;

        Vector3 jumpDirection =
            GetCameraRelativeMoveDirection(moveInput);

        // If the player jumps without movement input,
        // use the character's forward direction.
        if (jumpDirection.sqrMagnitude < 0.01f)
        {
            jumpDirection = transform.forward;
        }

        jumpDirection.y = 0f;

        jumpDirection.Normalize();

        jumpForwardVelocity =
            jumpDirection * jumpForwardForce;

        isGrounded = false;

        if (playerAnimator != null)
        {
            playerAnimator.SetGrounded(false);
        }
    }


    // =========================================================
    // START DIVE ROLL
    // =========================================================

    public void StartDiveRoll()
    {
        diveRollConsumed = true;

        jumpInputLocked = true;

        coyoteTimer = 0f;

        if (verticalVelocity > diveStartDownwardVelocity)
        {
            verticalVelocity =
                diveStartDownwardVelocity;
        }

        isGrounded = false;

        if (playerAnimator != null)
        {
            playerAnimator.SetGrounded(false);

            playerAnimator.SetDiveRolling(true);
        }
    }


    // =========================================================
    // ROTATION
    // =========================================================

    /// <summary>
    /// Rotates the player towards a supplied world-space direction.
    /// Used by locomotion states.
    /// </summary>
    public void RotateTowardsDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized
            );

        float t =
            1f - Mathf.Exp(
                -rotationSharpness * Time.deltaTime
            );

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                t
            );
    }


    // =========================================================
    // CAMERA RELATIVE MOVEMENT
    // =========================================================

    public Vector3 GetCameraRelativeMoveDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f)
        {
            return Vector3.zero;
        }

        // Fallback if no camera is available.
        if (cameraTransform == null)
        {
            Vector3 fallbackDirection =
                transform.right * input.x +
                transform.forward * input.y;

            fallbackDirection.y = 0f;

            return fallbackDirection.normalized;
        }

        Vector3 cameraForward =
            cameraTransform.forward;

        Vector3 cameraRight =
            cameraTransform.right;

        cameraForward.y = 0f;

        cameraRight.y = 0f;

        cameraForward.Normalize();

        cameraRight.Normalize();

        Vector3 moveDirection =
            cameraRight * input.x +
            cameraForward * input.y;

        moveDirection.y = 0f;

        if (moveDirection.sqrMagnitude < 0.01f)
        {
            return Vector3.zero;
        }

        return moveDirection.normalized;
    }


    public void RotateTowardsCameraForward()
    {
        if (cameraTransform == null)
            return;

        Vector3 forward = cameraTransform.forward;

        // Ignore camera vertical tilt.
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.01f)
            return;

        forward.Normalize();

        Quaternion targetRotation =
            Quaternion.LookRotation(forward);

        float t =
            1f - Mathf.Exp(
                -rotationSharpness * Time.deltaTime
            );

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                t
            );
    }   

    // =========================================================
    // ROOT MOTION CONTROL
    // =========================================================

    public void IgnoreHorizontalRootMotionForFrames(int frameCount)
    {
        ignoreHorizontalRootMotionFrames =
            Mathf.Max(
                ignoreHorizontalRootMotionFrames,
                frameCount
            );
    }


    public void ApplyRootMotionMovement(Vector3 rootMotionDelta)
    {
        ApplyGravity();

        // Vertical motion is controlled through our
        // CharacterController gravity/jump system.
        rootMotionDelta.y = 0f;

        if (ignoreHorizontalRootMotionFrames > 0)
        {
            ignoreHorizontalRootMotionFrames--;

            rootMotionDelta.x = 0f;

            rootMotionDelta.z = 0f;
        }

        Vector3 gravityDelta =
            Vector3.up *
            verticalVelocity *
            Time.deltaTime;

        Vector3 jumpForwardDelta =
            jumpForwardVelocity *
            Time.deltaTime;

        characterController.Move(
            rootMotionDelta +
            jumpForwardDelta +
            gravityDelta
        );

        if (!isGrounded)
        {
            jumpForwardVelocity =
                Vector3.MoveTowards(
                    jumpForwardVelocity,
                    Vector3.zero,
                    jumpForwardDamping *
                    Time.deltaTime
                );
        }
        else
        {
            jumpForwardVelocity =
                Vector3.zero;
        }
    }


    public void ApplyRootMotionRotation(
        Quaternion rootMotionRotation
    )
    {
        transform.rotation *= rootMotionRotation;
    }


    // =========================================================
    // GRAVITY
    // =========================================================

    private void ApplyGravity()
    {
        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity =
                groundedGravity;
        }
        else
        {
            verticalVelocity +=
                gravity * Time.deltaTime;
        }
    }


    // =========================================================
    // GROUND CHECK
    // =========================================================

    private void RefreshGroundCheck()
    {
        if (groundIgnoreTimer > 0f)
        {
            isGrounded = false;

            return;
        }

        Vector3 checkPosition =
            GetGroundCheckPosition();

        isGrounded =
            Physics.CheckSphere(
                checkPosition,
                groundCheckRadius,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );
    }


    private Vector3 GetGroundCheckPosition()
    {
        if (groundCheckPoint != null)
        {
            return groundCheckPoint.position;
        }

        return transform.position +
               Vector3.up *
               groundCheckVerticalOffset;
    }


    private void UpdateGroundIgnoreTimer()
    {
        if (groundIgnoreTimer > 0f)
        {
            groundIgnoreTimer -=
                Time.deltaTime;
        }
    }


    // =========================================================
    // COYOTE TIME
    // =========================================================

    private void UpdateCoyoteTimer()
    {
        if (isGrounded && verticalVelocity <= 0f)
        {
            coyoteTimer =
                coyoteTime;

            jumpConsumed =
                false;

            diveRollConsumed =
                false;
        }
        else
        {
            coyoteTimer -=
                Time.deltaTime;

            if (coyoteTimer < 0f)
            {
                coyoteTimer = 0f;
            }
        }
    }


    // =========================================================
    // JUMP INPUT LOCK
    // =========================================================

    private void UpdateJumpInputLock()
    {
        if (inputReader == null)
            return;

        if (
            inputReader.JumpReleasedThisFrame ||
            !inputReader.JumpHeld
        )
        {
            jumpInputLocked = false;
        }
    }


    // =========================================================
    // DEBUG GIZMOS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Vector3 checkPosition;

        if (groundCheckPoint != null)
        {
            checkPosition =
                groundCheckPoint.position;
        }
        else
        {
            checkPosition =
                transform.position +
                Vector3.up *
                groundCheckVerticalOffset;
        }

        Gizmos.DrawWireSphere(
            checkPosition,
            groundCheckRadius
        );
    }
}
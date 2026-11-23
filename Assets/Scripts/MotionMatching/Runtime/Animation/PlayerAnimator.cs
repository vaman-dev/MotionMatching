using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField] private Animator animator;


    // =========================================================
    // LOCOMOTION PARAMETERS
    // =========================================================

    [Header("Locomotion Parameter Names")]
    [SerializeField] private string moveXParameter = "MoveX";
    [SerializeField] private string moveZParameter = "MoveZ";
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string isMovingParameter = "IsMoving";
    [SerializeField] private string isSprintingParameter = "IsSprinting";


    // =========================================================
    // JUMP PARAMETERS
    // =========================================================

    [Header("Jump Parameter Names")]
    [SerializeField] private string jumpParameter = "Jump";
    [SerializeField] private string isGroundedParameter = "IsGrounded";


    // =========================================================
    // DIVE ROLL PARAMETERS
    // =========================================================

    [Header("Dive Roll Parameter Names")]
    [SerializeField] private string diveRollParameter = "DiveRoll";
    [SerializeField] private string isDiveRollingParameter = "IsDiveRolling";


    // =========================================================
    // RIFLE PARAMETERS
    // =========================================================

    [Header("Rifle Parameter Names")]
    [SerializeField] private string hasRifleParameter = "HasRifle";


    // =========================================================
    // LOCOMOTION SPEED VALUES
    // =========================================================

    [Header("Locomotion Speed Values")]
    [SerializeField] private float idleSpeedValue = 0f;
    [SerializeField] private float walkSpeedValue = 0.5f;
    [SerializeField] private float sprintSpeedValue = 1f;


    // =========================================================
    // Aiming
    // =========================================================
    [SerializeField] private string isAimingParameter = "isAiming";


    // =========================================================
    // RIFLE SPRINT SETTINGS
    // =========================================================

    [Header("Rifle Sprint Settings")]

    [Tooltip(
        "When enabled, rifle sprinting is only allowed while moving forward."
    )]
    [SerializeField] private bool rifleSprintForwardOnly = false;

    [Tooltip(
        "Minimum forward input required for rifle sprint."
    )]
    [Range(0f, 1f)]
    [SerializeField] private float rifleSprintForwardThreshold = 0.8f;

    [Tooltip(
        "Maximum horizontal input allowed while starting rifle sprint."
    )]
    [Range(0f, 1f)]
    [SerializeField] private float rifleSprintSideThreshold = 0.25f;


    // =========================================================
    // ANIMATION SMOOTHING
    // =========================================================

    [Header("Animation Smoothing")]
    [SerializeField] private float dampSpeed = 12f;


    // =========================================================
    // DEBUG
    // =========================================================

    [Header("Debug")]
    [SerializeField] private bool debugRifleState = true;


    // =========================================================
    // ANIMATOR PARAMETER HASHES
    // =========================================================

    private int moveXHash;
    private int moveZHash;
    private int speedHash;

    private int isMovingHash;
    private int isSprintingHash;

    private int jumpHash;
    private int isGroundedHash;

    private int diveRollHash;
    private int isDiveRollingHash;

    private int hasRifleHash;
    private int isAimingHash;


    // =========================================================
    // CURRENT SMOOTHED VALUES
    // =========================================================

    private float currentMoveX;
    private float currentMoveZ;
    private float currentSpeed;


    // =========================================================
    // PUBLIC ACCESS
    // =========================================================

    public Animator UnityAnimator => animator;


    public bool HasRifle
    {
        get
        {
            if (animator == null)
                return false;

            return animator.GetBool(hasRifleHash);
        }
    }


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }


        // -------------------------
        // Locomotion
        // -------------------------

        moveXHash =
            Animator.StringToHash(
                moveXParameter
            );

        moveZHash =
            Animator.StringToHash(
                moveZParameter
            );

        speedHash =
            Animator.StringToHash(
                speedParameter
            );

        isMovingHash =
            Animator.StringToHash(
                isMovingParameter
            );

        isSprintingHash =
            Animator.StringToHash(
                isSprintingParameter
            );


        // -------------------------
        // Jump
        // -------------------------

        jumpHash =
            Animator.StringToHash(
                jumpParameter
            );

        isGroundedHash =
            Animator.StringToHash(
                isGroundedParameter
            );


        // -------------------------
        // Dive Roll
        // -------------------------

        diveRollHash =
            Animator.StringToHash(
                diveRollParameter
            );

        isDiveRollingHash =
            Animator.StringToHash(
                isDiveRollingParameter
            );


        // -------------------------
        // Rifle
        // -------------------------

        hasRifleHash =
            Animator.StringToHash(
                hasRifleParameter
            );

        // -------------------------
        //  Aiming
        // -------------------------
         isAimingHash =
        Animator.StringToHash(
            isAimingParameter
            );


        /*
         * IMPORTANT:
         *
         * Do NOT set HasRifle here.
         *
         * We allow the Animator Controller to keep its
         * configured startup value.
         *
         * Gameplay changes the value later through:
         *
         * SetHasRifle(true / false)
         */
    }


    // =========================================================
    // LOCOMOTION
    // =========================================================

    public void UpdateLocomotion(
        Vector2 moveInput,
        bool sprintInput
    )
    {
        if (animator == null)
            return;


        // -----------------------------------------------------
        // MOVEMENT INPUT
        // -----------------------------------------------------

        bool isMoving =
            moveInput.sqrMagnitude > 0.01f;


        Vector2 clampedInput =
            Vector2.ClampMagnitude(
                moveInput,
                1f
            );


        // -----------------------------------------------------
        // DETERMINE SPRINT
        // -----------------------------------------------------

        bool effectiveSprint =
            sprintInput &&
            isMoving;


        /*
         * Rifle locomotion rule:
         *
         * Shift + W
         *      ↓
         * Rifle / Gun Run
         *
         * Shift + A
         * Shift + D
         * Shift + S
         *
         *      ↓
         *
         * Stay in rifle walking / strafing animations.
         */
        if (
            HasRifle &&
            rifleSprintForwardOnly &&
            effectiveSprint
        )
        {
            bool movingForwardEnough =
                clampedInput.y >=
                rifleSprintForwardThreshold;


            bool movingMostlyStraight =
                Mathf.Abs(clampedInput.x) <=
                rifleSprintSideThreshold;


            effectiveSprint =
                movingForwardEnough &&
                movingMostlyStraight;
        }


        // -----------------------------------------------------
        // LOCOMOTION SPEED VALUE
        // -----------------------------------------------------

        float locomotionValue =
            idleSpeedValue;


        if (isMoving)
        {
            locomotionValue =
                effectiveSprint
                    ? sprintSpeedValue
                    : walkSpeedValue;
        }


        // -----------------------------------------------------
        // TARGET BLEND TREE VALUES
        // -----------------------------------------------------

        float targetMoveX =
            clampedInput.x *
            locomotionValue;


        float targetMoveZ =
            clampedInput.y *
            locomotionValue;


        float targetSpeed =
            locomotionValue;


        if (!isMoving)
        {
            targetMoveX = 0f;

            targetMoveZ = 0f;

            targetSpeed =
                idleSpeedValue;
        }


        // -----------------------------------------------------
        // ANIMATION SMOOTHING
        // -----------------------------------------------------

        float t =
            1f -
            Mathf.Exp(
                -dampSpeed *
                Time.deltaTime
            );


        currentMoveX =
            Mathf.Lerp(
                currentMoveX,
                targetMoveX,
                t
            );


        currentMoveZ =
            Mathf.Lerp(
                currentMoveZ,
                targetMoveZ,
                t
            );


        currentSpeed =
            Mathf.Lerp(
                currentSpeed,
                targetSpeed,
                t
            );


        // -----------------------------------------------------
        // SEND VALUES TO ANIMATOR
        // -----------------------------------------------------

        animator.SetFloat(
            moveXHash,
            currentMoveX
        );


        animator.SetFloat(
            moveZHash,
            currentMoveZ
        );


        animator.SetFloat(
            speedHash,
            currentSpeed
        );


        animator.SetBool(
            isMovingHash,
            isMoving
        );


        animator.SetBool(
            isSprintingHash,
            effectiveSprint
        );
    }


    // =========================================================
    // RIFLE
    // =========================================================

    public void SetHasRifle(
        bool value
    )
    {
        if (animator == null)
            return;


        animator.SetBool(
            hasRifleHash,
            value
        );


        if (debugRifleState)
        {
            Debug.Log(
                $"HasRifle changed to: {value}",
                this
            );
        }
    }


    public bool IsAiming
    {
        get
        {
            if (animator == null)
                return false;

            return animator.GetBool(
                isAimingHash
            );
        }
    }



    // =========================================================
    // IsAiming
    // =========================================================

    public void SetAiming(bool value)
    {
        if (animator == null)
            return;

        animator.SetBool(
            isAimingHash,
            value
        );
    }


    // =========================================================
    // JUMP
    // =========================================================

    public void SetJumpTrigger()
    {
        if (animator == null)
            return;


        animator.ResetTrigger(
            diveRollHash
        );


        animator.SetTrigger(
            jumpHash
        );
    }


    public void SetGrounded(
        bool isGrounded
    )
    {
        if (animator == null)
            return;


        animator.SetBool(
            isGroundedHash,
            isGrounded
        );
    }


    // =========================================================
    // DIVE ROLL
    // =========================================================

    public void SetDiveRollTrigger()
    {
        if (animator == null)
            return;


        animator.ResetTrigger(
            jumpHash
        );


        animator.SetTrigger(
            diveRollHash
        );
    }


    public void SetDiveRolling(
        bool isDiveRolling
    )
    {
        if (animator == null)
            return;


        animator.SetBool(
            isDiveRollingHash,
            isDiveRolling
        );
    }


    // =========================================================
    // RESET LOCOMOTION
    // =========================================================

    public void ResetLocomotion()
    {
        if (animator == null)
            return;


        currentMoveX =
            0f;

        currentMoveZ =
            0f;

        currentSpeed =
            idleSpeedValue;


        animator.SetFloat(
            moveXHash,
            0f
        );


        animator.SetFloat(
            moveZHash,
            0f
        );


        animator.SetFloat(
            speedHash,
            idleSpeedValue
        );


        animator.SetBool(
            isMovingHash,
            false
        );


        animator.SetBool(
            isSprintingHash,
            false
        );
    }
}

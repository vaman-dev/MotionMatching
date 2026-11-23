using UnityEngine;

public class PlayerDiveRollState : IPlayerState
{
    private readonly PlayerController player;

    private float stateTimer;

    public PlayerDiveRollState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        stateTimer = 0f;

        // Ignore the first horizontal root-motion frame
        // to prevent unwanted movement spikes
        // when transitioning into the roll.
        player.IgnoreHorizontalRootMotionForFrames(1);


        // =====================================================
        // DETERMINE ROLL DIRECTION
        // =====================================================

        Vector2 moveInput =
            player.InputReader != null
                ? player.InputReader.Move
                : Vector2.zero;

        Vector3 rollDirection =
            player.GetCameraRelativeMoveDirection(
                moveInput
            );

        // If the player is not providing movement input,
        // roll in the direction the character is already facing.
        if (rollDirection.sqrMagnitude < 0.01f)
        {
            rollDirection =
                player.transform.forward;
        }

        player.RotateTowardsDirection(
            rollDirection
        );


        // =====================================================
        // ANIMATION
        // =====================================================

        if (player.PlayerAnimator != null)
        {
            player.PlayerAnimator.SetDiveRollTrigger();

            player.PlayerAnimator.SetDiveRolling(true);
        }


        // =====================================================
        // START ROLL
        // =====================================================

        player.StartDiveRoll();
    }


    public void Tick()
    {
        stateTimer += Time.deltaTime;

        Vector2 moveInput =
            player.InputReader.Move;

        bool isMoving =
            moveInput.sqrMagnitude > 0.01f;

        bool isSprinting =
            player.InputReader.SprintHeld &&
            isMoving;


        // =====================================================
        // UPDATE LOCOMOTION PARAMETERS
        // =====================================================

        if (player.PlayerAnimator != null)
        {
            player.PlayerAnimator.UpdateLocomotion(
                moveInput,
                isSprinting
            );
        }


        // =====================================================
        // FINISH DIVE ROLL
        // =====================================================

        if (
            stateTimer >= player.MinDiveRollStateTime &&
            player.IsGrounded
        )
        {
            player.ChangeState(
                PlayerStateId.Locomotion
            );
        }
    }


    public void Exit()
    {
        if (player.PlayerAnimator != null)
        {
            player.PlayerAnimator.SetDiveRolling(false);
        }
    }
}
using UnityEngine;

public class PlayerJumpState : IPlayerState
{
    private readonly PlayerController player;

    private float stateTimer;

    public PlayerJumpState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        stateTimer = 0f;

        if (player.PlayerAnimator != null)
        {
            player.PlayerAnimator.SetJumpTrigger();
            player.PlayerAnimator.SetGrounded(false);
        }

        player.StartJump();
    }

    public void Tick()
    {
        stateTimer += Time.deltaTime;

        Vector2 moveInput = player.InputReader.Move;

        bool isMoving = moveInput.sqrMagnitude > 0.01f;

        bool isSprinting =
            player.InputReader.SprintHeld &&
            isMoving;


        // =====================================================
        // DIVE ROLL CHECK
        // =====================================================

        if (
            player.InputReader.DiveRollPressedThisFrame &&
            player.CanStartDiveRoll()
        )
        {
            player.ChangeState(PlayerStateId.DiveRoll);
            return;
        }


        // =====================================================
        // JUMP MOVEMENT ROTATION
        // =====================================================

        if (isMoving)
        {
            Vector3 moveDirection =
                player.GetCameraRelativeMoveDirection(
                    moveInput
                );

            player.RotateTowardsDirection(
                moveDirection
            );
        }


        // =====================================================
        // UPDATE ANIMATOR
        // =====================================================

        if (player.PlayerAnimator != null)
        {
            player.PlayerAnimator.UpdateLocomotion(
                moveInput,
                isSprinting
            );
        }


        // =====================================================
        // RETURN TO LOCOMOTION
        // =====================================================

        if (
            stateTimer >= player.MinJumpStateTime &&
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
    }
}
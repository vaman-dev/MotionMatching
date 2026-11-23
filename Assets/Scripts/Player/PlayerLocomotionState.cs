using UnityEngine;

public class PlayerLocomotionState : IPlayerState
{
    private readonly PlayerController player;

    public PlayerLocomotionState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
    }

    public void Tick()
    {
        Vector2 moveInput =
            player.InputReader.Move;

        bool isMoving =
            moveInput.sqrMagnitude > 0.01f;

        bool isSprinting =
            player.InputReader.SprintHeld &&
            isMoving;


        // ---------------------------------------------
        // DIVE ROLL
        // ---------------------------------------------

        if (
            player.InputReader.DiveRollPressedThisFrame &&
            player.CanStartDiveRoll()
        )
        {
            player.ChangeState(
                PlayerStateId.DiveRoll
            );

            return;
        }


        // ---------------------------------------------
        // JUMP
        // ---------------------------------------------

        if (
            player.InputReader.JumpPressedThisFrame &&
            player.CanJump()
        )
        {
            player.ChangeState(
                PlayerStateId.Jump
            );

            return;
        }


        // ---------------------------------------------
        // ROTATION
        // ---------------------------------------------

        if (isMoving)
        {
            player.RotateTowardsCameraForward();
        }


        // ---------------------------------------------
        // ANIMATION
        // ---------------------------------------------

        if (player.PlayerAnimator != null)
        {
            player.PlayerAnimator.UpdateLocomotion(
                moveInput,
                isSprinting
            );
        }
    }


    public void Exit()
    {
    }
}
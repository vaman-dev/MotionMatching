using UnityEngine;

public class PlayerLocomotionState : IPlayerState
{
    private readonly PlayerController player;


    public PlayerLocomotionState(
        PlayerController player
    )
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
            moveInput.sqrMagnitude >
            0.01f;


        bool isSprinting =
            player.InputReader.SprintHeld &&
            isMoving;


        bool isAiming =
            player.IsAiming;


        // =====================================================
        // DIVE ROLL
        // =====================================================

        if (
            player.InputReader
                .DiveRollPressedThisFrame &&
            player.CanStartDiveRoll()
        )
        {
            player.ChangeState(
                PlayerStateId.DiveRoll
            );

            return;
        }


        // =====================================================
        // JUMP
        // =====================================================

        if (
            player.InputReader
                .JumpPressedThisFrame &&
            player.CanJump()
        )
        {
            player.ChangeState(
                PlayerStateId.Jump
            );

            return;
        }


        // =====================================================
        // ROTATION
        // =====================================================

        if (isAiming)
        {
            /*
             * TPS AIM MODE
             *
             * Character always faces the
             * camera's horizontal direction.
             *
             * A / D therefore become strafe.
             * S becomes backwards movement.
             */
            player.RotateTowardsCameraForward();
        }

        else if (isMoving)
        {
            /*
             * FREE LOCOMOTION
             *
             * Character turns toward the
             * requested movement direction.
             */

            Vector3 moveDirection =
                player.GetCameraRelativeMoveDirection(
                    moveInput
                );


            player.RotateTowardsDirection(
                moveDirection
            );
        }


        // =====================================================
        // ANIMATION
        // =====================================================

        if (player.PlayerAnimator != null)
        {
            player.PlayerAnimator.SetAiming(
                isAiming
            );


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
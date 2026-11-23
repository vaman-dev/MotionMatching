using UnityEngine;

public class FootstepResetStateBehaviour : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        FootstepAnimationEventReceiver receiver = animator.GetComponent<FootstepAnimationEventReceiver>();

        if (receiver != null)
            receiver.ResetFootstepGate();
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        FootstepAnimationEventReceiver receiver = animator.GetComponent<FootstepAnimationEventReceiver>();

        if (receiver != null)
            receiver.ResetFootstepGate();
    }
}
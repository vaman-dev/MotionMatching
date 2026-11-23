using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MotionMatching.Runtime
{
    /// <summary>Binds the retained free camera to the same Look action owned by the player input reader.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class FreeCameraLookInput : MonoBehaviour
    {
        [SerializeField] private CinemachineInputAxisController axisController;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string lookActionPath = "Player/Look";
        private InputActionReference _lookReference;

        private void Awake()
        {
            if (axisController == null || inputActions == null)
            {
                Debug.LogError("Free camera requires an axis controller and the player's input action asset.", this);
                enabled = false;
                return;
            }

            InputAction action = inputActions.FindAction(lookActionPath, false);
            if (action == null)
            {
                Debug.LogError("Free camera Look action was not found: " + lookActionPath, this);
                enabled = false;
                return;
            }

            _lookReference = InputActionReference.Create(action);
            axisController.AutoEnableInputs = false;
            foreach (var controller in axisController.Controllers)
            {
                if (controller.Name != "Look Orbit X" && controller.Name != "Look Orbit Y") continue;
                controller.Input.InputAction = _lookReference;
                controller.Input.CancelDeltaTime = true;
            }
        }

        private void OnDestroy()
        {
            if (_lookReference == null) return;
            if (axisController != null)
                foreach (var controller in axisController.Controllers)
                    if (controller.Input.InputAction == _lookReference)
                        controller.Input.InputAction = null;
            Destroy(_lookReference);
        }
    }
}

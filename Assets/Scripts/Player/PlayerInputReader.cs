using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class PlayerInputReader : MonoBehaviour
{
    // =========================================================
    // INPUT ACTION REFERENCES
    // =========================================================

    [Header("Input Action References")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;

    [Tooltip("Fallback source for Look when the InputActionReference is not assigned.")]
    [SerializeField] private InputActionAsset inputActionsAsset;

    [SerializeField] private string lookActionPath = "Player/Look";

    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference diveRollAction;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference rightMouseAction;


    // =========================================================
    // MOVEMENT / LOOK
    // =========================================================

    public Vector2 Move { get; private set; }

    public Vector2 Look { get; private set; }

    public InputAction LookAction => GetLookAction();

    public bool HasMoveInput =>
        Move.sqrMagnitude > 0.01f;


    // =========================================================
    // SPRINT
    // =========================================================

    public bool SprintHeld { get; private set; }
    public bool SprintPressedThisFrame { get; private set; }
    public bool SprintReleasedThisFrame { get; private set; }


    // =========================================================
    // JUMP
    // =========================================================

    public bool JumpHeld { get; private set; }
    public bool JumpPressedThisFrame { get; private set; }
    public bool JumpReleasedThisFrame { get; private set; }


    // =========================================================
    // DIVE ROLL
    // =========================================================

    public bool DiveRollHeld { get; private set; }
    public bool DiveRollPressedThisFrame { get; private set; }
    public bool DiveRollReleasedThisFrame { get; private set; }


    // =========================================================
    // INTERACTION
    // =========================================================

    public bool InteractHeld { get; private set; }
    public bool InteractPressedThisFrame { get; private set; }
    public bool InteractReleasedThisFrame { get; private set; }


    // =========================================================
    // AIM
    // =========================================================

    public bool RightMouseHeld { get; private set; }

    public bool RightMousePressedThisFrame
    {
        get;
        private set;
    }

    public bool RightMouseReleasedThisFrame
    {
        get;
        private set;
    }


    // =========================================================
    // ENABLE
    // =========================================================

    private void OnEnable()
    {
        EnableAction(moveAction);
        EnableLookAction();

        EnableAction(sprintAction);
        EnableAction(jumpAction);
        EnableAction(diveRollAction);
        EnableAction(interactAction);
        EnableAction(rightMouseAction);
    }


    private void OnDisable()
    {
        DisableAction(moveAction);
        DisableLookAction();

        DisableAction(sprintAction);
        DisableAction(jumpAction);
        DisableAction(diveRollAction);
        DisableAction(interactAction);
        DisableAction(rightMouseAction);
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        // Movement
        Move =
            ReadVector2(moveAction);

        // Mouse / controller look
        Look = ReadLook();


        // Sprint
        SprintHeld =
            ReadButtonHeld(sprintAction);

        SprintPressedThisFrame =
            ReadButtonPressedThisFrame(sprintAction);

        SprintReleasedThisFrame =
            ReadButtonReleasedThisFrame(sprintAction);


        // Jump
        JumpHeld =
            ReadButtonHeld(jumpAction);

        JumpPressedThisFrame =
            ReadButtonPressedThisFrame(jumpAction);

        JumpReleasedThisFrame =
            ReadButtonReleasedThisFrame(jumpAction);


        // Dive roll
        DiveRollHeld =
            ReadButtonHeld(diveRollAction);

        DiveRollPressedThisFrame =
            ReadButtonPressedThisFrame(diveRollAction);

        DiveRollReleasedThisFrame =
            ReadButtonReleasedThisFrame(diveRollAction);


        // Interaction
        InteractHeld =
            ReadButtonHeld(interactAction);

        InteractPressedThisFrame =
            ReadButtonPressedThisFrame(interactAction);

        InteractReleasedThisFrame =
            ReadButtonReleasedThisFrame(interactAction);


        // Aim
        RightMouseHeld =
            ReadButtonHeld(rightMouseAction);

        RightMousePressedThisFrame =
            ReadButtonPressedThisFrame(
                rightMouseAction
            );

        RightMouseReleasedThisFrame =
            ReadButtonReleasedThisFrame(
                rightMouseAction
            );
    }


    // =========================================================
    // VECTOR INPUT
    // =========================================================

    private Vector2 ReadVector2(
        InputActionReference actionReference
    )
    {
        if (
            actionReference == null ||
            actionReference.action == null
        )
        {
            return Vector2.zero;
        }


        Vector2 value =
            actionReference.action
                .ReadValue<Vector2>();


        // Don't normalize Look input.
        if (
            actionReference == moveAction &&
            value.sqrMagnitude > 1f
        )
        {
            value.Normalize();
        }


        return value;
    }


    private Vector2 ReadLook()
    {
        InputAction action = GetLookAction();

        return action == null
            ? Vector2.zero
            : action.ReadValue<Vector2>();
    }


    private InputAction GetLookAction()
    {
        if (lookAction != null && lookAction.action != null)
        {
            return lookAction.action;
        }

        return inputActionsAsset == null
            ? null
            : inputActionsAsset.FindAction(lookActionPath, false);
    }


    private void EnableLookAction()
    {
        InputAction action = GetLookAction();

        if (action != null)
        {
            action.Enable();
        }
    }


    private void DisableLookAction()
    {
        InputAction action = GetLookAction();

        if (action != null)
        {
            action.Disable();
        }
    }


    // =========================================================
    // BUTTON INPUT
    // =========================================================

    private bool ReadButtonHeld(
        InputActionReference actionReference
    )
    {
        if (
            actionReference == null ||
            actionReference.action == null
        )
        {
            return false;
        }

        return actionReference.action.IsPressed();
    }


    private bool ReadButtonPressedThisFrame(
        InputActionReference actionReference
    )
    {
        if (
            actionReference == null ||
            actionReference.action == null
        )
        {
            return false;
        }

        return actionReference.action
            .WasPressedThisFrame();
    }


    private bool ReadButtonReleasedThisFrame(
        InputActionReference actionReference
    )
    {
        if (
            actionReference == null ||
            actionReference.action == null
        )
        {
            return false;
        }

        return actionReference.action
            .WasReleasedThisFrame();
    }


    // =========================================================
    // ACTION ENABLE / DISABLE
    // =========================================================

    private void EnableAction(
        InputActionReference actionReference
    )
    {
        if (
            actionReference != null &&
            actionReference.action != null
        )
        {
            actionReference.action.Enable();
        }
    }


    private void DisableAction(
        InputActionReference actionReference
    )
    {
        if (
            actionReference != null &&
            actionReference.action != null
        )
        {
            actionReference.action.Disable();
        }
    }
}

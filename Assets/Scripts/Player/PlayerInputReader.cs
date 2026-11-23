using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class PlayerInputReader : MonoBehaviour
{
    [Header("Input Action References")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference diveRollAction;
    [SerializeField] private InputActionReference InteractAction;
    [SerializeField] private InputActionReference rightMouseAction;

    public Vector2 Move { get; private set; }

    public bool SprintHeld { get; private set; }
    public bool SprintPressedThisFrame { get; private set; }
    public bool SprintReleasedThisFrame { get; private set; }

    public bool JumpHeld { get; private set; }
    public bool JumpPressedThisFrame { get; private set; }
    public bool JumpReleasedThisFrame { get; private set; }

    public bool DiveRollHeld { get; private set; }
    public bool DiveRollPressedThisFrame { get; private set; }
    public bool DiveRollReleasedThisFrame { get; private set; }

    public bool InteractHeld { get; private set; }
    public bool InteractPressedThisFrame { get; private set; }
    public bool InteractReleasedThisFrame  { get; private set; }

    public bool RightMouseHeld { get; private set; }
    public bool RightMousePressedThisFrame { get; private set; }
    public bool RightMouseReleasedThisFrame { get; private set; }

    public bool HasMoveInput => Move.sqrMagnitude > 0.01f;

    private void OnEnable()
    {
        EnableAction(moveAction);
        EnableAction(sprintAction);
        EnableAction(jumpAction);
        EnableAction(diveRollAction);
        EnableAction(InteractAction);
        EnableAction(rightMouseAction);
    }

    private void OnDisable()
    {
        DisableAction(moveAction);
        DisableAction(sprintAction);
        DisableAction(jumpAction);
        DisableAction(diveRollAction);
        DisableAction(InteractAction);
        DisableAction(rightMouseAction);
    }

    private void Update()
    {
        Move = ReadMoveInput();

        SprintHeld = ReadButtonHeld(sprintAction);
        SprintPressedThisFrame = ReadButtonPressedThisFrame(sprintAction);
        SprintReleasedThisFrame = ReadButtonReleasedThisFrame(sprintAction);

        JumpHeld = ReadButtonHeld(jumpAction);
        JumpPressedThisFrame = ReadButtonPressedThisFrame(jumpAction);
        JumpReleasedThisFrame = ReadButtonReleasedThisFrame(jumpAction);

        DiveRollHeld = ReadButtonHeld(diveRollAction);
        DiveRollPressedThisFrame = ReadButtonPressedThisFrame(diveRollAction);
        DiveRollReleasedThisFrame = ReadButtonReleasedThisFrame(diveRollAction);

        InteractHeld = ReadButtonHeld(InteractAction);
        InteractPressedThisFrame = ReadButtonPressedThisFrame(InteractAction);
        InteractReleasedThisFrame = ReadButtonReleasedThisFrame(InteractAction);

        if (InteractPressedThisFrame)
        {
            Debug.Log("INTERACT INPUT PRESSED");
        }

        RightMouseHeld = ReadButtonHeld(rightMouseAction);
        RightMousePressedThisFrame = ReadButtonPressedThisFrame(rightMouseAction);
        RightMouseReleasedThisFrame = ReadButtonReleasedThisFrame(rightMouseAction);
    }

    private Vector2 ReadMoveInput()
    {
        if (moveAction == null || moveAction.action == null)
            return Vector2.zero;

        Vector2 value = moveAction.action.ReadValue<Vector2>();

        if (value.sqrMagnitude > 1f)
            value.Normalize();

        return value;
    }

    private bool ReadButtonHeld(InputActionReference actionReference)
    {
        if (actionReference == null || actionReference.action == null)
            return false;

        return actionReference.action.IsPressed();
    }

    private bool ReadButtonPressedThisFrame(InputActionReference actionReference)
    {
        if (actionReference == null || actionReference.action == null)
            return false;

        return actionReference.action.WasPressedThisFrame();
    }

    private bool ReadButtonReleasedThisFrame(InputActionReference actionReference)
    {
        if (actionReference == null || actionReference.action == null)
            return false;

        return actionReference.action.WasReleasedThisFrame();
    }

    private void EnableAction(InputActionReference actionReference)
    {
        if (actionReference != null && actionReference.action != null)
            actionReference.action.Enable();
    }

    private void DisableAction(InputActionReference actionReference)
    {
        if (actionReference != null && actionReference.action != null)
            actionReference.action.Disable();
    }
}
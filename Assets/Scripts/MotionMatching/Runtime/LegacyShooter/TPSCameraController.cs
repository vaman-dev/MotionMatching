using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-50)]
public class TPSCameraController : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("Player")]
    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private PlayerInputReader inputReader;


    [Header("Camera References")]
    [SerializeField]
    private Camera unityCamera;

    [SerializeField]
    private CinemachineCamera freeCamera;

    [SerializeField]
    private CinemachineInputAxisController freeCameraInputController;

    [SerializeField]
    private CinemachineOrbitalFollow freeCameraOrbitalFollow;

    [SerializeField]
    private CinemachineCamera aimCamera;

    [SerializeField]
    private Transform aimCameraTarget;


    // =========================================================
    // CAMERA PRIORITIES
    // =========================================================

    [Header("Camera Priorities")]
    [SerializeField]
    private int freeCameraPriority = 10;

    [SerializeField]
    private int aimCameraPriority = 20;

    //Free Camera vertical orbit//

    [Header("Free Camera Vertical Mapping")]
    [SerializeField]
    private float freeVerticalAxisMin = 15f;

    [SerializeField]
    private float freeVerticalAxisMax = 45f;

    [SerializeField]
    private float aimPitchForFreeMin = 35f;

    [SerializeField]
    private float aimPitchForFreeMax = -25f;


    // =========================================================
    // AIM ROTATION
    // =========================================================

    [Header("Aim Rotation")]
    [SerializeField]
    private float mouseSensitivity = 0.08f;

    [SerializeField]
    private float minimumPitch = -35f;

    [SerializeField]
    private float maximumPitch = 70f;

    [SerializeField]
    private LayerMask aimSyncLayers = ~0;

    [SerializeField]
    private float aimSyncDistance = 200f;


    // =========================================================
    // INTERNAL STATE
    // =========================================================

    private float yaw;
    private float pitch;

    private bool previousAimState;


    // =========================================================
    // PUBLIC ACCESS
    // =========================================================

    public bool IsAiming =>
        playerController != null &&
        playerController.IsAiming;


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        if (playerController == null)
        {
            playerController =
                GetComponent<PlayerController>();
        }

        if (inputReader == null)
        {
            inputReader =
                GetComponent<PlayerInputReader>();
        }

        if (
            unityCamera == null &&
            Camera.main != null
        )
        {
            unityCamera =
                Camera.main;
        }

        if (
            freeCameraInputController == null &&
            freeCamera != null
        )
        {
            freeCameraInputController =
                freeCamera.GetComponent<CinemachineInputAxisController>();
        }

        if (
            freeCameraOrbitalFollow == null &&
            freeCamera != null
        )
        {
            freeCameraOrbitalFollow =
                freeCamera.GetComponent<CinemachineOrbitalFollow>();
        }

        ConfigureFreeCameraLookInput();
    }


    private void Start()
    {
        // Start the aim target facing exactly where
        // the currently rendered camera is looking.
        SyncAimTargetWithCamera();

        previousAimState = false;

        UpdateCameraPriority(false);

        UpdateFreeCameraInputState(false);
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        bool isAiming =
            IsAiming;


        // =========================================================
        // AIM STATE CHANGED
        // =========================================================

        if (isAiming != previousAimState)
        {
            if (isAiming)
            {
                // -------------------------------------------------
                // FREE -> AIM
                // -------------------------------------------------
                // Sync the AimCameraTarget BEFORE the aim camera
                // becomes live.
                SyncAimTargetWithCamera();
            }
            else
            {
                // -------------------------------------------------
                // AIM -> FREE
                // -------------------------------------------------
                // Sync the free Orbital Follow axes BEFORE the
                // free camera becomes dominant.
                SyncFreeCameraOrbit();
            }


            UpdateCameraPriority(
                isAiming
            );


            previousAimState =
                isAiming;
        }


        // =========================================================
        // INPUT OWNERSHIP
        // =========================================================

        UpdateFreeCameraInputState(
            isAiming
        );


        // =========================================================
        // AIM ROTATION
        // =========================================================

        if (isAiming)
        {
            UpdateAimRotation();
        }

        // IMPORTANT:
        //
        // DO NOT continuously call SyncAimTargetWithCamera()
        // while not aiming.
        //
        // During a Cinemachine blend, unityCamera is the blended
        // result. Moving AimCameraTarget from that blended result
        // modifies one of the blend endpoints and creates a
        // feedback loop / visible drift.
    }

    // =========================================================
    // AIM CAMERA ROTATION
    // =========================================================

    private void UpdateAimRotation()
    {
        if (
            inputReader == null ||
            aimCameraTarget == null
        )
        {
            return;
        }

        Vector2 lookInput =
            inputReader.Look;


        yaw +=
            lookInput.x *
            mouseSensitivity;


        pitch -=
            lookInput.y *
            mouseSensitivity;


        pitch =
            Mathf.Clamp(
                pitch,
                minimumPitch,
                maximumPitch
            );


        aimCameraTarget.rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f
            );
    }


    // =========================================================
    // FREE -> AIM SYNCHRONIZATION
    // =========================================================

    private void SyncAimTargetWithCamera()
    {
        if (
            unityCamera == null ||
            aimCameraTarget == null
        )
        {
            return;
        }


        Vector3 viewPoint =
            GetCameraViewCenterPoint();


        Vector3 direction =
            viewPoint -
            aimCameraTarget.position;


        if (
            direction.sqrMagnitude <
            0.0001f
        )
        {
            return;
        }


        Quaternion rotation =
            Quaternion.LookRotation(
                direction,
                Vector3.up
            );


        Vector3 euler =
            rotation.eulerAngles;


        yaw =
            NormalizeAngle(
                euler.y
            );


        pitch =
            NormalizeAngle(
                euler.x
            );


        pitch =
            Mathf.Clamp(
                pitch,
                minimumPitch,
                maximumPitch
            );


        aimCameraTarget.rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f
            );
    }


    // =========================================================
    // AIM -> FREE SYNCHRONIZATION
    // =========================================================

    private void SyncFreeCameraOrbit()
    {
        if (
            freeCameraOrbitalFollow == null ||
            unityCamera == null
        )
        {
            return;
        }

        // =====================================================
        // HORIZONTAL / YAW
        // =====================================================

        Vector3 cameraForward =
            unityCamera.transform.forward;

        Vector3 flatForward =
            new Vector3(
                cameraForward.x,
                0f,
                cameraForward.z
            );

        if (flatForward.sqrMagnitude > 0.0001f)
        {
            flatForward.Normalize();

            float cameraYaw =
                Mathf.Atan2(
                    flatForward.x,
                    flatForward.z
                ) * Mathf.Rad2Deg;

            freeCameraOrbitalFollow.HorizontalAxis.Value =
                cameraYaw;
        }


        // =====================================================
        // VERTICAL / PITCH
        // =====================================================

        float cameraPitch =
            Mathf.Asin(
                Mathf.Clamp(
                    cameraForward.normalized.y,
                    -1f,
                    1f
                )
            ) * Mathf.Rad2Deg;

        float t =
            Mathf.InverseLerp(
                aimPitchForFreeMin,
                aimPitchForFreeMax,
                cameraPitch
            );

        float verticalValue =
            Mathf.Lerp(
                freeVerticalAxisMin,
                freeVerticalAxisMax,
                t
            );

        freeCameraOrbitalFollow.VerticalAxis.Value =
            verticalValue;
    }

    // =========================================================
    // CAMERA VIEW CENTRE
    // =========================================================

    private Vector3 GetCameraViewCenterPoint()
    {
        Ray ray =
            unityCamera.ViewportPointToRay(
                new Vector3(
                    0.5f,
                    0.5f,
                    0f
                )
            );


        if (
            Physics.Raycast(
                ray,
                out RaycastHit hit,
                aimSyncDistance,
                aimSyncLayers,
                QueryTriggerInteraction.Ignore
            )
        )
        {
            return hit.point;
        }


        return
            ray.origin +
            ray.direction *
            aimSyncDistance;
    }


    // =========================================================
    // CAMERA PRIORITY
    // =========================================================

    private void UpdateCameraPriority(
        bool aiming
    )
    {
        if (freeCamera != null)
        {
            freeCamera.Priority =
                freeCameraPriority;
        }


        if (aimCamera != null)
        {
            aimCamera.Priority =
                aiming
                    ? aimCameraPriority
                    : 0;
        }
    }


    // =========================================================
    // FREE CAMERA INPUT
    // =========================================================

    private void UpdateFreeCameraInputState(
        bool isAiming
    )
    {
        if (freeCameraInputController == null)
        {
            return;
        }


        // Free camera uses Cinemachine Input Axis Controller
        // only while normal/free-looking.
        //
        // During ADS the TPSCameraController itself consumes
        // PlayerInputReader.Look and rotates AimCameraTarget.
        freeCameraInputController.enabled =
            !isAiming;
    }


    // =========================================================
    // INPUT SYSTEM -> CINEMACHINE
    // =========================================================

    private void ConfigureFreeCameraLookInput()
    {
        if (
            freeCameraInputController == null ||
            inputReader == null ||
            inputReader.LookAction == null
        )
        {
            return;
        }


        // PlayerInputReader already owns the Player/Look
        // InputAction.  Give Cinemachine a reference to the
        // SAME action instead of maintaining another action.
        InputActionReference lookActionReference =
            InputActionReference.Create(
                inputReader.LookAction
            );


        // PlayerInputReader manages action enabling/disabling.
        freeCameraInputController.AutoEnableInputs =
            false;


        foreach (
            var controller
            in freeCameraInputController.Controllers
        )
        {
            if (
                controller.Name != "Look Orbit X" &&
                controller.Name != "Look Orbit Y"
            )
            {
                continue;
            }


            controller.Input.InputAction =
                lookActionReference;


            // Mouse delta is already frame delta-based.
            controller.Input.CancelDeltaTime =
                true;
        }
    }


    // =========================================================
    // ANGLE UTILITY
    // =========================================================

    private float NormalizeAngle(
        float angle
    )
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }
}
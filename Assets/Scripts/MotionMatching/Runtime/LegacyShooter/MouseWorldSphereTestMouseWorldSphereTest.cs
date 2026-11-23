using UnityEngine;
using UnityEngine.InputSystem;

public class MouseWorldSphereTest : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform sphere;


    // =========================================================
    // SETTINGS
    // =========================================================

    [Header("Settings")]

    [Tooltip("Fixed world depth from the camera.")]
    [SerializeField] private float fixedDepth = 10f;

    [Tooltip("Optional smoothing for sphere movement.")]
    [SerializeField] private float followSharpness = 20f;


    // =========================================================
    // PUBLIC ACCESS
    // =========================================================

    public Vector3 AimPoint { get; private set; }

    public bool HasAimPoint { get; private set; }


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }


        // Prevent the sphere itself from affecting
        // any other raycasts in the project.
        if (sphere != null)
        {
            int ignoreRaycastLayer =
                LayerMask.NameToLayer(
                    "Ignore Raycast"
                );

            if (ignoreRaycastLayer >= 0)
            {
                sphere.gameObject.layer =
                    ignoreRaycastLayer;
            }
        }
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (
            targetCamera == null ||
            sphere == null
        )
        {
            HasAimPoint = false;
            return;
        }


        if (Mouse.current == null)
        {
            HasAimPoint = false;
            return;
        }


        // -----------------------------------------------------
        // READ MOUSE POSITION
        // -----------------------------------------------------

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();


        // -----------------------------------------------------
        // SCREEN POSITION
        //
        // X = mouse horizontal position
        // Y = mouse vertical position
        // Z = fixed distance from camera
        // -----------------------------------------------------

        Vector3 screenPosition =
            new Vector3(
                mousePosition.x,
                mousePosition.y,
                fixedDepth
            );


        // -----------------------------------------------------
        // SCREEN -> WORLD
        // -----------------------------------------------------

        AimPoint =
            targetCamera.ScreenToWorldPoint(
                screenPosition
            );


        HasAimPoint = true;


        // -----------------------------------------------------
        // SMOOTH FOLLOW
        // -----------------------------------------------------

        float t =
            1f -
            Mathf.Exp(
                -followSharpness *
                Time.deltaTime
            );


        sphere.position =
            Vector3.Lerp(
                sphere.position,
                AimPoint,
                t
            );
    }
}
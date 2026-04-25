using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

public class ItemSpawn : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject itemToSpawn;
    public float spawnDistance = 1.5f;
    public float translateSpeed = 1.0f;

    [Header("Controller")]
    public XRNode controllerNode = XRNode.RightHand;

    [Header("Rotation Settings")]
    public bool invertYaw = true;
    public bool invertPitch = true;
    public bool invertRoll = false;

    private static int activePreviews = 0;
    public static bool IsPreviewActive => activePreviews > 0;

    private GameObject currentPreview;
    private bool isPreviewMode = false;
    private Transform cameraTransform;
    private Quaternion lastControllerRotation;

    private Vector3 forwardDir;   // horizontal camera forward at spawn
    private Vector3 rightDir;     // horizontal camera right at spawn
    private Vector3 upDir;        // world up (Vector3.up)

    private InputDevice device;
    private bool wasJoystickPressed = false;

    void Start()
    {
        cameraTransform = Camera.main?.transform;
        if (cameraTransform == null)
            Debug.LogError("ItemSpawn: No main camera found!");
        upDir = Vector3.up;
    }

    void Update()
    {
        device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (!device.isValid) return;

        device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool joystickPressed);
        if (joystickPressed && !wasJoystickPressed)
        {
            if (SelectionByRay.CurrentSelected == null)
            {
                if (!isPreviewMode)
                    SpawnPreview();
                else
                    FinaliseItem();
            }
        }
        wasJoystickPressed = joystickPressed;

        if (isPreviewMode && currentPreview != null)
        {
            device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 thumbstick);
            device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripPressed);
            device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed);

            if (!triggerPressed)
            {
                Vector3 move = Vector3.zero;
                float step = translateSpeed * Time.deltaTime;

                if (!gripPressed)
                {
                    move.y = thumbstick.y * step;
                    move += rightDir * (thumbstick.x * step);
                }
                else
                {
                    move += forwardDir * (thumbstick.y * step);
                }
                currentPreview.transform.position += move;
            }
            else
            {
                // ----- ROTATION (camera‑relative axes: rightDir, upDir, forwardDir) -----
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion currentRot);
                Quaternion delta = currentRot * Quaternion.Inverse(lastControllerRotation);
                Vector3 deltaEuler = delta.eulerAngles;

                float yaw = deltaEuler.y;
                if (yaw > 180f) yaw -= 360f;
                if (invertYaw) yaw = -yaw;

                float pitch = deltaEuler.x;
                if (pitch > 180f) pitch -= 360f;
                if (invertPitch) pitch = -pitch;

                float roll = deltaEuler.z;
                if (roll > 180f) roll -= 360f;
                if (invertRoll) roll = -roll;

                // Apply rotations around captured camera‑relative axes
                currentPreview.transform.Rotate(rightDir, pitch, Space.World);
                currentPreview.transform.Rotate(upDir, yaw, Space.World);
                currentPreview.transform.Rotate(forwardDir, roll, Space.World);

                lastControllerRotation = currentRot;
            }
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out lastControllerRotation);
        }
    }

    void SpawnPreview()
    {
        if (itemToSpawn == null)
        {
            Debug.LogWarning($"{gameObject.name}: No prefab assigned!");
            return;
        }

        Vector3 spawnPos = cameraTransform.position + cameraTransform.forward * spawnDistance;
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, spawnDistance))
            spawnPos = hit.point + hit.normal * 0.1f;

        Vector3 camForward = cameraTransform.forward;
        forwardDir = Vector3.ProjectOnPlane(camForward, Vector3.up).normalized;
        if (forwardDir == Vector3.zero) forwardDir = Vector3.forward;

        Quaternion spawnRotation = Quaternion.LookRotation(forwardDir, Vector3.up);
        currentPreview = Instantiate(itemToSpawn, spawnPos, spawnRotation);

        currentPreview.layer = LayerMask.NameToLayer("Ignore Raycast");

        Rigidbody rb = currentPreview.GetComponent<Rigidbody>();
        if (rb == null) rb = currentPreview.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = currentPreview.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null) grab.enabled = false;

        Vector3 camRight = cameraTransform.right;
        rightDir = Vector3.ProjectOnPlane(camRight, Vector3.up).normalized;
        if (rightDir == Vector3.zero) rightDir = Vector3.right;

        device.TryGetFeatureValue(CommonUsages.deviceRotation, out lastControllerRotation);

        isPreviewMode = true;
        activePreviews++;
        DisablePlayerMovement(true);
    }

    void FinaliseItem()
    {
        if (currentPreview == null) return;

        Rigidbody rb = currentPreview.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = currentPreview.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab == null) grab = currentPreview.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        grab.enabled = true;

        int selectableLayer = LayerMask.NameToLayer("Selectable");
        if (selectableLayer == -1) selectableLayer = 0;
        currentPreview.layer = selectableLayer;

        isPreviewMode = false;
        activePreviews--;
        currentPreview = null;
        DisablePlayerMovement(false);
    }

    void DisablePlayerMovement(bool disable)
    {
        XROrigin xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin == null) return;

        CharacterController cc = xrOrigin.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = !disable;

        LocomotionSystem ls = xrOrigin.GetComponentInChildren<LocomotionSystem>();
        if (ls != null) ls.enabled = !disable;

        var providers = xrOrigin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>();
        foreach (var provider in providers)
            provider.enabled = !disable;
    }
}
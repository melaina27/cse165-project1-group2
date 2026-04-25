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

    // Static counter to track how many previews are active (both hands)
    private static int activePreviews = 0;
    public static bool IsPreviewActive => activePreviews > 0;

    // Private state
    private GameObject currentPreview;
    private bool isPreviewMode = false;
    private Transform cameraTransform;
    private Quaternion lastControllerRotation;

    private Vector3 forwardDir;
    private Vector3 rightDir;

    private InputDevice device;
    private bool wasJoystickPressed = false;

    void Start()
    {
        cameraTransform = Camera.main?.transform;
        if (cameraTransform == null)
            Debug.LogError("AdvancedItemPlacer: No main camera found!");
    }

    void Update()
    {
        device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (!device.isValid) return;

        device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool joystickPressed);
        if (joystickPressed && !wasJoystickPressed)
        {
            // NEW: Do not allow spawn or finalise if an object is currently selected
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
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion currentRot);
                Quaternion delta = currentRot * Quaternion.Inverse(lastControllerRotation);
                Vector3 deltaEuler = delta.eulerAngles;

                float yawDelta = deltaEuler.y;
                if (yawDelta > 180f) yawDelta -= 360f;
                if (invertYaw) yawDelta = -yawDelta;
                currentPreview.transform.Rotate(Vector3.up, yawDelta, Space.World);

                float pitchDelta = deltaEuler.x;
                if (pitchDelta > 180f) pitchDelta -= 360f;
                if (invertPitch) pitchDelta = -pitchDelta;
                currentPreview.transform.Rotate(Vector3.right, pitchDelta, Space.World);

                float rollDelta = deltaEuler.z;
                if (rollDelta > 180f) rollDelta -= 360f;
                if (invertRoll) rollDelta = -rollDelta;
                currentPreview.transform.Rotate(Vector3.forward, rollDelta, Space.World);

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

        // Set layer to Selectable so it can be selected later
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
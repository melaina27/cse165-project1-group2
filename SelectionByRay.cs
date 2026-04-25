using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

public class SelectionByRay : MonoBehaviour
{
    [Header("Ray Settings")]
    public float rayLength = 10f;
    public LayerMask selectableLayers;      // Objects that can be selected (turned red)
    public Color hoverColor = Color.blue;
    public Color selectedColor = Color.red;
    public Color defaultColor = Color.white;
    public Color teleportRayColor = Color.green;

    [Header("Teleport")]
    public float teleportOffset = 0.1f;

    [Header("Controller")]
    public XRNode controllerNode = XRNode.RightHand;

    public static GameObject CurrentSelected { get; private set; }

    private LineRenderer lineRenderer;
    private InputDevice device;
    private GameObject currentHoverObject;
    private MaterialPropertyBlock propertyBlock;
    private bool wasTriggerPressed = false;
    private bool wasGripPressed = false;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.01f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.white;
        lineRenderer.endColor = Color.white;
        lineRenderer.enabled = false;

        propertyBlock = new MaterialPropertyBlock();

        if (selectableLayers == 0)
        {
            int selLayer = LayerMask.NameToLayer("Selectable");
            if (selLayer != -1)
                selectableLayers = 1 << selLayer;
            else
                Debug.LogError("SelectionByRay: 'Selectable' layer not found.");
        }
    }

    void Update()
    {
        if (ItemSpawn.IsPreviewActive)
        {
            lineRenderer.enabled = false;
            ClearHover();
            return;
        }

        device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (!device.isValid) return;

        device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed);
        device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripPressed);

        Vector3 rayOrigin = transform.position;
        Vector3 rayDirection = transform.forward;

        // Single raycast that hits everything (excluding "Ignore Raycast")
        int layerMask = ~LayerMask.GetMask("Ignore Raycast");
        RaycastHit hit;
        bool hitSomething = Physics.Raycast(rayOrigin, rayDirection, out hit, rayLength, layerMask);
        Vector3 hitPoint = hitSomething ? hit.point : rayOrigin + rayDirection * rayLength;
        GameObject hitObject = hitSomething ? hit.collider.gameObject : null;

        // Determine if the hit object is selectable (belongs to selectableLayers)
        bool isSelectable = hitSomething && ((1 << hit.collider.gameObject.layer) & selectableLayers) != 0;
        bool isTeleport = hitSomething && !isSelectable;

        bool buttonHeld = triggerPressed || gripPressed;

        // Ray visual – green when teleport is possible
        if (buttonHeld)
        {
            lineRenderer.enabled = true;
            lineRenderer.startColor = isSelectable ? Color.white : teleportRayColor;
            lineRenderer.endColor = isSelectable ? Color.white : teleportRayColor;
            lineRenderer.SetPosition(0, rayOrigin);
            lineRenderer.SetPosition(1, hitPoint);
        }
        else
        {
            lineRenderer.enabled = false;
        }

        // --- Selection / Teleport on button release ---
        bool triggerJustReleased = wasTriggerPressed && !triggerPressed;
        bool gripJustReleased = wasGripPressed && !gripPressed;
        if (triggerJustReleased || gripJustReleased)
        {
            if (isSelectable && hitObject != null)
            {
                SelectObject(hitObject);
            }
            else if (isTeleport)
            {
                TeleportTo(hit.point);
            }
        }

        // --- Hover (blue) only over selectable objects (and not selected) ---
        if (buttonHeld && isSelectable && hitObject != null && hitObject != CurrentSelected)
        {
            if (currentHoverObject != hitObject)
            {
                ClearHover();
                currentHoverObject = hitObject;
                ApplyColor(currentHoverObject, hoverColor);
            }
        }
        else
        {
            ClearHover();
        }

        // Deselection on button press while selected
        if (CurrentSelected != null && (triggerPressed && !wasTriggerPressed || gripPressed && !wasGripPressed))
        {
            DeselectCurrent();
        }

        wasTriggerPressed = triggerPressed;
        wasGripPressed = gripPressed;
    }

    void ApplyColor(GameObject obj, Color color)
    {
        if (obj == null) return;
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", color);
            rend.SetPropertyBlock(propertyBlock);
        }
    }

    void ClearHover()
    {
        if (currentHoverObject != null && currentHoverObject != CurrentSelected)
        {
            Color restoreColor = (currentHoverObject == CurrentSelected) ? selectedColor : defaultColor;
            ApplyColor(currentHoverObject, restoreColor);
            currentHoverObject = null;
        }
        else if (currentHoverObject != null && currentHoverObject == CurrentSelected)
        {
            currentHoverObject = null;
        }
    }

    void SelectObject(GameObject obj)
    {
        if (CurrentSelected == obj) return;

        if (CurrentSelected != null)
            DeselectCurrent();

        // Make kinematic for manipulation
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        else
        {
            rb = obj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        CurrentSelected = obj;
        ApplyColor(CurrentSelected, selectedColor);
        if (currentHoverObject == obj)
            currentHoverObject = null;
        Debug.Log($"Selected: {obj.name}");
    }

    void DeselectCurrent()
    {
        if (CurrentSelected == null) return;
        Rigidbody rb = CurrentSelected.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
        ApplyColor(CurrentSelected, defaultColor);
        CurrentSelected = null;
        Debug.Log("Deselected");
    }

    void TeleportTo(Vector3 hitPoint)
    {
        XROrigin origin = FindObjectOfType<XROrigin>();
        if (origin == null)
        {
            Debug.LogError("No XROrigin found!");
            return;
        }

        // Get camera's local position relative to the XR Origin
        Vector3 cameraLocalPos = origin.Camera.transform.localPosition;
        float eyeHeight = cameraLocalPos.y;  // typically around 1.6

        // Desired camera position: above the hit point by eye height
        Vector3 desiredCameraPos = hitPoint + Vector3.up * eyeHeight;

        // Calculate new XR Origin position so that the camera ends up at desiredCameraPos
        Vector3 newOriginPos = desiredCameraPos - cameraLocalPos;

        origin.transform.position = newOriginPos;

        Debug.Log($"Teleported to {hitPoint}, new origin {newOriginPos}, camera now at {origin.Camera.transform.position}");
    }
}
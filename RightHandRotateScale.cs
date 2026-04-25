using UnityEngine;
using UnityEngine.XR;

public class RightHandRotateScale : MonoBehaviour
{
    [Header("Rotation Settings (Trigger)")]
    public bool invertYaw = true;
    public bool invertPitch = true;
    public bool invertRoll = false;
    public float rotateSpeed = 1.0f;

    [Header("Scale Settings (Grip + Vertical Move)")]
    public float scaleSpeed = 0.5f;          // Sensitivity (1 unit up = 50% scale)
    public float minScale = 0.1f;
    public float maxScale = 5f;

    private InputDevice device;
    private GameObject target;
    private Quaternion lastControllerRotation;
    private Vector3 lastControllerPosition;
    private bool wasTriggerPressed = false;
    private bool wasGripPressed = false;
    private bool scalingActive = false;

    void Start()
    {
        device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }

    void Update()
    {
        device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!device.isValid) return;

        target = SelectionByRay.CurrentSelected;
        if (target == null) return;

        device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed);
        device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripPressed);

        // ----- ROTATION (trigger held, grip not held) -----
        if (triggerPressed && !gripPressed)
        {
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion currentRot);
            if (wasTriggerPressed)
            {
                Quaternion delta = currentRot * Quaternion.Inverse(lastControllerRotation);
                Vector3 deltaEuler = delta.eulerAngles;

                float yaw = deltaEuler.y;
                if (yaw > 180f) yaw -= 360f;
                if (invertYaw) yaw = -yaw;
                target.transform.Rotate(Vector3.up, yaw * rotateSpeed, Space.World);

                float pitch = deltaEuler.x;
                if (pitch > 180f) pitch -= 360f;
                if (invertPitch) pitch = -pitch;
                target.transform.Rotate(Vector3.right, pitch * rotateSpeed, Space.World);

                float roll = deltaEuler.z;
                if (roll > 180f) roll -= 360f;
                if (invertRoll) roll = -roll;
                target.transform.Rotate(Vector3.forward, roll * rotateSpeed, Space.World);
            }
            lastControllerRotation = currentRot;
        }
        else
        {
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out lastControllerRotation);
        }

        // ----- SCALE (grip held, trigger not held) -----
        if (gripPressed && !triggerPressed)
        {
            if (!wasGripPressed)
            {
                // First frame: remember starting position
                scalingActive = true;
                lastControllerPosition = transform.position;
            }
            else if (scalingActive)
            {
                // Move controller up/down changes scale
                float deltaY = transform.position.y - lastControllerPosition.y;
                if (Mathf.Abs(deltaY) > 0.001f)
                {
                    float scaleFactor = 1f + deltaY * scaleSpeed;
                    Vector3 newScale = target.transform.localScale * scaleFactor;
                    newScale = Vector3.Max(newScale, Vector3.one * minScale);
                    newScale = Vector3.Min(newScale, Vector3.one * maxScale);
                    target.transform.localScale = newScale;
                    // Update last position for continuous scaling
                    lastControllerPosition = transform.position;
                }
            }
        }
        else
        {
            scalingActive = false;
        }

        wasTriggerPressed = triggerPressed;
        wasGripPressed = gripPressed;
    }
}
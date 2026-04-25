using UnityEngine;
using UnityEngine.XR;

public class SmoothOrientation : MonoBehaviour
{
    [Header("Yaw (Left/Right)")]
    public float turnSpeed = 90f;
    public float smoothTime = 0.1f;

    private InputDevice rightHandDevice;
    private float targetYaw;
    private float yawVelocity;

    void Start()
    {
        rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        targetYaw = transform.eulerAngles.y;
    }

    void Update()
    {
        if (!rightHandDevice.isValid)
            rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (rightHandDevice.isValid && rightHandDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 thumbstick))
        {
            float inputX = thumbstick.x;
            if (Mathf.Abs(inputX) > 0.1f)
                targetYaw += inputX * turnSpeed * Time.deltaTime;
        }

        float newYaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref yawVelocity, smoothTime);
        transform.rotation = Quaternion.Euler(0, newYaw, 0);
    }
}
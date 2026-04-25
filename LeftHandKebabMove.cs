using UnityEngine;
using UnityEngine.XR;

public class LeftHandKebabMove : MonoBehaviour
{
    [Header("Kebab Move Settings")]
    public float followDistance = 1.5f;
    public float moveSmoothness = 15f;

    private InputDevice device;
    private GameObject target;

    void Start()
    {
        device = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
    }

    void Update()
    {
        device = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!device.isValid) return;

        target = SelectionByRay.CurrentSelected;
        if (target == null) return;

        if (ItemSpawn.IsPreviewActive) return;

        Vector3 targetPos = transform.position + transform.forward * followDistance;
        // No jitter because the object's Rigidbody is kinematic while selected
        target.transform.position = Vector3.Lerp(target.transform.position, targetPos, moveSmoothness * Time.deltaTime);
    }
}
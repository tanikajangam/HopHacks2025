using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class VRInput : MonoBehaviour
{
    private InputDevice leftController;
    private InputDevice rightController;

    public const float ZOOM_AMT = 0.02f;
    public const float ZOOM_MAX = 5f;

    public int sphereStop = 1;
    public float zoomLevel = 0;
    public BrainTransformations transformations;

    void Start()
    {
        Debug.Log("starting vr input!! ");
        sphereStop = 1;
        InitializeControllers();
    }

    void InitializeControllers()
    {
        List<InputDevice> devices = new List<InputDevice>();

        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, devices);
        if (devices.Count > 0)
            leftController = devices[0];

        devices.Clear();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
        if (devices.Count > 0)
            rightController = devices[0];
    }

    void Update()
    {
        // if controllers get lost, reinitialize
        if (!leftController.isValid || !rightController.isValid)
            InitializeControllers();

        // --- LEFT CONTROLLER BUTTONS ---
        CheckControllerInputs(leftController, "left");

        // --- RIGHT CONTROLLER BUTTONS ---
        CheckControllerInputs(rightController, "right");
    }

    void CheckControllerInputs(InputDevice controller, string hand)
    {
        if (!controller.isValid) return;

        // primary button (A on right, X on left)
        if (controller.TryGetFeatureValue(CommonUsages.primaryButton, out bool primary) && primary)
        {
            Debug.Log($"{hand} Primary pressed");

            sphereStop = 0;
        }

        // secondary button (B on right, Y on left)
        if (controller.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondary) && secondary)
            Debug.Log($"{hand} Secondary pressed");

        // grip button, lower one
        if (controller.TryGetFeatureValue(CommonUsages.gripButton, out bool gripButton) && gripButton)
        {
            Debug.Log($"{hand} Grip button pressed");
            if (hand.Equals("left"))
            {
                sphereStop = 0;
            }
            else
            {
                sphereStop = 1;
            }
        }

        // trigger button, higher one
        if (controller.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerButton) && triggerButton)
        {
            Debug.Log($"{hand} Trigger button pressed");
            if (hand.Equals("left"))
            {
                sphereStop = 0;
                if (zoomLevel > 0)
                {
                    zoomLevel -= ZOOM_AMT;
                }

                transformations.ZoomOut();

            }
            else
            {
                sphereStop = 1;
                if (zoomLevel < ZOOM_MAX)
                {
                    zoomLevel += ZOOM_AMT;
                }
                transformations.ZoomIn();
            }

        }

        // stick press (click)
        if (controller.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool stickClick) && stickClick)
            Debug.Log($"{hand} Stick pressed");

        // stick touch
        if (controller.TryGetFeatureValue(CommonUsages.primary2DAxisTouch, out bool stickTouch) && stickTouch)
            Debug.Log($"{hand} Stick touched");

        // trigger analog value (0.0 to 1.0)
        if (controller.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue) && triggerValue > 0.05f)
            Debug.Log($"{hand} Trigger value: {triggerValue}");

        // grip analog value (0.0 to 1.0)
        if (controller.TryGetFeatureValue(CommonUsages.grip, out float gripValue) && gripValue > 0.05f)
            Debug.Log($"{hand} Grip value: {gripValue}");

        // thumbstick axis
        if (controller.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick))
        {
            
            // RIGHT STICK CONTROLS ROTATION
            if (hand.Equals("right"))
            {
                if (stick.magnitude > 0.1f) // Deadzone
                {
                    // Horizontal stick (x) rotates around Y axis
                    float yRotation = stick.x;

                    // Vertical stick (y) rotates around X axis
                    float xRotation = stick.y;

                    //transform.Rotate(xRotation, yRotation, 0f, Space.Self);
                    transformations.UpdateRotation(xRotation, yRotation);
                }
            }
        }
    }

    public int GetSphereStop()
    {
        return sphereStop;
    }

    public float GetZoomLevel()
    {
        return zoomLevel;
    }
}

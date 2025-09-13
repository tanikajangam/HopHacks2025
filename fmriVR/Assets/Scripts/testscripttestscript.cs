using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;




public class testscripttestscript : MonoBehaviour
{
    public Vector3 pointA = new Vector3(5, 2, 2); // Start point
    public Vector3 pointB = new Vector3(0, 2, 2); // End point
    public float speed = 2f; // Movement speed
    //public float toChange = 5f;
    public Slider slider; 

    private Vector3 target;
    public VRInput input;

    void Start()
    {
        Debug.Log("TESTING TESTING THIS SHOULD WORK !!!!");
        // Start moving toward pointB
        target = pointB;

        if (slider != null)
        {
            // Subscribe to slider change events
            slider.onValueChanged.AddListener(OnSliderChanged);
        }
    }

    void Update()
    {
        // Move toward the target point
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime * input.GetSphereStop());

        // If we've reached the target, switch to the other point
        if (Vector3.Distance(transform.position, target) < 0.01f)
        {
            target = (target == pointA) ? pointB : pointA;
        }

    }

    private void OnSliderChanged(float value)
    {
        Debug.Log("slider: " + value);

        if (speed != null)
            speed = value;
    }
}

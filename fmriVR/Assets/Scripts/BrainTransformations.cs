using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrainTransformations : MonoBehaviour
{
    // Start is called before the first frame update


    /*
        IMPORTANT THINGS THIS NEEDS TO DO: 

        rotate itself when theres a swipe in vr
        have a scale feature (zoom in zoom out?)
        
        double slider thing? 
        slice selector?

    */
    public Vector3 defaultRotationSpeed;

    [Range(0.1f, 1f)]
    public float scaleFactor;

    void Start()
    {
        //defaultRotationSpeed = new Vector3(0f, 2f, 0f);
        defaultRotationSpeed = new Vector3(0f, 0f, 0f);
        //scaleFactor = .2f;
        scaleFactor = 1f;
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(defaultRotationSpeed * Time.deltaTime);
        transform.localScale = Vector3.one * scaleFactor;
    }

    
}

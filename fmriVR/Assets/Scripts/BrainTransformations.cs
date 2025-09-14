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
    public const float ROT_AMT = 10f;

    
    public const float SCALE_MIN = 0.1f;
    public const float SCALE_MAX = 3f;
    public const float SCALE_AMT = 0.05f;

    [Range(SCALE_MIN, SCALE_MAX)]
    public float scaleFactor = 0.4f;


    void Start()
    {
        //defaultRotationSpeed = new Vector3(0f, 2f, 0f);
        defaultRotationSpeed = new Vector3(0f, 0f, 0f);
        //scaleFactor = .2f;
        //scaleFactor = .4f;
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(defaultRotationSpeed * Time.deltaTime);
        transform.localScale = Vector3.one * scaleFactor;
    }

    public void UpdateRotation(float xRotationMag, float yRotationMag)
    {
        defaultRotationSpeed = new Vector3(-xRotationMag * ROT_AMT, -yRotationMag * ROT_AMT, 0);
    }

    public void ZoomIn()
    {
        if (scaleFactor < SCALE_MAX)
        {
            scaleFactor += SCALE_AMT;
        }
        
    }

    public void ZoomOut()
    {
        if (scaleFactor > SCALE_MIN)
        {
            scaleFactor -= SCALE_AMT;
        }
    }
    
}

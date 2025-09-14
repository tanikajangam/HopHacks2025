using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZoomRepresentation : MonoBehaviour
{

    //public float zoomValue = 0;
    public VRInput input;
    // Start is called before the first frame update
    void Start()
    {
        //zoomValue = 0;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 newPos = transform.position;
        newPos.x = input.GetZoomLevel(); // only change X
        transform.position = newPos;
    }
}

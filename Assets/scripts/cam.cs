using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class cam : MonoBehaviour
{
    WebCamTexture webcam;
    public RawImage img;
    // Start is called before the first frame update
    void Start()
    {
        WebCamTexture webcamTexture = new WebCamTexture();
        img.texture = webcamTexture;
        webcamTexture.Play();
    }
}

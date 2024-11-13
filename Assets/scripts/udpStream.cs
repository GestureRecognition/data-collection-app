using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine.UI;

public class udpStream : MonoBehaviour
{
    public int port = 9001; // Set to match Python's UDP video port
    public RawImage display; // Optional: Assign a UI RawImage for displaying frames
    public Renderer renderer; // Optional: Assign if displaying on a GameObject (e.g., a quad)

    private UdpClient udpClient;
    private IPEndPoint endPoint;
    private Texture2D texture;

    private void Start()
    {
        // Initialize UDP client
        udpClient = new UdpClient(port);
        endPoint = new IPEndPoint(IPAddress.Any, port);

        // Initialize texture for received frames (modify size if needed)
        texture = new Texture2D(320, 240, TextureFormat.RGB24, false);

        // Start listening for video frames in a new thread
        Thread receiveThread = new Thread(new ThreadStart(ReceiveFrames));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    private void ReceiveFrames()
    {
        while (true)
        {
            try
            {
                byte[] data = udpClient.Receive(ref endPoint);

                // Read frame size (first 4 bytes as an int)
                int frameSize = BitConverter.ToInt32(data, 0);
                byte[] frameData = new byte[frameSize];
                Array.Copy(data, 4, frameData, 0, frameSize);

                // Update texture on main thread
                UpdateTexture(frameData);
            }
            catch (Exception ex)
            {
                Debug.Log("Error receiving frame: " + ex.Message);
            }
        }
    }

    private void UpdateTexture(byte[] frameData)
    {
        // Load image data into texture
        texture.LoadImage(frameData);

        // Update the UI or GameObject with the new texture
        if (display != null)
        {
            display.texture = texture;
        }

        if (renderer != null)
        {
            renderer.material.mainTexture = texture;
        }
    }

    private void OnApplicationQuit()
    {
        // Close UDP client on application exit
        udpClient.Close();
    }

}

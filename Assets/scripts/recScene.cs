using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Debug = UnityEngine.Debug;

public class recScene : MonoBehaviour
{
    private bool isRecording = false;
    private bool isReceiving = true; // Flag to control thread execution
    public RawImage displayScreen; // for displaying the stream
    private int port = 9001;
    private UdpClient streamClient;
    private IPEndPoint endPoint;
    private Texture2D texture;

    private Transform previewLayout;
    private Image[] sideImages = new Image[5];

    private Image recording_indicator;
    private TextMeshProUGUI countdown;

    private string serverIP = "127.0.0.1";
    private int serverPort = 9000;
    private UdpClient requestClient;
    private Thread receiveThread;
    private ConcurrentQueue<byte[]> frameQueue = new ConcurrentQueue<byte[]>();



    void Start()
    {
        requestClient = new UdpClient();
        requestClient.Connect(serverIP, serverPort);
        streamClient = new UdpClient(port);
        endPoint = new IPEndPoint(IPAddress.Any, port);

        texture = new Texture2D(1000, 680, TextureFormat.RGB24, false);
        displayScreen = GameObject.Find("displayScreen").GetComponent<RawImage>();

        isReceiving = true;
        receiveThread = new Thread(ReceiveFrames)
        {
            IsBackground = true
        };
        receiveThread.Start();


        setupRecording();
        setupSideImages();
    }

    private void ReceiveFrames()
    {
        while (isReceiving)
        {
            try
            {
                byte[] data = streamClient.Receive(ref endPoint);

                // Read frame size (first 4 bytes as an int)
                int frameSize = BitConverter.ToInt32(data, 0);
                byte[] frameData = new byte[frameSize];
                Array.Copy(data, 4, frameData, 0, frameSize);

                frameQueue.Enqueue(frameData);
            }
            catch (SocketException ex) when (isReceiving == false)
            {
                // Expected exception when closing the socket, break the loop
                Debug.Log("StreamClient closed.");
                break;
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

        if (displayScreen != null)
        {
            displayScreen.texture = texture;
        }
    }

    void setupRecording()
    {
        recording_indicator = GameObject.Find("recording_indicator").GetComponent<Image>();
        recording_indicator.gameObject.SetActive(false);
        countdown = GameObject.Find("countdown").GetComponent<TextMeshProUGUI>();
        countdown.text = "3";
        Debug.Log("done setting up recording");
        StartCoroutine(StartCountdown());
    }

    IEnumerator StartCountdown()
    {
        for (int i = 3; i > 0; i--)
        {
            countdown.text = i.ToString();
            yield return new WaitForSeconds(1);
        }
        countdown.gameObject.SetActive(false);
        StartRecording();
    }

    async void StartRecording()
    {
        isRecording = true;
        recording_indicator.gameObject.SetActive(true);

        byte[] data = Encoding.UTF8.GetBytes("StartRecording");
        try
        {
            await requestClient.SendAsync(data, data.Length);
            Debug.Log("Recording started.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error while sending or receiving UDP message: " + ex.Message);
        }
    }

    async void StopRecording()
    {
        isRecording = false;
        recording_indicator.gameObject.SetActive(false);

        byte[] data = Encoding.UTF8.GetBytes("StopRecording");
        try
        {
            await requestClient.SendAsync(data, data.Length);
            Debug.Log($"Recording stopped.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error while sending or receiving UDP message: " + ex.Message);
        }
    }

    void setupSideImages()
    {
        previewLayout = GameObject.Find("preview_layout").GetComponent<Transform>();
        if (MainManager.Instance?.randomMp4Files != null)
        {
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                sideImages[i] = previewLayout.GetChild(i).GetComponent<Image>();
                string imagePath = Path.Combine(MainManager.Instance.imageDirectoryPath, MainManager.Instance.randomMp4Files[i] + "_frame_3.png");
                if (File.Exists(imagePath))
                {
                    // Load the image into a Texture2D
                    byte[] imageBytes = File.ReadAllBytes(imagePath);
                    Texture2D texture = new Texture2D(2, 2);
                    texture.LoadImage(imageBytes);
                    // Convert the Texture2D into a Sprite
                    Sprite tempSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    sideImages[i].sprite = tempSprite;
                }
                else
                {
                    Debug.LogWarning("Image not found at: " + imagePath);
                }
            }
        }
        else
        {
            Debug.LogWarning("MainManager is not instantiated");
        }
    }

    // Update is called once per frame
    void Update()
    {
        while (frameQueue.TryDequeue(out byte[] frameData))
        {
            UpdateTexture(frameData);
        }
        if (Input.GetKeyDown(KeyCode.Space) && !isRecording)
        {
            StartRecording();
        }
        else if (Input.GetKeyDown(KeyCode.Space) && isRecording)
        {
            StopRecording();
        }
    }

    async public void SaveNext()
    {
        byte[] data = Encoding.UTF8.GetBytes("StopStreaming");
        try
        {
            await requestClient.SendAsync(data, data.Length);
            Debug.Log($"Streaming stopped.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error while sending or receiving UDP message: " + ex.Message);
        }
        streamClient.Close();
        SceneManager.LoadScene("Main");
    }

    async public void ExitBtn()
    {
        byte[] data = Encoding.UTF8.GetBytes("StopStreaming");
        try
        {
            await requestClient.SendAsync(data, data.Length);
            Debug.Log($"Streaming stopped.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error while sending or receiving UDP message: " + ex.Message);
        }
        //Save Log TODO
        Application.Quit();
        Debug.Log("Application is exiting");
        UnityEditor.EditorApplication.isPlaying = false;
    }

    private void OnApplicationQuit()
    {
        isReceiving = false; // Signal thread to stop
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(); // Wait for the thread to exit
        }
        streamClient.Close(); // Safely close UdpClient
    }
}

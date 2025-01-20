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
using UnityEngine.Video;


using Debug = UnityEngine.Debug;
public enum AppState
{
    Initializing,
    MainMenu,
    Playing,
    Paused,
    GameOver,
    Exiting
}

public class recScene : MonoBehaviour
{
    //Arguments ==================================
    

    //Components =================================
    private Button exitButton; // exit the application, just use the previous function
    private Button recordButton; // send start recording, turn on start recording
    private Button stopButton; // (send stop recording to server) 5 go back to main, 1-4 next gesture
    private Button backButton; // 1 back to main screen without shuffle, 4-5 delete recording and move back one state
    private TextMeshProUGUI currentGestureText;

    private UdpClient requestClient;
    private UdpClient streamClient;
    private IPEndPoint endPoint; //streamClient End Point
    private Thread receiveStreamThread;
    private RawImage cameraDisplayScreen;
    private ConcurrentQueue<byte[]> frameQueue = new ConcurrentQueue<byte[]>();
    private Texture2D cameraStreamFrame;
    private VideoPlayer gestureVideoPlayer;
    private Image recording_indicator;

    private Texture2D kinectStreamFrame;
    private RawImage kinectDisplayScreen; //TODO for Kinect

    //Variables ==================================
    private bool isRecording = false;
    private int currentGestureIndex = 0;

    
    void Start()
    {
        // Grab all the buttons
        exitButton = GameObject.Find("exitButton").GetComponent<Button>();
        backButton = GameObject.Find("backButton").GetComponent<Button>();
        recordButton = GameObject.Find("recordButton").GetComponent<Button>();
        stopButton = GameObject.Find("stopButton").GetComponent<Button>();
        currentGestureText = GameObject.Find("currentGestureText").GetComponent<TextMeshProUGUI>();
        recordButton.onClick.AddListener(() => StartRecording());
        stopButton.onClick.AddListener(() => StopRecording());
        backButton.onClick.AddListener(() => BackButton());
        exitButton.onClick.AddListener(() => ExitButton());

        // Initialize the 2DTexture and Displays
        cameraStreamFrame = new Texture2D(750, 400, TextureFormat.RGB24, false);
        kinectStreamFrame = new Texture2D(750, 400, TextureFormat.RGB24, false);
        cameraDisplayScreen = GameObject.Find("cameraDisplayScreen").GetComponent<RawImage>();
        kinectDisplayScreen = GameObject.Find("kinectDisplayScreen").GetComponent<RawImage>();
        gestureVideoPlayer = GameObject.Find("GesturePreviewPlayer").GetComponent<VideoPlayer>();
        recording_indicator = GameObject.Find("recording_indicator").GetComponent<Image>();

        // Connect The UDP Clients
        requestClient = new UdpClient();
        requestClient.Connect(MainManager.Instance.serverIP, MainManager.Instance.serverPort);
        streamClient = new UdpClient(MainManager.Instance.streamingPort);
        endPoint = new IPEndPoint(IPAddress.Any, MainManager.Instance.streamingPort);

        // TODO Start Receiving frames Thread
        receiveStreamThread = new Thread(ReceiveFrames)
        {
            IsBackground = true
        };
        receiveStreamThread.Start();

        // Initialize Components States
        recording_indicator.gameObject.SetActive(false);
        stopButton.interactable = false;
        UpdateCurrentGestureText();
        PlayGestureVideo();
    }

    // Setup Functions ====================================
    // Setup Functions [END]===============================


    // Stream-related Functions ===========================
    private void ReceiveFrames()
    {
        try
        {
            while (true) // Infinite loop to receive frames
            {
                byte[] data = streamClient.Receive(ref endPoint);

                // Read frame size (first 4 bytes as an int)
                int frameSize = BitConverter.ToInt32(data, 0);
                byte[] frameData = new byte[frameSize];
                Array.Copy(data, 4, frameData, 0, frameSize);

                frameQueue.Enqueue(frameData);
            }
        }
        catch (SocketException)
        {
            // Expected exception when streamClient is closed
            Debug.Log("StreamClient closed, stopping ReceiveFrames thread.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error in ReceiveFrames: " + ex.Message);
        }
    }
    // Stream-related Functions [END]======================


    // Utils Functions ====================================
    private void UpdateTexture(byte[] frameData)
    {
        // Load image data into texture
        cameraStreamFrame.LoadImage(frameData);
        if (cameraDisplayScreen != null)
        {
            cameraDisplayScreen.texture = cameraStreamFrame;
        }
    }
    void PlayGestureVideo()
    {
        gestureVideoPlayer.url = Path.Combine(MainManager.Instance.videoDirectoryPath, MainManager.Instance.gestureMiniBatch[currentGestureIndex] + ".mp4");
        gestureVideoPlayer.Play();
    }
    void UpdateCurrentGestureText()
    {
        currentGestureText.text = $"{currentGestureIndex + 1}/5";
    }
    void LoadMain()
    {
        if (receiveStreamThread != null && receiveStreamThread.IsAlive)
        {
            streamClient.Close();
            receiveStreamThread.Join();
        }
        SceneManager.LoadScene("Main");
    }
    string ToNumericName(string gestID)
    {
        string numID;
        if (gestID.StartsWith("EGO")) numID = "0";
        else if (gestID.StartsWith("ETC")) numID = "1";
        else if (gestID.StartsWith("NUM")) numID = "2";
        else numID = "3";
        numID += gestID.Split('_')[1];
        return numID;
    }
    // Utils Functions [END]===============================


    // Button Functions ===================================
    public async void StartRecording()
    {
        isRecording = true;
        recordButton.interactable = false;
        stopButton.interactable = true;
        recording_indicator.gameObject.SetActive(true);

        // StartRecording:<NumericGestureID>
        string startRecordingMessage = "StartRecording:" + ToNumericName(MainManager.Instance.gestureMiniBatch[currentGestureIndex]);
        byte[] data = Encoding.UTF8.GetBytes(startRecordingMessage);
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
    public async void StopRecording()
    {
        isRecording = false;
        recordButton.interactable = true;
        stopButton.interactable = false;
        recording_indicator.gameObject.SetActive(false);

        byte[] data = Encoding.UTF8.GetBytes("StopRecording");
        try
        {
            await requestClient.SendAsync(data, data.Length);
            Debug.Log("Recording stopped.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error while sending or receiving UDP message: " + ex.Message);
        }

        if (currentGestureIndex < 4) 
        {
            currentGestureIndex++;
            UpdateCurrentGestureText();
            PlayGestureVideo();
        }
        else
        {
            data = Encoding.UTF8.GetBytes("StopStreaming");
            try
            {
                await requestClient.SendAsync(data, data.Length);
                Debug.Log("Streaming stopped.");
            }
            catch (Exception ex)
            {
                Debug.LogError("Error while sending or receiving UDP message: " + ex.Message);
            }
            LoadMain();
        }
    }
    public async void BackButton()
    {
        byte[] data = Encoding.UTF8.GetBytes("StopStreaming");
        try
        {
            await requestClient.SendAsync(data, data.Length);
            Debug.Log("Recording stopped.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error while sending or receiving UDP message: " + ex.Message);
        }
        MainManager.Instance.noNeedToShuffle = true;
        LoadMain();
    }
    public async void ExitButton()
    {
        byte[] data = Encoding.UTF8.GetBytes("StopStreaming");
        try
        {
            await requestClient.SendAsync(data, data.Length);
            Debug.Log("Recording stopped.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error while sending or receiving UDP message: " + ex.Message);
        }
        Application.Quit();
        Debug.Log("Application is exiting");
        UnityEditor.EditorApplication.isPlaying = false;
    }
    // Button Functions [END]==============================


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

    private void OnApplicationQuit()
    {
        if (receiveStreamThread != null && receiveStreamThread.IsAlive)
        {
            streamClient.Close(); // Closing the client stops the thread
            receiveStreamThread.Join(); // Wait for the thread to exit
        }
        Debug.Log("Application exiting.");
    }

}

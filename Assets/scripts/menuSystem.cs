using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Reflection;
using UnityEditor.PackageManager;
using UnityEditor.VersionControl;

public class menuSystem : MonoBehaviour
{
    private string directoryPath = ".\\Assets\\trialPreview";
    private string previewType = "0.6x";
    private string videoDirectoryPath;
    private List<string> allMp4Files = new List<string>();
    private string displayedGesture;
    private TextMeshProUGUI descText;
    private int gestureIndex;

    private string GestDescFilename = "GestureDescription.csv";
    private Dictionary<string, string> gestureData = new Dictionary<string, string>();

    private Button[] gestureButtons = new Button[5];
    private Button lastSelectedButton;

    private VideoPlayer videoPlayer;

    private string imagePreviewType = "0.6x_5FRAME";
    private Transform[] layoutPanels = new Transform[5];
    private Image[,] previewImages = new Image[5, 5];

    // UDP server address and port
    private string serverIP = "127.0.0.1";
    private int serverPort = 9000;
    UdpClient requestClient;

    private bool glassesConnected = true;
    private string recordingDevice = "Camera";
    private string saveAt = "Phone";
    private Button recDeviceBtn;
    private Button recSaveAtBtn;
    private Button refreshButton;

    void Start()
    {
        videoDirectoryPath = Path.Combine(directoryPath, previewType);
        MainManager.Instance.imageDirectoryPath = Path.Combine(directoryPath, imagePreviewType);
        videoPlayer = GameObject.Find("GesturePreviewPlayer").GetComponent<VideoPlayer>();

        FetchMp4Files();
        while (MainManager.Instance.shuffledQueue.Count < 5) ShuffleAndFillQueue();
        SelectRandomMp4Files();

        if (MainManager.Instance.randomMp4Files.Count != 0)
        {
            displayedGesture = MainManager.Instance.randomMp4Files[0];
            foreach (string file in MainManager.Instance.randomMp4Files)
            {
                Debug.Log("Selected MP4 file: " + file);
            }
        }

        ReadDescCSV();

        descText = GameObject.Find("Description").GetComponent<TextMeshProUGUI>();
        gestureIndex = 0;
        updateText();

        SetupGestureButtons();
        if (gestureButtons.Length > 0) gestureButtons[0].Select();
        lastSelectedButton = gestureButtons[0];

        requestClient = new UdpClient();
        requestClient.Connect(serverIP, serverPort);

        recDeviceBtn = GameObject.Find("btn_recdev").GetComponent<Button>();
        recSaveAtBtn = GameObject.Find("btn_saveat").GetComponent<Button>();
        refreshButton = GameObject.Find("btn_refresh").GetComponent<Button>();
        GameObject.Find("refresh_text").GetComponent<TextMeshProUGUI>().color = Color.red;
        recSaveAtBtn.gameObject.SetActive(false);
        recDeviceBtn.interactable = false;
        CheckGlassesStatus();

        SetupTransformPanels();
        PlayVideo();
    }

    async Task<bool> StartStreamingRequest()
    {
        if (recordingDevice == "Glasses" && !glassesConnected) return false;
        // TODO Filename
        string statusMessage = "StartStreaming:" + "TestingFilename" + ":" + recordingDevice + ":" + saveAt;
        byte[] data = Encoding.UTF8.GetBytes(statusMessage);
        try
        {
            await requestClient.SendAsync(data, data.Length);
            Task<UdpReceiveResult> receiveTask = requestClient.ReceiveAsync();
            if (await System.Threading.Tasks.Task.WhenAny(receiveTask, System.Threading.Tasks.Task.Delay(5000)) == receiveTask)
            {
                string response = Encoding.UTF8.GetString(receiveTask.Result.Buffer);
                Debug.Log("Response received: " + response);

                if (response == "StartedStreaming") return true;
                else return false;
            }
            else
            {
                Debug.LogError("Server response timed out");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error while sending or receiving UDP message: " + ex.Message);
            return false;
        }
    }

    async Task<bool> SendGlassesStatusRequest()
    {
        byte[] data = Encoding.UTF8.GetBytes("GlassesStatus");
        try
        {
            await requestClient.SendAsync(data, data.Length);

            // Wait for the response with a timeout
            Task<UdpReceiveResult> receiveTask = requestClient.ReceiveAsync();
            if (await System.Threading.Tasks.Task.WhenAny(receiveTask, System.Threading.Tasks.Task.Delay(5000)) == receiveTask)
            {
                string response = Encoding.UTF8.GetString(receiveTask.Result.Buffer);
                Debug.Log("Response received: " + response);

                if (response == "GlassesConnected") return true;
                else return false;
            }
            else
            {
                Debug.LogError("Server response timed out");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error while sending or receiving UDP message: " + ex.Message);
            return false;
        }
    }



    void FetchMp4Files()
    {
        if (Directory.Exists(videoDirectoryPath))
        {
            string[] mp4Files = Directory.GetFiles(videoDirectoryPath, "*.mp4");
            foreach (string filePath in mp4Files)
            {
                allMp4Files.Add(Path.GetFileName(filePath));
            }

            Debug.Log("Found " + allMp4Files.Count + " MP4 files.");
        }
        else
        {
            Debug.LogError("Directory does not exist: " + videoDirectoryPath);
        }
    }

    void ShuffleAndFillQueue()
    {
        List<string> shuffledList = allMp4Files.OrderBy(x => UnityEngine.Random.value).ToList();
        MainManager.Instance.shuffledQueue.Clear();
        foreach (string file in shuffledList)
        {
            MainManager.Instance.shuffledQueue.Enqueue(file);
        }
    }

    void SelectRandomMp4Files()
    {
        while (MainManager.Instance.shuffledQueue.Count < 5)
        {
            ShuffleAndFillQueue();
        }

        int numberOfFilesToSelect = Mathf.Min(5, MainManager.Instance.shuffledQueue.Count);
        MainManager.Instance.randomMp4Files = new List<string>();
        for (int i = 0; i < numberOfFilesToSelect; i++)
        {
            if (MainManager.Instance.shuffledQueue.Count > 0)
            {
                MainManager.Instance.randomMp4Files.Add(Path.GetFileNameWithoutExtension(MainManager.Instance.shuffledQueue.Dequeue()));
            }
        }
    }

    public void ReadDescCSV()
    {
        string filePath = Path.Combine(directoryPath, GestDescFilename);
        if (File.Exists(filePath))
        {
            string[] csvLines = File.ReadAllLines(filePath);

            foreach (string line in csvLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] values = line.Split(';');

                if (values[0] == "ID") continue;

                string id = values[0];
                string description = values[1];

                gestureData.Add(id, description);
            }

            Debug.Log("CSV file read successfully.");
        }
        else
        {
            Debug.LogError("CSV file not found at: " + filePath);
        }
    }

    void SetupGestureButtons()
    {
        for (int i = 0; i < gestureButtons.Length; i++)
        {
            int index = i;
            gestureButtons[index] = GameObject.Find("Gesture_" + index).GetComponent<Button>();
            gestureButtons[index].onClick.AddListener(() => OnGestureButtonClick(index));
        }
    }

    void SetupTransformPanels()
    {
        for (int i = 0; i < layoutPanels.Length; i++)
        {
            int index = i;
            layoutPanels[index] = GameObject.Find("LayoutPanel_" + index).GetComponent<Transform>();
            for (int j = 0; j < layoutPanels[index].childCount; j++)
            {
                previewImages[i, j] = layoutPanels[index].GetChild(j).GetComponent<Image>();

                string imagePath = Path.Combine(MainManager.Instance.imageDirectoryPath, MainManager.Instance.randomMp4Files[i] + "_frame_" + (j + 1) + ".png");
                if (File.Exists(imagePath))
                {
                    byte[] imageBytes = File.ReadAllBytes(imagePath);
                    Texture2D texture = new Texture2D(2, 2);
                    texture.LoadImage(imageBytes);
                    Sprite tempSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    previewImages[i, j].sprite = tempSprite;
                }
                else
                {
                    Debug.LogWarning("Image not found at: " + imagePath);
                }
            }
        }
    }

    void OnGestureButtonClick(int buttonIndex)
    {
        gestureIndex = buttonIndex;
        updateText();
        Debug.Log("Gesture Button " + buttonIndex + " clicked");
        lastSelectedButton = gestureButtons[buttonIndex];
        PlayVideo();
    }

    public void nextGest()
    {
        if (gestureIndex < 4)
        {
            gestureIndex++;
            updateText();
            PlayVideo();
        }
        Debug.Log("Next Pressed");
        gestureButtons[gestureIndex].Select();
        lastSelectedButton = gestureButtons[gestureIndex];
    }

    public void prevGest()
    {
        if (gestureIndex > 0)
        {
            gestureIndex--;
            updateText();
            PlayVideo();
        }
        Debug.Log("Prev Pressed");
        gestureButtons[gestureIndex].Select();
        lastSelectedButton = gestureButtons[gestureIndex];
    }

    void updateText()
    {
        string GestID = MainManager.Instance.randomMp4Files[gestureIndex];
        string numID;
        if (GestID.StartsWith("EGO")) numID = "0";
        else if (GestID.StartsWith("ETC")) numID = "1";
        else if (GestID.StartsWith("NUM")) numID = "2";
        else numID = "3";
        numID += GestID.Split('_')[1];

        if (gestureData.ContainsKey(GestID))
        {
            descText.text = numID + "(" + GestID + ") : " + gestureData[GestID];
        }
        else
        {
            Debug.LogWarning(GestID + " : ID not found in CSV data.");
            descText.text = GestID + " : ID not found in CSV data.";
        }
    }

    void PlayVideo()
    {
        string videoPath = Path.Combine(videoDirectoryPath, MainManager.Instance.randomMp4Files[gestureIndex] + ".mp4");
        videoPlayer.url = videoPath;
        videoPlayer.Play();
        Debug.Log("Playing video: " + videoPath);
    }

    void Update()
    {
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            if (lastSelectedButton != null)
            {
                lastSelectedButton.Select();
            }
        }
    }

    public async void loadRecording()
    {
        bool streamingStart = await StartStreamingRequest();
        if (streamingStart) SceneManager.LoadScene("Recording");
        else Debug.Log("Unexpected Error Failed to loadRecording");
    }

    public async void CheckGlassesStatus()
    {
        glassesConnected = await SendGlassesStatusRequest();
        if (glassesConnected)
        {
            recDeviceBtn.interactable = true;
            GameObject.Find("refresh_text").GetComponent<TextMeshProUGUI>().color = Color.green;
            Debug.Log("Glasses Connected");
        }
        else
        {
            recDeviceBtn.interactable = false;
            recSaveAtBtn.gameObject.SetActive(false);
            recDeviceBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Camera";
            GameObject.Find("refresh_text").GetComponent<TextMeshProUGUI>().color = Color.red;
            Debug.Log("Glasses Not Connected");
        }
        lastSelectedButton = gestureButtons[gestureIndex];
    }


    public void toggleDeviceButton()
    {
        if (recordingDevice == "Camera") {
            recDeviceBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Glasses";
            recordingDevice = "Glasses";
            recSaveAtBtn.gameObject.SetActive(true);
        } else if (recordingDevice == "Glasses") {
            recDeviceBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Camera";
            recordingDevice = "Camera";
            recSaveAtBtn.gameObject.SetActive(false);
        } else {
            recDeviceBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Camera";
            recordingDevice = "Camera";
            recSaveAtBtn.gameObject.SetActive(false);
        }
        lastSelectedButton = gestureButtons[gestureIndex];
    }

    public void toggleSaveAt() 
    {
        if (saveAt == "Phone") {
            recSaveAtBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Local";
            saveAt = "Local";
        } else if (recordingDevice == "Local") {
            recSaveAtBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Phone";
            saveAt = "Phone";
        } else {
            recSaveAtBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Phone";
            saveAt = "Phone";
        }
        lastSelectedButton = gestureButtons[gestureIndex];
    }
}

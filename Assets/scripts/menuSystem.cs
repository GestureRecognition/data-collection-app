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
using TMPro.Examples;
using System.Text.RegularExpressions;

public class menuSystem : MonoBehaviour
{
    //Arguments ==================================
    private int maxFileNameLength = 50;

    //Components =================================
    private TextMeshProUGUI descText;
    private Button[] gestureButtons = new Button[5];
    private Button lastSelectedButton;
    private Button recDeviceBtn;
    private Button recSaveAtBtn;
    private Button refreshButton;
    private VideoPlayer videoPlayer;
    private Transform[] layoutPanels = new Transform[5];
    private Image[,] previewImages = new Image[5, 5];
    private UdpClient requestClient;
    private Toggle dateToggle; 
    private TMP_InputField inputFieldFilename;

    //Variables ==================================
    private string displayedGesture;
    private int selectedGestureIndex = 0;
    private bool glassesConnected = true;
    private string recordingDevice = "Camera";
    private string saveAt = "Phone";
    private string validatedFileName = "";
    private bool isDateOn;

    void Start()
    {
        Debug.Log("Menu System Start Function");  //DEBUG
        // Initialize ServerPort
        requestClient = new UdpClient();
        requestClient.Connect(MainManager.Instance.serverIP, MainManager.Instance.serverPort);

        // Select Random Mini Batch
        if (MainManager.Instance.noNeedToShuffle) {
            MainManager.Instance.noNeedToShuffle = false;
        }
        else {
            MainManager.Instance.SelectRandomMiniBatch();
        }
        displayedGesture = MainManager.Instance.gestureMiniBatch[0];

        // Initializing Components
        videoPlayer = GameObject.Find("GesturePreviewPlayer").GetComponent<VideoPlayer>();
        descText = GameObject.Find("Description").GetComponent<TextMeshProUGUI>();
        dateToggle = GameObject.Find("dateToggle").GetComponent<Toggle>();
        inputFieldFilename = GameObject.Find("inputFieldFilename").GetComponent<TMP_InputField>();

        if (dateToggle != null)
        {
            isDateOn = dateToggle.isOn;
            dateToggle.onValueChanged.AddListener(OnDateToggleValueChanged);
        }
        if (inputFieldFilename != null)
        {
            inputFieldFilename.onValueChanged.AddListener(OnInputFilenameChanged);
        }
        selectedGestureIndex = 0;
        UpdateDescText();
        SetupGestureButtons();
        if (gestureButtons.Length > 0) gestureButtons[0].Select();
        lastSelectedButton = gestureButtons[0];

        recDeviceBtn = GameObject.Find("btn_recdev").GetComponent<Button>();
        recSaveAtBtn = GameObject.Find("btn_saveat").GetComponent<Button>();
        refreshButton = GameObject.Find("btn_refresh").GetComponent<Button>();
        GameObject.Find("refresh_text").GetComponent<TextMeshProUGUI>().color = Color.red;
        recSaveAtBtn.gameObject.SetActive(false);
        recDeviceBtn.interactable = false;

        CheckGlassesStatus();
        SetupTransformPanels();
        PlayGestureVideo();
    }

    // Request Functions ==================================
    async Task<bool> StartStreamingRequest()
    {
        if (recordingDevice == "Glasses" && !glassesConnected) return false;
        // StartStreaming:<FilenameNote>:<isDateOn>:<recordingDevice>:<saveAt>
        string statusMessage = "StartStreaming:" 
            + (validatedFileName == ""? "NoNote":validatedFileName) 
            + ":" + (isDateOn ? "1" : "0") 
            + ":" + recordingDevice 
            + ":" + saveAt;
        Debug.Log(statusMessage); // DEBUG
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
    // Request Functions [END]=============================


    // Setup Functions ====================================
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
            layoutPanels[i] = GameObject.Find($"LayoutPanel_{i}").transform;
            LoadPreviewImages(i);
        }
    }
    void LoadPreviewImages(int panelIndex)
    {
        for (int j = 0; j < layoutPanels[panelIndex].childCount; j++)
        {
            var child = layoutPanels[panelIndex].GetChild(j);
            previewImages[panelIndex, j] = child.GetComponent<Image>();

            string imagePath = Path.Combine(
                MainManager.Instance.imageDirectoryPath,
                $"{MainManager.Instance.gestureMiniBatch[panelIndex]}_frame_{j + 1}.png"
            );

            if (File.Exists(imagePath))
            {
                previewImages[panelIndex, j].sprite = LoadSprite(imagePath);
            }
            else
            {
                Debug.LogWarning($"Image not found: {imagePath}");
            }
        }
    }
    Sprite LoadSprite(string imagePath)
    {
        byte[] imageBytes = File.ReadAllBytes(imagePath);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(imageBytes);
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }
    void OnGestureButtonClick(int buttonIndex)
    {
        selectedGestureIndex = buttonIndex;
        UpdateDescText();
        Debug.Log("Gesture Button " + buttonIndex + " clicked");
        lastSelectedButton = gestureButtons[buttonIndex];
        PlayGestureVideo();
    }
    // Setup Functions [END]===============================


    // Utils Functions ====================================
    void UpdateDescText()
    {
        string gestID = MainManager.Instance.gestureMiniBatch[selectedGestureIndex];
        string numID = ToNumericName(gestID);

        if (MainManager.Instance.gestureDescData.ContainsKey(gestID))
        {
            descText.text = numID + "(" + gestID + ") : " + MainManager.Instance.gestureDescData[gestID];
        }
        else
        {
            Debug.LogWarning(numID + "(" + gestID + " ) : ID not found in CSV data.");
            descText.text = numID + "(" + gestID + " ) : ID not found in CSV data.";
        }
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
    string ToAlphaName(string numID)
    {
        string gestID;
        if (numID.StartsWith("0")) gestID = "EGO";
        else if (numID.StartsWith("1")) gestID = "ETC";
        else if (numID.StartsWith("2")) gestID = "NUM";
        else gestID = "ETC";
        gestID += "_" + numID.Substring(1);
        return gestID;
    }
    void PlayGestureVideo()
    {
        string videoPath = Path.Combine(MainManager.Instance.videoDirectoryPath, MainManager.Instance.gestureMiniBatch[selectedGestureIndex] + ".mp4");
        videoPlayer.url = videoPath;
        videoPlayer.Play();
        Debug.Log("Playing video: " + videoPath);
    }
    // Utils Functions [END]===============================


    // Button Functions ====================================
    public async void LoadRecording()
    {
        bool streamingStart = await StartStreamingRequest();
        if (streamingStart) SceneManager.LoadScene("Record2");
        else Debug.Log("Unexpected Error Failed to loadRecording");
        SceneManager.LoadScene("Record2"); //temp DEBUG because i dont want to activate the python server TODO Remove
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
        lastSelectedButton = gestureButtons[selectedGestureIndex];
    }
    public void ToggleDeviceButton()
    {
        recDeviceBtn.GetComponentInChildren<TextMeshProUGUI>().text = recordingDevice == "Camera" ? "Glasses" : "Camera";
        recordingDevice = recordingDevice == "Camera" ? "Glasses" : "Camera";
        recSaveAtBtn.gameObject.SetActive(recordingDevice == "Glasses");
        lastSelectedButton = gestureButtons[selectedGestureIndex];
    }
    public void ToggleSaveAt() 
    {
        recSaveAtBtn.GetComponentInChildren<TextMeshProUGUI>().text = saveAt == "Phone" ? "Local" : "Phone";
        saveAt = saveAt == "Phone" ? "Local" : "Phone";
        lastSelectedButton = gestureButtons[selectedGestureIndex];
    }
    public void PrevGest()
    {
        if (selectedGestureIndex > 0)
        {
            selectedGestureIndex--;
            UpdateDescText();
            PlayGestureVideo();
        }
        Debug.Log("Prev Pressed");
        gestureButtons[selectedGestureIndex].Select();
        lastSelectedButton = gestureButtons[selectedGestureIndex];
    }
    public void NextGest()
    {
        if (selectedGestureIndex < 4)
        {
            selectedGestureIndex++;
            UpdateDescText();
            PlayGestureVideo();
        }
        Debug.Log("Next Pressed");
        gestureButtons[selectedGestureIndex].Select();
        lastSelectedButton = gestureButtons[selectedGestureIndex];
    }
    public void OnDateToggleValueChanged(bool value)
    {
        isDateOn = value;
    }
    public void OnInputFilenameChanged(string input)
    {
        string sanitizedInput = Regex.Replace(input, @"[^a-zA-Z0-9_\-]", ""); // Remove special characters except '_' and '-'
        if (sanitizedInput.Length > maxFileNameLength)
        {
            sanitizedInput = sanitizedInput.Substring(0, maxFileNameLength);
        }
        validatedFileName = sanitizedInput;
        if (input != sanitizedInput)
        {
            inputFieldFilename.text = sanitizedInput; // Reflect sanitized input
        }
    }
    public void ExitButton()
    {
        Application.Quit();
        Debug.Log("Application is exiting");
        UnityEditor.EditorApplication.isPlaying = false;
    }
    // Button Functions [END]===============================

    void Update()
    {
        if (!inputFieldFilename.isFocused && Input.GetKeyDown(KeyCode.Space))
        {
            LoadRecording();
        }
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            if (lastSelectedButton != null)
            {
                lastSelectedButton.Select();
            }
        }
    }
}

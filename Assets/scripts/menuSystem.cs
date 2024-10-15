using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;  // For accessing file directories
using System.Linq;  // For selecting random elements from a list
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.Video;

public class menuSystem : MonoBehaviour
{
    private string directoryPath = ".\\Assets\\trialPreview";
    private string previewType = "0.6x";
    private string videoDirectoryPath;
    private List<string> allMp4Files = new List<string>();
    //private Queue<string> shuffledQueue = new Queue<string>();
    private List<string> randomMp4Files = new List<string>();
    private string displayedGesture;
    private TextMeshProUGUI descText;
    private int gestureIndex;

    // for reading csv
    private string GestDescFilename = "GestureDescription.csv";
    private Dictionary<string, string> gestureData = new Dictionary<string, string>();

    // Button references
    private Button[] gestureButtons = new Button[5];
    private Button lastSelectedButton;

    // Video Player reference
    private VideoPlayer videoPlayer;

    // Preview Image Layout
    private string imagePreviewType = "0.6x_5FRAME";
    private string imageDirectoryPath;
    private Transform[] layoutPanels = new Transform[5];
    private Image[,] previewImages = new Image[5, 5];

    // Start is called before the first frame update
    void Start()
    {
        videoDirectoryPath = Path.Combine(directoryPath, previewType);
        imageDirectoryPath = Path.Combine(directoryPath, imagePreviewType);
        videoPlayer = GameObject.Find("GesturePreviewPlayer").GetComponent<VideoPlayer>();

        // Fetch MP4 file names from the directory
        FetchMp4Files();

        // Shuffle files and fill the queue
        while (MainManager.Instance.shuffledQueue.Count < 5) ShuffleAndFillQueue();

        // Select 5 random MP4 file names from the queue
        SelectRandomMp4Files();

        // Debug the selected random files
        if (randomMp4Files.Count != 0)
        {
            displayedGesture = randomMp4Files[0];
            foreach (string file in randomMp4Files)
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

        SetupTransformPanels();


        PlayVideo();
    }

    // Fetch all MP4 files in the specified directory and store them in allMp4Files list
    void FetchMp4Files()
    {
        if (Directory.Exists(videoDirectoryPath))
        {
            // Get all MP4 files in the directory
            string[] mp4Files = Directory.GetFiles(videoDirectoryPath, "*.mp4");

            // Add file names (without path) to the allMp4Files list
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

    // Shuffle the allMp4Files and fill the queue
    void ShuffleAndFillQueue()
    {
        // Shuffle the list
        List<string> shuffledList = allMp4Files.OrderBy(x => UnityEngine.Random.value).ToList();

        // Clear existing queue and enqueue all shuffled files into the queue
        MainManager.Instance.shuffledQueue.Clear();
        foreach (string file in shuffledList)
        {
            MainManager.Instance.shuffledQueue.Enqueue(file);
        }
    }

    // Select 5 random MP4 file names from the queue
    void SelectRandomMp4Files()
    {
        // If the queue has fewer than 5 files, reshuffle and refill it
        while (MainManager.Instance.shuffledQueue.Count < 5)
        {
            ShuffleAndFillQueue();
        }

        // Ensure we don't try to select more than what's available
        int numberOfFilesToSelect = Mathf.Min(5, MainManager.Instance.shuffledQueue.Count);

        // Dequeue 5 files from the shuffled queue
        randomMp4Files = new List<string>();
        for (int i = 0; i < numberOfFilesToSelect; i++)
        {
            if (MainManager.Instance.shuffledQueue.Count > 0)
            {
                randomMp4Files.Add(Path.GetFileNameWithoutExtension(MainManager.Instance.shuffledQueue.Dequeue()));
            }
        }
    }

    public void ReadDescCSV()
    {
        string filePath = Path.Combine(directoryPath, GestDescFilename);
        if (File.Exists(filePath))
        {
            string[] csvLines = File.ReadAllLines(filePath); // Read all lines from CSV file

            foreach (string line in csvLines)
            {
                // Skip empty lines or headers if needed
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Split the line into components by semicolon
                string[] values = line.Split(';');

                // Skip the first line
                if (values[0] == "ID") continue;

                // Assuming first value is ID and second is Description
                string id = values[0];  // Convert first column to integer (ID)
                string description = values[1]; // Second column is Description

                // Add the ID and Description to the dictionary
                gestureData.Add(id, description);
            }

            Debug.Log("CSV file read successfully.");
        }
        else
        {
            Debug.LogError("CSV file not found at: " + filePath);
        }
    }

    // Function to set up the onClick events for the buttons
    void SetupGestureButtons()
    {
        for (int i = 0; i < gestureButtons.Length; i++)
        {
            int index = i; // Local copy for the closure issue
            gestureButtons[index] = GameObject.Find("Gesture_" + index).GetComponent<Button>();
            gestureButtons[index].onClick.AddListener(() => OnGestureButtonClick(index));
        }
    }

    // Function to set up the preview display layout panels
    void SetupTransformPanels()
    {
        for (int i = 0; i < layoutPanels.Length; i++)
        {
            int index = i; // Local copy for the closure issue
            layoutPanels[index] = GameObject.Find("LayoutPanel_" + index).GetComponent<Transform>();
            for (int j = 0; j < layoutPanels[index].childCount; j++)
            {
                previewImages[i, j] = layoutPanels[index].GetChild(j).GetComponent<Image>();

                string imagePath = Path.Combine(imageDirectoryPath, randomMp4Files[i] + "_frame_" + (j+1) + ".png");
                if (File.Exists(imagePath))
                {
                    // Load the image into a Texture2D
                    byte[] imageBytes = File.ReadAllBytes(imagePath);
                    Texture2D texture = new Texture2D(2, 2);
                    texture.LoadImage(imageBytes);
                    // Convert the Texture2D into a Sprite
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

    // This function is called when a button is clicked
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
        if (gestureData.ContainsKey(randomMp4Files[gestureIndex]))
        {
            descText.text = randomMp4Files[gestureIndex] + " : " + gestureData[randomMp4Files[gestureIndex]];
        }
        else
        {
            Debug.LogWarning("ID not found in CSV data.");
            descText.text = "ID not found in CSV data.";
        }
    }

    void PlayVideo()
    {
        // Set the video clip to the selected video file
        string videoPath = Path.Combine(videoDirectoryPath, randomMp4Files[gestureIndex] + ".mp4");
        videoPlayer.url = videoPath;

        // Play the video
        videoPlayer.Play();
        Debug.Log("Playing video: " + videoPath);
    }

    // Update is called once per frame
    void Update()
    {
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            // Re-select the last selected button
            if (lastSelectedButton != null)
            {
                lastSelectedButton.Select();
            }
        }
    }

}

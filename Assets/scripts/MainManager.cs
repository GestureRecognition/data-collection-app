using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;

public class MainManager : MonoBehaviour
{
    public static MainManager Instance;
    // Main Manager Instance is used to maintain data presistence between scenes.

    //Arguments ==================================
    private string directoryPath = ".\\Assets\\trialPreview";
    private string previewType = "0.6x";
    private string imagePreviewType = "0.6x_5FRAME";
    private string GestDescFilename = "GestureDescription.csv";
    private string saveFileName = "QueueData.json";
    public string serverIP = "127.0.0.1";
    public int serverPort = 9000;
    public int streamingPort = 9001;


    //Public Variables ===========================
    public string imageDirectoryPath;
    public string videoDirectoryPath;
    public List<string> allGestureClass = new List<string>();
    public Dictionary<string, string> gestureDescData = new Dictionary<string, string>();
    public Queue<string> shuffledQueue = new Queue<string>();
    public List<string> gestureMiniBatch = new List<string>();
    public bool noNeedToShuffle = false;



    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        Debug.Log("MainManager Initialization"); //DEBUG
        //App Initialization ===========================
        videoDirectoryPath = Path.Combine(directoryPath, previewType);
        imageDirectoryPath = Path.Combine(directoryPath, imagePreviewType);
        FetchGestureClasses();
        ReadDescCSV();
        LoadQueueData();
    }

    void FetchGestureClasses()
    {
        if (Directory.Exists(videoDirectoryPath))
        {
            string[] mp4Files = Directory.GetFiles(videoDirectoryPath, "*.mp4");
            foreach (string filePath in mp4Files)
            {
                allGestureClass.Add(Path.GetFileName(filePath));
            }

            Debug.Log("Found " + allGestureClass.Count + " MP4 files.");
        }
        else
        {
            Debug.LogError("Directory does not exist: " + videoDirectoryPath);
        }
    }

    public void ShuffleAndFillQueue()
    {
        List<string> shuffledList = allGestureClass.OrderBy(x => UnityEngine.Random.value).ToList();
        shuffledQueue.Clear(); //TODO CHeck maybe we dont need this
        foreach (string file in shuffledList)
        {
            shuffledQueue.Enqueue(file);
        }
    }

    public void SelectRandomMiniBatch()
    {
        while (shuffledQueue.Count < 5)
        {
            ShuffleAndFillQueue();
        }

        int numberOfFilesToSelect = Mathf.Min(5, shuffledQueue.Count);
        gestureMiniBatch = new List<string>();
        for (int i = 0; i < numberOfFilesToSelect; i++)
        {
            if (shuffledQueue.Count > 0)
            {
                gestureMiniBatch.Add(Path.GetFileNameWithoutExtension(shuffledQueue.Dequeue()));
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

                gestureDescData.Add(id, description);
            }

            Debug.Log("CSV file read successfully.");
        }
        else
        {
            Debug.LogError("CSV file not found at: " + filePath);
        }
    }

    public void SaveQueueData()
    {
        string filePath = Path.Combine(directoryPath, saveFileName);

        var data = new
        {
            ShuffledQueue = shuffledQueue.ToArray(),
            GestureMiniBatch = gestureMiniBatch
        };

        try
        {
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(filePath, json);
            Debug.Log("Queue data saved to JSON: " + filePath);
        }
        catch (IOException e)
        {
            Debug.LogError("Error saving queue data to JSON: " + e.Message);
        }
    }
    public void LoadQueueData()
    {
        string filePath = Path.Combine(directoryPath, saveFileName);

        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var data = JsonConvert.DeserializeObject<QueueData>(json);

                if (data != null)
                {
                    shuffledQueue = new Queue<string>(data.ShuffledQueue);
                    gestureMiniBatch = new List<string>(data.GestureMiniBatch);
                    noNeedToShuffle = true;
                    Debug.Log("Queue data loaded from JSON: " + filePath);
                }
            }
            catch (IOException e)
            {
                Debug.LogError("Error loading queue data from JSON: " + e.Message);
            }
        }
        else
        {
            Debug.Log("Queue data JSON file not found. Skipping load.");
        }
    }

    [System.Serializable]
    private class QueueData
    {
        public string[] ShuffledQueue;
        public List<string> GestureMiniBatch;
    }

    private void Start()
    {
        Debug.Log("Start function MainManager"); //DEBUG
    }

    private void OnApplicationQuit()
    {
        SaveQueueData();
    }
}


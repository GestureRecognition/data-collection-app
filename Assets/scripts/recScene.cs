using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Debug = UnityEngine.Debug;

public class recScene : MonoBehaviour
{
    public float delayBetweenFrames = 0.04f; // 25 FPS recording/playback
    private List<Texture2D> frames = new List<Texture2D>();
    private bool isRecording = false;
    private bool isPlaying = false;
    public RawImage targetRawImage; // The RawImage you want to "screenshot"
    public RawImage scrn;           // RawImage where frames will be displayed
    private float timeSinceLastFrame = 0f;

    private Transform previewLayout;
    private Image[] sideImages = new Image[5];

    private Image recording_indicator;
    private TextMeshProUGUI countdown;

    // Start is called before the first frame update
    void Start()
    {
        setupRecording();
        setupSideImages();
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
            countdown.text = i.ToString();    // Update the text to the current countdown value
            yield return new WaitForSeconds(1);  // Wait for 1 second
        }
        countdown.gameObject.SetActive(false);  // Option 2: Disable the countdown GameObject
        StartRecording();
    }

    void StartRecording()
    {
        frames.Clear();
        isRecording = true;
        recording_indicator.gameObject.SetActive(true);
        Debug.Log("Recording started.");
    }

    void StopRecording()
    {
        isRecording = false;
        recording_indicator.gameObject.SetActive(false);
        Debug.Log($"Recording stopped. Total frames recorded: {frames.Count}");
        StartPlayback();
    }

    // Capture the current state of the target RawImage
    Texture2D CaptureFrameFromRawImage()
    {
        Texture2D frame = new Texture2D(targetRawImage.texture.width, targetRawImage.texture.height, TextureFormat.RGB24, false);

        // Copy the RawImage texture to a Texture2D
        RenderTexture rt = RenderTexture.GetTemporary(targetRawImage.texture.width, targetRawImage.texture.height);
        Graphics.Blit(targetRawImage.texture, rt);
        RenderTexture.active = rt;

        frame.ReadPixels(new Rect(0, 0, targetRawImage.texture.width, targetRawImage.texture.height), 0, 0);
        frame.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        Debug.Log("Captured frame from RawImage.");
        return frame;
    }

    void RecordFrame()
    {
        if (isRecording)
        {
            Texture2D frame = CaptureFrameFromRawImage();
            frames.Add(frame);

            Debug.Log($"Captured Frame {frames.Count}. Resolution: {frame.width}x{frame.height}");
        }
    }

    void DisplayFrame(Texture2D frame)
    {
        scrn.texture = frame;
        Debug.Log("Displaying frame on screen.");
    }

    void SetWhiteDisplay()
    {
        // Create a new white texture and apply it to the scrn RawImage
        Texture2D whiteTexture = new Texture2D(scrn.texture.width, scrn.texture.height);
        Color whiteColor = Color.white;

        // Fill the texture with white color
        for (int y = 0; y < whiteTexture.height; y++)
        {
            for (int x = 0; x < whiteTexture.width; x++)
            {
                whiteTexture.SetPixel(x, y, whiteColor);
            }
        }
        whiteTexture.Apply();

        // Assign the white texture to the RawImage
        scrn.texture = whiteTexture;
        Debug.Log("Display set to white.");
    }

    void StartPlayback()
    {
        if (!isPlaying && frames.Count > 0)
        {
            Debug.Log($"Starting playback. Total frames to display: {frames.Count}");
            StartCoroutine(Playback());
        }
        else
        {
            Debug.LogWarning("No frames available for playback or playback already in progress.");
        }
    }

    IEnumerator Playback()
    {
        isPlaying = true;
        for (int i = 0; i < frames.Count; i++)
        {
            DisplayFrame(frames[i]);
            Debug.Log($"Displaying frame {i + 1}/{frames.Count}.");
            yield return new WaitForSeconds(delayBetweenFrames);
        }

        // Set the display to white after playback
        SetWhiteDisplay();

        isPlaying = false;
        Debug.Log("Playback finished.");
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

    // Save each frame to disk as an image file
    void SaveFramesAsImages()
    {
        string directoryPath = Application.persistentDataPath + "/Output/recorded_frames/";
        //string directoryPath = "./Output/recorded_frames/";

        // Ensure directory exists
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        for (int i = 0; i < frames.Count; i++)
        {
            byte[] bytes = frames[i].EncodeToPNG();  // Save as PNG
            string filePath = directoryPath + "frame_" + i.ToString("D4") + ".png"; // Frame_0000.png
            File.WriteAllBytes(filePath, bytes);

            Debug.Log($"Saved frame {i} to {filePath}");
        }
    }

    void ClearSavedFrameImages()
    {
        string directoryPath = Application.persistentDataPath + "/Output/recorded_frames/";
        //string directoryPath = "./Output/recorded_frames/";

        // Check if the directory exists
        if (Directory.Exists(directoryPath))
        {
            // Get all files in the directory and delete them
            string[] files = Directory.GetFiles(directoryPath);
            foreach (string file in files)
            {
                File.Delete(file);
                Debug.Log($"Deleted {file}");
            }

            // Optionally, delete the directory itself
            Directory.Delete(directoryPath);
            Debug.Log($"Deleted directory {directoryPath}");
        }
        else
        {
            Debug.LogWarning("No saved frames directory found.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isRecording)
        {
            StartRecording();
        }
        else if (Input.GetKeyDown(KeyCode.Space) && isRecording)
        {
            StopRecording();
        }
        if (Input.GetKeyDown(KeyCode.Return) && !isRecording)
        {
            StartPlayback();
        }
    }

    private void LateUpdate()
    {
        timeSinceLastFrame += Time.deltaTime;
        if (isRecording && timeSinceLastFrame >= delayBetweenFrames)
        {
            RecordFrame();
            timeSinceLastFrame = 0f;

            Debug.Log("New frame recorded based on delayBetweenFrames.");
        }
    }

    public void SaveNext()
    {
        // Save the individual frames
        SaveFramesAsImages();
        /*
        string videoName = string.Join("-", MainManager.Instance.randomMp4Files);
        string videoPath = Application.persistentDataPath + "/Output/" + videoName + " " + DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + ".mp4";
        //string videoPath = "./Output/" + videoName + " " + DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + ".mp4";

        // Call FFmpeg to convert images to video (make sure FFmpeg is installed)
        string ffmpegPath = "C:\\ProgramData\\chocoportable\\lib\\ffmpeg\\tools\\ffmpeg";  // PATH to FFmpeg executable \\tools\\ffmpeg
        string inputPattern = Application.persistentDataPath + "/Output/recorded_frames/frame_%04d.png";  // Input file pattern
        //string inputPattern = "./Output/recorded_frames/frame_%04d.png";  // Input file pattern
        string arguments = $"-r 25 -i \"{inputPattern}\" -vcodec libx264 -pix_fmt yuv420p \"{videoPath}\"";  // FFmpeg arguments

        // Start the FFmpeg process
        ProcessStartInfo startInfo = new ProcessStartInfo(ffmpegPath, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        Process ffmpegProcess = new Process
        {
            StartInfo = startInfo
        };

        ffmpegProcess.Start();

        string ffmpegOutput = ffmpegProcess.StandardError.ReadToEnd();
        ffmpegProcess.WaitForExit();

        if (ffmpegProcess.ExitCode == 0)
        {
            Debug.Log($"Video saved to {videoPath}");
        }
        else
        {
            Debug.LogError($"FFmpeg error: {ffmpegOutput}");
        }
        */
        ClearSavedFrameImages();

        SceneManager.LoadScene("Main");
    }

    public void ExitBtn()
    {
        //Save Log TODO
        Application.Quit();
        Debug.Log("Application is exiting");
        UnityEditor.EditorApplication.isPlaying = false;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class recordSystem : MonoBehaviour
{
    public float delayBetweenFrames = 0.04f; // 25 FPS recording/playback
    private List<Texture2D> frames = new List<Texture2D>();
    private bool isRecording = false;
    private bool isPlaying = false;
    public RawImage targetRawImage; // The RawImage you want to "screenshot"
    public RawImage scrn;           // RawImage where frames will be displayed

    private float timeSinceLastFrame = 0f;

    void StartRecording()
    {
        frames.Clear();
        isRecording = true;
        Debug.Log("Recording started.");
    }

    void StopRecording()
    {
        isRecording = false;
        Debug.Log($"Recording stopped. Total frames recorded: {frames.Count}");
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

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isRecording)
        {
            StartRecording();
        } else if (Input.GetKeyDown(KeyCode.Space) && isRecording)
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
}
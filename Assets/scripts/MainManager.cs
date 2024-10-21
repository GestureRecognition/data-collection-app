using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MainManager : MonoBehaviour
{
    public static MainManager Instance;

    public string imageDirectoryPath;


    public Queue<string> shuffledQueue = new Queue<string>();
    public List<string> randomMp4Files = new List<string>();

    public string recordingOutputPath = "";


    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    
}


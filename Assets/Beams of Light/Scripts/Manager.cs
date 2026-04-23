using UnityEngine;

public class Manager : MonoBehaviour
{
    public static Manager instance;
    public int itemsRequired = 3;
    private int currentItems = 0;

    void Awake()
    {
        instance = this;
        QualitySettings.vSyncCount = 1; // 0 = Off, 1 = Every VBlank, 2 = Every second VBlank
        
        #if UNITY_2022_2_OR_NEWER
        Application.targetFrameRate = (int)Screen.currentResolution.refreshRateRatio.value;
        #else
        Application.targetFrameRate = (int)Screen.currentResolution.refreshRate;
        #endif
    }

    public void ItemCollected()
    {
        currentItems++;
        if (currentItems >= itemsRequired)
        {
            CompleteLevel();
        }
    }

    void CompleteLevel()
    {
        // Logic to spawn the exit door or move to the next slice
        Debug.Log("Level Complete! The exit is open.");
    }
}
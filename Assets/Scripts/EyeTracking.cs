using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using Varjo.XR;
using static Varjo.XR.VarjoEyeTracking;
using System.Collections;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils.GUI;
using Unity.VisualScripting;

public enum GazeSource
{
    InputSubsystem, 
    GazeAPI
}

public class EyeTracking : MonoBehaviour
{
    private static readonly Vector3 lockedZPos = new Vector3(float.NaN, float.NaN, 0.0f); // only Z matters

    [Header("Render Texture")]
    public RenderTexture renderTexture1;
    [Header("Gaze Data Source Selection")]
    public GazeSource gazeDataSourceSelection = GazeSource.InputSubsystem;

    [Header("Visualizations")]
    public Transform fixationPointTrans;
    public Transform leftEyeTrans;
    public Transform rightEyeTrans;

    [Header("camera")]
    public Camera XRCamera;

    [Header("Gaze Indicator")]
    public GameObject scotomaGaze;

    [Header("Gaze data output frequency Setting")]
    public VarjoEyeTracking.GazeOutputFrequency freq;

    [Header("Gaze Ray Radius")]
    public float GazeRadius = 0.01f;

    [Header("Scotoma Gaze Point floating Distance")]
    public float FloatingScotomaDistance = 5.0f;

    [Header("Render Texture")]
    public RenderTexture renderTexture;

    [Header("Scotoma Toggle")]
    public bool toggleScotoma = false;

    [Header("Fixed Camera")]
    public Camera FixedCamera;

    private List<InputDevice> inputDevices = new List<InputDevice>();
    private InputDevice aDevice;
    private Eyes eye;
    private VarjoEyeTracking.GazeData GazeData;
    private List<VarjoEyeTracking.GazeData> DataSinceLastUpdate;
    private Vector3 LeftEyePos; 
    private Vector3 RightEyePos;
    private Quaternion RightEyeRot;
    private Quaternion LeftEyeRot;
    private Vector3 FixationPoint;
    private Vector3 Direction;
    private Vector3 RayOrigin;
    private RaycastHit Hit;
    private float Distance;
    private StreamWriter writer = null;
    private bool Logging = false;

    private static readonly string[] ColNames = { "frame", "captureTime", "logTime", "HMDPos_x", "HMDPos_y", "HMDPos_z", "HMDRot_1", "HMDRot_2", "HMDRot_3", "HMDRot_4", "gazeStatus", "combinedGazedForward_x","combinedGazedForward_y", "combinedGazedForward_z", "combinedGazePos_x", "combinedGazePos_y", "combinedGazePos_z", "focusDistance", "focusStability", "AOI", "hitCoordinate_x", "hitCoordinate_y", "hitCoordinate_z", "screenPoint_x", "screenPoint_y", "screenPoint_z"};
    private const string validString = "VALID";
    private const string invalidString = "INVALID";

    int GazeDataCount = 0;
    float GazeTimer = 0f;
    //private SceneFader sceneFader;

    private float appTimer = 1000.0f;  // Timer
    private bool isRecalibrating = false;
    void ObtainDevice()
    {
        InputDevices.GetDevicesAtXRNode(XRNode.CenterEye, inputDevices);
        aDevice = inputDevices.FirstOrDefault();

    }

    void OnEnable()
    {
        if (!aDevice.isValid)
        {
            ObtainDevice();
        }
    }

    // Start is called before the first frame update
    private void Start()
    {
        Application.onBeforeRender += LockCameraZ;

        if (VarjoEyeTracking.IsGazeAllowed() && VarjoEyeTracking.IsGazeCalibrated())
        {
            scotomaGaze.SetActive(toggleScotoma);

        }
        else
        {
            scotomaGaze.SetActive(false);
        }

        //Uncomment to record gaze data and screen capture
        //StartRecordingData();
        //StartCoroutine(CaptureRenderTextureEverySecond());
    }


    private IEnumerator CheckCalibrationStatusAndStartRecording()
    {
        // Force a new gaze calibration at the beginning of the scene
        Debug.Log("Requesting gaze calibration to reset at the start.");
        VarjoEyeTracking.RequestGazeCalibration();

        // Wait briefly to allow the SDK to enter calibration mode
        yield return new WaitForSeconds(0.5f);

        // Now wait until calibration completes
        while (!VarjoEyeTracking.IsGazeCalibrated())
        {
            Debug.Log("Waiting for recalibration to complete...");
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("Recalibration completed successfully.");

        // Ensure gaze tracking is allowed and toggle scotomaGaze based on calibration status
        if (VarjoEyeTracking.IsGazeAllowed())
        {
            scotomaGaze.SetActive(toggleScotoma);
        }
        else
        {
            scotomaGaze.SetActive(false);
            Debug.LogWarning("Gaze tracking is not allowed or calibration failed.");
        }

        //Uncomment to record gaze data and screen capture
        // Start recording data only after calibration is fully complete
        //StartRecordingData();
        //StartCoroutine(CaptureRenderTextureEverySecond());
    }


    // Update is called once per frame
    void Update()
    {

        appTimer -= Time.deltaTime;

                if (appTimer <= 0f)
                {
                    Debug.Log("Time's up!");

                    // Get the current active scene index
                    int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;

                    // Check if this is the last scene in the build settings
                    if (currentSceneIndex < SceneManager.sceneCountInBuildSettings - 1)
                    {
                            SceneManager.LoadScene(currentSceneIndex + 1);         
                        // Load the next scene
                       
                    }
                    else
                    {
                        // If it's the last scene, quit the application
                        Debug.Log("All scenes completed. Quitting the application.");
                        Application.Quit();

        #if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;  // Stops play mode in the Unity Editor
        #endif
                    }
                }

        if (VarjoEyeTracking.IsGazeAllowed() && VarjoEyeTracking.IsGazeCalibrated())
        {
            if(!aDevice.isValid)
            {
                ObtainDevice();
            }

            scotomaGaze.SetActive(toggleScotoma);

            if (gazeDataSourceSelection == GazeSource.InputSubsystem)
            {
                if(aDevice.TryGetFeatureValue(CommonUsages.eyesData, out eye))
                {
                    if(eye.TryGetLeftEyePosition(out LeftEyePos))
                    { 
                        leftEyeTrans.localPosition =LeftEyePos;
                    }
                    if(eye.TryGetLeftEyeRotation(out RightEyeRot))
                    {
                        leftEyeTrans.localRotation = LeftEyeRot;
                    }
                    if(eye.TryGetRightEyePosition(out RightEyePos))
                    {
                        rightEyeTrans.localPosition = RightEyePos;
                    }
                    if(eye.TryGetRightEyeRotation(out RightEyeRot))
                    {
                        rightEyeTrans.localRotation = RightEyeRot;
                    }
                    if(eye.TryGetFixationPoint(out FixationPoint))
                    {
                        fixationPointTrans.localPosition = FixationPoint;
                    }
                }

                //Set raycast origin point to VR Camera position
                RayOrigin = XRCamera.transform.position;

                //Calculate direction frm VR camera towards fixation point
                Direction = (fixationPointTrans.position - XRCamera.transform.position).normalized;
            }
            else
            {
                GazeData = VarjoEyeTracking.GetGaze();
                if(GazeData.status != VarjoEyeTracking.GazeStatus.Invalid)
                {
                    Debug.Log("Gaze Data Status: " + GazeData.status);
                    // GazeRay vectors are relative to the HMD pose so they need to be transformed to world space
                    if (GazeData.leftStatus != VarjoEyeTracking.GazeEyeStatus.Invalid)
                    {
                        leftEyeTrans.position =XRCamera.transform.TransformPoint(GazeData.left.origin);
                        leftEyeTrans.rotation = Quaternion.LookRotation(XRCamera.transform.TransformDirection(GazeData.left.forward));
                    }
                    if(GazeData.rightStatus != VarjoEyeTracking.GazeEyeStatus.Invalid)
                    {
                        rightEyeTrans.transform.position = XRCamera.transform.TransformPoint(GazeData.right.origin);
                        rightEyeTrans.transform.rotation = Quaternion.LookRotation(XRCamera.transform.TransformDirection(GazeData.right.forward));
                    }

                    // Set gaze origin as raycast origin
                    RayOrigin = XRCamera.transform.TransformPoint(GazeData.gaze.origin);

                    // Set gaze direction as raycast direction
                    Direction = XRCamera.transform.TransformDirection(GazeData.gaze.forward);

                    // Fixation point can be calculated using ray origin, direction and focus distance
                    fixationPointTrans.position = RayOrigin + Direction * GazeData.focusDistance;

                }
            }

        }
        else
        {
            if (!VarjoEyeTracking.IsGazeCalibrated() && !isRecalibrating)
            {
                // Pause the game and request recalibration if calibration is lost
                isRecalibrating = true;
                StartCoroutine(RecalibrateAndPauseGame());
            }
        }
        //Perform raycast to detect if gaze is hitting an AOI
        Ray gazeRay = new Ray(RayOrigin, Direction);
        string currentAOI = "None";  // Default value for AOI
        Vector3 hitPoint = Vector3.zero;
        Vector3 screenPoint = Vector3.zero;
        if (Physics.Raycast(gazeRay, out Hit))
        {
            GameObject hitObject = Hit.collider.gameObject;
            if (hitObject.CompareTag("AOI"))
            {
                currentAOI = hitObject.name;  // AOI detected
                hitPoint = Hit.point; //store the hit coordinates in world space
                screenPoint = FixedCamera.WorldToScreenPoint(hitPoint); //convert world space to screen point

                Debug.Log($"CameraPixel width: {XRCamera.pixelWidth},{FixedCamera.pixelWidth}, PixelHeight: {XRCamera.pixelHeight},{FixedCamera.pixelHeight}");
                
                Debug.Log($"Current AOI: {currentAOI}, Hit Point: {screenPoint}");
            }

        }
        // If gaze ray didn't hit anything, the gaze target is shown at fixed distance
        scotomaGaze.transform.position = RayOrigin + Direction * FloatingScotomaDistance;
        scotomaGaze.transform.LookAt(RayOrigin, Vector3.up);
        scotomaGaze.transform.localScale = Vector3.one * FloatingScotomaDistance;

        Debug.Log("updating logging variable: "+ Logging);
        if(Logging)
        {
            GazeTimer += Time.deltaTime;
            if (GazeTimer > 1f)
            {
                GazeDataCount = 0;
                GazeTimer = 0f;
            }

            int DataCount = VarjoEyeTracking.GetGazeList(out DataSinceLastUpdate);
            GazeDataCount += DataCount;
            Debug.Log("DataCount: " + DataCount); // Should be > 0 for logging to occur

            //Uncomment to record gaze data
            //for (int i=0; i<DataCount; i++)
            //{
                //LogEyeTrackingData(DataSinceLastUpdate[i], currentAOI, hitPoint, screenPoint);
            //}
        }
    }

    private IEnumerator RecalibrateAndPauseGame()
    {
        Debug.Log("Calibration lost. Pausing game and requesting recalibration.");

        // Pause the game
        Time.timeScale = 0;

        // Request recalibration
        VarjoEyeTracking.RequestGazeCalibration();

        // Wait until calibration completes
        while (!VarjoEyeTracking.IsGazeCalibrated())
        {
            Debug.Log("Waiting for recalibration to complete...");
            yield return new WaitForSecondsRealtime(0.5f); // Use real-time wait to avoid being affected by Time.timeScale
        }

        Debug.Log("Recalibration completed. Resuming game.");

        // Resume the game
        Time.timeScale = 1;
        isRecalibrating = false;
    }


    void LogEyeTrackingData(VarjoEyeTracking.GazeData Data, string AOI, Vector3 hitPoint, Vector3 screenPoint)
    {
        Debug.Log("Logging Eye Tracking Data...");

        string[] loggingData = new string[13];

        // Gaze data frame number
        loggingData[0] = Data.frameNumber.ToString();

        // Gaze data capture time (nanoseconds)
        loggingData[1] = Data.captureTime.ToString();

        //Log time(milliseconds)
        loggingData[2] = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond).ToString();

        //HMD Position & Rotation
        loggingData[3] = XRCamera.transform.localPosition.ToString("F3");
        loggingData[4] = XRCamera.transform.localRotation.ToString("F3");

        //combinedGaze
        bool invalid = Data.status == VarjoEyeTracking.GazeStatus.Invalid;
        loggingData[5] = invalid ? invalidString : validString;
        loggingData[6] = invalid ? "": Data.gaze.forward.ToString("F3");
        loggingData[7] = invalid ? "" : Data.gaze.origin.ToString("F3"); //figure out why it is outputting (0,0,0) 0nly

        //focusDist
        loggingData[8] = invalid ? "" : Data.focusDistance.ToString();
        loggingData[9] = invalid ? "" : Data.focusStability.ToString();

        loggingData[10] = AOI;
        loggingData[11] = hitPoint.ToString();
        loggingData[12] = screenPoint.ToString();
 

        WriteData(loggingData);

    }

    void WriteData(string[] Values)
    {
        if (!Logging || writer == null)
            return;

        string Line = "";
        for (int i = 0; i < Values.Length; ++i)
        {

            Values[i] = Values[i].Replace("\r", "").Replace("\n", ""); // Remove new lines so they don't break csv

            if (Values[i].StartsWith("(") | Values[i].EndsWith(")"))
            {
                Values[i] = Values[i].Replace("(", "").Replace(")", "");
            }
            Line += Values[i] + (i == (Values.Length - 1) ? "" : ","); // Do not add semicolon to last data string
        }
        Debug.Log("Writing Line: " + Line);
        writer.WriteLine(Line);
    }

    public void StartRecordingData()
    {
        Logging = true;
        string sceneName = SceneManager.GetActiveScene().name;
        string LogPath = Application.dataPath + "/Logs/";
        Directory.CreateDirectory(LogPath);

        DateTime now = DateTime.Now;
        string FileName = string.Format("{0}_{1}-{2:00}-{3:00}-{4:00}-{5:00}", sceneName, now.Year, now.Month, now.Day, now.Hour, now.Minute);

        string Path = LogPath + FileName + ".csv";
        writer = new StreamWriter(Path);

        string colNames = string.Join(",", ColNames);
        Debug.Log("Writing Column Names: "+ colNames);
        writer.WriteLine(colNames);
        Debug.Log("Log file started at: " + Path);

    }

    void StopRecordingData()
    {
        if (!Logging)
            return;
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
          
        }

        Logging = false;
        Debug.Log("Data Recording ended");
    }
    void OnApplicationQuit()
    {
        StopRecordingData();
        StopAllCoroutines(); // Stop any running coroutines
    }

    private IEnumerator CaptureRenderTextureEverySecond()
    {
        while (true) // Infinite loop to capture every second
        {
            yield return new WaitForSeconds(1f); // Wait for 1 second
            CaptureRenderTexture(); // Capture the Render Texture
        }
    }

    void CaptureRenderTexture()
    {
        if (renderTexture == null)
        {
            Debug.LogError("Render Texture is not set.");
            return;
        }

        // Create a Texture2D to hold the captured image
        Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);

        // Set the active Render Texture
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = renderTexture;

        // Read pixels from the Render Texture
        texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture.Apply();

        // Reset the active Render Texture
        RenderTexture.active = currentRT;

        // Use the same directory as the CSV log file
        // Get the active scene name
        string sceneName = SceneManager.GetActiveScene().name;
        string logPath = Application.dataPath + "/Logs/";

        // Create a unique filename for the Render Texture capture
        string filePath = Path.Combine(logPath, $"RenderTextureCapture_{sceneName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

        // Save the texture as a PNG file
        byte[] bytes = texture.EncodeToPNG();
        File.WriteAllBytes(filePath, bytes);
        Debug.Log($"Render Texture captured and saved to: {filePath}");

        // Cleanup
        Destroy(texture);
    }

    public Vector2 GetNormalizedGazePosition()
    {
        if (VarjoEyeTracking.IsGazeAllowed() && VarjoEyeTracking.IsGazeCalibrated())
        {

            VarjoEyeTracking.GazeData gazeData = VarjoEyeTracking.GetGaze();
            Vector3 gazeWorldPos = XRCamera.transform.TransformPoint(gazeData.gaze.origin + gazeData.gaze.forward * FloatingScotomaDistance);

            // Convert from world space to viewport coordinates (0,1)
            Vector3 screenGaze = Camera.main.WorldToViewportPoint(gazeWorldPos);

            float gazeX = Mathf.Clamp01(screenGaze.x);
            float gazeY = screenGaze.y;
            // Flip Y if using Direct3D-like APIs 
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 ||
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal ||
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan)
            {
                gazeY = 1.0f - gazeY;
            }
            gazeY = Mathf.Clamp01(gazeY);

            Debug.Log($"Gaze.X: {gazeX} , Gaze.Y: {gazeY}");
            return new Vector2(gazeX,gazeY);
        }
        else
        {
            Debug.Log("Returning hardcoded vector: Vector2(0.5f, 0.5f");
            return new Vector2(0.5f, 0.5f);
        }

       
    }
    void LockCameraZ()
    {
        if (XRCamera != null)
        {
            Vector3 localPos = XRCamera.transform.localPosition;
            localPos.z = lockedZPos.z;
            XRCamera.transform.localPosition = localPos;
        }
    }
    void OnDestroy()
    {
        Application.onBeforeRender -= LockCameraZ;
    }
}

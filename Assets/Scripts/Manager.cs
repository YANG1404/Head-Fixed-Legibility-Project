using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.IO;
using System.Text;
using UnityEngine.XR;

/// <summary>
/// VR Head Fixed Ui Text Legibility Experiment Manager
/// 
/// Purpose: 
/// Manages a VR Head Fixed Ui text legibility experiment in Meta Quest 3. 
/// It ramdomly spawns text elements , tracks user typing inputs, and records the exact visual angles (AngleX, AngleY).
/// 
/// Key Features:
/// - Spawns text in a 3x3 grid ensuring no overlaps (Safe Extents).
/// - Calculates the exact horizontal and vertical eccentricity (AngleX, AngleY) relative to the VR Camera(replacement for user view).
/// - CSV data logging using a Dictionary to prevent data loss and allow rewinding.
/// - Dynamic, non-intrusive screenshot capture using camera lens shifting to avoid perspective distortion.
/// 
/// How to Use:
/// Attach this script to an empty Manager GameObject. Assign the Target Canvas(or Panel), VR Camera, and UI Prefab.
/// Use keyboard shortcuts (1: Start, 2: Next, 3: Prev, 4: Capture) to control the experiment flow.
/// </summary>
public class Manager : MonoBehaviour
{
    [Header("UI Spawn Area")]
    [Tooltip("The RectTransform of the base Canvas or a full-screen empty panel where text will be spawned.(Set up alpha 0 in the experiment)")]
    public RectTransform targetCanvasArea;

    [Header("FOV Verification Markers")]
    [Tooltip("Assign empty objects (or images) placed exactly at the top, bottom, left, and right edges of the spawn area. Used to verify physical FOV boundaries.")]
    public Transform topMarker;
    public Transform bottomMarker;
    public Transform leftMarker;
    public Transform rightMarker;

    [Header("Prefab & Spawning Rules")]
    [Tooltip("Assign a prefab with an Image (Background) at the root and a TextMeshProUGUI as a child. (Auto-layout components must be removed)")]
    public GameObject textPrefab;

    [Header("Font Size Pool (8-Step Log Scale)")]
    public float[] descendingSizePool = { 201.0f, 122.9f, 75.3f, 46.1f, 28.3f, 17.3f, 10.6f, 6.5f };

    [Tooltip("Minimum safe distance (padding) in pixels between spawned text backgrounds to prevent overlapping.")]
    public float itemPadding = 20f;

    [Header("Gaze & Tracking Settings")]
    [Tooltip("The UI element indicating the current target text to the user.")]
    public RectTransform highlightCircle;

    [Tooltip("The central reticle representing the user's gaze center. It will be forced to render on top to prevent VR sickness and guide the user.")]
    public RectTransform centerReticle;

    [Header("Camera & Capture Settings")]
    [Tooltip("The main VR Camera tracking the user's head.")]
    public Camera vrCamera;
    public int captureWidth = 1100;
    public int captureHeight = 960;
    public Vector3 capturePositionOffset = Vector3.zero;
    public Vector3 captureRotationOffset = Vector3.zero;

    [Tooltip("Lens Shift Y value. Enter a negative value (e.g., -0.1 to -0.2) to capture more of the lower area without causing perspective distortion (keystoning).")]
    public float captureLensShiftY = -0.15f;

    private struct SpawnedTextInfo
    {
        public GameObject obj;
        public TextMeshProUGUI textMesh;
        public RectTransform bgRect;
        public string zoneName;
    }

    // Snellen Alphabet: Standard optotypes used for visual acuity testing.
    private readonly string alphabetPool = "CDEFLOPTZ";
    private List<SpawnedTextInfo> currentTargetInfos = new List<SpawnedTextInfo>();
    private int currentTargetIndex = -1;

    private int currentSessionCount = 0;
    private int accumulatedTextCount = 0;

    private string sessionFolderPath;
    private string logFilePath;

    // Using a Dictionary to safely store and overwrite logs. Essential for handling the 'Undo' feature.
    private Dictionary<int, string> logDataMap = new Dictionary<int, string>();

    void Start()
    {
        // Ensure the VR render scale is default to maintain consistent visual fidelity during the test.
        XRSettings.eyeTextureResolutionScale = 1.0f;

        // 1. Setup Session Directory and CSV Log File
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string baseFolder = Path.Combine(Application.persistentDataPath, "Experiment_Log");

        if (!Directory.Exists(baseFolder)) Directory.CreateDirectory(baseFolder);

        sessionFolderPath = Path.Combine(baseFolder, $"Result_{timestamp}");
        Directory.CreateDirectory(sessionFolderPath);

        logFilePath = Path.Combine(sessionFolderPath, "TypingTestLog.csv");
        string header = "Session,GlobalIndex,LocalIndex,TargetChar,AngleX,AngleY,UserInput,IsCorrect,FontSize,SpawnZone\n";
        File.WriteAllText(logFilePath, header, new UTF8Encoding(true));

        Debug.Log($"[System] Session folder and log file created!\nPath: {sessionFolderPath}");  // path of the session folder

        // 2. Validate the FOV mapping for debugging
        LogSpawnAreaBoundaries();
        CheckMarkerAngles();
    }

    void Update()
    {
        // ==========================================
        // Hotkey Manual (For Experimenter/Debugging)
        // 1: Start a new session round (Clears and respawns texts)
        // 2: Proceed to the next target text
        // 3: Revert to the previous target text (Undo mistake)
        // 4: Force a manual screenshot capture
        // ==========================================
        if (Input.GetKeyDown(KeyCode.Alpha1)) { StartNewRound(); return; }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { ProceedToNextTarget(); return; }
        if (Input.GetKeyDown(KeyCode.Alpha3)) { ProceedToPreviousTarget(); return; }
        if (Input.GetKeyDown(KeyCode.Alpha4)) { StartCoroutine(ManualCaptureRoutine()); return; }

        // Track user typing input
        if (currentTargetIndex >= 0 && currentTargetIndex < currentTargetInfos.Count)
        {
            string inputStr = Input.inputString.ToUpper();
            foreach (char c in inputStr)
            {
                if (alphabetPool.Contains(c) || c == '0')
                {
                    HandleUserInput(c.ToString());
                    break;
                }
            }
        }
    }

    public void StartNewRound()
    {
        if (targetCanvasArea == null)
        {
            Debug.LogError("[Error] Target Canvas Area is empty! Please assign a panel in the Inspector.");
            return;
        }

        if (currentTargetInfos != null) accumulatedTextCount += currentTargetInfos.Count;

        ClearTexts();
        currentSessionCount++;

        Debug.Log($"[Session Start] Session: {currentSessionCount} | Target Area({targetCanvasArea.rect.width}x{targetCanvasArea.rect.height})");

        SpawnTexts();

        // If spawn was successful, update the UI and capture the initial layout state.
        if (currentTargetInfos.Count > 0)
        {
            Canvas.ForceUpdateCanvases();
            StartCoroutine(CaptureAndHighlightRoutine());
        }
    }

    // ==========================================
    // 3x3 Grid Spawning Logic
    // ==========================================
    private void SpawnTexts()
    {
        List<char> availableChars = new List<char>(alphabetPool.ToCharArray());

        // Calculate maximum radiuses (Half width/height of the total canvas area)
        float maxRx = targetCanvasArea.rect.width / 2f;
        float maxRy = targetCanvasArea.rect.height / 2f;

        // Mathematically divide the canvas into 3x3 equal cells
        float cellWidth = targetCanvasArea.rect.width / 3f;
        float cellHeight = targetCanvasArea.rect.height / 3f;

        string[] rowNames = { "Top", "Middle", "Bottom" };
        string[] colNames = { "Left", "Center", "Right" };

        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                if (availableChars.Count == 0) break;

                // Pick a random un-used character from the pool
                int charIndex = Random.Range(0, availableChars.Count);
                char selectedChar = availableChars[charIndex];
                availableChars.RemoveAt(charIndex);

                // Pick a random font size from the 8-step array
                float randomFontSize = descendingSizePool[Random.Range(0, descendingSizePool.Length)];

                GameObject newObj = Instantiate(textPrefab, targetCanvasArea);
                RectTransform bgRect = newObj.GetComponent<RectTransform>();
                TextMeshProUGUI textMesh = newObj.GetComponentInChildren<TextMeshProUGUI>();
                RectTransform textRect = textMesh.GetComponent<RectTransform>();

                // Reset transforms to avoid scaling issues inside the Canvas
                bgRect.localScale = Vector3.one;
                bgRect.localRotation = Quaternion.identity;
                textRect.localScale = Vector3.one;
                textRect.localRotation = Quaternion.identity;

                textMesh.text = selectedChar.ToString();
                textMesh.fontSize = randomFontSize;
                textMesh.color = Color.white;
                textMesh.alignment = TextAlignmentOptions.CenterGeoAligned;

                // Dynamically fit the background to the exact size of the rendered text
                Vector2 exactFitSize = textMesh.GetPreferredValues(selectedChar.ToString(), randomFontSize, 0f);
                bgRect.sizeDelta = exactFitSize;
                textRect.sizeDelta = exactFitSize;
                textRect.anchoredPosition = Vector2.zero;

                // Calculate the boundaries of the current cell based on its row and column
                float xMin = -maxRx + (col * cellWidth);
                float xMax = xMin + cellWidth;
                float yMax = maxRy - (row * cellHeight);
                float yMin = yMax - cellHeight;

                // Apply padding to ensure the text doesn't spawn exactly on the cell border
                Vector2 safeExtents = (exactFitSize + new Vector2(itemPadding, itemPadding)) / 2f;

                float randomX = Random.Range(xMin + safeExtents.x, xMax - safeExtents.x);
                float randomY = Random.Range(yMin + safeExtents.y, yMax - safeExtents.y);
                Vector2 localSpawnPos = new Vector2(randomX, randomY);

                bgRect.anchoredPosition = localSpawnPos;
                bgRect.anchoredPosition3D = new Vector3(localSpawnPos.x, localSpawnPos.y, 0f);

                SpawnedTextInfo info = new SpawnedTextInfo
                {
                    obj = newObj,
                    textMesh = textMesh,
                    bgRect = bgRect,
                    zoneName = $"Grid_{rowNames[row]}-{colNames[col]}"
                };
                currentTargetInfos.Add(info);
            }
        }
    }

    private void HandleUserInput(string inputChar)
    {
        SpawnedTextInfo targetInfo = currentTargetInfos[currentTargetIndex];

        string expectedChar = targetInfo.textMesh.text;
        string recordInput = (inputChar == "0") ? "NotVisible" : inputChar;
        bool isCorrect = (inputChar == expectedChar);

        float effectiveFontSize = Mathf.Round(targetInfo.textMesh.fontSize * 100f) / 100f;

        // ==========================================
        // Visual Angle (Eccentricity) Calculation
        // ==========================================
        float angleX = 0f, angleY = 0f;
        if (vrCamera != null)
        {
            Vector3 textWorldPos = targetInfo.obj.transform.position;
            Vector3 camWorldPos = vrCamera.transform.position;
            Vector3 dirToText = textWorldPos - camWorldPos;

            // 1. Transform the world direction into the VR Camera's local space.
            // This ensures angles are measured relative to where the user's head is actually pointing.
            Vector3 localDir = vrCamera.transform.InverseTransformDirection(dirToText);

            // 2. Use Atan2 (Trigonometry) to calculate horizontal and vertical deviation from the center axis.
            angleX = Mathf.Round(Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg * 100f) / 100f;
            angleY = Mathf.Round(Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg * 100f) / 100f;
        }

        int globalIndex = accumulatedTextCount + currentTargetIndex + 1;
        int localIndex = currentTargetIndex + 1;

        string consoleLog = $"Sec {currentSessionCount} | Target {expectedChar} | AngleX {angleX}° | AngleY {angleY}° | Input {recordInput} | Zone {targetInfo.zoneName}";
        Debug.Log(consoleLog);

        string fileLog = $"{currentSessionCount},{globalIndex},{localIndex},{expectedChar},{angleX},{angleY},{recordInput},{isCorrect},{effectiveFontSize},{targetInfo.zoneName}\n";

        // Store the log string in a dictionary using globalIndex as the key.
        logDataMap[globalIndex] = fileLog;

        // Rewrite the entire file to ensure data integrity even if the app crashes.
        RewriteLogFile();

        ProceedToNextTarget();
    }

    private void RewriteLogFile()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("Session,GlobalIndex,LocalIndex,TargetChar,AngleX,AngleY,UserInput,IsCorrect,FontSize,SpawnZone\n");

        // Sort keys to ensure chronological order before rewriting the file.
        List<int> keys = new List<int>(logDataMap.Keys);
        keys.Sort();
        foreach (int key in keys) sb.Append(logDataMap[key]);

        File.WriteAllText(logFilePath, sb.ToString(), new UTF8Encoding(true));
    }

    private void ProceedToNextTarget()
    {
        if (currentTargetIndex < 0 || currentTargetIndex >= currentTargetInfos.Count) return;

        currentTargetIndex++;

        if (currentTargetIndex < currentTargetInfos.Count) UpdateHighlight();
        else
        {
            Debug.Log("== All texts completed! Automatically moving to the next Session. ==");
            if (highlightCircle != null) highlightCircle.gameObject.SetActive(false);
            currentTargetIndex = -1;
            StartNewRound();
        }
    }

    private void ProceedToPreviousTarget()
    {
        if (currentTargetIndex > 0)
        {
            currentTargetIndex--;

            int targetGlobalIndex = accumulatedTextCount + currentTargetIndex + 1;
            List<int> keysToRemove = new List<int>();

            // Identify and remove logs for targets ahead of the current index (Undo functionality).
            foreach (int key in logDataMap.Keys)
            {
                if (key >= targetGlobalIndex) keysToRemove.Add(key);
            }
            foreach (int key in keysToRemove) logDataMap.Remove(key);

            RewriteLogFile();
            UpdateHighlight();
        }
    }

    private void UpdateHighlight()
    {
        if (highlightCircle == null) return;

        highlightCircle.gameObject.SetActive(true);
        SpawnedTextInfo targetInfo = currentTargetInfos[currentTargetIndex];

        // Ensure the mesh is updated so we can retrieve accurate center coordinates.
        targetInfo.textMesh.ForceMeshUpdate();
        Vector3 exactTextCenter = targetInfo.textMesh.transform.TransformPoint(targetInfo.textMesh.textBounds.center);
        highlightCircle.position = exactTextCenter;

        float circleSize = targetInfo.textMesh.fontSize * 1.5f;
        highlightCircle.sizeDelta = new Vector2(circleSize, circleSize);

        // Force UI rendering order: Highlight circle on top of texts, Reticle on top of everything.
        highlightCircle.SetAsLastSibling();
        if (centerReticle != null) centerReticle.SetAsLastSibling();
    }

    private void ClearTexts()
    {
        foreach (var info in currentTargetInfos) if (info.obj != null) Destroy(info.obj);
        currentTargetInfos.Clear();
        if (highlightCircle != null) highlightCircle.gameObject.SetActive(false);
    }

    private IEnumerator CaptureAndHighlightRoutine()
    {
        // Wait for the end of the frame to ensure all UI elements are fully rendered before capturing.
        yield return new WaitForEndOfFrame();
        string filename = $"Capture_Session_{currentSessionCount}.png";
        TakeScreenshotAndSave(filename);
        currentTargetIndex = 0;
        UpdateHighlight();
    }

    private IEnumerator ManualCaptureRoutine()
    {
        yield return new WaitForEndOfFrame();
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"Capture_Manual_{timestamp}.png";
        TakeScreenshotAndSave(filename);
    }

    private void TakeScreenshotAndSave(string filename)
    {
        if (vrCamera != null)
        {
            // Create a temporary camera for the screenshot so it doesn't disrupt the user's actual VR view.
            RenderTexture rt = new RenderTexture(captureWidth, captureHeight, 24);
            GameObject tempCamObj = new GameObject("TempCaptureCamera");
            Camera tempCam = tempCamObj.AddComponent<Camera>();
            tempCam.CopyFrom(vrCamera);

            // Apply lens shift: Shifts the image plane without rotating the camera, 
            // preventing the UI from looking trapezoidal (perspective distortion).
            tempCam.usePhysicalProperties = true;
            tempCam.lensShift = new Vector2(0f, captureLensShiftY);

            tempCamObj.transform.SetParent(vrCamera.transform);
            tempCamObj.transform.localPosition = capturePositionOffset;
            tempCamObj.transform.localRotation = Quaternion.Euler(captureRotationOffset);

            tempCam.targetTexture = rt;
            tempCam.Render();

            RenderTexture.active = rt;
            Texture2D screenShot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
            screenShot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            screenShot.Apply();

            RenderTexture.active = null;
            Destroy(rt);
            Destroy(tempCamObj);

            byte[] bytes = screenShot.EncodeToPNG();
            string path = Path.Combine(sessionFolderPath, filename);
            File.WriteAllBytes(path, bytes);
            Destroy(screenShot);
        }
    }

    private void LogSpawnAreaBoundaries()
    {
        if (targetCanvasArea == null || vrCamera == null)
        {
            Debug.LogWarning("[Boundary Check] Target Canvas Area or VR Camera is missing. Cannot calculate angles.");
            return;
        }

        // Retrieve the 4 physical corners of the canvas in World Space.
        Vector3[] worldCorners = new Vector3[4];
        targetCanvasArea.GetWorldCorners(worldCorners);

        float minAngleX = float.MaxValue, maxAngleX = float.MinValue;
        float minAngleY = float.MaxValue, maxAngleY = float.MinValue;

        Vector3 camWorldPos = vrCamera.transform.position;

        // Calculate the maximum FOV boundaries using the same logic as the user input tracker.
        foreach (Vector3 cornerWorldPos in worldCorners)
        {
            Vector3 dirToCorner = cornerWorldPos - camWorldPos;
            Vector3 localDir = vrCamera.transform.InverseTransformDirection(dirToCorner);

            float angleX = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            float angleY = Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg;

            if (angleX < minAngleX) minAngleX = angleX;
            if (angleX > maxAngleX) maxAngleX = angleX;
            if (angleY < minAngleY) minAngleY = angleY;
            if (angleY > maxAngleY) maxAngleY = angleY;
        }

        Debug.Log($"<color=cyan>[Spawn Area FOV Boundaries (Camera Relative)]</color>\n" +
                  $"- <b>Horizontal (AngleX):</b> {minAngleX:F2}° ~ {maxAngleX:F2}°\n" +
                  $"- <b>Vertical (AngleY):</b> {minAngleY:F2}° ~ {maxAngleY:F2}°");
    }

    // A context menu command allows researchers to test this function directly from the Unity Editor.
    [ContextMenu("Verify Marker Angles (AngleX, AngleY)")]
    public void CheckMarkerAngles()
    {
        if (vrCamera == null)
        {
            Debug.LogError("[Error] VR Camera is missing. Cannot calculate angles.");
            return;
        }

        Debug.Log("<color=yellow>=== Marker FOV Verification ===</color>");

        LogSingleMarker("Top Marker", topMarker);
        LogSingleMarker("Bottom Marker", bottomMarker);
        LogSingleMarker("Left Marker", leftMarker);
        LogSingleMarker("Right Marker", rightMarker);

        Debug.Log("<color=yellow>================================</color>");
    }

    private void LogSingleMarker(string markerName, Transform marker)
    {
        if (marker == null)
        {
            Debug.LogWarning($"[Warning] {markerName} is not assigned in the Inspector.");
            return;
        }

        Vector3 markerWorldPos = marker.position;
        Vector3 camWorldPos = vrCamera.transform.position;
        Vector3 dirToMarker = markerWorldPos - camWorldPos;

        Vector3 localDir = vrCamera.transform.InverseTransformDirection(dirToMarker);

        float angleX = Mathf.Round(Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg * 100f) / 100f;
        float angleY = Mathf.Round(Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg * 100f) / 100f;

        Debug.Log($"▶ <b>{markerName}</b> - Horizontal(AngleX): {angleX}° | Vertical(AngleY): {angleY}°");
    }
}
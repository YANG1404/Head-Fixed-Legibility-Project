# Head-Fixed Legibility Project 🥽

A Unity-based VR experiment designed for Meta Quest 3 to measure and analyze Head-fixed UI text legibility in VR .

## ⚙️ Initial settings


## 🚀 Getting Started

### Prerequisites
* Unity (Recommend 2022.3 LTS or newer)
* Meta XR Core SDK (for Meta Quest 3)
* TextMeshPro

### Installation & Setup
1. Clone this repository or download the ZIP file.
2. Open the project in Unity.
3. Create an empty GameObject in your scene and attach the `Manager.cs` script.
4. Assign the required references in the Inspector:
   * **Target Canvas Area**: A UI Canvas (Render Mode: World Space).
   * **Text Prefab**: A UI Image with a TextMeshProUGUI child (**Note:** Remove any Auto-layout components like Layout Groups or Content Size Fitters).
   * **VR Camera**: The main camera tracking the headset.
   * **Markers & Reticles**: Assign appropriate UI elements for highlights and center gaze.

## 🎮 Controls (Keyboard Hotkeys)
* `1` : Start a new session round (Clears and respawns texts).
* `2` : Proceed to the next target text.
* `3` : Revert to the previous target text (Undo mistake).
* `4` : Force a manual screenshot capture.

## 📊 Data Output
Experiment logs and captures are automatically saved to your device's persistent data path:
* **Windows (Editor)**: `C:\Users\[Username]\AppData\LocalLow\[CompanyName]\[ProjectName]\Experiment_Log\`
* **Meta Quest 3**: `\Android\data\com.[CompanyName].[ProjectName]\files\Experiment_Log\`

**CSV Columns:**
`Session`, `GlobalIndex`, `LocalIndex`, `TargetChar`, `AngleX`, `AngleY`, `UserInput`, `IsCorrect`, `FontSize`, `SpawnZone`

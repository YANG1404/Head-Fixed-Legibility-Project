# Head-Fixed Legibility Project 🥽

A Unity-based VR experiment designed for Meta Quest 3 to measure and analyze Head-fixed UI text legibility in VR .

## 🚀 Getting Started

### Prerequisites
* Unity (Recommend 2022.3 LTS or newer)
  * Download Unity with Android Build Support
  * Edit -> Project Settings -> Check "Open XR" in Desktop and Android/Meta
  * Project Validation -> Fix or Fix all
  * File -> Build Profiles -> Meta Quest(or Android) -> Enable
  * Meta XR All-in-One SDK
* Meta Horizon (Mobile)
  * Create Account or Log in
  * Pairing with Meta Quest(HMD)
* Meta Horizon Link (Desktop)
  * link with Meta Quest(HMD)

### Installation & Setup
1. Clone this repository or download the ZIP file.
2. Open the project in Unity. (Check version and Universal 3D)
3. Open the Scene in [Assets/RPGPP_LT/Scene/rpgpp_lt_scene_1.0.unity]
4. Check the "Manager" object and verify that it is properly assigned.
   * **Target Canvas Area**: GridPanel
   * **FOV Verification Markers**: Top / Bottom / Left / Right
   * **Text Prefab**: TextBox
   * **Highlight Circle**: Highlight_Circle
   * **Center Reticle**: Center_Reticle
   * **VR Camera**: CenterEyeAnchor
5. If not, Assign the reference following Description

## 🎮 Controls (Keyboard Hotkeys)
* `1` : Start a new session round (Clears and respawns texts).
* `2` : Proceed to the next target text.
* `3` : Revert to the previous target text (Undo mistake).
* `4` : Force a manual screenshot capture.
* `0` : record 'NotVisible' when user can't  target letter
* `C,D,E,F,L,O,P,T,Z` : Input-enabled letter 

## 📊 Data Output
Experiment logs and captures are automatically saved to your device's persistent data path:
* **Windows (Editor)**: `C:\Users\[Username]\AppData\LocalLow\[CompanyName]\[ProjectName]\Experiment_Log\`
* **CSV Columns:**
  * `Session`: The current round number of the experiment.
  * `GlobalIndex`: The total cumulative count of targets spawned since the application started.
  * `LocalIndex`: The sequence number within the current 3x3 session (1 to 9).
  * `TargetChar`: The actual alphabet character displayed on the screen.
  * `AngleX`: The horizontal visual angle (eccentricity) from CenterEyeAnchor.
  * `AngleY`: The vertical visual angle from CenterEyeAnchor.
  * `UserInput`: The character typed by the user (`NotVisible` if `0` is pressed).
  * `IsCorrect`: Boolean (`True`/`False`) indicating if the user input matches the target.
  * `FontSize`: The font size of the text used for this specific trial.
  * `SpawnZone`: The physical grid location where the text was spawned (e.g., `Grid_Top-Left`).
 

## ⚙️ Component Description in Unity
If you want to edit and customize this project, This description can help you.

### Hierarchy
* `GameEnvirement` : Includes all map components. (unity free assets)
* `[BuildingBlock] Camera Rig` : XR camera supported by Meta SDK
  * `TrackingSpace`: Includes all components for tracking the HMD, controllers, and hands.
    * `CenterEyeAnchor` : The role of the center of both lenses
      * `Canvas`
        * `GridPanel`: Normally set the Alpha value to 0, and add the value only when verification is required.
        * `Highlight_Circle`(inactive): Points to the target latter.
        * `Center_Reticle`(inactive): Representative of the user's straight line of sight
* `Manager`: Empty object to conduct experiment. It contain `Manager.cs`
  
### Project/Asset
* `Prefabs/TextBox.prefab` : Text with back UI Image Adjustable according to text size
* `Prototype Map/Scenes/Prototype Map.unity` : Scene for teaser figure
* `RPGPP_LT/Scene/rpgpp_lt_scene_1.0.unity` : Scene for experiment
* `Scripts/Manager.cs` : Main Script to conduct an experiment 

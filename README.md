# 👾 Zolak: The Reactive Desktop Companion

Zolak is a lightweight, fully reactive, WPF-based desktop pet companion. It's more than just a sprite on your screen—it's a creature with a brain (FSM), physics, and a mood system that reacts to your presence and neglect.

---

## 🚀 How to Create Your Own Pet

Customize Zolak with your own characters! Here is the recommended "Premium Workflow" to generate and integrate assets seamlessly.

### 🎨 Phase 1: Generation & Concept
1.  **Generate Concepts**: Use **Gemini** to describe your character (e.g., *"A small, grumpy forest spirit with glowing eyes"*).
2.  **Refine with [Mixboard](https://mixboard.google.com/)**: Use Mixboard to blend styles or generate consistent variations of your character for different states.

### ✂️ Phase 2: Bulk Processing
Once you have your frames, they need to be cleaned up for the transparent engine:
1.  **Background Removal**: Upload your frames to [**bgsweep.com**](https://bgsweep.com/) for instant bulk background removal.
2.  **Uniform Cropping**: Use [**bulkimagecrop.com**](https://bulkimagecrop.com/) to ensure all frames for a specific state have identical dimensions. This prevents "jittering" during animation.

### 📂 Phase 3: Integration
Drop your processed frames into the assets folder using this structure:
```text
Assets/sprites/
└── {YourCharacterName}/
    ├── Idle/
    │   ├── 1.png
    │   └── 2.png...
    ├── Walk/
    ├── Run/
    └── Angry/
```
*The engine will automatically detect and load your new character into the system tray menu!*

---

## 🛠️ Getting Started (For Developers)

### Build and Run
```bash
# Restore dependencies
dotnet restore

# Run the app
dotnet run
```

### Create a Single-File Executable
Run the included PowerShell script to create a portable `.exe` that you can share with friends:
```powershell
./build_exe.ps1
```

---

## 🎛️ Control Panel

Zolak includes a built-in **VS Code-style Control Panel** for real-time configuration—no code editing required.

### Opening
Right-click the system tray icon → **Control Panel**, or click the character name in the status bar.

### Features
| Page | What you can do |
| :--- | :--- |
| **States** | Adjust state probabilities, min/max durations, and hover effects. Click the ✏️ pencil to edit any state's frame sequence. |
| **State Editor** | Drag-and-drop to reorder frames, add new PNGs, delete frames, and preview the animation in real-time. Hit **Save** to persist changes. |
| **Settings** | Change character, pet size, animation speed, gravity, bored threshold, and startup behavior—all with live sliders. |

### Theme
Toggle between **Dark Mode** and **Light Mode** from the sidebar. Your preference is saved to `zolak-config.json`.

### Runtime Config
All settings are stored in `Config/zolak-config.json` and loaded dynamically at startup. Changes apply instantly without rebuilding.

---

## 🧠 The Engine: How it Works

Zolak is built on a strictly decoupled architecture, separating the visual shell from the logical brain.

### 1. The "Brain" (Finite State Machine)
The `PetFSM.cs` handles all logic independently of the frame rate. It operates in three distinct modes:
*   **Mode A (Autonomous)**: Randomly switches between `Idle`, `Sit`, `Walk`, and `Run` based on weighted RNG.
*   **Mode B (Interactive)**: Triggers `Angry` or `Tease` states when the mouse interacts with the pet.
*   **Mode C (Neglect)**: After 5 minutes of inactivity, Zolak enters a `Bored` state until manually awoken.

### 2. The "Body" (WPF Shell)
*   **Transparency**: Uses `AllowsTransparency="True"` and `Background="Transparent"` for a pixel-only feel.
*   **Physics**: Implements a gravity system that kicks in after a `DragMove()` concludes, ensuring the pet falls back to the taskbar "floor".
*   **DPI Awareness**: Automatically calculates screen bounds based on the primary monitor's working area.

### 3. Asset Management & Optimization
*   **RAM Caching**: `AssetManager.cs` preloads all `BitmapImage` instances into RAM on startup to eliminate disk lag during state transitions.
*   **Frame Flipping**: To save space, we only store right-facing assets and use programmatic `ScaleTransform` flipping for left-facing movement.

---

## 📐 Project Blueprint

| File | Purpose |
| :--- | :--- |
| **`GameLoopManager.cs`** | The heartbeat. Runs at 60 ticks/s to query the FSM and update physics. |
| **`AssetManager.cs`** | Singleton preloader for character sprite dictionaries. |
| **`PetFSM.cs`** | Pure C# logic handling state transitions and RNG movement. |
| **`MainWindow.xaml`** | The visual layer and system tray integration. |
| **`PetState.cs`** | Defines the available behavior states (Walk, Run, Angry, etc.). |
| **`ControlPanelWindow.xaml`** | VS Code-style control panel with sidebar, themes, and dot canvas. |
| **`Pages/StatesPage.xaml`** | State config table and animated preview cards. |
| **`Pages/StateEditorPage.xaml`** | Frame sequence editor with drag-drop and live preview. |
| **`Pages/SettingsPage.xaml`** | Pet behavior and appearance settings with slider+input controls. |
| **`ZolakConfig.cs`** | Runtime configuration POCO model. |
| **`ConfigManager.cs`** | JSON load/save for `zolak-config.json`. |
| **`Themes/`** | Dark.xaml and Light.xaml VS Code-inspired theme dictionaries. |

---

> [!TIP]
> **Pro Tip**: Use the System Tray icon to hot-swap characters, or click the character name in the Control Panel status bar to cycle through them!

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

### 📂 Phase 3: Integration (Control Panel Method)
1.  Open the **Control Panel** → **Settings**.
2.  Click the **+ (Plus)** button next to the character dropdown.
3.  Enter your pet's name. Zolak will automatically clone **Karkoor** as a template to ensure your new pet has all the necessary state folders.
4.  Navigate to **States** and use the **State Editor** to replace the template frames with your own!

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
Run the included PowerShell script to create a portable `.exe`:
```powershell
./build_exe.ps1
```

---

## 🎛️ Control Panel

Zolak includes a built-in **VS Code-style Control Panel** for real-time configuration—no code editing required.

### Features
| Page | What you can do |
| :--- | :--- |
| **States** | Adjust state probabilities, durations, and hover effects. Click ✏️ to edit frames for the **current active character**. |
| **State Editor** | Safe drag-and-drop frame reordering. Locks to your target character so background switches won't interrupt your work. |
| **Settings** | Character management (Add/Delete), pet size, physics, and startup behavior. |
| **Factory Reset** | Revert all configurations and custom characters to original defaults. |

### Character Management
*   **Add (+)**: Clones a template structure for a new character instantly.
*   **Delete (🗑️)**: Removes custom character folders from your disk. (Original characters **Ahmad, Karkoor, and Za3tar** are protected).
*   **Status Bar**: Click the character name in the bottom-left of the Control Panel to quickly cycle through available pets.

---

## 🧠 The Engine: How it Works

Zolak is built on a strictly decoupled architecture, separating the visual shell from the logical brain.

### 1. The "Brain" (Finite State Machine)
The `PetFSM.cs` handles all logic independently of the frame rate. It operates in three distinct modes:
*   **Mode A (Autonomous)**: Randomly switches between `Idle`, `Sit`, `Walk`, and `Run` based on weighted RNG.
*   **Mode B (Interactive)**: Triggers `Hover` or `Exit` states when the mouse interacts with the pet.
*   **Mode C (Neglect)**: After a configurable threshold, Zolak enters a `Bored` state until manually awoken.

### 2. The "Body" (WPF Shell)
*   **Transparency**: Uses `AllowsTransparency="True"` and `Background="Transparent"` for a pixel-only feel.
*   **Physics**: Implements a gravity system that kicks in after a drag concludes, ensuring the pet falls to the floor.
*   **Atomic Reloads**: Assets are swapped atomically in memory, ensuring no flickers or "missing state" crashes during real-time edits.

---

## 📐 Project Blueprint

| File | Purpose |
| :--- | :--- |
| **`GameLoopManager.cs`** | The 60 fps heartbeat. Updates physics and queries the FSM. |
| **`AssetManager.cs`** | Atomic asset cache. Handles multi-character loading and memory-safe swaps. |
| **`PetFSM.cs`** | Pure C# logic handling state transitions and RNG movement. |
| **`MainWindow.xaml`** | The visual pet window and system tray integration. |
| **`ControlPanelWindow.xaml`** | The main dashboard with navigation, status bar, and themes. |
| **`Pages/`** | Contains `StatesPage`, `StateEditorPage`, and `SettingsPage`. |
| **`Config/`** | Runtime JSON configuration management. |
| **`Themes/`** | Dark and Light XAML theme dictionaries. |

---

> [!TIP]
> **Pro Tip**: If you mess up your settings or characters, use the **Factory Reset** button in Settings to return Zolak to its original state!

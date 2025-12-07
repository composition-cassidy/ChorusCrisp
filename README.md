# Chorus Crisp

A Vegas Pro script for creating punchy, layered vocal chop effects commonly used in Sparta Remixes..

## For Developers

### Building from Source

This project uses a modular C# structure and outputs as a DLL for Vegas Pro.

#### Prerequisites
- .NET Framework 4.8 SDK
- Visual Studio or VS Code with C# extensions
- Vegas Pro (for ScriptPortal.Vegas.dll reference)

#### Build Steps
1. Clone the repository
2. Open the project in VS Code or Visual Studio
3. Build the Release configuration:
   ```bash
   dotnet build ChorusCrisp.csproj --configuration Release
   ```
4. The compiled DLL will be at: `bin\Release\ChorusCrisp.dll`

#### Project Structure
- `EntryPoint.cs` - Main Vegas script entry point
- `AudioProcessor.cs` - Core audio processing logic
- `SettingsManager.cs` - Settings and presets management
- `ChorusCrispDialog.cs` - UI dialog implementation

#### Creating Releases
1. Update version in `Properties\AssemblyInfo.cs`
2. Build Release DLL: `dotnet build --configuration Release`
3. Create GitHub Release with the `ChorusCrisp.dll` from `bin\Release\`
4. Tag the release with version number

## What It Does

Chorus Crisp automates a technique where each audio clip is:
1. **Split** at a tiny offset (20-60ms from the start)
2. **Overlapped** - the second clip is extended backwards to layer with the first
3. **Crossfaded** - smooth transition between the overlapping sections
4. **Ducked** - the second clip's volume is reduced to let the attack punch through

This creates that crispy, doubled vocal effect that's signature to Sparta Remixes.

## Before & After

### Before
![Before applying Chorus Crisp](images/before.png)

### After
![After applying Chorus Crisp](images/after.png)

Notice how each clip now has an overlapping section with crossfades, and the second portion of each clip shows reduced volume (the percentages like 57.4%, 46.1%, etc.).

## Installation

### Option 1: Download Release DLL (Recommended)
1. Go to [Releases](https://github.com/adiglows/ChorusCrisp/releases) and download the latest `ChorusCrisp.dll`
2. Place it in your Vegas Script Menu folder:
   ```
   C:\Users\[YourName]\Documents\Vegas Script Menu\
   ```
   Or create a subfolder:
   ```
   C:\Users\[YourName]\Documents\Vegas Script Menu\ChorusCrisp\ChorusCrisp.dll
   ```
3. Restart Vegas Pro or go to **Tools → Scripting → Rescan Script Menu Folder**

### Option 2: Build from Source
See the "For Developers" section above for building instructions.

## Usage

1. **Select** the audio clips you want to process on the timeline
2. Go to **Tools → Scripting → ChorusCrisp**
3. Choose a **Preset** or adjust the parameters manually:
   - **Splice Position**: Where to cut each clip (20ms - 60ms)
   - **Volume Duck**: How much to reduce the second clip's volume (0 to -15 dB)
   - **Offset**: How much the second clip overlaps with the first (0% - 100%)
   - **Crossfade Curve**: The easing type for the crossfade
4. Click **Apply Crisp**

## Presets

| Preset | Splice Position | Volume Duck | Offset | Crossfade Curve |
|--------|-----------------|-------------|--------|-----------------|
| **Jario Style** | 50% | -3.0 dB | 80% | Smooth |
| **Standard** | 40% | -3.0 dB | 90% | Linear |
| **Snappy** | 35% | -5.0 dB | 95% | Fast |

Select a preset from the dropdown to instantly apply these settings. If you adjust the sliders manually, the preset will automatically switch to "Custom".

## Parameters

| Parameter | Range | Description |
|-----------|-------|-------------|
| Splice Position | 0% - 100% (20ms - 60ms) | How far into each clip to make the cut. Lower = tighter attack. |
| Volume Duck | 0 dB to -15 dB | How much quieter the second clip should be. More negative = more ducking. |
| Offset | 0% - 100% | How much the second clip overlaps. Higher = more overlap and layering. |
| Crossfade Curve | Linear, Fast, Slow, Sharp, Smooth | The shape of the crossfade between clips. |

## Saved Settings

Your settings are automatically saved when you click **Apply Crisp** and restored the next time you open the script. Settings persist even after closing Vegas Pro.

Settings are stored in:
```
%AppData%\ChorusCrisp\settings.txt
```

## How It Works

For each selected audio event, the script:

1. Calls `Split()` at the splice offset to create two events
2. Adjusts the second event's `Take.Offset` and `Start` position to extend it backwards
3. Sets `FadeIn` on the second event and `FadeOut` on the first event
4. Applies volume reduction via `AudioEvent.NormalizeGain` on the second clip only

All operations are wrapped in an `UndoBlock` so you can Ctrl+Z the entire batch.

## License

Free to use and modify. Credit appreciated but not required..

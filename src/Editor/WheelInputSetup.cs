// ============================================================================
// Nightflow - Wheel Input Setup
// Editor tool for configuring Unity Input Manager for steering wheel support
// ============================================================================

using UnityEngine;
using UnityEditor;
using System.IO;
using Nightflow.Utilities;

namespace Nightflow.Editor
{
    /// <summary>
    /// Editor utility for setting up steering wheel input axes in Unity.
    /// Configures Input Manager for Logitech G920/G29 wheels.
    /// Access via menu: Nightflow > Input > Setup Wheel Axes
    /// </summary>
    public static class WheelInputSetup
    {
        private const string PluginsPath = "Assets/Plugins";
        private const string LogitechDllName = "LogitechSteeringWheelEnginesWrapper.dll";

        [MenuItem("Nightflow/Input/Setup Wheel Axes", false, 200)]
        public static void SetupWheelAxes()
        {
            if (EditorUtility.DisplayDialog("Setup Wheel Input Axes",
                "This will add the following Input Manager axes for steering wheel support:\n\n" +
                "- Wheel Steering (Joy1 Axis 1)\n" +
                "- Wheel Throttle (Joy1 Axis 4)\n" +
                "- Wheel Brake (Joy1 Axis 5)\n" +
                "- Wheel Clutch (Joy1 Axis 3)\n" +
                "- Joy1Axis1 through Joy1Axis10\n\n" +
                "Continue?", "Setup", "Cancel"))
            {
                AddWheelAxesToInputManager();
                EditorUtility.DisplayDialog("Setup Complete",
                    "Wheel input axes have been configured.\n\n" +
                    "If you have a Logitech G920/G29, it should now work with the game.\n\n" +
                    "For force feedback, ensure LogitechSteeringWheelEnginesWrapper.dll is in the Plugins folder.",
                    "OK");
            }
        }

        [MenuItem("Nightflow/Input/Check Logitech SDK", false, 201)]
        public static void CheckLogitechSDK()
        {
            bool found = false;
            string foundPath = "";

            // Check common locations
            string[] searchPaths = {
                Path.Combine(PluginsPath, LogitechDllName),
                Path.Combine(PluginsPath, "x86_64", LogitechDllName),
                Path.Combine(PluginsPath, "x64", LogitechDllName),
                Path.Combine("Assets", LogitechDllName)
            };

            foreach (string path in searchPaths)
            {
                if (File.Exists(path))
                {
                    found = true;
                    foundPath = path;
                    break;
                }
            }

            if (found)
            {
                EditorUtility.DisplayDialog("Logitech SDK Found",
                    $"LogitechSteeringWheelEnginesWrapper.dll found at:\n{foundPath}\n\n" +
                    "Force feedback should work correctly.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Logitech SDK Not Found",
                    "LogitechSteeringWheelEnginesWrapper.dll was not found.\n\n" +
                    "To enable force feedback:\n" +
                    "1. Download Logitech Gaming SDK\n" +
                    "2. Copy LogitechSteeringWheelEnginesWrapper.dll to Assets/Plugins\n\n" +
                    "The wheel will still work for input without the SDK, but force feedback will be disabled.",
                    "OK");
            }
        }

        [MenuItem("Nightflow/Input/Create Wheel Input Documentation", false, 202)]
        public static void CreateWheelDocumentation()
        {
            string docPath = "Assets/Documentation";
            if (!AssetDatabase.IsValidFolder(docPath))
            {
                AssetDatabase.CreateFolder("Assets", "Documentation");
            }

            string content = GetWheelDocumentation();
            string filePath = Path.Combine(docPath, "WheelInputSetup.md");

            File.WriteAllText(filePath, content);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Documentation Created",
                $"Wheel input documentation created at:\n{filePath}",
                "OK");

            // Open the file
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(filePath));
        }

        private static void AddWheelAxesToInputManager()
        {
            // Get the InputManager.asset
            SerializedObject inputManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);

            SerializedProperty axesProperty = inputManager.FindProperty("m_Axes");

            // Add named wheel axes
            AddAxisIfNotExists(axesProperty, "Wheel Steering", 1, 1, 0.1f, true);
            AddAxisIfNotExists(axesProperty, "Wheel Throttle", 4, 1, 0.1f, false);
            AddAxisIfNotExists(axesProperty, "Wheel Brake", 5, 1, 0.1f, false);
            AddAxisIfNotExists(axesProperty, "Wheel Clutch", 3, 1, 0.1f, false);

            // Add generic joystick axes for fallback (Joy1Axis1-10)
            for (int axis = 1; axis <= 10; axis++)
            {
                string axisName = $"Joy1Axis{axis}";
                AddAxisIfNotExists(axesProperty, axisName, axis, 1, 0f, axis == 1);
            }

            // Add axes for second joystick slot (in case wheel is detected as Joy2)
            for (int axis = 1; axis <= 5; axis++)
            {
                string axisName = $"Joy2Axis{axis}";
                AddAxisIfNotExists(axesProperty, axisName, axis, 2, 0f, axis == 1);
            }

            inputManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Log.System("WheelInputSetup", "Wheel input axes configured successfully!");
        }

        private static void AddAxisIfNotExists(SerializedProperty axesProperty, string axisName,
            int axisNumber, int joystickNumber, float deadZone, bool isSteering)
        {
            // Check if axis already exists
            for (int i = 0; i < axesProperty.arraySize; i++)
            {
                SerializedProperty axis = axesProperty.GetArrayElementAtIndex(i);
                SerializedProperty name = axis.FindPropertyRelative("m_Name");
                if (name.stringValue == axisName)
                {
                    Log.System("WheelInputSetup", $"Axis '{axisName}' already exists, skipping");
                    return;
                }
            }

            // Add new axis
            axesProperty.arraySize++;
            SerializedProperty newAxis = axesProperty.GetArrayElementAtIndex(axesProperty.arraySize - 1);

            newAxis.FindPropertyRelative("m_Name").stringValue = axisName;
            newAxis.FindPropertyRelative("descriptiveName").stringValue = "";
            newAxis.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
            newAxis.FindPropertyRelative("negativeButton").stringValue = "";
            newAxis.FindPropertyRelative("positiveButton").stringValue = "";
            newAxis.FindPropertyRelative("altNegativeButton").stringValue = "";
            newAxis.FindPropertyRelative("altPositiveButton").stringValue = "";
            newAxis.FindPropertyRelative("gravity").floatValue = 0f;
            newAxis.FindPropertyRelative("dead").floatValue = deadZone;
            newAxis.FindPropertyRelative("sensitivity").floatValue = 1f;
            newAxis.FindPropertyRelative("snap").boolValue = false;
            newAxis.FindPropertyRelative("invert").boolValue = false;
            newAxis.FindPropertyRelative("type").intValue = 2; // Joystick Axis
            newAxis.FindPropertyRelative("axis").intValue = axisNumber - 1; // 0-indexed
            newAxis.FindPropertyRelative("joyNum").intValue = joystickNumber;

            Log.System("WheelInputSetup", $"Added axis: {axisName}");
        }

        private static string GetWheelDocumentation()
        {
            return @"# Steering Wheel Setup Guide

## Supported Wheels
- Logitech G920 (Xbox/PC)
- Logitech G29 (PlayStation/PC)
- Other DirectInput compatible wheels (basic support)

## Quick Setup
1. In Unity, go to **Nightflow > Input > Setup Wheel Axes**
2. Click **Setup** to configure Input Manager

## Force Feedback Setup
To enable force feedback effects:

1. Download the Logitech Gaming SDK from:
   https://www.logitechg.com/en-us/innovation/developer-lab.html

2. Locate `LogitechSteeringWheelEnginesWrapper.dll` from the SDK

3. Copy the DLL to `Assets/Plugins/` in your Unity project

4. The game will automatically detect and use the SDK

## Input Mapping

### G920/G29 Axis Layout
| Axis | Function |
|------|----------|
| Axis 1 (X) | Steering Wheel (-1 to 1) |
| Axis 3 | Clutch Pedal (1 to -1, needs normalization) |
| Axis 4 | Throttle Pedal (1 to -1, needs normalization) |
| Axis 5 | Brake Pedal (1 to -1, needs normalization) |

### Button Layout
| Button | G920 Function |
|--------|---------------|
| 0 | A Button |
| 1 | B Button |
| 2 | X Button |
| 3 | Y Button |
| 4 | Left Bumper / Left Paddle |
| 5 | Right Bumper / Right Paddle |
| 6 | Back/View |
| 7 | Start/Menu |
| 12-17 | H-Shifter Gears 1-6 (with shifter addon) |
| 18 | H-Shifter Reverse |

## Troubleshooting

### Wheel not detected
1. Ensure wheel is in the correct mode (Xbox mode for G920)
2. Check Device Manager to verify drivers are installed
3. Try unplugging and reconnecting the wheel
4. Restart Unity Editor

### Force feedback not working
1. Verify `LogitechSteeringWheelEnginesWrapper.dll` is in Plugins folder
2. Check Nightflow > Input > Check Logitech SDK
3. Ensure FFB is enabled in game settings
4. Test in Logitech Gaming Software first

### Pedals inverted or not responding
The G920/G29 pedals report values from 1 (not pressed) to -1 (fully pressed).
The `WheelInputManager` normalizes these automatically.

If issues persist, check the raw axis values in Unity's Input Debugger.

## Code Integration

The wheel input integrates with the existing input system:

```csharp
// Check if wheel is connected
if (WheelInputManager.Instance?.IsWheelConnected == true)
{
    float steering = WheelInputManager.Instance.Steering;
    float throttle = WheelInputManager.Instance.Throttle;
    float brake = WheelInputManager.Instance.Brake;
}

// Force feedback
WheelInputManager.Instance?.PlayFrontalCollision(50);
WheelInputManager.Instance?.PlaySurfaceEffect(0, 30, 50);
```

## Settings

Wheel settings are saved in the player's settings file:
- Force Feedback Enabled/Disabled
- Force Feedback Strength (0-100%)
- Collision Intensity
- Road Surface Intensity
- Speed Resistance Intensity
- Steering Deadzone
- Pedal Deadzone
- Wheel Rotation Degrees (for soft-lock)
";
        }
    }
}

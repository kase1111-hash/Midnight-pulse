LOGITECH STEERING WHEEL SDK SETUP
==================================

For force feedback support with Logitech G920/G29 wheels, you need to add the
Logitech Gaming SDK to your Unity project.

STEPS:
------

1. Download the Logitech Gaming SDK from:
   https://www.logitechg.com/en-us/innovation/developer-lab.html

2. Extract the SDK and locate:
   LogitechSteeringWheelEnginesWrapper.dll

3. In your Unity project, create folder:
   Assets/Plugins/

4. Copy the DLL:
   - For 64-bit: Assets/Plugins/x86_64/LogitechSteeringWheelEnginesWrapper.dll
   - For 32-bit: Assets/Plugins/x86/LogitechSteeringWheelEnginesWrapper.dll
   - Or both for multi-platform support

5. In Unity, select the DLL(s) and configure platform settings:
   - x86_64 DLL: Enable only for "Windows x64"
   - x86 DLL: Enable only for "Windows x86"

WITHOUT THE SDK:
----------------
The wheel will still work for input (steering, pedals, buttons), but force
feedback effects (road feel, collisions, etc.) will be disabled.

VERIFICATION:
-------------
In Unity Editor, go to:
Nightflow > Input > Check Logitech SDK

This will verify if the SDK is properly installed.

AXIS CONFIGURATION:
-------------------
Run this first if you haven't already:
Nightflow > Input > Setup Wheel Axes

This configures the Unity Input Manager with the correct axis mappings for
Logitech wheels.

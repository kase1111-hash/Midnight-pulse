// ============================================================================
// Nightflow - Input Binding Manager
// Handles input rebinding, mode detection, and provides input state queries
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Nightflow.Save;

namespace Nightflow.Input
{
    /// <summary>
    /// Manages input bindings, mode detection, and rebinding workflow.
    /// Singleton MonoBehaviour that bridges SaveManager bindings to runtime input.
    /// </summary>
    public class InputBindingManager : MonoBehaviour
    {
        public static InputBindingManager Instance { get; private set; }

        // Events
        public event Action<InputAction, KeyCode> OnKeyRebound;
        public event Action<InputMode> OnInputModeChanged;
        public event Action OnRebindCancelled;
        public event Action<InputAction> OnRebindStarted;

        // Current state
        private InputMode currentInputMode = InputMode.Auto;
        private InputMode detectedInputMode = InputMode.Keyboard;
        private InputBindingPreset keyboardBindings;
        private InputBindingPreset gamepadBindings;
        private ControlSettings controlSettings;

        // Rebinding state
        private bool isRebinding;
        private InputAction rebindingAction;
        private bool rebindingPrimary;
        private float rebindTimeout = 10f;
        private float rebindTimer;

        // Input detection
        private float lastKeyboardInputTime;
        private float lastGamepadInputTime;
        private const float InputModeSwapDelay = 0.5f;

        // Cache for input state
        private Dictionary<InputAction, bool> actionStates = new Dictionary<InputAction, bool>();
        private Dictionary<InputAction, float> axisStates = new Dictionary<InputAction, float>();

        // Keys that cannot be rebound
        private static readonly HashSet<KeyCode> ForbiddenKeys = new HashSet<KeyCode>
        {
            KeyCode.None,
            KeyCode.Escape, // Always pause/back
            KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4,
            KeyCode.F5, KeyCode.F6, KeyCode.F7, KeyCode.F8,
            KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12,
            KeyCode.Print, KeyCode.SysReq,
            KeyCode.LeftWindows, KeyCode.RightWindows,
            KeyCode.LeftCommand, KeyCode.RightCommand
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeBindings();
        }

        private void InitializeBindings()
        {
            // Load bindings from SaveManager if available
            if (SaveManager.Instance != null)
            {
                controlSettings = SaveManager.Instance.GetSettings().Controls;
                keyboardBindings = controlSettings.KeyboardBindings ?? InputBindingPreset.CreateDefaultKeyboard();
                gamepadBindings = controlSettings.GamepadBindings ?? InputBindingPreset.CreateDefaultGamepad();
                currentInputMode = controlSettings.PreferredInputMode;
            }
            else
            {
                // Use defaults if SaveManager not ready
                keyboardBindings = InputBindingPreset.CreateDefaultKeyboard();
                gamepadBindings = InputBindingPreset.CreateDefaultGamepad();
                controlSettings = new ControlSettings();
            }
        }

        private void Update()
        {
            if (isRebinding)
            {
                UpdateRebinding();
            }
            else
            {
                DetectInputMode();
                UpdateInputStates();
            }
        }

        #region Input Mode Detection

        private float lastWheelInputTime;

        private void DetectInputMode()
        {
            if (currentInputMode != InputMode.Auto)
            {
                detectedInputMode = currentInputMode;
                return;
            }

            // Check for wheel input first (highest priority when connected)
            if (HasWheelInput())
            {
                lastWheelInputTime = Time.unscaledTime;
            }

            // Check for keyboard input
            if (UnityEngine.Input.anyKeyDown && !IsGamepadInput() && !IsWheelInput())
            {
                lastKeyboardInputTime = Time.unscaledTime;
            }

            // Check for gamepad input
            if (HasGamepadInput() && !IsWheelInput())
            {
                lastGamepadInputTime = Time.unscaledTime;
            }

            // Switch mode based on most recent input
            InputMode newMode = detectedInputMode;

            // Wheel takes priority when connected and active
            if (lastWheelInputTime > lastKeyboardInputTime + InputModeSwapDelay &&
                lastWheelInputTime > lastGamepadInputTime + InputModeSwapDelay)
            {
                newMode = InputMode.Wheel;
            }
            else if (lastGamepadInputTime > lastKeyboardInputTime + InputModeSwapDelay)
            {
                newMode = InputMode.Gamepad;
            }
            else if (lastKeyboardInputTime > lastGamepadInputTime + InputModeSwapDelay)
            {
                newMode = InputMode.Keyboard;
            }

            if (newMode != detectedInputMode)
            {
                detectedInputMode = newMode;
                OnInputModeChanged?.Invoke(detectedInputMode);
            }
        }

        private bool IsWheelInput()
        {
            var wheelManager = WheelInputManager.Instance;
            return wheelManager != null && wheelManager.IsWheelConnected;
        }

        private bool HasWheelInput()
        {
            var wheelManager = WheelInputManager.Instance;
            if (wheelManager == null || !wheelManager.IsWheelConnected)
                return false;

            // Check for significant wheel input
            return Mathf.Abs(wheelManager.Steering) > 0.1f ||
                   wheelManager.Throttle > 0.1f ||
                   wheelManager.Brake > 0.1f;
        }

        private bool IsGamepadInput()
        {
            // Check common gamepad buttons
            for (int i = 0; i <= 19; i++)
            {
                if (UnityEngine.Input.GetKey((KeyCode)((int)KeyCode.JoystickButton0 + i)))
                    return true;
            }
            return false;
        }

        private bool HasGamepadInput()
        {
            // Check axes
            if (Mathf.Abs(UnityEngine.Input.GetAxis("Horizontal")) > 0.2f ||
                Mathf.Abs(UnityEngine.Input.GetAxis("Vertical")) > 0.2f)
            {
                return true;
            }

            // Check buttons
            return IsGamepadInput();
        }

        #endregion

        #region Input Queries

        /// <summary>
        /// Check if an action is currently pressed.
        /// </summary>
        public bool IsActionPressed(InputAction action)
        {
            var binding = GetActiveBinding(action);
            if (binding == null) return false;

            // Check keyboard
            if (detectedInputMode != InputMode.Gamepad)
            {
                var primary = binding.GetPrimaryKeyCode();
                var alternate = binding.GetAlternateKeyCode();

                if (primary != KeyCode.None && UnityEngine.Input.GetKey(primary))
                    return true;
                if (alternate != KeyCode.None && UnityEngine.Input.GetKey(alternate))
                    return true;
            }

            // Check gamepad
            if (detectedInputMode != InputMode.Keyboard)
            {
                if (!string.IsNullOrEmpty(binding.GamepadButton))
                {
                    try
                    {
                        if (UnityEngine.Input.GetButton(binding.GamepadButton))
                            return true;
                    }
                    catch { }
                }
            }

            return false;
        }

        /// <summary>
        /// Check if an action was just pressed this frame.
        /// </summary>
        public bool IsActionDown(InputAction action)
        {
            var binding = GetActiveBinding(action);
            if (binding == null) return false;

            // Check keyboard
            if (detectedInputMode != InputMode.Gamepad)
            {
                var primary = binding.GetPrimaryKeyCode();
                var alternate = binding.GetAlternateKeyCode();

                if (primary != KeyCode.None && UnityEngine.Input.GetKeyDown(primary))
                    return true;
                if (alternate != KeyCode.None && UnityEngine.Input.GetKeyDown(alternate))
                    return true;
            }

            // Check gamepad
            if (detectedInputMode != InputMode.Keyboard)
            {
                if (!string.IsNullOrEmpty(binding.GamepadButton))
                {
                    try
                    {
                        if (UnityEngine.Input.GetButtonDown(binding.GamepadButton))
                            return true;
                    }
                    catch { }
                }
            }

            return false;
        }

        /// <summary>
        /// Get axis value for an action (0 or 1 for digital, -1 to 1 for analog).
        /// </summary>
        public float GetActionAxis(InputAction action)
        {
            var binding = GetActiveBinding(action);
            if (binding == null) return 0f;

            float value = 0f;

            // Check keyboard (digital)
            if (detectedInputMode != InputMode.Gamepad)
            {
                var primary = binding.GetPrimaryKeyCode();
                var alternate = binding.GetAlternateKeyCode();

                if ((primary != KeyCode.None && UnityEngine.Input.GetKey(primary)) ||
                    (alternate != KeyCode.None && UnityEngine.Input.GetKey(alternate)))
                {
                    value = 1f;
                }
            }

            // Check gamepad axis (analog)
            if (detectedInputMode != InputMode.Keyboard && !string.IsNullOrEmpty(binding.GamepadAxis))
            {
                try
                {
                    float axisValue = UnityEngine.Input.GetAxis(binding.GamepadAxis);
                    if (binding.GamepadAxisPositive)
                    {
                        value = Mathf.Max(value, Mathf.Max(0f, axisValue));
                    }
                    else
                    {
                        value = Mathf.Max(value, Mathf.Max(0f, -axisValue));
                    }
                }
                catch { }
            }

            return value;
        }

        /// <summary>
        /// Get steering axis (-1 to 1) from SteerLeft and SteerRight actions.
        /// </summary>
        public float GetSteerAxis()
        {
            // Use wheel steering when in wheel mode
            if (detectedInputMode == InputMode.Wheel)
            {
                var wheelManager = WheelInputManager.Instance;
                if (wheelManager != null && wheelManager.IsWheelConnected)
                {
                    return wheelManager.Steering;
                }
            }

            float left = GetActionAxis(InputAction.SteerLeft);
            float right = GetActionAxis(InputAction.SteerRight);

            // Also check raw horizontal axis for analog input
            float analogSteer = 0f;
            if (detectedInputMode != InputMode.Keyboard)
            {
                try
                {
                    analogSteer = UnityEngine.Input.GetAxis("Horizontal");
                }
                catch { }
            }

            float digital = right - left;
            return Mathf.Abs(analogSteer) > Mathf.Abs(digital) ? analogSteer : digital;
        }

        /// <summary>
        /// Get throttle axis from wheel or standard input.
        /// </summary>
        public float GetWheelThrottle()
        {
            if (detectedInputMode == InputMode.Wheel)
            {
                var wheelManager = WheelInputManager.Instance;
                if (wheelManager != null && wheelManager.IsWheelConnected)
                {
                    return wheelManager.Throttle;
                }
            }
            return GetActionAxis(InputAction.Accelerate);
        }

        /// <summary>
        /// Get brake axis from wheel or standard input.
        /// </summary>
        public float GetWheelBrake()
        {
            if (detectedInputMode == InputMode.Wheel)
            {
                var wheelManager = WheelInputManager.Instance;
                if (wheelManager != null && wheelManager.IsWheelConnected)
                {
                    return wheelManager.Brake;
                }
            }
            return GetActionAxis(InputAction.Brake);
        }

        /// <summary>
        /// Get clutch axis from wheel (0 if not using wheel).
        /// </summary>
        public float GetWheelClutch()
        {
            if (detectedInputMode == InputMode.Wheel)
            {
                var wheelManager = WheelInputManager.Instance;
                if (wheelManager != null && wheelManager.IsWheelConnected)
                {
                    return wheelManager.Clutch;
                }
            }
            return 0f;
        }

        private InputBinding GetActiveBinding(InputAction action)
        {
            var preset = detectedInputMode == InputMode.Gamepad ? gamepadBindings : keyboardBindings;
            return preset?.GetBinding(action);
        }

        #endregion

        #region Input State Cache

        private void UpdateInputStates()
        {
            foreach (InputAction action in Enum.GetValues(typeof(InputAction)))
            {
                actionStates[action] = IsActionPressed(action);
                axisStates[action] = GetActionAxis(action);
            }
        }

        #endregion

        #region Rebinding

        /// <summary>
        /// Start rebinding an action. Returns false if already rebinding.
        /// </summary>
        public bool StartRebinding(InputAction action, bool primary = true)
        {
            if (isRebinding) return false;

            isRebinding = true;
            rebindingAction = action;
            rebindingPrimary = primary;
            rebindTimer = rebindTimeout;

            OnRebindStarted?.Invoke(action);
            return true;
        }

        /// <summary>
        /// Cancel current rebinding operation.
        /// </summary>
        public void CancelRebinding()
        {
            if (!isRebinding) return;

            isRebinding = false;
            OnRebindCancelled?.Invoke();
        }

        private void UpdateRebinding()
        {
            rebindTimer -= Time.unscaledDeltaTime;
            if (rebindTimer <= 0f)
            {
                CancelRebinding();
                return;
            }

            // Check for cancel (Escape)
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                CancelRebinding();
                return;
            }

            // Check for any key press
            if (UnityEngine.Input.anyKeyDown)
            {
                KeyCode pressedKey = DetectPressedKey();
                if (pressedKey != KeyCode.None && !ForbiddenKeys.Contains(pressedKey))
                {
                    ApplyRebinding(pressedKey);
                    return;
                }
            }
        }

        private KeyCode DetectPressedKey()
        {
            // Check all key codes
            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                // Skip mouse buttons and joystick buttons for keyboard rebinding
                if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6)
                    continue;
                if (key >= KeyCode.JoystickButton0)
                    continue;

                if (UnityEngine.Input.GetKeyDown(key))
                    return key;
            }
            return KeyCode.None;
        }

        private void ApplyRebinding(KeyCode newKey)
        {
            // Check for conflicts
            var conflict = FindConflict(newKey, rebindingAction);
            if (conflict.HasValue)
            {
                // Clear the conflicting binding
                ClearBinding(conflict.Value, newKey);
            }

            // Apply the new binding
            var binding = keyboardBindings.GetBinding(rebindingAction);
            if (binding == null)
            {
                binding = new InputBinding { Action = rebindingAction };
                keyboardBindings.SetBinding(binding);
            }

            if (rebindingPrimary)
            {
                binding.SetPrimaryKey(newKey);
            }
            else
            {
                binding.SetAlternateKey(newKey);
            }

            // Save to settings
            SaveBindings();

            isRebinding = false;
            OnKeyRebound?.Invoke(rebindingAction, newKey);
        }

        private InputAction? FindConflict(KeyCode key, InputAction excludeAction)
        {
            foreach (var binding in keyboardBindings.Bindings)
            {
                if (binding.Action == excludeAction)
                    continue;

                if (binding.IsKeyBound(key))
                    return binding.Action;
            }
            return null;
        }

        private void ClearBinding(InputAction action, KeyCode key)
        {
            var binding = keyboardBindings.GetBinding(action);
            if (binding == null) return;

            if (binding.GetPrimaryKeyCode() == key)
                binding.SetPrimaryKey(KeyCode.None);
            if (binding.GetAlternateKeyCode() == key)
                binding.SetAlternateKey(KeyCode.None);
        }

        /// <summary>
        /// Reset all bindings to default.
        /// </summary>
        public void ResetToDefaults()
        {
            keyboardBindings = InputBindingPreset.CreateDefaultKeyboard();
            gamepadBindings = InputBindingPreset.CreateDefaultGamepad();
            SaveBindings();
        }

        #endregion

        #region Persistence

        private void SaveBindings()
        {
            if (SaveManager.Instance == null) return;

            controlSettings.KeyboardBindings = keyboardBindings;
            controlSettings.GamepadBindings = gamepadBindings;
            SaveManager.Instance.MarkDirty();
        }

        /// <summary>
        /// Reload bindings from SaveManager.
        /// </summary>
        public void ReloadBindings()
        {
            InitializeBindings();
        }

        #endregion

        #region Public Accessors

        public InputMode CurrentInputMode => currentInputMode;
        public InputMode DetectedInputMode => detectedInputMode;
        public bool IsRebinding => isRebinding;
        public InputAction RebindingAction => rebindingAction;
        public float RebindTimeRemaining => rebindTimer;

        public void SetInputMode(InputMode mode)
        {
            currentInputMode = mode;
            controlSettings.PreferredInputMode = mode;
            SaveBindings();
            OnInputModeChanged?.Invoke(detectedInputMode);
        }

        /// <summary>
        /// Get display name for a key binding.
        /// </summary>
        public string GetBindingDisplayName(InputAction action, bool primary = true)
        {
            var binding = keyboardBindings.GetBinding(action);
            if (binding == null) return "---";

            var key = primary ? binding.GetPrimaryKeyCode() : binding.GetAlternateKeyCode();
            if (key == KeyCode.None) return "---";

            return GetKeyDisplayName(key);
        }

        /// <summary>
        /// Get human-readable name for a key.
        /// </summary>
        public static string GetKeyDisplayName(KeyCode key)
        {
            return key switch
            {
                KeyCode.UpArrow => "↑",
                KeyCode.DownArrow => "↓",
                KeyCode.LeftArrow => "←",
                KeyCode.RightArrow => "→",
                KeyCode.Space => "SPACE",
                KeyCode.Return => "ENTER",
                KeyCode.Escape => "ESC",
                KeyCode.Backspace => "BACK",
                KeyCode.Tab => "TAB",
                KeyCode.LeftShift => "L-SHIFT",
                KeyCode.RightShift => "R-SHIFT",
                KeyCode.LeftControl => "L-CTRL",
                KeyCode.RightControl => "R-CTRL",
                KeyCode.LeftAlt => "L-ALT",
                KeyCode.RightAlt => "R-ALT",
                _ => key.ToString().ToUpper()
            };
        }

        /// <summary>
        /// Get display name for an input action.
        /// </summary>
        public static string GetActionDisplayName(InputAction action)
        {
            return action switch
            {
                InputAction.Accelerate => "ACCELERATE",
                InputAction.Brake => "BRAKE",
                InputAction.SteerLeft => "STEER LEFT",
                InputAction.SteerRight => "STEER RIGHT",
                InputAction.Handbrake => "HANDBRAKE",
                InputAction.LookBack => "LOOK BACK",
                InputAction.CameraToggle => "CAMERA",
                InputAction.Pause => "PAUSE",
                InputAction.Confirm => "CONFIRM",
                InputAction.Cancel => "CANCEL/BACK",
                InputAction.MenuUp => "MENU UP",
                InputAction.MenuDown => "MENU DOWN",
                InputAction.MenuLeft => "MENU LEFT",
                InputAction.MenuRight => "MENU RIGHT",
                _ => action.ToString().ToUpper()
            };
        }

        #endregion
    }
}

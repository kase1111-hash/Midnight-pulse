// ============================================================================
// Nightflow - Wheel Input Manager
// Logitech G920 steering wheel support with force feedback
// ============================================================================

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Nightflow.Utilities;

namespace Nightflow.Input
{
    /// <summary>
    /// Manages Logitech G920 steering wheel input and force feedback.
    /// Detects wheel connection, reads analog inputs, and provides FFB interface.
    /// </summary>
    public class WheelInputManager : MonoBehaviour
    {
        public static WheelInputManager Instance { get; private set; }

        // Events
        public event Action OnWheelConnected;
        public event Action OnWheelDisconnected;

        // G920 Identification
        private const int LOGITECH_VENDOR_ID = 0x046D;
        private const int G920_PRODUCT_ID = 0xC262;
        private const int G29_PRODUCT_ID = 0xC24F;

        // Input state
        private bool isWheelConnected;
        private float steeringPosition;      // -1 to 1, 900° rotation mapped
        private float throttlePedal;         // 0 to 1
        private float brakePedal;            // 0 to 1
        private float clutchPedal;           // 0 to 1
        private int currentGear;             // -1=R, 0=N, 1-6=gears
        private WheelButtonState buttonState;

        // Configuration
        [Header("Wheel Settings")]
        [SerializeField] private float steeringDeadzone = 0.02f;
        [SerializeField] private float pedalDeadzone = 0.05f;
        [SerializeField] private float steeringLinearity = 1.0f;
        [SerializeField] private bool invertSteering = false;
        [SerializeField] private float wheelRotationDegrees = 900f;

        [Header("Force Feedback")]
        [SerializeField] private bool forceFeedbackEnabled = true;
        [SerializeField] [Range(0f, 1f)] private float forceFeedbackStrength = 0.7f;

        // Detection
        private float detectionTimer;
        private const float DETECTION_INTERVAL = 2f;
        private int wheelJoystickIndex = -1;

        // Unity Input axes (configured in Input Manager)
        private const string AXIS_STEERING = "Wheel Steering";
        private const string AXIS_THROTTLE = "Wheel Throttle";
        private const string AXIS_BRAKE = "Wheel Brake";
        private const string AXIS_CLUTCH = "Wheel Clutch";

        // Fallback axes (standard joystick)
        private const string FALLBACK_STEERING = "Horizontal";
        private const string FALLBACK_THROTTLE = "Joy1Axis10";  // Right trigger style
        private const string FALLBACK_BRAKE = "Joy1Axis9";      // Left trigger style

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeForceFeedback();
        }

        private void Start()
        {
            DetectWheel();
        }

        private void Update()
        {
            // Periodic wheel detection
            detectionTimer += Time.unscaledDeltaTime;
            if (detectionTimer >= DETECTION_INTERVAL)
            {
                detectionTimer = 0f;
                DetectWheel();
            }

            if (isWheelConnected)
            {
                ReadWheelInput();
            }
        }

        private void OnDestroy()
        {
            StopAllForceFeedback();
            ShutdownForceFeedback();
        }

        #region Wheel Detection

        private void DetectWheel()
        {
            string[] joystickNames = UnityEngine.Input.GetJoystickNames();
            bool wasConnected = isWheelConnected;
            isWheelConnected = false;
            wheelJoystickIndex = -1;

            for (int i = 0; i < joystickNames.Length; i++)
            {
                string name = joystickNames[i];
                if (string.IsNullOrEmpty(name)) continue;

                // Check for Logitech G920/G29
                string nameLower = name.ToLower();
                if (nameLower.Contains("g920") || nameLower.Contains("g29") ||
                    nameLower.Contains("logitech") && nameLower.Contains("wheel"))
                {
                    isWheelConnected = true;
                    wheelJoystickIndex = i;
                    break;
                }

                // Also check for generic racing wheel
                if (nameLower.Contains("racing") || nameLower.Contains("wheel") ||
                    nameLower.Contains("driving force"))
                {
                    isWheelConnected = true;
                    wheelJoystickIndex = i;
                    break;
                }
            }

            // Fire events on state change
            if (isWheelConnected && !wasConnected)
            {
                Log.System("WheelInputManager", $"Logitech wheel connected (Joystick {wheelJoystickIndex})");
                OnWheelConnected?.Invoke();
                InitializeWheelAxes();
            }
            else if (!isWheelConnected && wasConnected)
            {
                Log.System("WheelInputManager", "Wheel disconnected");
                OnWheelDisconnected?.Invoke();
                StopAllForceFeedback();
            }
        }

        private void InitializeWheelAxes()
        {
            // The G920 typically maps as:
            // Axis 1 (X): Steering wheel (-1 to 1)
            // Axis 2 (Y): Combined pedals or throttle
            // Axis 3: Clutch
            // Axis 4: Throttle (separate)
            // Axis 5: Brake (separate)
            // Note: Exact mapping depends on driver configuration
        }

        #endregion

        #region Input Reading

        private void ReadWheelInput()
        {
            // Read steering (Axis 1 / X axis on most wheels)
            float rawSteering = GetWheelAxis(1);
            steeringPosition = ProcessSteering(rawSteering);

            // Read pedals - G920 uses separate axes
            // Throttle is typically axis 4 or axis 10 (right trigger mapping)
            // Brake is typically axis 5 or axis 9 (left trigger mapping)
            throttlePedal = ProcessPedal(GetThrottleAxis());
            brakePedal = ProcessPedal(GetBrakeAxis());
            clutchPedal = ProcessPedal(GetClutchAxis());

            // Read gear shifter
            ReadGearShifter();

            // Read buttons
            ReadButtons();
        }

        private float GetWheelAxis(int axisIndex)
        {
            if (wheelJoystickIndex < 0) return 0f;

            string axisName = $"Joy{wheelJoystickIndex + 1}Axis{axisIndex}";
            try
            {
                return UnityEngine.Input.GetAxisRaw(axisName);
            }
            catch
            {
                // Fallback to generic axis
                try
                {
                    return UnityEngine.Input.GetAxisRaw(FALLBACK_STEERING);
                }
                catch
                {
                    return 0f;
                }
            }
        }

        private float GetThrottleAxis()
        {
            // G920 throttle: Try multiple possible mappings
            float value = 0f;

            // Try dedicated wheel axis first
            try
            {
                // Axis 4 is common for throttle on G920
                value = UnityEngine.Input.GetAxisRaw($"Joy{wheelJoystickIndex + 1}Axis4");
                // Throttle axes often report 1 at rest, -1 at full press
                // Normalize: (-1 to 1) where 1 = no press, -1 = full press
                value = (1f - value) * 0.5f;
            }
            catch { }

            // If that didn't work, try axis 10 (trigger style)
            if (Mathf.Abs(value) < 0.01f)
            {
                try
                {
                    value = Mathf.Max(0f, UnityEngine.Input.GetAxisRaw($"Joy{wheelJoystickIndex + 1}Axis10"));
                }
                catch { }
            }

            return value;
        }

        private float GetBrakeAxis()
        {
            float value = 0f;

            try
            {
                // Axis 5 is common for brake on G920
                value = UnityEngine.Input.GetAxisRaw($"Joy{wheelJoystickIndex + 1}Axis5");
                value = (1f - value) * 0.5f;
            }
            catch { }

            if (Mathf.Abs(value) < 0.01f)
            {
                try
                {
                    value = Mathf.Max(0f, UnityEngine.Input.GetAxisRaw($"Joy{wheelJoystickIndex + 1}Axis9"));
                }
                catch { }
            }

            return value;
        }

        private float GetClutchAxis()
        {
            try
            {
                // Axis 3 is typically clutch
                float value = UnityEngine.Input.GetAxisRaw($"Joy{wheelJoystickIndex + 1}Axis3");
                return (1f - value) * 0.5f;
            }
            catch
            {
                return 0f;
            }
        }

        private float ProcessSteering(float raw)
        {
            if (invertSteering)
                raw = -raw;

            // Apply deadzone
            if (Mathf.Abs(raw) < steeringDeadzone)
                return 0f;

            // Remap from deadzone
            float sign = Mathf.Sign(raw);
            float magnitude = (Mathf.Abs(raw) - steeringDeadzone) / (1f - steeringDeadzone);

            // Apply linearity curve
            magnitude = Mathf.Pow(magnitude, steeringLinearity);

            return sign * magnitude;
        }

        private float ProcessPedal(float raw)
        {
            // Clamp to 0-1 range
            raw = Mathf.Clamp01(raw);

            // Apply deadzone
            if (raw < pedalDeadzone)
                return 0f;

            // Remap from deadzone
            return (raw - pedalDeadzone) / (1f - pedalDeadzone);
        }

        private void ReadGearShifter()
        {
            // G920 shifter buttons: 12-17 typically for gears 1-6, reverse is separate
            // This is controller-specific; G920 shifter is an optional add-on

            currentGear = 0; // Neutral by default

            if (wheelJoystickIndex < 0) return;

            // Check for H-pattern shifter (G920 Driving Force Shifter)
            for (int i = 0; i < 6; i++)
            {
                // Buttons 12-17 for gears 1-6
                if (IsWheelButtonPressed(12 + i))
                {
                    currentGear = i + 1;
                    break;
                }
            }

            // Button 18 typically for reverse
            if (IsWheelButtonPressed(18))
            {
                currentGear = -1;
            }
        }

        private void ReadButtons()
        {
            buttonState = new WheelButtonState
            {
                A = IsWheelButtonPressed(0),
                B = IsWheelButtonPressed(1),
                X = IsWheelButtonPressed(2),
                Y = IsWheelButtonPressed(3),
                LeftBumper = IsWheelButtonPressed(4),
                RightBumper = IsWheelButtonPressed(5),
                Back = IsWheelButtonPressed(6),
                Start = IsWheelButtonPressed(7),
                LeftPaddle = IsWheelButtonPressed(4),  // Often same as bumpers
                RightPaddle = IsWheelButtonPressed(5),
                DPadUp = IsWheelButtonPressed(12) || UnityEngine.Input.GetKey(KeyCode.JoystickButton12),
                DPadDown = IsWheelButtonPressed(13) || UnityEngine.Input.GetKey(KeyCode.JoystickButton13),
                DPadLeft = IsWheelButtonPressed(14) || UnityEngine.Input.GetKey(KeyCode.JoystickButton14),
                DPadRight = IsWheelButtonPressed(15) || UnityEngine.Input.GetKey(KeyCode.JoystickButton15)
            };
        }

        private bool IsWheelButtonPressed(int buttonIndex)
        {
            if (wheelJoystickIndex < 0) return false;

            try
            {
                KeyCode button = (KeyCode)((int)KeyCode.Joystick1Button0 +
                    (wheelJoystickIndex * 20) + buttonIndex);
                return UnityEngine.Input.GetKey(button);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Force Feedback - Windows DirectInput

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN

        // DirectInput Force Feedback via P/Invoke
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        // Logitech Gaming SDK (LogitechSteeringWheelEnginesWrapper.dll)
        // These are the typical exports from the Logitech SDK
        private delegate bool LogiSteeringInitialize(bool ignoreXInputControllers);
        private delegate bool LogiUpdate();
        private delegate bool LogiIsConnected(int index);
        private delegate bool LogiPlayConstantForce(int index, int magnitudePercentage);
        private delegate bool LogiStopConstantForce(int index);
        private delegate bool LogiPlayDamperForce(int index, int coefficientPercentage);
        private delegate bool LogiStopDamperForce(int index);
        private delegate bool LogiPlaySpringForce(int index, int offsetPercentage, int saturationPercentage, int coefficientPercentage);
        private delegate bool LogiStopSpringForce(int index);
        private delegate bool LogiPlaySideCollisionForce(int index, int magnitudePercentage);
        private delegate bool LogiPlayFrontalCollisionForce(int index, int magnitudePercentage);
        private delegate bool LogiPlaySurfaceEffect(int index, int type, int magnitudePercentage, int period);
        private delegate bool LogiStopSurfaceEffect(int index);
        private delegate bool LogiPlayCarAirborne(int index);
        private delegate bool LogiStopCarAirborne(int index);
        private delegate bool LogiPlaySlipperyRoadEffect(int index, int magnitudePercentage);
        private delegate bool LogiStopSlipperyRoadEffect(int index);
        private delegate void LogiSteeringShutdown();

        private IntPtr logitechDllHandle = IntPtr.Zero;
        private LogiSteeringInitialize logiInit;
        private LogiUpdate logiUpdate;
        private LogiIsConnected logiIsConnected;
        private LogiPlayConstantForce logiPlayConstantForce;
        private LogiStopConstantForce logiStopConstantForce;
        private LogiPlayDamperForce logiPlayDamperForce;
        private LogiStopDamperForce logiStopDamperForce;
        private LogiPlaySpringForce logiPlaySpringForce;
        private LogiStopSpringForce logiStopSpringForce;
        private LogiPlaySideCollisionForce logiPlaySideCollision;
        private LogiPlayFrontalCollisionForce logiPlayFrontalCollision;
        private LogiPlaySurfaceEffect logiPlaySurface;
        private LogiStopSurfaceEffect logiStopSurface;
        private LogiPlaySlipperyRoadEffect logiPlaySlippery;
        private LogiStopSlipperyRoadEffect logiStopSlippery;
        private LogiSteeringShutdown logiShutdown;

        private bool ffbInitialized = false;

        private void InitializeForceFeedback()
        {
            if (!forceFeedbackEnabled) return;

            try
            {
                // Try to load Logitech SDK
                logitechDllHandle = LoadLibrary("LogitechSteeringWheelEnginesWrapper.dll");
                if (logitechDllHandle == IntPtr.Zero)
                {
                    // Try alternate location
                    logitechDllHandle = LoadLibrary("Plugins/LogitechSteeringWheelEnginesWrapper.dll");
                }

                if (logitechDllHandle != IntPtr.Zero)
                {
                    LoadLogitechFunctions();

                    if (logiInit != null)
                    {
                        ffbInitialized = logiInit(false);
                        if (ffbInitialized)
                        {
                            Log.System("WheelInputManager", "Logitech Force Feedback initialized");
                        }
                    }
                }
                else
                {
                    Log.System("WheelInputManager", "Logitech SDK not found, force feedback disabled");
                }
            }
            catch (Exception e)
            {
                Log.SystemWarn("WheelInputManager", $"FFB init failed: {e.Message}");
            }
        }

        private void LoadLogitechFunctions()
        {
            logiInit = GetDelegate<LogiSteeringInitialize>("LogiSteeringInitialize");
            logiUpdate = GetDelegate<LogiUpdate>("LogiUpdate");
            logiIsConnected = GetDelegate<LogiIsConnected>("LogiIsConnected");
            logiPlayConstantForce = GetDelegate<LogiPlayConstantForce>("LogiPlayConstantForce");
            logiStopConstantForce = GetDelegate<LogiStopConstantForce>("LogiStopConstantForce");
            logiPlayDamperForce = GetDelegate<LogiPlayDamperForce>("LogiPlayDamperForce");
            logiStopDamperForce = GetDelegate<LogiStopDamperForce>("LogiStopDamperForce");
            logiPlaySpringForce = GetDelegate<LogiPlaySpringForce>("LogiPlaySpringForce");
            logiStopSpringForce = GetDelegate<LogiStopSpringForce>("LogiStopSpringForce");
            logiPlaySideCollision = GetDelegate<LogiPlaySideCollisionForce>("LogiPlaySideCollisionForce");
            logiPlayFrontalCollision = GetDelegate<LogiPlayFrontalCollisionForce>("LogiPlayFrontalCollisionForce");
            logiPlaySurface = GetDelegate<LogiPlaySurfaceEffect>("LogiPlaySurfaceEffect");
            logiStopSurface = GetDelegate<LogiStopSurfaceEffect>("LogiStopSurfaceEffect");
            logiPlaySlippery = GetDelegate<LogiPlaySlipperyRoadEffect>("LogiPlaySlipperyRoadEffect");
            logiStopSlippery = GetDelegate<LogiStopSlipperyRoadEffect>("LogiStopSlipperyRoadEffect");
            logiShutdown = GetDelegate<LogiSteeringShutdown>("LogiSteeringShutdown");
        }

        private T GetDelegate<T>(string functionName) where T : Delegate
        {
            IntPtr procAddress = GetProcAddress(logitechDllHandle, functionName);
            if (procAddress != IntPtr.Zero)
            {
                return Marshal.GetDelegateForFunctionPointer<T>(procAddress);
            }
            return null;
        }

        private void ShutdownForceFeedback()
        {
            if (ffbInitialized && logiShutdown != null)
            {
                logiShutdown();
            }

            if (logitechDllHandle != IntPtr.Zero)
            {
                FreeLibrary(logitechDllHandle);
                logitechDllHandle = IntPtr.Zero;
            }

            ffbInitialized = false;
        }

#else
        // Non-Windows platforms - stub implementations
        private void InitializeForceFeedback() { }
        private void ShutdownForceFeedback() { }
        private bool ffbInitialized = false;
#endif

        #endregion

        #region Force Feedback Effects

        /// <summary>
        /// Play a constant force effect (e.g., for pulling to one side).
        /// </summary>
        /// <param name="magnitude">-100 to 100. Negative = left, positive = right.</param>
        public void PlayConstantForce(int magnitude)
        {
            if (!ffbInitialized || !forceFeedbackEnabled) return;

            magnitude = Mathf.Clamp(magnitude, -100, 100);
            magnitude = (int)(magnitude * forceFeedbackStrength);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            logiPlayConstantForce?.Invoke(wheelJoystickIndex, magnitude);
#endif
        }

        /// <summary>
        /// Stop constant force effect.
        /// </summary>
        public void StopConstantForce()
        {
            if (!ffbInitialized) return;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            logiStopConstantForce?.Invoke(wheelJoystickIndex);
#endif
        }

        /// <summary>
        /// Play damper effect (resistance to movement).
        /// </summary>
        /// <param name="coefficient">0 to 100. Higher = more resistance.</param>
        public void PlayDamperForce(int coefficient)
        {
            if (!ffbInitialized || !forceFeedbackEnabled) return;

            coefficient = Mathf.Clamp(coefficient, 0, 100);
            coefficient = (int)(coefficient * forceFeedbackStrength);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            logiPlayDamperForce?.Invoke(wheelJoystickIndex, coefficient);
#endif
        }

        /// <summary>
        /// Stop damper effect.
        /// </summary>
        public void StopDamperForce()
        {
            if (!ffbInitialized) return;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            logiStopDamperForce?.Invoke(wheelJoystickIndex);
#endif
        }

        /// <summary>
        /// Play spring/centering force.
        /// </summary>
        /// <param name="offset">Center offset (-100 to 100).</param>
        /// <param name="saturation">Max force (0 to 100).</param>
        /// <param name="coefficient">Spring strength (0 to 100).</param>
        public void PlaySpringForce(int offset, int saturation, int coefficient)
        {
            if (!ffbInitialized || !forceFeedbackEnabled) return;

            saturation = (int)(saturation * forceFeedbackStrength);
            coefficient = (int)(coefficient * forceFeedbackStrength);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            logiPlaySpringForce?.Invoke(wheelJoystickIndex, offset, saturation, coefficient);
#endif
        }

        /// <summary>
        /// Stop spring force.
        /// </summary>
        public void StopSpringForce()
        {
            if (!ffbInitialized) return;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            logiStopSpringForce?.Invoke(wheelJoystickIndex);
#endif
        }

        /// <summary>
        /// Play side collision effect (impact from left or right).
        /// </summary>
        /// <param name="magnitude">0 to 100. Use negative for left impact.</param>
        public void PlaySideCollision(int magnitude)
        {
            if (!ffbInitialized || !forceFeedbackEnabled) return;

            magnitude = (int)(Mathf.Abs(magnitude) * forceFeedbackStrength);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            logiPlaySideCollision?.Invoke(wheelJoystickIndex, magnitude);
#endif
        }

        /// <summary>
        /// Play frontal collision effect.
        /// </summary>
        /// <param name="magnitude">0 to 100.</param>
        public void PlayFrontalCollision(int magnitude)
        {
            if (!ffbInitialized || !forceFeedbackEnabled) return;

            magnitude = (int)(magnitude * forceFeedbackStrength);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            logiPlayFrontalCollision?.Invoke(wheelJoystickIndex, magnitude);
#endif
        }

        /// <summary>
        /// Play surface effect (road texture rumble).
        /// </summary>
        /// <param name="type">0=Sine, 1=Square, 2=Triangle</param>
        /// <param name="magnitude">0 to 100.</param>
        /// <param name="period">Period in ms (1-150).</param>
        public void PlaySurfaceEffect(int type, int magnitude, int period)
        {
            if (!ffbInitialized || !forceFeedbackEnabled) return;

            magnitude = (int)(magnitude * forceFeedbackStrength);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            logiPlaySurface?.Invoke(wheelJoystickIndex, type, magnitude, period);
#endif
        }

        /// <summary>
        /// Stop surface effect.
        /// </summary>
        public void StopSurfaceEffect()
        {
            if (!ffbInitialized) return;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            logiStopSurface?.Invoke(wheelJoystickIndex);
#endif
        }

        /// <summary>
        /// Play slippery road effect (reduced grip feel).
        /// </summary>
        /// <param name="magnitude">0 to 100.</param>
        public void PlaySlipperyRoad(int magnitude)
        {
            if (!ffbInitialized || !forceFeedbackEnabled) return;

            magnitude = (int)(magnitude * forceFeedbackStrength);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            logiPlaySlippery?.Invoke(wheelJoystickIndex, magnitude);
#endif
        }

        /// <summary>
        /// Stop slippery road effect.
        /// </summary>
        public void StopSlipperyRoad()
        {
            if (!ffbInitialized) return;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            logiStopSlippery?.Invoke(wheelJoystickIndex);
#endif
        }

        /// <summary>
        /// Stop all force feedback effects.
        /// </summary>
        public void StopAllForceFeedback()
        {
            StopConstantForce();
            StopDamperForce();
            StopSpringForce();
            StopSurfaceEffect();
            StopSlipperyRoad();
        }

        #endregion

        #region Public Properties

        public bool IsWheelConnected => isWheelConnected;
        public bool IsForceFeedbackAvailable => ffbInitialized && forceFeedbackEnabled;
        public float Steering => steeringPosition;
        public float Throttle => throttlePedal;
        public float Brake => brakePedal;
        public float Clutch => clutchPedal;
        public int Gear => currentGear;
        public WheelButtonState Buttons => buttonState;

        public float ForceFeedbackStrength
        {
            get => forceFeedbackStrength;
            set => forceFeedbackStrength = Mathf.Clamp01(value);
        }

        public bool ForceFeedbackEnabled
        {
            get => forceFeedbackEnabled;
            set => forceFeedbackEnabled = value;
        }

        #endregion
    }

    /// <summary>
    /// Button state for wheel controller.
    /// </summary>
    public struct WheelButtonState
    {
        public bool A;
        public bool B;
        public bool X;
        public bool Y;
        public bool LeftBumper;
        public bool RightBumper;
        public bool Back;
        public bool Start;
        public bool LeftPaddle;
        public bool RightPaddle;
        public bool DPadUp;
        public bool DPadDown;
        public bool DPadLeft;
        public bool DPadRight;
    }
}

// ============================================================================
// Nightflow - Settings UI Controller
// Handles settings panel UI: tabs, bindings, sliders, toggles, and save integration
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Nightflow.Save;
using Nightflow.Input;

namespace Nightflow.UI
{
    /// <summary>
    /// Controls the settings panel UI, syncing with SaveManager and InputBindingManager.
    /// </summary>
    public class SettingsUIController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        // Root elements
        private VisualElement root;
        private VisualElement settingsOverlay;
        private VisualElement settingsPanel;

        // Tabs
        private Button tabControls;
        private Button tabAudio;
        private Button tabDisplay;
        private Button tabGameplay;
        private VisualElement controlsContent;
        private VisualElement audioContent;
        private VisualElement displayContent;
        private VisualElement gameplayContent;

        // Rebind prompt
        private VisualElement rebindPrompt;
        private Label rebindText;
        private Label rebindTimer;

        // Binding buttons (action -> (primary, alternate))
        private Dictionary<InputAction, (Button primary, Button alternate)> bindingButtons;

        // Settings controls
        private Slider sliderSensitivity;
        private Slider sliderDeadzone;
        private Toggle toggleInvertSteering;
        private Toggle toggleVibration;
        private Slider sliderMaster;
        private Slider sliderMusic;
        private Slider sliderSfx;
        private Slider sliderEngine;
        private Slider sliderAmbient;
        private Toggle toggleMuteBg;
        private Toggle toggleFullscreen;
        private Toggle toggleVsync;
        private DropdownField dropdownQuality;
        private Toggle toggleFps;
        private Toggle toggleSpeedometer;
        private Toggle toggleDamage;
        private DropdownField dropdownHudScale;
        private DropdownField dropdownSpeedUnit;
        private Toggle toggleCameraShake;
        private Slider sliderShakeIntensity;
        private Toggle toggleMotionBlur;
        private Toggle toggleTutorials;

        // Value labels
        private Label valueSensitivity;
        private Label valueDeadzone;
        private Label valueMaster;
        private Label valueMusic;
        private Label valueSfx;
        private Label valueEngine;
        private Label valueAmbient;
        private Label valueShakeIntensity;

        // Buttons
        private Button resetControlsButton;
        private Button applyButton;
        private Button backButton;

        // State
        private bool isVisible;
        private SettingsData pendingSettings;
        private int activeTab;

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }
        }

        private void Start()
        {
            InitializeUI();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void InitializeUI()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null)
            {
                return;
            }

            root = uiDocument.rootVisualElement;
            settingsOverlay = root.Q<VisualElement>("settings-overlay");
            settingsPanel = root.Q<VisualElement>("settings-panel");

            if (settingsOverlay == null)
            {
                Debug.LogWarning("SettingsUIController: settings-overlay not found");
                return;
            }

            // Tabs
            tabControls = root.Q<Button>("tab-controls");
            tabAudio = root.Q<Button>("tab-audio");
            tabDisplay = root.Q<Button>("tab-display");
            tabGameplay = root.Q<Button>("tab-gameplay");

            controlsContent = root.Q<VisualElement>("controls-tab-content");
            audioContent = root.Q<VisualElement>("audio-tab-content");
            displayContent = root.Q<VisualElement>("display-tab-content");
            gameplayContent = root.Q<VisualElement>("gameplay-tab-content");

            // Rebind prompt
            rebindPrompt = root.Q<VisualElement>("rebind-prompt");
            rebindText = root.Q<Label>("rebind-text");
            rebindTimer = root.Q<Label>("rebind-timer");

            // Initialize binding buttons
            InitializeBindingButtons();

            // Controls sliders/toggles
            sliderSensitivity = root.Q<Slider>("slider-sensitivity");
            sliderDeadzone = root.Q<Slider>("slider-deadzone");
            toggleInvertSteering = root.Q<Toggle>("toggle-invert-steering");
            toggleVibration = root.Q<Toggle>("toggle-vibration");
            valueSensitivity = root.Q<Label>("value-sensitivity");
            valueDeadzone = root.Q<Label>("value-deadzone");

            // Audio sliders
            sliderMaster = root.Q<Slider>("slider-master");
            sliderMusic = root.Q<Slider>("slider-music");
            sliderSfx = root.Q<Slider>("slider-sfx");
            sliderEngine = root.Q<Slider>("slider-engine");
            sliderAmbient = root.Q<Slider>("slider-ambient");
            toggleMuteBg = root.Q<Toggle>("toggle-mute-bg");
            valueMaster = root.Q<Label>("value-master");
            valueMusic = root.Q<Label>("value-music");
            valueSfx = root.Q<Label>("value-sfx");
            valueEngine = root.Q<Label>("value-engine");
            valueAmbient = root.Q<Label>("value-ambient");

            // Display toggles/dropdowns
            toggleFullscreen = root.Q<Toggle>("toggle-fullscreen");
            toggleVsync = root.Q<Toggle>("toggle-vsync");
            dropdownQuality = root.Q<DropdownField>("dropdown-quality");
            toggleFps = root.Q<Toggle>("toggle-fps");
            toggleSpeedometer = root.Q<Toggle>("toggle-speedometer");
            toggleDamage = root.Q<Toggle>("toggle-damage");
            dropdownHudScale = root.Q<DropdownField>("dropdown-hud-scale");

            // Gameplay toggles/dropdowns
            dropdownSpeedUnit = root.Q<DropdownField>("dropdown-speed-unit");
            toggleCameraShake = root.Q<Toggle>("toggle-camera-shake");
            sliderShakeIntensity = root.Q<Slider>("slider-shake-intensity");
            toggleMotionBlur = root.Q<Toggle>("toggle-motion-blur");
            toggleTutorials = root.Q<Toggle>("toggle-tutorials");
            valueShakeIntensity = root.Q<Label>("value-shake-intensity");

            // Buttons
            resetControlsButton = root.Q<Button>("reset-controls");
            applyButton = root.Q<Button>("settings-apply");
            backButton = root.Q<Button>("settings-back");

            // Setup callbacks
            SetupCallbacks();

            // Start hidden
            Hide();
        }

        private void InitializeBindingButtons()
        {
            bindingButtons = new Dictionary<InputAction, (Button, Button)>();

            // Map actions to button names
            var actionButtonMap = new Dictionary<InputAction, (string primary, string alt)>
            {
                { InputAction.Accelerate, ("bind-accelerate-primary", "bind-accelerate-alt") },
                { InputAction.Brake, ("bind-brake-primary", "bind-brake-alt") },
                { InputAction.SteerLeft, ("bind-steer-left-primary", "bind-steer-left-alt") },
                { InputAction.SteerRight, ("bind-steer-right-primary", "bind-steer-right-alt") },
                { InputAction.Handbrake, ("bind-handbrake-primary", "bind-handbrake-alt") },
                { InputAction.Pause, ("bind-pause-primary", "bind-pause-alt") },
                { InputAction.LookBack, ("bind-look-back-primary", "bind-look-back-alt") },
            };

            foreach (var kvp in actionButtonMap)
            {
                var primary = root.Q<Button>(kvp.Value.primary);
                var alt = root.Q<Button>(kvp.Value.alt);

                if (primary != null && alt != null)
                {
                    bindingButtons[kvp.Key] = (primary, alt);

                    // Setup click handlers
                    var action = kvp.Key;
                    primary.clicked += () => StartRebind(action, true);
                    alt.clicked += () => StartRebind(action, false);
                }
            }
        }

        private void SetupCallbacks()
        {
            // Tab buttons
            if (tabControls != null) tabControls.clicked += () => SwitchTab(0);
            if (tabAudio != null) tabAudio.clicked += () => SwitchTab(1);
            if (tabDisplay != null) tabDisplay.clicked += () => SwitchTab(2);
            if (tabGameplay != null) tabGameplay.clicked += () => SwitchTab(3);

            // Sliders with value display
            SetupSlider(sliderSensitivity, valueSensitivity, v => $"{v:F1}");
            SetupSlider(sliderDeadzone, valueDeadzone, v => $"{v:F2}");
            SetupSlider(sliderMaster, valueMaster, v => $"{Mathf.RoundToInt(v * 100)}%");
            SetupSlider(sliderMusic, valueMusic, v => $"{Mathf.RoundToInt(v * 100)}%");
            SetupSlider(sliderSfx, valueSfx, v => $"{Mathf.RoundToInt(v * 100)}%");
            SetupSlider(sliderEngine, valueEngine, v => $"{Mathf.RoundToInt(v * 100)}%");
            SetupSlider(sliderAmbient, valueAmbient, v => $"{Mathf.RoundToInt(v * 100)}%");
            SetupSlider(sliderShakeIntensity, valueShakeIntensity, v => $"{Mathf.RoundToInt(v * 100)}%");

            // Buttons
            if (resetControlsButton != null) resetControlsButton.clicked += OnResetControls;
            if (applyButton != null) applyButton.clicked += OnApply;
            if (backButton != null) backButton.clicked += OnBack;
        }

        private void SetupSlider(Slider slider, Label valueLabel, Func<float, string> formatter)
        {
            if (slider == null) return;

            slider.RegisterValueChangedCallback(evt =>
            {
                if (valueLabel != null)
                {
                    valueLabel.text = formatter(evt.newValue);
                }
            });
        }

        private void SubscribeToEvents()
        {
            var bindingManager = InputBindingManager.Instance;
            if (bindingManager != null)
            {
                bindingManager.OnKeyRebound += OnKeyRebound;
                bindingManager.OnRebindCancelled += OnRebindCancelled;
                bindingManager.OnRebindStarted += OnRebindStarted;
            }
        }

        private void UnsubscribeFromEvents()
        {
            var bindingManager = InputBindingManager.Instance;
            if (bindingManager != null)
            {
                bindingManager.OnKeyRebound -= OnKeyRebound;
                bindingManager.OnRebindCancelled -= OnRebindCancelled;
                bindingManager.OnRebindStarted -= OnRebindStarted;
            }
        }

        private void Update()
        {
            if (!isVisible) return;

            // Update rebind timer display
            var bindingManager = InputBindingManager.Instance;
            if (bindingManager != null && bindingManager.IsRebinding)
            {
                if (rebindTimer != null)
                {
                    rebindTimer.text = Mathf.CeilToInt(bindingManager.RebindTimeRemaining).ToString();
                }
            }
        }

        #region Show/Hide

        public void Show()
        {
            if (settingsOverlay == null) return;

            isVisible = true;
            settingsOverlay.RemoveFromClassList("hidden");

            // Load current settings
            LoadSettings();

            // Update binding displays
            RefreshBindingDisplay();

            // Select first tab
            SwitchTab(0);
        }

        public void Hide()
        {
            if (settingsOverlay == null) return;

            isVisible = false;
            settingsOverlay.AddToClassList("hidden");

            // Cancel any active rebind
            var bindingManager = InputBindingManager.Instance;
            if (bindingManager != null && bindingManager.IsRebinding)
            {
                bindingManager.CancelRebinding();
            }

            HideRebindPrompt();
        }

        public bool IsVisible => isVisible;

        #endregion

        #region Tab Management

        private void SwitchTab(int tabIndex)
        {
            activeTab = tabIndex;

            // Update tab button states
            UpdateTabButton(tabControls, tabIndex == 0);
            UpdateTabButton(tabAudio, tabIndex == 1);
            UpdateTabButton(tabDisplay, tabIndex == 2);
            UpdateTabButton(tabGameplay, tabIndex == 3);

            // Show/hide content
            SetTabContentVisible(controlsContent, tabIndex == 0);
            SetTabContentVisible(audioContent, tabIndex == 1);
            SetTabContentVisible(displayContent, tabIndex == 2);
            SetTabContentVisible(gameplayContent, tabIndex == 3);
        }

        private void UpdateTabButton(Button button, bool selected)
        {
            if (button == null) return;

            if (selected)
            {
                button.AddToClassList("selected");
            }
            else
            {
                button.RemoveFromClassList("selected");
            }
        }

        private void SetTabContentVisible(VisualElement content, bool visible)
        {
            if (content == null) return;

            if (visible)
            {
                content.RemoveFromClassList("hidden");
            }
            else
            {
                content.AddToClassList("hidden");
            }
        }

        #endregion

        #region Settings Load/Save

        private void LoadSettings()
        {
            var saveManager = SaveManager.Instance;
            if (saveManager == null) return;

            var settings = saveManager.GetSettings();
            pendingSettings = settings;

            // Controls
            SetSliderValue(sliderSensitivity, settings.Controls.SteeringSensitivity);
            SetSliderValue(sliderDeadzone, settings.Controls.SteeringDeadzone);
            SetToggleValue(toggleInvertSteering, settings.Controls.InvertSteering);
            SetToggleValue(toggleVibration, settings.Controls.VibrationEnabled);

            // Audio
            SetSliderValue(sliderMaster, settings.Audio.MasterVolume);
            SetSliderValue(sliderMusic, settings.Audio.MusicVolume);
            SetSliderValue(sliderSfx, settings.Audio.SFXVolume);
            SetSliderValue(sliderEngine, settings.Audio.EngineVolume);
            SetSliderValue(sliderAmbient, settings.Audio.AmbientVolume);
            SetToggleValue(toggleMuteBg, settings.Audio.MuteInBackground);

            // Display
            SetToggleValue(toggleFullscreen, settings.Display.Fullscreen);
            SetToggleValue(toggleVsync, settings.Display.VSync);
            SetDropdownValue(dropdownQuality, settings.Display.QualityLevel);
            SetToggleValue(toggleFps, settings.Display.ShowFPS);
            SetToggleValue(toggleSpeedometer, settings.Display.ShowSpeedometer);
            SetToggleValue(toggleDamage, settings.Display.ShowDamageIndicator);
            SetDropdownValue(dropdownHudScale, (int)settings.Display.HUDScale);

            // Gameplay
            SetDropdownValue(dropdownSpeedUnit, (int)settings.Gameplay.SpeedUnit);
            SetToggleValue(toggleCameraShake, settings.Gameplay.CameraShake);
            SetSliderValue(sliderShakeIntensity, settings.Gameplay.CameraShakeIntensity);
            SetToggleValue(toggleMotionBlur, settings.Gameplay.MotionBlur);
            SetToggleValue(toggleTutorials, settings.Gameplay.Tutorials);
        }

        private void ApplySettings()
        {
            var saveManager = SaveManager.Instance;
            if (saveManager == null) return;

            var settings = saveManager.GetSettings();

            // Controls
            settings.Controls.SteeringSensitivity = GetSliderValue(sliderSensitivity, 1f);
            settings.Controls.SteeringDeadzone = GetSliderValue(sliderDeadzone, 0.1f);
            settings.Controls.InvertSteering = GetToggleValue(toggleInvertSteering);
            settings.Controls.VibrationEnabled = GetToggleValue(toggleVibration);

            // Audio
            settings.Audio.MasterVolume = GetSliderValue(sliderMaster, 1f);
            settings.Audio.MusicVolume = GetSliderValue(sliderMusic, 0.7f);
            settings.Audio.SFXVolume = GetSliderValue(sliderSfx, 1f);
            settings.Audio.EngineVolume = GetSliderValue(sliderEngine, 0.8f);
            settings.Audio.AmbientVolume = GetSliderValue(sliderAmbient, 0.5f);
            settings.Audio.MuteInBackground = GetToggleValue(toggleMuteBg);

            // Display
            settings.Display.Fullscreen = GetToggleValue(toggleFullscreen);
            settings.Display.VSync = GetToggleValue(toggleVsync);
            settings.Display.QualityLevel = GetDropdownValue(dropdownQuality, 2);
            settings.Display.ShowFPS = GetToggleValue(toggleFps);
            settings.Display.ShowSpeedometer = GetToggleValue(toggleSpeedometer);
            settings.Display.ShowDamageIndicator = GetToggleValue(toggleDamage);
            settings.Display.HUDScale = (HUDScale)GetDropdownValue(dropdownHudScale, 1);

            // Gameplay
            settings.Gameplay.SpeedUnit = (SpeedUnit)GetDropdownValue(dropdownSpeedUnit, 0);
            settings.Gameplay.CameraShake = GetToggleValue(toggleCameraShake);
            settings.Gameplay.CameraShakeIntensity = GetSliderValue(sliderShakeIntensity, 1f);
            settings.Gameplay.MotionBlur = GetToggleValue(toggleMotionBlur);
            settings.Gameplay.Tutorials = GetToggleValue(toggleTutorials);

            // Apply and save
            saveManager.UpdateSettings(settings);
        }

        #endregion

        #region Rebinding

        private void StartRebind(InputAction action, bool primary)
        {
            var bindingManager = InputBindingManager.Instance;
            if (bindingManager == null) return;

            if (bindingManager.StartRebinding(action, primary))
            {
                ShowRebindPrompt(action);
            }
        }

        private void ShowRebindPrompt(InputAction action)
        {
            if (rebindPrompt == null) return;

            rebindPrompt.RemoveFromClassList("hidden");

            if (rebindText != null)
            {
                string actionName = InputBindingManager.GetActionDisplayName(action);
                rebindText.text = $"Press a key for {actionName}...";
            }
        }

        private void HideRebindPrompt()
        {
            if (rebindPrompt == null) return;
            rebindPrompt.AddToClassList("hidden");
        }

        private void OnRebindStarted(InputAction action)
        {
            // Highlight the button being rebound
            if (bindingButtons.TryGetValue(action, out var buttons))
            {
                buttons.primary?.AddToClassList("listening");
                buttons.alternate?.AddToClassList("listening");
            }
        }

        private void OnKeyRebound(InputAction action, KeyCode key)
        {
            HideRebindPrompt();
            RefreshBindingDisplay();

            // Remove listening class
            if (bindingButtons.TryGetValue(action, out var buttons))
            {
                buttons.primary?.RemoveFromClassList("listening");
                buttons.alternate?.RemoveFromClassList("listening");
            }
        }

        private void OnRebindCancelled()
        {
            HideRebindPrompt();

            // Remove listening class from all buttons
            foreach (var buttons in bindingButtons.Values)
            {
                buttons.primary?.RemoveFromClassList("listening");
                buttons.alternate?.RemoveFromClassList("listening");
            }
        }

        private void RefreshBindingDisplay()
        {
            var bindingManager = InputBindingManager.Instance;
            if (bindingManager == null) return;

            foreach (var kvp in bindingButtons)
            {
                var action = kvp.Key;
                var (primary, alternate) = kvp.Value;

                string primaryText = bindingManager.GetBindingDisplayName(action, true);
                string altText = bindingManager.GetBindingDisplayName(action, false);

                if (primary != null) primary.text = primaryText;
                if (alternate != null) alternate.text = altText;
            }
        }

        #endregion

        #region Button Handlers

        private void OnResetControls()
        {
            var bindingManager = InputBindingManager.Instance;
            if (bindingManager != null)
            {
                bindingManager.ResetToDefaults();
                RefreshBindingDisplay();
            }

            // Reset control settings to defaults
            SetSliderValue(sliderSensitivity, 1f);
            SetSliderValue(sliderDeadzone, 0.1f);
            SetToggleValue(toggleInvertSteering, false);
            SetToggleValue(toggleVibration, true);
        }

        private void OnApply()
        {
            ApplySettings();
            Hide();
        }

        private void OnBack()
        {
            Hide();
        }

        #endregion

        #region UI Helpers

        private void SetSliderValue(Slider slider, float value)
        {
            if (slider != null) slider.value = value;
        }

        private float GetSliderValue(Slider slider, float defaultValue)
        {
            return slider != null ? slider.value : defaultValue;
        }

        private void SetToggleValue(Toggle toggle, bool value)
        {
            if (toggle != null) toggle.value = value;
        }

        private bool GetToggleValue(Toggle toggle)
        {
            return toggle != null && toggle.value;
        }

        private void SetDropdownValue(DropdownField dropdown, int index)
        {
            if (dropdown != null && index >= 0 && index < dropdown.choices.Count)
            {
                dropdown.index = index;
            }
        }

        private int GetDropdownValue(DropdownField dropdown, int defaultValue)
        {
            return dropdown != null ? dropdown.index : defaultValue;
        }

        #endregion
    }
}

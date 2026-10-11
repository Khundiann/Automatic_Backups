using UnityEngine;
using MelonLoader;

#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.UI.Settings;
#elif MONO
using ScheduleOne;
using ScheduleOne.UI.Settings;
#else
    // Other configs could go here
#endif

namespace AutomaticBackups
{
    [RegisterTypeInIl2Cpp]
    public class AutoSaveToggle : SettingsToggle
    {
        public GameObject saveTimeSlider = null;

        public void Configure(UIToggle toggle, GameObject timeSlider)
        {
            uiToggle = toggle;
            saveTimeSlider = timeSlider;
            WireToggleListener();
        }

        public void Start()
        {
            // Rebind after Awake so the native SettingsToggle lifecycle cannot
            // replace the callback configured on this injected component.
            WireToggleListener();

            bool enableAutoSave = Core.enableAutoSave.Value;

            // Set Schedule I's custom UIToggle according to enableAutoSave.
            if (uiToggle == null)
            {
                Core.Log("Failed to initialize the auto-save toggle: no UIToggle reference was found.");
                return;
            }
            uiToggle.SetStateWithoutNotify(enableAutoSave);

            if (saveTimeSlider == null)
            {
                Core.Log("Failed to initialize the auto-save toggle: no time slider reference was provided.");
                return;
            }

            // Show/hide the time slider based on our enableAutoSave setting
            saveTimeSlider.SetActive(enableAutoSave);
        }

        void WireToggleListener()
        {
            if (uiToggle == null)
                return;

            uiToggle.OnChanged.RemoveAllListeners();
#if MONO
            uiToggle.OnChanged.AddListener(new UnityEngine.Events.UnityAction<bool>(OnValueChanged));
#else
            uiToggle.OnChanged.AddListener(new System.Action<bool>(OnValueChanged));
#endif
        }
        
#if IL2CPP
        public override void OnValueChanged(bool value)
#else
        protected override void OnValueChanged(bool value)
#endif
        {
            if (saveTimeSlider != null)
                saveTimeSlider.SetActive(value);
            Core.enableAutoSave.Value = value;
            Core.SchedulePreferenceSave();
        }
    }
}

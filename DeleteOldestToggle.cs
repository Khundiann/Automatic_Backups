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
    public class DeleteOldestToggle : SettingsToggle
    {
        protected GameObject retainedCountSlider = null;

        public void Configure(UIToggle toggle, GameObject retentionSlider)
        {
            uiToggle = toggle;
            retainedCountSlider = retentionSlider;
            WireToggleListener();
        }

        public void Start()
        {
            // Rebind after Awake so the native SettingsToggle lifecycle cannot
            // replace the callback configured on this injected component.
            WireToggleListener();

            if (retainedCountSlider == null)
            {
                Core.Log("Failed to initialize the retention toggle: no retention slider reference was provided.");
                return;
            }

            // Show/hide the retained count slider based on our enableAutoDelete setting.
            retainedCountSlider.SetActive(Core.enableAutoDelete.Value);
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
            if (retainedCountSlider != null)
                retainedCountSlider.SetActive(value);
            Core.enableAutoDelete.Value = value;
            Core.SchedulePreferenceSave();
        }
    }
}

using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.UI.Settings;
using Il2CppTMPro;
#elif MONO
using ScheduleOne;
using ScheduleOne.UI.Settings;
using TMPro;
#else
    // Other configs could go here
#endif

namespace AutomaticBackups
{
    [RegisterTypeInIl2Cpp]
    public class RetentionAmountSlider : SettingsSlider
    {
        protected int multiplier;

        public Slider SliderReference => base.slider;
        public TextMeshProUGUI ValueLabelReference => base.valueLabel;
        public bool DisplayValueReference => base.DisplayValue;
        public float ValueDisplayTimeReference => base.ValueDisplayTime;

        public void Configure(Slider sliderComponent, TextMeshProUGUI valueLabelComponent, bool displayValue, float valueDisplayTime)
        {
            base.slider = sliderComponent;
            base.valueLabel = valueLabelComponent;
            base.DisplayValue = displayValue;
            base.ValueDisplayTime = valueDisplayTime;
            WireSliderListener();
        }

        protected virtual void OnEnable()
        {
            // Rebind after Awake in case the native SettingsSlider lifecycle added
            // or restored listeners after this injected component was configured.
            WireSliderListener();

            multiplier = Mathf.Max(1, Core.retentionSliderMultiplier.Value);
            int minValue = DivideAndFloor(Core.retentionSliderMinFiles.Value, multiplier);
            int maxValue = DivideAndFloor(Core.retentionSliderMaxFiles.Value, multiplier);
            int value = DivideAndFloor(Core.autoDeleteRetentionCount.Value, multiplier);

            base.slider.minValue = minValue;
            base.slider.maxValue = maxValue;
            base.slider.SetValueWithoutNotify(value);
            if (base.valueLabel != null)
            {
                base.valueLabel.text = GetDisplayValue(base.slider.value);
                base.valueLabel.enabled = true;
            }
        }

        void WireSliderListener()
        {
            if (base.slider == null)
                return;

            base.slider.onValueChanged.RemoveAllListeners();
#if MONO
            base.slider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(OnValueChanged));
#else
            base.slider.onValueChanged.AddListener(new System.Action<float>(OnValueChanged));
#endif
        }

        int DivideAndFloor(int numerator, int denominator)
        {
            float quotient = (float)numerator / denominator;
            return (int)Mathf.Floor(quotient);
        }

#if IL2CPP
        public
#else
        protected 
#endif
        override string GetDisplayValue(float value)
        {
            return ((int)Mathf.Round(value * multiplier)).ToString();
        }
        
#if IL2CPP
        public
#else
        protected 
#endif
            override void OnValueChanged(float value)
        {
            value = NormalizeGamepadStep(value);
            this.timeOnValueChange = Time.time;
            if (this.DisplayValue && this.valueLabel != null)
            {
                this.valueLabel.text = this.GetDisplayValue(value);
                this.valueLabel.enabled = true;
            }
            Core.autoDeleteRetentionCount.Value = (int)value * multiplier;
            Core.SchedulePreferenceSave();
        }

        float NormalizeGamepadStep(float value)
        {
            if (!GameInput.GetCurrentInputDeviceIsGamepad())
                return value;

            int currentValue = DivideAndFloor(Core.autoDeleteRetentionCount.Value, multiplier);
            float adjustedValue = currentValue;
            if (value > currentValue)
                adjustedValue++;
            else if (value < currentValue)
                adjustedValue--;

            adjustedValue = Mathf.Clamp(adjustedValue, base.slider.minValue, base.slider.maxValue);
            base.slider.SetValueWithoutNotify(adjustedValue);
            return adjustedValue;
        }
    }
}

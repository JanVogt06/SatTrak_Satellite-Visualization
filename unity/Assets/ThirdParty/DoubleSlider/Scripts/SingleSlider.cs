#region Includes

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

#endregion

namespace DoubleSlider.Scripts
{
    [RequireComponent(typeof(Slider))]
    public class SingleSlider : MonoBehaviour
    {
        #region Variables

        [Header("References")]
        [SerializeField] private Label _label;

        private Slider _slider;

        public bool IsEnabled
        {
            get { return _slider.interactable; }
            set { _slider.interactable = value; }

        }
        public float Value
        {
            get { return _slider.value; }
            set
            {
                _slider.value = value;
                _slider.onValueChanged.Invoke(_slider.value);

                UpdateLabel();
            }
        }
        public bool WholeNumbers
        {
            get { return _slider.wholeNumbers; }
            set { _slider.wholeNumbers = value; }
        }

        #endregion

        private void Awake()
        {
            if (!TryGetComponent<Slider>(out _slider))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("Missing Slider Component");
#endif
            }
        }

        public void Setup(float value, float minValue, float maxValue, UnityAction<float> valueChanged)
        {
            _slider.minValue = minValue;
            _slider.maxValue = maxValue;

            _slider.value = value;
            _slider.onValueChanged.AddListener(Slider_OnValueChanged);
            _slider.onValueChanged.AddListener(valueChanged);

            UpdateLabel();
        }

        private void Slider_OnValueChanged(float arg0)
        {
            UpdateLabel();
        }

        protected virtual void UpdateLabel()
        {
            if (_label == null) { return; }

            if (Mathf.Approximately(Value, 1000))
            {
                string txt = LocalizationSettings.StringDatabase
                       .GetLocalizedString("GameScene", "gs.unlimited");
                _label.Text = txt;
            }
            else
            {
                _label.Text = Value.ToString("F1") + " km";
            }
        }

        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += _ => UpdateLabel();
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= _ => UpdateLabel();
        }
    }
}
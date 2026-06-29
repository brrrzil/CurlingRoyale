using UnityEngine;
using UnityEngine.UI;

namespace CurlingRoyale.Combat
{
    /// <summary>
    /// HP-бар в World Space над камнем.
    /// Вешать на дочерний GameObject, на котором лежит World Space Canvas со Slider.
    /// Автоматически подписывается на события StoneCombat при Init.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class HealthBar : MonoBehaviour
    {
        [Header("Цвета")]
        [Tooltip("Цвет заполнения (текущее HP).")]
        [SerializeField] private Color fillColor = new Color(0.2f, 0.85f, 0.3f, 1f);

        [Tooltip("Цвет фона (потерянное HP).")]
        [SerializeField] private Color backgroundColor = new Color(0.85f, 0.2f, 0.2f, 1f);

        [Header("Анимация (опционально)")]
        [Tooltip("Скорость плавного изменения полосы. 0 = мгновенно.")]
        [Min(0f)] [SerializeField] private float smoothSpeed = 8f;

        private Slider slider;
        private Image fillImage;
        private Image backgroundImage;
        private float targetValue;

        void Awake()
        {
            slider = GetComponent<Slider>();

            // Настроить цвета Image внутри Slider (структура по умолчанию: Background + Fill).
            var images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject == slider.gameObject) continue;
                if (img.type == Image.Type.Filled || img.name.ToLower().Contains("fill"))
                    fillImage = img;
                else
                    backgroundImage = img;
            }
            ApplyColors();
        }

        void OnEnable()
        {
            // Ищем родительский StoneCombat (если HealthBar — дочерний объект).
            var combat = GetComponentInParent<StoneCombat>();
            if (combat != null)
                Init(combat);
        }

        /// <summary>
        /// Привязать HP-бар к конкретному StoneCombat.
        /// Вызывать из спавнера или из самого StoneCombat в Start.
        /// </summary>
        public void Init(StoneCombat combat)
        {
            if (combat == null) return;
            combat.onHealthChanged.AddListener(HandleHealthChanged);
            // Проставляем текущее значение сразу.
            HandleHealthChanged(combat.CurrentHP, combat.MaxHP);
        }

        private void HandleHealthChanged(int current, int max)
        {
            if (max <= 0) return;
            targetValue = (float)current / max;
            if (smoothSpeed <= 0f) slider.value = targetValue;
        }

        void Update()
        {
            // Плавная анимация полосы (если smoothSpeed > 0).
            if (smoothSpeed > 0f && !Mathf.Approximately(slider.value, targetValue))
                slider.value = Mathf.MoveTowards(slider.value, targetValue, smoothSpeed * Time.deltaTime);
        }

        private void ApplyColors()
        {
            if (fillImage != null) fillImage.color = fillColor;
            if (backgroundImage != null) backgroundImage.color = backgroundColor;
        }
    }
}
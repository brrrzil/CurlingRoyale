using UnityEngine;
using UnityEngine.UI;

namespace CurlingRoyale.Combat
{
    /// <summary>
    /// HP-бар камня на World Space Canvas с двумя Image (фон + заливка).
    /// В Awake скрипт отвязывается от родителя и каждый кадр следит за позицией
    /// камня + offset (rotation = identity — не наследует вращение камня).
    /// Никакого ручного wiring в Editor: добавил HealthBar на Stone → всё работает.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthBar : MonoBehaviour
    {
        [Header("Дизайн")]
        [Tooltip("Ширина полного бара в пикселях канваса. 200 = 2 world units при canvasScale 0.01.")]
        [Min(10f)] public float canvasWidthPx = 200f;

        [Tooltip("Высота бара в пикселях канваса.")]
        [Min(2f)] public float canvasHeightPx = 24f;

        [Tooltip("Масштаб Canvas — переводит пиксели в мировые единицы. " +
                 "0.01 = 200px \u2192 2 world units (тонкая полоска над камнем).")]
        [Min(0.001f)] public float canvasWorldScale = 0.01f;

        [Tooltip("Смещение над камнем (мировые координаты).")]
        public Vector3 offset = new Vector3(0f, 1.2f, 0f);

        [Header("Цвета")]
        public Color highColor = new Color(0.30f, 0.85f, 0.30f, 1f);
        public Color midColor = new Color(0.95f, 0.85f, 0.30f, 1f);
        public Color lowColor = new Color(0.95f, 0.30f, 0.30f, 1f);
        public Color backgroundColor = new Color(0.10f, 0.10f, 0.10f, 0.75f);

        [Header("Сортировка")]
        public int sortingOrder = 100;

        // ─── Внутреннее ─────────────────────────────────────
        private StoneCombat target;
        private Transform targetTransform;
        private Transform canvasTransform;
        private RectTransform fillRT;
        private Image fillImage;
        private RectTransform bgRT;
        private Image bgImage;
        private float currentFill01 = 1f;

        void Awake()
        {
            // Отвязываемся от родителя (чтобы вращение камня не двигало бар).
            if (transform.parent != null)
                transform.SetParent(null, true);

            BuildCanvas();
        }

        void Start()
        {
            if (target == null) target = GetComponentInParent<StoneCombat>();
            if (target == null) return;
            targetTransform = target.transform;

            target.onHealthChanged.AddListener(OnHealthChanged);
            OnHealthChanged(target.CurrentHP, target.MaxHP);
        }

        void OnDisable()
        {
            if (target != null) target.onHealthChanged.RemoveListener(OnHealthChanged);
        }

        void LateUpdate()
        {
            if (targetTransform == null) return;
            transform.position = targetTransform.position + offset;
            transform.rotation = Quaternion.identity;
        }

        // ─── Создание UI ─────────────────────────────────────

        private void BuildCanvas()
        {
            // Canvas (World Space).
            var canvasGO = new GameObject("HP_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasTransform = canvasGO.transform;
            canvasTransform.SetParent(transform, false);

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortingOrder;

            var crt = canvasGO.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(canvasWidthPx, canvasHeightPx);
            canvasTransform.localScale = Vector3.one * canvasWorldScale;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100f;
            scaler.referencePixelsPerUnit = 100f;

            // Background Image.
            var bgGO = new GameObject("BG", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(canvasTransform, false);
            bgImage = bgGO.GetComponent<Image>();
            bgImage.color = backgroundColor;
            bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            // Fill Image.
            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(canvasTransform, false);
            fillImage = fillGO.GetComponent<Image>();
            fillImage.color = highColor;
            fillRT = fillGO.GetComponent<RectTransform>();
            // Якоря слева — fill «растёт» вправо.
            fillRT.anchorMin = new Vector2(0f, 0f);
            fillRT.anchorMax = new Vector2(0f, 1f);
            fillRT.pivot = new Vector2(0f, 0.5f);
            fillRT.anchoredPosition = Vector2.zero;
            fillRT.sizeDelta = new Vector2(canvasWidthPx, 0f);
        }

        // ─── Логика ─────────────────────────────────────

        private void OnHealthChanged(int current, int max)
        {
            if (max <= 0) return;
            currentFill01 = Mathf.Clamp01((float)current / max);

            if (fillRT != null)
            {
                fillRT.sizeDelta = new Vector2(canvasWidthPx * currentFill01, 0f);
            }
            if (fillImage != null)
            {
                fillImage.color = currentFill01 > 0.6f ? highColor
                    : currentFill01 > 0.3f ? midColor
                    : lowColor;
            }
        }
    }
}

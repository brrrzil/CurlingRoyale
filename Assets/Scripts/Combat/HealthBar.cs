using UnityEngine;
using UnityEngine.UI;

namespace CurlingRoyale.Combat
{
    /// <summary>
    /// HP-бар камня. ВАЖНО: должен быть дочерним GameObject к Stone (parent-child в иерархии).
    /// В LateUpdate скрипт жёстко фиксирует позицию = parent.position + offset (мировые координаты)
    /// и rotation = identity — поэтому камень вращается по velocity, а бар остаётся
    /// горизонтальным и всегда висит НАД камнем, не поворачиваясь вместе с ним.
    /// Canvas + Image UI создаются программно в Awake.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthBar : MonoBehaviour
    {
        [Header("Дизайн")]
        [Tooltip("Ширина полного бара в пикселях Canvas. 200 + canvasWorldScale 0.01 = 2 world units.")]
        [Min(10f)] public float canvasWidthPx = 200f;

        [Tooltip("Высота бара в пикселях Canvas.")]
        [Min(2f)] public float canvasHeightPx = 24f;

        [Tooltip("Масштаб Canvas: переводит пиксели в world units. 0.01 = 200px → 2 world units.")]
        [Min(0.001f)] public float canvasWorldScale = 0.01f;

        [Tooltip("Смещение над камнем (в мировых координатах).")]
        public Vector3 offset = new Vector3(0f, 1.2f, 0f);

        [Header("Цвета")]
        public Color highColor = new Color(0.30f, 0.85f, 0.30f, 1f);
        public Color midColor = new Color(0.95f, 0.85f, 0.30f, 1f);
        public Color lowColor = new Color(0.95f, 0.30f, 0.30f, 1f);
        public Color backgroundColor = new Color(0.10f, 0.10f, 0.10f, 0.75f);

        [Header("Сортировка")]
        public int sortingOrder = 100;

        // Внутреннее
        private StoneCombat target;
        private Transform targetTransform;
        private RectTransform fillRT;
        private Image fillImage;
        private RectTransform bgRT;
        private Image bgImage;

        void Awake()
        {
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
            // 1) Каждый кадр — позиция родителя (камня) + offset в МИРОВЫХ координатах.
            //    Так камень крутится по velocity, а бар всегда висит НАД камнем в мире.
            // 2) rotation = identity — стираем вращение от родителя, бар всегда горизонтальный.
            if (targetTransform == null) return;
            transform.position = targetTransform.position + offset;
            transform.rotation = Quaternion.identity;
        }

        // ─── Создание Canvas + Image ─────────────────────────

        private void BuildCanvas()
        {
            // Чистим возможные существующие Canvas/UI внутри HealthBar-объекта (от старого префаба).
            // Оставляем только transform самого GameObject.
            // Просто создаём Canvas как дочерний — старые будут проблемой для UI,
            // но они отсутствуют в коде, поэтому просто игнорируем.

            var canvasGO = new GameObject("HP_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvasTransform = canvasGO.transform;
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

            // Background Image
            var bgGO = new GameObject("BG", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(canvasTransform, false);
            bgImage = bgGO.GetComponent<Image>();
            bgImage.color = backgroundColor;
            bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            // Fill Image
            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(canvasTransform, false);
            fillImage = fillGO.GetComponent<Image>();
            fillImage.color = highColor;
            fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.anchorMin = new Vector2(0f, 0f);
            fillRT.anchorMax = new Vector2(0f, 1f);
            fillRT.pivot = new Vector2(0f, 0.5f);
            fillRT.anchoredPosition = Vector2.zero;
            fillRT.sizeDelta = new Vector2(canvasWidthPx, 0f);
        }

        // ─── Логика ──────────────────────────────────────────

        private void OnHealthChanged(int current, int max)
        {
            if (max <= 0) return;
            float t = Mathf.Clamp01((float)current / max);

            if (fillRT != null)
                fillRT.sizeDelta = new Vector2(canvasWidthPx * t, 0f);

            if (fillImage != null)
                fillImage.color = t > 0.6f ? highColor : (t > 0.3f ? midColor : lowColor);
        }
    }
}

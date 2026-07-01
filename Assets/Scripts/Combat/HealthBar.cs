using UnityEngine;

namespace CurlingRoyale.Combat
{
    /// <summary>
    /// HP-бар камня на чистом SpriteRenderer (без UGUI).
    /// Вешается на свой собственный GameObject (дочерний к Stone).
    /// По умолчанию позиция (0, 1.2, 0), размер 1.5×0.2 — настраивается в Inspector.
    /// Sprite создаётся программно из Texture2D.whiteTexture.
    /// Подписывается на StoneCombat.onHealthChanged в OnEnable.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthBar : MonoBehaviour
    {
        [Header("Дизайн")]
        [Tooltip("Ширина полного бара (мировые единицы).")]
        [Min(0.1f)] public float fullWidth = 1.5f;

        [Tooltip("Высота бара (мировые единицы).")]
        [Min(0.05f)] public float barHeight = 0.2f;

        [Tooltip("Смещение над камнем.")]
        public Vector3 offset = new Vector3(0f, 1.2f, 0f);

        [Header("Цвета")]
        public Color highColor = new Color(0.30f, 0.85f, 0.30f, 1f); // > 0.6
        public Color midColor = new Color(0.95f, 0.85f, 0.30f, 1f); // > 0.3
        public Color lowColor = new Color(0.95f, 0.30f, 0.30f, 1f); // <= 0.3

        [Header("Цвет фона (опционально)")]
        [Tooltip("Если задан — отображается тёмная полоска под fill (рамка HP).")]
        public SpriteRenderer backgroundRenderer;
        public Color backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        [Header("Сортировка")]
        [Tooltip("Sorting Order — должен быть выше остальных спрайтов чтобы не перекрывался.")]
        public int sortingOrder = 10;

        // ─── Внутреннее ─────────────────────────────────────────────
        private SpriteRenderer fillRenderer;
        private StoneCombat target;

        void Reset()
        {
            // Первый раз когда компонент добавляется — задаём дефолтную позицию/размер.
            transform.localPosition = offset;
            transform.localScale = new Vector3(fullWidth, barHeight, 1f);
        }

        void OnEnable()
        {
            if (target == null) target = GetComponentInParent<StoneCombat>();
            if (target == null) return;

            EnsureFillRenderer();
            EnsureBackground();

            // Подписка на события здоровья.
            target.onHealthChanged.AddListener(OnHealthChanged);
            // Применить текущее HP сразу.
            OnHealthChanged(target.CurrentHP, target.MaxHP);
        }

        void OnDisable()
        {
            if (target != null) target.onHealthChanged.RemoveListener(OnHealthChanged);
        }

        // ─── Рендер ───────────────────────────────────────────────────

        private void EnsureFillRenderer()
        {
            if (fillRenderer != null) return;
            if (!TryGetComponent(out fillRenderer))
                fillRenderer = gameObject.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = GetOrCreateBarSprite();
            fillRenderer.sortingOrder = sortingOrder;
        }

        private void EnsureBackground()
        {
            if (backgroundRenderer != null) return;
            var bg = new GameObject("Background");
            bg.transform.SetParent(transform, false);
            bg.transform.localPosition = Vector3.zero;
            bg.transform.localScale = Vector3.one; // заполняет весь parent
            backgroundRenderer = bg.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = GetOrCreateBarSprite();
            backgroundRenderer.color = backgroundColor;
            backgroundRenderer.sortingOrder = sortingOrder - 1;
        }

        // ─── Логика ──────────────────────────────────────────────────

        private void OnHealthChanged(int current, int max)
        {
            if (max <= 0 || fillRenderer == null) return;

            float t = Mathf.Clamp01((float)current / max);

            // Сжатие ширины (pivot = left edge благодаря sprite anchor).
            // transform.localScale.x = fullWidth * t
            // Остальные оси сохраняем от базовых значений.
            transform.localPosition = offset;
            transform.localScale = new Vector3(fullWidth * t, barHeight, 1f);

            // Цвет по диапазону.
            fillRenderer.color = t > 0.6f ? highColor : (t > 0.3f ? midColor : lowColor);
        }

        // ─── Sprite cache ─────────────────────────────────────────────

        private static Sprite cachedBarSprite;

        private static Sprite GetOrCreateBarSprite()
        {
            if (cachedBarSprite != null) return cachedBarSprite;
            // Белая текстура 4×4, pivot в (0, 0.5) — спрайт «растёт» вправо от левого края.
            // PPU = 10 → базовый размер в мире 4/10 = 0.4, scale для 1.5 unit bar = 3.75.
            cachedBarSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 4f, 4f),
                new Vector2(0f, 0.5f),
                10f);
            cachedBarSprite.name = "HPBarSprite";
            return cachedBarSprite;
        }
    }
}

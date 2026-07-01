using UnityEngine;

namespace CurlingRoyale.Combat
{
    /// <summary>
    /// HP-бар камня на чистом SpriteRenderer (без UGUI).
    /// Вешается на свой собственный GameObject (можно дочерний к Stone).
    /// В Awake скрипт отвязывается от родителя в World Space — так камень может
    /// крутиться (rotateTowardsVelocity), а HP-бар всегда горизонтальный.
    /// Позиция: position камня + offset (мировые координаты).
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthBar : MonoBehaviour
    {
        [Header("Дизайн")]
        [Tooltip("Ширина полного бара (мировые единицы).")]
        [Min(0.1f)] public float fullWidth = 1.5f;

        [Tooltip("Высота бара (мировые единицы).")]
        [Min(0.05f)] public float barHeight = 0.2f;

        [Tooltip("Смещение над камнем (мировые координаты, не локальные).")]
        public Vector3 offset = new Vector3(0f, 1.2f, 0f);

        [Header("Цвета")]
        public Color highColor = new Color(0.30f, 0.85f, 0.30f, 1f);
        public Color midColor = new Color(0.95f, 0.85f, 0.30f, 1f);
        public Color lowColor = new Color(0.95f, 0.30f, 0.30f, 1f);

        [Header("Цвет фона")]
        public SpriteRenderer backgroundRenderer;
        public Color backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        [Header("Сортировка")]
        public int sortingOrder = 10;

        // Внутреннее
        private SpriteRenderer fillRenderer;
        private StoneCombat target;
        private Transform targetTransform;
        private Vector3 baseScale;

        void Reset()
        {
            transform.localPosition = offset;
            baseScale = new Vector3(fullWidth, barHeight, 1f);
            transform.localScale = baseScale;
        }

        void Awake()
        {
            // Отвязываемся от камня. rotation камня перестаёт влиять на HP-бар.
            if (transform.parent != null)
                transform.SetParent(null, true);

            if (baseScale == Vector3.zero)
                baseScale = transform.localScale == Vector3.zero
                    ? new Vector3(fullWidth, barHeight, 1f)
                    : transform.localScale;
        }

        void Start()
        {
            if (target == null) target = GetComponentInParent<StoneCombat>();
            if (target == null) return;
            targetTransform = target.transform;

            EnsureFillRenderer();
            EnsureBackground();

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
            bg.transform.localScale = Vector3.one;
            backgroundRenderer = bg.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = GetOrCreateBarSprite();
            backgroundRenderer.color = backgroundColor;
            backgroundRenderer.sortingOrder = sortingOrder - 1;
        }

        private void OnHealthChanged(int current, int max)
        {
            if (max <= 0 || fillRenderer == null) return;
            float t = Mathf.Clamp01((float)current / max);
            transform.localScale = new Vector3(baseScale.x * t, baseScale.y, 1f);
            fillRenderer.color = t > 0.6f ? highColor : (t > 0.3f ? midColor : lowColor);
        }

        private static Sprite cachedBarSprite;

        private static Sprite GetOrCreateBarSprite()
        {
            if (cachedBarSprite != null) return cachedBarSprite;
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

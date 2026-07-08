using UnityEngine;

namespace CurlingRoyale.Arena
{
    /// <summary>
    /// Размещает фоновый спрайт так, чтобы его внутренний круг совпал с
    /// коллайдером арены (ArenaBorder.CurrentRadius).
    ///
    /// Как использовать:
    /// 1. Создай пустой GameObject 'ArenaBackground' на сцене.
    /// 2. Повесь этот скрипт.
    /// 3. В Inspector:
    ///    - Background Sprite: твой PNG (Assets/Sprites/Arena_Cyberpunk_BG.png).
    ///    - Arena Border: ссылка на GameObject с компонентом ArenaBorder.
    ///    - Circle Radius Ratio (Image): доля радиуса видимого круга в спрайте
    ///      относительно ширины картинки (для нашего спрайта ≈ 0.13 -- подбирай
    ///      пока центр-круг не совпадёт с бортом арены).
    ///    - Offset World: если хочешь сместить фон (по умолчанию 0).
    /// 4. Sorting Order: -10 (чтобы было позади камней).
    ///
    /// Скрипт автоматически:
    ///  - ставит GameObject на позицию ArenaBorder
    ///  - масштабирует спрайт так, чтобы радиус видимого круга = ArenaBorder.CurrentRadius
    ///  - сохраняет пропорции спрайта (scale.x == scale.y)
    /// </summary>
    public class ArenaBackground : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Спрайт фона. Если null -- ищет SpriteRenderer на этом GameObject.")]
        [SerializeField] private Sprite backgroundSprite;

        [Tooltip("ArenaBorder, к которому выравниваемся.")]
        [SerializeField] private ArenaBorder arenaBorder;

        [Header("Параметры выравнивания")]
        [Tooltip("Доля радиуса ВИДИМОГО центрального круга относительно ширины спрайта. " +
                 "0.13 для Arena_Cyberpunk_BG.png. Подбирай пока круг не совпадёт с коллайдером.")]
        [Range(0.01f, 0.5f)] [SerializeField] private float circleRadiusRatio = 0.13f;

        [Tooltip("Дополнительное смещение фона (мировые единицы).")]
        [SerializeField] private Vector2 offsetWorld = Vector2.zero;

        [Header("Render")]
        [Tooltip("Sorting Order. Ниже -- позади остальных спрайтов.")]
        [SerializeField] private int sortingOrder = -10;

        private void Awake()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            if (backgroundSprite != null) sr.sprite = backgroundSprite;
            sr.sortingOrder = sortingOrder;
        }

        private void Start()
        {
            AlignToArena();
        }

        /// <summary>
        /// Можно дёрнуть вручную (например, после изменения radius через shrinking).
        /// </summary>
        public void AlignToArena()
        {
            if (arenaBorder == null)
            {
                Debug.LogWarning("[ArenaBackground] ArenaBorder не назначен -- фон НЕ выровнен.");
                return;
            }

            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null)
            {
                Debug.LogWarning("[ArenaBackground] SpriteRenderer или Sprite отсутствует.");
                return;
            }

            // Позиция = позиция арены + опциональное смещение
            Vector3 targetPos = arenaBorder.transform.position + (Vector3)offsetWorld;
            targetPos.z = 0f; // 2D
            transform.position = targetPos;

            // Целевой мировой радиус круга
            float worldRadius = arenaBorder.CurrentRadius;

            // Спрайт имеет bounds.size.x в world units при scale=1.
            // Видимый радиус круга (доля от ширины) = circleRadiusRatio.
            // World radius sprite-а = bounds.size.x * circleRadiusRatio.
            // Чтобы он стал worldRadius -> масштаб = worldRadius / (bounds.size.x * circleRadiusRatio).
            float spriteRadiusWorld = sr.sprite.bounds.size.x * circleRadiusRatio;
            float scale = worldRadius / Mathf.Max(0.001f, spriteRadiusWorld);

            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
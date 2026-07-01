using UnityEngine;
using CurlingRoyale.Configs;

namespace CurlingRoyale.Arena
{
    /// <summary>
    /// EdgeCollider2D для круглой арены. Читает параметры из ArenaConfig.
    /// GameManager может вызвать Regenerate(radius) для shrinking.
    /// </summary>
    [RequireComponent(typeof(EdgeCollider2D))]
    public class ArenaBorder : MonoBehaviour
    {
        [Header("Конфигурация")]
        [Tooltip("ScriptableObject с параметрами арены. Если null — используются fallback-значения.")]
        [SerializeField] private ArenaConfig config;

        [Tooltip("Текущий радиус. Перезаписывается из config.currentRadius в OnValidate.")]
        [SerializeField] private float currentRadius = 5.125f;

        private EdgeCollider2D edge;

        void Awake()
        {
            edge = GetComponent<EdgeCollider2D>();
            ApplyRadiusFromConfig();
        }

        void Start()
        {
            Regenerate(currentRadius);
        }

        void OnValidate()
        {
            if (config != null) currentRadius = config.initialRadius;
        }

        /// <summary>
        /// Прочитать радиус из конфига (если есть). Вызывать после изменения config.
        /// </summary>
        public void ApplyRadiusFromConfig()
        {
            if (config != null)
                currentRadius = config.initialRadius;
        }

        /// <summary>
        /// Пересоздать EdgeCollider2D с заданным радиусом. Вызывает GameManager при shrinking.
        /// </summary>
        public void Regenerate(float radius)
        {
            if (edge == null) edge = GetComponent<EdgeCollider2D>();
            if (edge == null) return;

            currentRadius = Mathf.Max(0.1f, radius);
            int pointCount = config != null ? config.edgeColliderPoints : 64;

            Vector2[] pts = new Vector2[pointCount + 1];
            for (int i = 0; i <= pointCount; i++)
            {
                float angle = (float)i / pointCount * Mathf.PI * 2f;
                pts[i] = new Vector2(
                    Mathf.Cos(angle) * currentRadius,
                    Mathf.Sin(angle) * currentRadius);
            }
            edge.points = pts;
        }

        public float CurrentRadius => currentRadius;
    }
}

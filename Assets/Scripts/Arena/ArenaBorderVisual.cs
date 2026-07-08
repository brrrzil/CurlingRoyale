using UnityEngine;

namespace CurlingRoyale.Arena
{
    /// <summary>
    /// Рисует яркую неоновую окружность ВДОЛЬ ArenaBorder (EdgeCollider2D),
    /// чтобы граница шахты была очевидна.
    ///
    /// Использует LineRenderer с замкнутым кругом точек. Цвет, ширина и pulse
    /// эффект настраиваются.
    ///
    /// Подключение:
    /// 1. Создай GameObject 'ArenaBorderVisual'.
    /// 2. Повесь этот скрипт.
    /// 3. В Inspector: arenaBorder = ссылка на GameObject с ArenaBorder.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ArenaBorderVisual : MonoBehaviour
    {
        [Header("Что подсвечивать")]
        [SerializeField] private ArenaBorder arenaBorder;

        [Header("Внешний вид")]
        [Tooltip("Цвет неоновой линии вдоль границы.")]
        [SerializeField] private Color neonColor = new Color(0.2f, 0.95f, 1f, 0.85f); // cyan
        [Tooltip("Ширина линии в мировых единицах.")]
        [Min(0.01f)] [SerializeField] private float lineWidth = 0.15f;
        [Tooltip("Сегментов в круг (больше = глаже).")]
        [Range(16, 256)] [SerializeField] private int segments = 96;

        [Header("Pulse-анимация")]
        [Tooltip("Если true -- линия пульсирует (alpha 0.5..1.0)")]
        [SerializeField] private bool pulseEnabled = true;
        [Tooltip("Скорость пульсации (циклов в секунду).")]
        [Min(0f)] [SerializeField] private float pulseSpeed = 1.2f;
        [Tooltip("Минимальная alpha при пульсации.")]
        [Range(0f, 1f)] [SerializeField] private float pulseMinAlpha = 0.4f;
        [Tooltip("Максимальная alpha при пульсации.")]
        [Range(0f, 1f)] [SerializeField] private float pulseMaxAlpha = 1.0f;

        [Header("Слой рендера")]
        [SerializeField] private int sortingOrder = -5;

        private LineRenderer line;
        private float baseAlpha;
        private float baseWidth;

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = true;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.sortingOrder = sortingOrder;
            line.material = new Material(Shader.Find("Sprites/Default"));
            baseAlpha = neonColor.a;
            baseWidth = lineWidth;
        }

        void Start()
        {
            BuildCircle();
        }

        void OnEnable()
        {
            BuildCircle();
        }

        /// <summary>
        /// Перестроить линию (например, после shrinking).
        /// </summary>
        public void BuildCircle()
        {
            if (arenaBorder == null) return;
            line.startWidth = baseWidth;
            line.endWidth = baseWidth;

            float r = arenaBorder.CurrentRadius;
            Vector3 center = arenaBorder.transform.position;
            center.z = 0f;

            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
        }

        void Update()
        {
            if (line == null) return;

            if (pulseEnabled)
            {
                float t = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f; // 0..1
                float a = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, t);
                Color c = neonColor;
                c.a = baseAlpha * a;
                line.startColor = c;
                line.endColor = c;
            }
            else
            {
                line.startColor = neonColor;
                line.endColor = neonColor;
            }
        }
    }
}
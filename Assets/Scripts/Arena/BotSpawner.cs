using System.Collections;
using UnityEngine;

namespace CurlingRoyale.Arena
{
    /// <summary>
    /// Спавнит N копий BotDrone префаба по кругу радиуса spawnRadius
    /// с центром в позиции этого объекта.
    /// Полезно для FFA-7 (или любого N ботов без player-а), либо для
    /// смешанного режима (1 player в центре + 7 ботов по кругу).
    ///
    /// Если isAutoSpawn = true и botPrefab назначен -- спавн происходит
    /// автоматически в Start (с задержкой). Иначе вызывается SpawnAll() вручную.
    /// </summary>
    public class BotSpawner : MonoBehaviour
    {
        [Header("Что спавнить")]
        [Tooltip("Префаб бота (должен иметь StoneCombat + BotController + Rigidbody2D + Collider2D).")]
        [SerializeField] private GameObject botPrefab;

        [Header("Где расставить")]
        [Tooltip("Центр круга. По умолчанию = transform.position.")]
        [SerializeField] private Vector2 center = Vector2.zero;

        [Tooltip("Радиус круга для расстановки.")]
        [Min(0.5f)] [SerializeField] private float spawnRadius = 3.8f;

        [Tooltip("Сколько ботов спавнить (по кругу).")]
        [Min(1)] [SerializeField] private int botCount = 7;

        [Tooltip("Первый угол смещения (градусы). 0 = первая точка справа.")]
        [Range(0f, 360f)] [SerializeField] private float startAngleDegrees = 90f;

        [Header("Автоспавн")]
        [Tooltip("Если true -- спавн в Start() с указанной задержкой.")]
        [SerializeField] private bool isAutoSpawn = true;

        [Tooltip("Задержка перед спавном (чтобы GameManager.StartMatch успел инициализировать state).")]
        [Min(0f)] [SerializeField] private float spawnDelay = 0.1f;

        void Start()
        {
            if (isAutoSpawn)
            {
                StartCoroutine(SpawnAllDelayed());
            }
        }

        public IEnumerator SpawnAllDelayed()
        {
            yield return new WaitForSeconds(spawnDelay);
            SpawnAll();
        }

        /// <summary>
        /// Спавнит <botCount> копий префаба по кругу. Уже заспавненных не трогает.
        /// </summary>
        public int SpawnAll()
        {
            if (botPrefab == null)
            {
                Debug.LogError("[BotSpawner] botPrefab не назначен -- спавн отменён.", this);
                return 0;
            }
            if (botCount <= 0) return 0;

            // Если этот объект на ненулевой позиции -- используем её как центр.
            Vector2 c = center == Vector2.zero ? (Vector2)transform.position : center;

            int spawned = 0;
            for (int i = 0; i < botCount; i++)
            {
                float t = botCount == 1 ? 0f : (i / (float)botCount);
                float angleDeg = startAngleDegrees + t * 360f;
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(c.x + Mathf.Cos(angleRad) * spawnRadius,
                                          c.y + Mathf.Sin(angleRad) * spawnRadius,
                                          0f);
                Instantiate(botPrefab, pos, Quaternion.identity);
                spawned++;
            }
            return spawned;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Vector2 c = center == Vector2.zero ? (Vector2)transform.position : center;
            Gizmos.color = new Color(0.3f, 0.85f, 0.4f, 0.6f);
            const int seg = 64;
            Vector3 prev = new Vector3(c.x + spawnRadius, c.y, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = (i / (float)seg) * Mathf.PI * 2f;
                Vector3 p = new Vector3(c.x + Mathf.Cos(a) * spawnRadius, c.y + Mathf.Sin(a) * spawnRadius, 0f);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            for (int i = 0; i < botCount; i++)
            {
                float t = botCount == 1 ? 0f : (i / (float)botCount);
                float a = (startAngleDegrees + t * 360f) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(c.x + Mathf.Cos(a) * spawnRadius, c.y + Mathf.Sin(a) * spawnRadius, 0f);
                Gizmos.DrawSphere(p, 0.15f);
            }
        }
#endif
    }
}

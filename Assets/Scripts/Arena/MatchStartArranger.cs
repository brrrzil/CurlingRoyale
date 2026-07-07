using UnityEngine;
using CurlingRoyale.Combat;
using CurlingRoyale.Bots;
using CurlingRoyale.Player;

namespace CurlingRoyale.Arena
{
    /// <summary>
    /// Расставляет все StoneCombat в круг на каждом MatchStart.
    /// Игрок на СЛЕВОМ краю (angleDeg=180), боты по равным углам на остальных позициях.
    ///
    /// Подписывается на GameManager.onStateChanged и срабатывает на Menu/MatchStart --
    /// оба состояния достаточно чтобы переставить камни перед фазой InProgress.
    ///
    /// Работает для ботов, заспавненных BotSpawner'ом, и для камня игрока в сцене.
    /// </summary>
    public class MatchStartArranger : MonoBehaviour
    {
        [Header("Параметры расстановки")]
        [Tooltip("Радиус круга для расстановки камней.")]
        [Min(0.5f)] [SerializeField] private float radius = 4.2f;

        [Tooltip("Угол для игрока (градусы). 180 = слева, 0 = справа, 90 = сверху.")]
        [Range(0f, 360f)] [SerializeField] private float playerAngleDegrees = 180f;

        [Header("Авто-поиск камней")]
        [Tooltip("Если true -- ищем PlayerStone/Bots в сцене автоматически каждый Match.")]
        [SerializeField] private bool autoFindStones = true;

        [Tooltip("Игрок. Если null и autoFindStones=true -- найдём PlayerStone автоматически.")]
        [SerializeField] private Transform playerStoneOverride;

        private bool subscribed = false;

        void Awake()
        {
            if (playerStoneOverride != null) return;
            if (autoFindStones)
            {
                var pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
                if (pc != null) playerStoneOverride = pc.transform;
            }
        }

        void Start()
        {
            StartCoroutine(SubscribeWhenReady());
        }

        System.Collections.IEnumerator SubscribeWhenReady()
        {
            // Подождём GameManager.Instance.
            float deadline = Time.time + 2f;
            while (GameManager.Instance == null && Time.time < deadline)
                yield return null;
            TrySubscribe();
        }

        void TrySubscribe()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.onStateChanged -= OnGameStateChanged;
            GameManager.Instance.onStateChanged += OnGameStateChanged;
            subscribed = true;
        }

        void Update()
        {
            if (!subscribed) TrySubscribe();
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.onStateChanged -= OnGameStateChanged;
        }

        void OnGameStateChanged(GameManager.MatchState newState)
        {
            if (newState == GameManager.MatchState.MatchStart ||
                newState == GameManager.MatchState.Menu)
            {
                Arrange();
            }
        }

        /// <summary>
        /// Расставить камень игрока на playerAngleDegrees и ботов по остальным равным углам.
        /// </summary>
        public void Arrange()
        {
            // 1) Найти все камни на сцене.
            StoneCombat[] all = FindObjectsByType<StoneCombat>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0) return;

            // 2) Разделить на player и bots.
            var botTransforms = new System.Collections.Generic.List<Transform>();
            Transform playerT = playerStoneOverride;
            if (playerT == null)
            {
                foreach (var s in all)
                {
                    if (s == null) continue;
                    if (s.GetComponent<PlayerController>() != null)
                    {
                        playerT = s.transform;
                        break;
                    }
                }
            }

            foreach (var s in all)
            {
                if (s == null) continue;
                if (playerT != null && s.transform == playerT) continue;
                if (s.GetComponent<BotController>() != null)
                    botTransforms.Add(s.transform);
            }

            // 3) Поставить игрока на playerAngleDegrees.
            if (playerT != null)
            {
                float pa = playerAngleDegrees * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Cos(pa) * radius, Mathf.Sin(pa) * radius, 0f);
                var rb = playerT.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
                playerT.SetPositionAndRotation(pos, Quaternion.identity);
            }

            // 4) Поставить ботов по равным углам, начиная с правого.
            int n = botTransforms.Count;
            for (int i = 0; i < n; i++)
            {
                float angleDeg = (i / (float)n) * 360f;
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Cos(angleRad) * radius, Mathf.Sin(angleRad) * radius, 0f);
                var rb = botTransforms[i].GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
                botTransforms[i].SetPositionAndRotation(pos, Quaternion.identity);
            }
        }
    }
}

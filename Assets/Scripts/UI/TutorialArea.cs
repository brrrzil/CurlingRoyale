using UnityEngine;

namespace CurlingRoyale.UI
{
    /// <summary>
    /// Туториал-зона в MainMenu: маленькая арена с фиксированной камерой.
    /// Спавнит:
    ///   - PlayerDrone (infinite HP, full controls как в игре) -- чтобы игрок мог постреливать
    ///   - BotDrone (infinite HP, без управления) -- стационарная мишень
    ///   - HealthBar (на обоих, чтобы видно было)
    ///
    /// Камера НЕ следует за игроком. Игрок и бот спавнятся в центре, рядом друг с другом.
    /// При уходе со стартовой сцены -- GameObjects удаляются (DontDestroyOnLoad не ставим).
    ///
    /// Повесить на пустой GameObject "TutorialArea" в MainMenu.
    /// Перетащить в inspector:
    ///   - playerPrefab (PlayerDrone.prefab)
    ///   - botPrefab (BotDrone.prefab)
    ///   - spawnPoint (центр туториала)
    ///   - cameraPoint (Transform камеры, откуда смотрим)
    ///   - botSpawnOffset (Vector3 от player, где заспавнить бота)
    /// </summary>
    public class TutorialArea : MonoBehaviour
    {
        [Header("Префабы")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject botPrefab;

        [Header("Спавн")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Vector3 botSpawnOffset = new Vector3(2f, 0f, 0f);

        [Header("Камера (опционально, для фиксированного вида)")]
        [Tooltip("Если задано -- камера смотрит отсюда на центр спавна. Иначе Main Camera остаётся как есть.")]
        [SerializeField] private Transform cameraPoint;
        [SerializeField] private float cameraSize = 5f;

        [Header("Бессмертие")]
        [Tooltip("Установить обоим дронам HP = 99999, чтобы не умирали.")]
        [SerializeField] private bool infiniteHP = true;
        [SerializeField] private int infiniteHPValue = 99999;

        private GameObject spawnedPlayer;
        private GameObject spawnedBot;

        void Start()
        {
            if (playerPrefab == null || spawnPoint == null)
            {
                Debug.LogWarning("[TutorialArea] Не задан playerPrefab или spawnPoint.");
                return;
            }

            // Ставим камеру (если указана)
            if (cameraPoint != null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    cam.transform.position = cameraPoint.position;
                    cam.transform.rotation = cameraPoint.rotation;
                    if (cam.orthographic) cam.orthographicSize = cameraSize;
                }
            }

            // Спавним игрока
            spawnedPlayer = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
            spawnedPlayer.name = "TutorialPlayer";
            MakeInvincible(spawnedPlayer);

            // Спавним бота рядом
            if (botPrefab != null)
            {
                Vector3 botPos = spawnPoint.position + botSpawnOffset;
                spawnedBot = Instantiate(botPrefab, botPos, spawnPoint.rotation);
                spawnedBot.name = "TutorialBot";
                MakeInvincible(spawnedBot);
                DisableBotAI(spawnedBot);
            }
        }

        void OnDestroy()
        {
            if (spawnedPlayer != null) Destroy(spawnedPlayer);
            if (spawnedBot != null) Destroy(spawnedBot);
        }

        void MakeInvincible(GameObject go)
        {
            if (!infiniteHP) return;
            var combat = go.GetComponent<CurlingRoyale.Combat.StoneCombat>();
            if (combat == null) return;

            // CurrentHP -- авто-property с private set, ставится через рефлексию backing field.
            var t = typeof(CurlingRoyale.Combat.StoneCombat);
            var backing = t.GetField("<CurrentHP>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (backing != null) backing.SetValue(combat, infiniteHPValue);

            // Также подменим damageConfig чтобы MaxHP тоже стал большим.
            // Чтобы не ломать ссылку, просто создаём рантайм-инстанс с нашим maxHealth.
            var maxHealthField = typeof(CurlingRoyale.Combat.DamageConfig).GetField("maxHealth",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (maxHealthField != null)
            {
                var dummyConfig = ScriptableObject.CreateInstance<CurlingRoyale.Combat.DamageConfig>();
                dummyConfig.name = "DamageConfig_TutorialInfinite";
                maxHealthField.SetValue(dummyConfig, infiniteHPValue);
                var configField = t.GetField("damageConfig",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (configField != null) configField.SetValue(combat, dummyConfig);
            }

            // Дёрнем onHealthChanged чтобы UI обновился с новым HP
            var onHealthChangedField = t.GetField("onHealthChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (onHealthChangedField != null)
            {
                var unityEvent = onHealthChangedField.GetValue(combat) as UnityEngine.Events.UnityEvent<int, int>;
                if (unityEvent != null) unityEvent.Invoke(infiniteHPValue, infiniteHPValue);
            }
        }

        void DisableBotAI(GameObject botGo)
        {
            // Вырубаем BotController FSM чтобы бот просто стоял
            var bot = botGo.GetComponent<CurlingRoyale.Bots.BotController>();
            if (bot != null) bot.enabled = false;

            // Замораживаем физику чтобы бот не двигался
            var rb = botGo.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Static;
            }
        }
    }
}

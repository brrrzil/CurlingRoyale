using System;
using UnityEngine;
using CurlingRoyale.Arena;
using CurlingRoyale.Combat;
using CurlingRoyale.Configs;

namespace CurlingRoyale.Game
{
    /// <summary>
    /// Управляет состоянием матча. Singleton — GameManager.Instance.
    /// FSM: Menu → MatchStart → InProgress → MatchEnd → Menu.
    /// Авто-подсчёт живых StoneCombat, завершение матча при aliveCount <= 1.
    /// Управляет shrinking арены в финале (если задан ArenaConfig + ArenaBorder).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum MatchState
        {
            Menu,
            MatchStart,
            InProgress,
            Paused,
            MatchEnd,
        }

        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Lazy-find в сцене — для случая когда бот стартует раньше GameManager.
                    _instance = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
                }
                return _instance;
            }
            private set => _instance = value;
        }
        private static GameManager _instance;

        [Header("Настройки матча")]
        [Tooltip("Длительность фазы MatchStart (перед InProgress). Сейчас можно задать как 'tick before go'.")]
        [Min(0f)] public float chargePhaseDuration = 1.5f;

        [Header("Арена (опционально)")]
        [Tooltip("ScriptableObject с параметрами арены. Если задан — используется shrinking.")]
        [SerializeField] private ArenaConfig arenaConfig;

        [Tooltip("Border-объект, который будет регенериться при shrinking. " +
                 "Если null — арена остаётся статичной (без shrinking).")]
        [SerializeField] private ArenaBorder arenaBorder;

        [Header("Player (опционально)")]
        [Tooltip("StoneCombat игрока. Если задан — матч завершается при IsDead игрока. " +
                 "Если null — авто-поиск первого PlayerController в сцене, либо матч завершается только при aliveCount <= 1.")]
        [SerializeField] private StoneCombat playerStone;

        public MatchState State { get; private set; } = MatchState.Menu;
        public float PhaseTimeRemaining { get; private set; }
        public int AliveCount { get; private set; }
        public bool IsArenaShrinking { get; private set; }

        public event Action<MatchState> onStateChanged;
        public event Action<float> onPhaseTick;
        public event Action<int> onAliveCountChanged;

        private float aliveCountCheckInterval = 0.25f;
        private float nextAliveCheckTime;
        private bool hasStarted;
        private float lastPhaseStartTime;
        private float shrinkStartTime;
        private float previousRadius;
        private bool wasShrinkingLastFrame;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            // Авто-поиск игрока для завершения матча по IsDead.
            if (playerStone == null)
            {
                var pc = FindFirstObjectByType<CurlingRoyale.Player.PlayerController>(FindObjectsInactive.Include);
                if (pc != null) playerStone = pc.GetComponentInParent<StoneCombat>();
            }
            if (playerStone != null)
            {
                playerStone.onDeath.RemoveListener(OnPlayerDied);
                playerStone.onDeath.AddListener(OnPlayerDied);
                Debug.Log($"[GameManager] playerStone={playerStone.name}, onDeath subscribed");
            }
            else
            {
                Debug.LogWarning("[GameManager] playerStone == NULL -- матч не закроется по смерти игрока.");
            }

            // Авто-старт матча при загрузке сцены (для прототипа).
            // Позже заменить на UI-кнопку «Play».
            StartMatch();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                if (playerStone != null) playerStone.onDeath.RemoveListener(OnPlayerDied);
                Instance = null;
            }
        }

        /// <summary>
        /// Если камню игрока осталось жить -- матч немедленно завершается
        /// (не ждём aliveCount &lt;= 1).
        /// </summary>
        public void OnPlayerDied()
        {
            if (State == MatchState.MatchEnd || State == MatchState.Menu) return;
            EndMatch();
        }

        // ─── Управление состоянием ──────────────────────────────────

        public void StartMatch()
        {
            hasStarted = true;
            lastPhaseStartTime = Time.time;
            shrinkStartTime = 0f;
            IsArenaShrinking = false;
            ChangeState(MatchState.MatchStart);
            PhaseTimeRemaining = chargePhaseDuration;
            Invoke(nameof(EnterInProgress), chargePhaseDuration);
        }

        public void EndMatch(int winnerId = -1) => ChangeState(MatchState.MatchEnd);
        public void ReturnToMenu() => ChangeState(MatchState.Menu);

        // ─── Кнопки UI (для OnClick в Inspector) ───

        /// <summary>Перезапустить матч (из MatchEnd → MatchStart).</summary>
        public void Restart()
        {
            if (State == MatchState.MatchEnd || State == MatchState.InProgress || State == MatchState.MatchStart)
            {
                StartMatch();
            }
        }

        /// <summary>Поставить матч на паузу.</summary>
        public void Pause()
        {
            if (State == MatchState.InProgress)
            {
                ChangeState(MatchState.Paused);
                Time.timeScale = 0f;
            }
        }

        /// <summary>Снять с паузы (alias для Continue).</summary>
        public void Resume()
        {
            if (State == MatchState.Paused)
            {
                Time.timeScale = 1f;
                ChangeState(MatchState.InProgress);
            }
        }

        /// <summary>Снять с паузы (alias).</summary>
        public void Continue() => Resume();

        /// <summary>Вернуться в главное меню (загрузить MainMenu сцену).</summary>
        public void GoHome()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        /// <summary>Вернуться в главное меню (alias).</summary>
        public void Home() => GoHome();

        private void EnterInProgress()
        {
            lastPhaseStartTime = Time.time;
            ChangeState(MatchState.InProgress);
            PhaseTimeRemaining = 0f;
            nextAliveCheckTime = Time.time;
        }

        // ─── Тик ─────────────────────────────────────────────────────

        void Update()
        {
            if (State == MatchState.MatchStart && PhaseTimeRemaining > 0f)
            {
                float elapsed = Time.time - lastPhaseStartTime;
                PhaseTimeRemaining = Mathf.Max(0f, chargePhaseDuration - elapsed);
                onPhaseTick?.Invoke(PhaseTimeRemaining);
            }

            if (State == MatchState.InProgress && Time.time >= nextAliveCheckTime)
            {
                CountAliveStones();
                nextAliveCheckTime = Time.time + aliveCountCheckInterval;
            }

            // Shrinking logic — только в фазе InProgress
            if (State == MatchState.InProgress && arenaConfig != null)
                UpdateShrinking();
        }

        private void ChangeState(MatchState newState)
        {
            State = newState;
            onStateChanged?.Invoke(newState);
        }

        private void CountAliveStones()
        {
            StoneCombat[] stones = FindObjectsByType<StoneCombat>(FindObjectsSortMode.None);
            int alive = 0;
            for (int i = 0; i < stones.Length; i++)
            {
                if (stones[i] != null && !stones[i].IsDead) alive++;
            }

            if (alive == AliveCount) return;
            AliveCount = alive;
            onAliveCountChanged?.Invoke(alive);

            if (alive <= 1 && hasStarted) EndMatch();
        }

        // ─── Shrinking ──────────────────────────────────────────────

        private void UpdateShrinking()
        {
            // Shrinking отключён если shrinkStartAliveCount в "сентиель" диапазоне:
            // либо <= 1 (выкл), либо > 8 (>= 999 — наш disable-флаг).
            // Корректный режим = 2..8 (FFA-2..FFA-8).
            int threshold = arenaConfig.shrinkStartAliveCount;
            bool enabled = threshold >= 2 && threshold <= 8;
            bool shouldShrink = enabled &&
                                AliveCount > 0 &&
                                AliveCount <= threshold;

            if (shouldShrink && !wasShrinkingLastFrame)
            {
                shrinkStartTime = Time.time;
                previousRadius = arenaConfig.initialRadius;
                arenaConfig.currentRadius = arenaConfig.initialRadius;
                IsArenaShrinking = true;
                ApplyArenaRadius(arenaConfig.initialRadius);
            }

            wasShrinkingLastFrame = shouldShrink;

            if (IsArenaShrinking)
            {
                float t = Mathf.Clamp01((Time.time - shrinkStartTime) / arenaConfig.shrinkDuration);
                float r = Mathf.Lerp(arenaConfig.initialRadius, arenaConfig.finalRadius, t);
                if (Mathf.Abs(r - previousRadius) > 0.001f)
                {
                    arenaConfig.currentRadius = r;
                    previousRadius = r;
                    ApplyArenaRadius(r);
                }
            }
        }

        private void ApplyArenaRadius(float r)
        {
            if (arenaBorder != null) arenaBorder.Regenerate(r);
            // ArenaScaler подхватит изменение автоматически в LateUpdate.
        }

        // ─── Утилиты ────────────────────────────────────────────────

        public static bool IsMatchActive =>
            Instance != null && (Instance.State == MatchState.MatchStart || Instance.State == MatchState.InProgress);
    }
}

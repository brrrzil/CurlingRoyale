using System;
using UnityEngine;

namespace CurlingRoyale.Game
{
    /// <summary>
    /// Управляет состоянием матча. Singleton — доступ через GameManager.Instance.
    /// FSM: Menu → MatchStart → InProgress → MatchEnd → Menu.
    /// События для UI подписки.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum MatchState
        {
            Menu,
            MatchStart,
            InProgress,
            MatchEnd,
        }

        public static GameManager Instance { get; private set; }

        [Header("Настройки матча")]
        [Tooltip("Длительность фазы одновременной зарядки перед разгоном (сек).")]
        [Min(0f)] public float chargePhaseDuration = 5f;

        [Tooltip("Сколько камней должно быть в матче.")]
        [Min(2)] public int expectedStoneCount = 8;

        public MatchState State { get; private set; } = MatchState.Menu;
        public float PhaseTimeRemaining { get; private set; }

        public event Action<MatchState> onStateChanged;
        public event Action<float> onPhaseTick;     // каждый кадр во время InProgress, значение — оставшееся время фазы
        public event Action<int> onAliveCountChanged; // (число живых камней)

        private int aliveCountCache = -1;
        private float lastTickTime;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── Управление состоянием ──────────────────────────────────

        public void StartMatch()
        {
            ChangeState(MatchState.MatchStart);
            PhaseTimeRemaining = chargePhaseDuration;
            lastTickTime = Time.time;
            // TODO: фаза "все готовятся" — зарядка
            Invoke(nameof(EnterInProgress), chargePhaseDuration);
        }

        public void EndMatch(int winnerId = -1)
        {
            ChangeState(MatchState.MatchEnd);
            // TODO: показать экран результатов
        }

        public void ReturnToMenu()
        {
            ChangeState(MatchState.Menu);
        }

        private void EnterInProgress()
        {
            ChangeState(MatchState.InProgress);
            PhaseTimeRemaining = 0f; // в этой фазе считаем не время, а число живых
        }

        // ─── Тик (вызывается из боевой системы или ежекадрово) ──────

        /// <summary>
        /// Сообщить GameManager-у текущее число живых камней.
        /// Если остался 1 — матч окончен.
        /// </summary>
        public void ReportAliveCount(int count)
        {
            if (count == aliveCountCache) return;
            aliveCountCache = count;
            onAliveCountChanged?.Invoke(count);

            if (State == MatchState.InProgress && count <= 1)
            {
                EndMatch();
            }
        }

        void Update()
        {
            if (State == MatchState.MatchStart && PhaseTimeRemaining > 0f)
            {
                PhaseTimeRemaining -= Time.deltaTime;
                onPhaseTick?.Invoke(PhaseTimeRemaining);
            }
        }

        private void ChangeState(MatchState newState)
        {
            State = newState;
            onStateChanged?.Invoke(newState);
        }

        // ─── Утилиты для UI ──────────────────────────────────────────

        public static bool IsMatchActive =>
            Instance != null && (Instance.State == MatchState.MatchStart || Instance.State == MatchState.InProgress);
    }
}

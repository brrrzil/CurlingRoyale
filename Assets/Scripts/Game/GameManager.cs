using System;
using UnityEngine;
using CurlingRoyale.Combat;

namespace CurlingRoyale.Game
{
    /// <summary>
    /// Управляет состоянием матча. Singleton — доступ через GameManager.Instance.
    /// FSM: Menu → MatchStart → InProgress → MatchEnd → Menu.
    /// Автоматически считает живых StoneCombat в сцене и завершает матч когда остался 1.
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

        public MatchState State { get; private set; } = MatchState.Menu;
        public float PhaseTimeRemaining { get; private set; }
        public int AliveCount { get; private set; }

        public event Action<MatchState> onStateChanged;
        public event Action<float> onPhaseTick;
        public event Action<int> onAliveCountChanged;

        private float aliveCountCheckInterval = 0.25f; // проверяем 4 раза в секунду
        private float nextAliveCheckTime;
        private bool hasStarted = false;

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
            hasStarted = true;
            ChangeState(MatchState.MatchStart);
            PhaseTimeRemaining = chargePhaseDuration;
            Invoke(nameof(EnterInProgress), chargePhaseDuration);
        }

        public void EndMatch(int winnerId = -1)
        {
            ChangeState(MatchState.MatchEnd);
            // PhaseTimeRemaining = 0; // можно показать экран результатов с таймером
        }

        public void ReturnToMenu()
        {
            hasStarted = false;
            ChangeState(MatchState.Menu);
        }

        private void EnterInProgress()
        {
            ChangeState(MatchState.InProgress);
            PhaseTimeRemaining = 0f;
            nextAliveCheckTime = Time.time;
        }

        // ─── Тик ─────────────────────────────────────────────────────

        void Update()
        {
            // Фаза одновременной зарядки: обратный отсчёт до разгона.
            if (State == MatchState.MatchStart && PhaseTimeRemaining > 0f)
            {
                PhaseTimeRemaining = Mathf.Max(0f, chargePhaseDuration - (Time.time - lastInvokeTime));
                onPhaseTick?.Invoke(PhaseTimeRemaining);
            }

            // В активной фазе — считаем живых камней на сцене.
            if (State == MatchState.InProgress && Time.time >= nextAliveCheckTime)
            {
                CountAliveStones();
                nextAliveCheckTime = Time.time + aliveCountCheckInterval;
            }
        }

        private float lastInvokeTime;

        private void ChangeState(MatchState newState)
        {
            State = newState;
            if (newState == MatchState.MatchStart) lastInvokeTime = Time.time;
            onStateChanged?.Invoke(newState);
        }

        private void CountAliveStones()
        {
            // Пересчитываем с небольшой задержкой — не каждый кадр, чтобы не нагружать.
            // FindObjectsByType — дорогая операция, но раз в 0.25 сек на 8 объектах это терпимо.
            StoneCombat[] stones = FindObjectsByType<StoneCombat>(FindObjectsSortMode.None);
            int alive = 0;
            for (int i = 0; i < stones.Length; i++)
            {
                if (stones[i] != null && !stones[i].IsDead) alive++;
            }

            if (alive == AliveCount) return;
            AliveCount = alive;
            onAliveCountChanged?.Invoke(alive);

            // Последний выжил — матч окончен. 0 — крайний случай, тоже финиш.
            if (alive <= 1 && hasStarted)
            {
                EndMatch();
            }
        }

        // ─── Утилиты для UI ──────────────────────────────────────────

        public static bool IsMatchActive =>
            Instance != null && (Instance.State == MatchState.MatchStart || Instance.State == MatchState.InProgress);
    }
}


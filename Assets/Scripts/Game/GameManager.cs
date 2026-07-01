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
            MatchEnd,
        }

        public static GameManager Instance { get; private set; }

        [Header("Настройки матча")]
        [Tooltip("Длительность фазы MatchStart (перед InProgress). Сейчас можно задать как 'tick before go'.")]
        [Min(0f)] public float chargePhaseDuration = 1.5f;

        [Header("Арена (опционально)")]
        [Tooltip("ScriptableObject с параметрами арены. Если задан — используется shrinking.")]
        [SerializeField] private ArenaConfig arenaConfig;

        [Tooltip("Border-объект, который будет регенериться при shrinking. " +
                 "Если null — арена остаётся статичной (без shrinking).")]
        [SerializeField] private ArenaBorder arenaBorder;

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

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
            // Решаем, стартовать ли сжатие.
            bool shouldShrink = arenaConfig.shrinkStartAliveCount > 0 &&
                                AliveCount > 0 &&
                                AliveCount <= arenaConfig.shrinkStartAliveCount;

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

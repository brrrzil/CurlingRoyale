using System;
using UnityEngine;

namespace CurlingRoyale.Skins
{
    /// <summary>
    /// Хранит выбор скина. Singleton (DontDestroyOnLoad), пишет в PlayerPrefs.
    /// При смене скина дёргает event — все DroneSkinApplier подписываются и применяют.
    ///
    /// Простой API:
    ///   SkinSelector.Instance.Current     — текущий DroneSkin (или null если реестр пуст)
    ///   SkinSelector.Instance.Next()      — переключить на следующий
    ///   SkinSelector.Instance.Select(id)  — выбрать по id
    /// </summary>
    [DisallowMultipleComponent]
    public class SkinSelector : MonoBehaviour
    {
        public static SkinSelector Instance { get; private set; }

        [Header("PlayerPrefs key")]
        [Tooltip("Ключ сохранения в PlayerPrefs.")]
        [SerializeField] private string prefsKey = "CurlingRoyale.SkinId";

        public event Action<DroneSkin> onSkinChanged;

        public DroneSkin Current { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Загрузить сохранённый выбор (если есть)
            var registry = DroneSkinsRegistry.Instance;
            if (registry == null || registry.Count == 0)
            {
                Debug.LogWarning("[SkinSelector] Реестр пуст — выбор скина недоступен.");
                return;
            }
            string savedId = PlayerPrefs.GetString(prefsKey, "");
            int idx = registry.GetIndexById(savedId);
            if (idx < 0) idx = 0; // первый по умолчанию
            Current = registry.Get(idx);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Переключить на следующий скин (зациклено).
        /// </summary>
        public void Next()
        {
            var registry = DroneSkinsRegistry.Instance;
            if (registry == null || registry.Count == 0) return;
            int curIdx = Current != null ? registry.GetIndexById(Current.skinId) : 0;
            int next = (curIdx + 1) % registry.Count;
            SelectByIndex(next);
        }

        /// <summary>
        /// Выбрать скин по индексу.
        /// </summary>
        public void SelectByIndex(int index)
        {
            var registry = DroneSkinsRegistry.Instance;
            if (registry == null) return;
            Current = registry.Get(index);
            if (Current == null) return;
            PlayerPrefs.SetString(prefsKey, Current.skinId);
            PlayerPrefs.Save();
            onSkinChanged?.Invoke(Current);
        }

        /// <summary>
        /// Выбрать скин по id.
        /// </summary>
        public void SelectById(string id)
        {
            var registry = DroneSkinsRegistry.Instance;
            if (registry == null) return;
            int idx = registry.GetIndexById(id);
            if (idx >= 0) SelectByIndex(idx);
        }
    }
}

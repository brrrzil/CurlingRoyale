using System.Collections.Generic;
using UnityEngine;

namespace CurlingRoyale.Skins
{
    /// <summary>
    /// Реестр всех скинов. Singleton (DontDestroyOnLoad). Держит SkinId → DroneSkin,
    /// отдаёт текущий выбранный. Выбор сохраняется в PlayerPrefs.
    ///
    /// Создание реестра: задать в инспекторе список skins[].
    /// Если skins[] пусто и autoLoadFromResources=true -- попробует загрузить все DroneSkin
    /// из Resources/Skins/.
    /// </summary>
    [CreateAssetMenu(menuName = "Curling Royale/Drone Skins Registry", fileName = "DroneSkinsRegistry")]
    public class DroneSkinsRegistry : ScriptableObject
    {
        [Tooltip("Список всех доступных скинов. Порядок = id индекса.")]
        [SerializeField] private DroneSkin[] skins;

        [Tooltip("Если skins[] пусто — загрузить все DroneSkin из Resources/Skins/.")]
        [SerializeField] private bool autoLoadFromResources = true;

        [Header("Runtime singleton")]
        [Tooltip("Реестр, который используется во время игры. Если null — грузится из Resources.")]
        [SerializeField] private static DroneSkinsRegistry runtimeInstance;

        public static DroneSkinsRegistry Instance
        {
            get
            {
                if (runtimeInstance == null)
                {
                    runtimeInstance = Resources.Load<DroneSkinsRegistry>("Skins/DefaultSkinsRegistry");
                    if (runtimeInstance == null && autoLoadFromResources_Internal)
                    {
                        // fallback: загружаем все .asset из Resources/Skins/ напрямую
                        var all = Resources.LoadAll<DroneSkin>("Skins");
                        if (all != null && all.Length > 0)
                        {
                            runtimeInstance = CreateInstance<DroneSkinsRegistry>();
                            runtimeInstance.skins = all;
                        }
                    }
                }
                return runtimeInstance;
            }
        }

        private static bool autoLoadFromResources_Internal = true;

        public static void SetInstance(DroneSkinsRegistry registry)
        {
            runtimeInstance = registry;
        }

        public IReadOnlyList<DroneSkin> All
        {
            get
            {
                EnsureLoaded();
                return skins ?? System.Array.Empty<DroneSkin>();
            }
        }

        public int Count => All.Count;

        public DroneSkin Get(int index)
        {
            EnsureLoaded();
            if (skins == null || skins.Length == 0) return null;
            return skins[Mathf.Clamp(index, 0, skins.Length - 1)];
        }

        public DroneSkin GetById(string id)
        {
            EnsureLoaded();
            if (skins == null) return null;
            for (int i = 0; i < skins.Length; i++)
            {
                if (skins[i] != null && skins[i].skinId == id) return skins[i];
            }
            return null;
        }

        public int GetIndexById(string id)
        {
            EnsureLoaded();
            if (skins == null) return -1;
            for (int i = 0; i < skins.Length; i++)
            {
                if (skins[i] != null && skins[i].skinId == id) return i;
            }
            return -1;
        }

        private void EnsureLoaded()
        {
            if (skins != null && skins.Length > 0) return;
            if (!autoLoadFromResources) return;
            var all = Resources.LoadAll<DroneSkin>("Skins");
            if (all != null && all.Length > 0) skins = all;
        }
    }
}

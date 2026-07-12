using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CurlingRoyale.UI
{
    /// <summary>
    /// Биндер кнопок MainMenu.
    ///
    /// Два режима:
    /// 1) **Ручные bindings** в Inspector (рекомендуется): массив `bindings`.
    ///    Каждый binding -- кнопка + опционально панель.
    ///    -- panel != null: клик toggle'ит панель.
    ///    -- panel == null: ничего не делает (используй OnClick в Inspector
    ///       для вызова публичных методов: OnPlay, OnFullScreen, OnExit).
    ///
    /// 2) **Авто-режим** (если bindings пустое): для каждой Button в сцене
    ///    ищется панель по имени (SettingsButton -> Settings/SettingsPanel).
    ///    Если панели нет -- ничего не делает.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuButtonBinder : MonoBehaviour
    {
        [System.Serializable]
        public class Binding
        {
            [Tooltip("Кнопка в MainMenu.")]
            public Button button;

            [Tooltip("Панель, которую открывает/закрывает кнопка. null = клик игнорируется биндером (используй OnClick в Inspector).")]
            public GameObject panel;
        }

        [Header("Ручная привязка (если пусто -- авто-режим)")]
        [SerializeField] private Binding[] bindings;

        private void Start()
        {
            // Скрываем все панели при старте
            if (bindings != null && bindings.Length > 0)
            {
                foreach (var b in bindings)
                {
                    if (b != null && b.panel != null) b.panel.SetActive(false);
                }
            }
            else
            {
                foreach (var panel in FindAllPanels())
                {
                    if (panel != null) panel.SetActive(false);
                }
            }

            // Привязываем кнопки (только те, у которых есть панель в bindings)
            if (bindings != null && bindings.Length > 0)
            {
                foreach (var b in bindings)
                {
                    if (b == null || b.button == null || b.panel == null) continue;
                    var captured = b;
                    b.button.onClick.AddListener(() => OnBindingClicked(captured));
                }
            }
            else
            {
                // Авто-режим: каждая Button ищет панель по имени
                var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var btn in buttons)
                {
                    if (btn == null) continue;
                    var panel = FindPanelForButton(btn.gameObject.name);
                    if (panel == null) continue;
                    string capturedName = btn.gameObject.name;
                    btn.onClick.AddListener(() => OnAutoButtonClicked(capturedName));
                }
            }
        }

        void OnBindingClicked(Binding b)
        {
            if (b.panel != null) TogglePanel(b.panel);
        }

        void OnAutoButtonClicked(string buttonName)
        {
            var panel = FindPanelForButton(buttonName);
            if (panel != null) TogglePanel(panel);
        }

        // ─── Toggle / close panel ───
        void TogglePanel(GameObject panel)
        {
            bool willBeActive = !panel.activeSelf;
            if (willBeActive)
            {
                CloseAllPanelsExcept(panel);
                panel.SetActive(true);
                WireCloseButton(panel);
                CreateBackdrop(panel);
            }
            else
            {
                CloseBackdropFor(panel);
                panel.SetActive(false);
            }
        }

        void CloseAllPanelsExcept(GameObject except)
        {
            if (bindings != null && bindings.Length > 0)
            {
                foreach (var b in bindings)
                {
                    if (b == null || b.panel == null || b.panel == except) continue;
                    CloseBackdropFor(b.panel);
                    b.panel.SetActive(false);
                }
            }
            else
            {
                foreach (var p in FindAllPanels())
                {
                    if (p == null || p == except) continue;
                    CloseBackdropFor(p);
                    p.SetActive(false);
                }
            }
        }

        void WireCloseButton(GameObject panel)
        {
            var buttons = panel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                string n = btn.gameObject.name.ToLower();
                if (n == "close" || n == "closebutton" || n == "x" || n == "back" || n.Contains("close"))
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => {
                        CloseBackdropFor(panel);
                        panel.SetActive(false);
                    });
                }
            }
        }

        // ─── Backdrop ───
        private readonly Dictionary<GameObject, GameObject> backdrops = new Dictionary<GameObject, GameObject>();

        void CreateBackdrop(GameObject panel)
        {
            if (backdrops.ContainsKey(panel)) return;
            var canvas = panel.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var go = new GameObject("Backdrop_" + panel.name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsFirstSibling();

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0.4f);

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => {
                CloseBackdropFor(panel);
                panel.SetActive(false);
            });

            backdrops[panel] = go;
        }

        void CloseBackdropFor(GameObject panel)
        {
            if (backdrops.TryGetValue(panel, out var bg))
            {
                if (bg != null) Destroy(bg);
                backdrops.Remove(panel);
            }
        }

        // ─── Авто-поиск панели ───
        GameObject FindPanelForButton(string buttonName)
        {
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            var candidates = new List<string> { buttonName, buttonName + "Panel" };
            string stripped = buttonName;
            if (stripped.EndsWith("Button")) stripped = stripped.Substring(0, stripped.Length - 6);
            if (stripped != buttonName)
            {
                candidates.Add(stripped);
                candidates.Add(stripped + "Panel");
            }
            foreach (var name in candidates)
            {
                foreach (var go in all)
                {
                    if (go == null || go.name != name) continue;
                    if (!go.scene.IsValid()) continue;
                    if (IsPanel(go)) return go;
                }
            }
            return null;
        }

        bool IsPanel(GameObject go)
        {
            if (go.GetComponent<Button>() != null) return false;
            return go.GetComponent<Image>() != null;
        }

        IEnumerable<GameObject> FindAllPanels()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                var images = canvas.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    var go = img.gameObject;
                    if (go.GetComponent<Button>() != null) continue;
                    yield return go;
                }
            }
        }

        // ─── Публичные методы для Button.OnClick() в Inspector ───
        // Перетащи MenuButtonBinder GameObject в OnClick(), выбери один из этих методов.

        /// <summary>Загрузить игровую сцену.</summary>
        public void OnPlay()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
        }

        /// <summary>Переключить fullscreen. Работает только в WebGL билде с fullscreen-template.</summary>
        public void OnFullScreen()
        {
            Screen.fullScreen = !Screen.fullScreen;
        }

        /// <summary>Выйти из игры.</summary>
        public void OnExit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}

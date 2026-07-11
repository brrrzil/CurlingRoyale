using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using CurlingRoyale.Skins;
using CurlingRoyale.Audio;

namespace CurlingRoyale.UI
{
    /// <summary>
    /// Менеджер стартовой сцены (MainMenu):
    ///   - Кнопка PLAY -- загружает GameScene.
    ///   - Магазин скинов -- Prev/Next + превью + название.
    ///   - Кнопки Mute Music / Mute SFX (на будущее).
    ///
    /// Повесить на GameObject "MainMenuManager" в сцене MainMenu.
    /// Все ссылки на UI -- через inspector.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Кнопки")]
        [SerializeField] private Button playButton;

        [Header("Магазин скинов")]
        [SerializeField] private TextMeshProUGUI skinNameText;
        [SerializeField] private Image skinPreviewImage; // UI Image -- красится в coreColor
        [SerializeField] private Button prevSkinButton;
        [SerializeField] private Button nextSkinButton;

        [Header("Настройки (опционально)")]
        [SerializeField] private Toggle musicToggle;
        [SerializeField] private Toggle sfxToggle;

        [Header("Сцена игры")]
        [Tooltip("Имя сцены, которую грузим по кнопке Play. Должна быть в Build Settings.")]
        [SerializeField] private string gameSceneName = "GameScene";

        void Start()
        {
            if (playButton != null) playButton.onClick.AddListener(OnPlayPressed);
            if (prevSkinButton != null) prevSkinButton.onClick.AddListener(OnPrevSkin);
            if (nextSkinButton != null) nextSkinButton.onClick.AddListener(OnNextSkin);

            // Подпишемся на смену скина (если SkinSelector уже создан)
            if (SkinSelector.Instance != null)
                SkinSelector.Instance.onSkinChanged += OnSkinChanged;

            RefreshSkinDisplay();

            // Загрузим настройки звука (если есть)
            if (musicToggle != null)
            {
                musicToggle.isOn = PlayerPrefs.GetInt("CurlingRoyale.MusicOn", 1) == 1;
                musicToggle.onValueChanged.AddListener(OnMusicToggle);
            }
            if (sfxToggle != null)
            {
                sfxToggle.isOn = PlayerPrefs.GetInt("CurlingRoyale.SfxOn", 1) == 1;
                sfxToggle.onValueChanged.AddListener(OnSfxToggle);
            }
        }

        void OnDestroy()
        {
            if (SkinSelector.Instance != null)
                SkinSelector.Instance.onSkinChanged -= OnSkinChanged;
        }

        void OnPlayPressed()
        {
            SceneManager.LoadScene(gameSceneName);
        }

        void OnPrevSkin()
        {
            var reg = DroneSkinsRegistry.Instance;
            if (reg == null || reg.Count == 0) return;
            int cur = SkinSelector.Instance != null && SkinSelector.Instance.Current != null
                ? reg.GetIndexById(SkinSelector.Instance.Current.skinId) : 0;
            int prev = (cur - 1 + reg.Count) % reg.Count;
            if (SkinSelector.Instance != null) SkinSelector.Instance.SelectByIndex(prev);
            else RefreshSkinDisplay();
        }

        void OnNextSkin()
        {
            var reg = DroneSkinsRegistry.Instance;
            if (reg == null || reg.Count == 0) return;
            int cur = SkinSelector.Instance != null && SkinSelector.Instance.Current != null
                ? reg.GetIndexById(SkinSelector.Instance.Current.skinId) : 0;
            int next = (cur + 1) % reg.Count;
            if (SkinSelector.Instance != null) SkinSelector.Instance.SelectByIndex(next);
            else RefreshSkinDisplay();
        }

        void OnSkinChanged(DroneSkin newSkin)
        {
            RefreshSkinDisplay();
        }

        void RefreshSkinDisplay()
        {
            DroneSkin current = null;
            if (SkinSelector.Instance != null && SkinSelector.Instance.Current != null)
                current = SkinSelector.Instance.Current;
            else
            {
                var reg = DroneSkinsRegistry.Instance;
                if (reg != null && reg.Count > 0) current = reg.Get(0);
            }

            if (current == null) return;

            if (skinNameText != null) skinNameText.text = current.skinName;
            if (skinPreviewImage != null) skinPreviewImage.color = current.coreColor;
        }

        void OnMusicToggle(bool on)
        {
            PlayerPrefs.SetInt("CurlingRoyale.MusicOn", on ? 1 : 0);
            PlayerPrefs.Save();
            if (MusicManager.Instance != null)
            {
                if (on) MusicManager.Instance.ResumeMusic();
                else MusicManager.Instance.PauseMusic();
            }
        }

        void OnSfxToggle(bool on)
        {
            PlayerPrefs.SetInt("CurlingRoyale.SfxOn", on ? 1 : 0);
            PlayerPrefs.Save();
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.SetVolume(on ? 0.7f : 0f);
            }
        }
    }
}

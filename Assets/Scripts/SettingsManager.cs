using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// MainMenu (veya Gameplay) sahnesindeki bir "SettingsPanel" üzerine eklenir.
// Inspector'dan slider/dropdown/toggle referanslarını sürükleyin.
//
// NOT (ayar genişletmesi): Bu bileşen artık değerleri SettingsRegistry'den okur/yazar; böylece
// Ayarlar panelindeki (SettingsPanel) yeni denetimlerle AYNI kaynağı paylaşır ve iki arayüz
// birbirini anında yansıtır. Eski alan adları, eski PlayerPrefs anahtarları ("MusicVolume",
// "SfxVolume", "Language") ve genel API korunmuştur — mevcut sahne bağlantıları çalışmaya devam eder.
public class SettingsManager : MonoBehaviour
{
    [Header("Ses")]
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("Görüntü")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;

    [Header("Dil")]
    public TMP_Dropdown languageDropdown;

    // --- YENİ (opsiyonel): Inspector'da boş bırakılabilir ---
    [Header("Ses (genişletme)")]
    public Slider uiVolumeSlider;
    public Slider masterVolumeSlider;
    public Toggle muteWhenUnfocusedToggle;

    private Resolution[] resolutions;
    private bool _suppress;   // geri besleme döngüsünü engeller

    private static readonly string[] LanguageNames = { "Türkçe", "English", "Deutsch", "中文", "Português" };

    void OnEnable()
    {
        SettingsEffects.Ensure();          // ayarları açılışta uygula (kalıcı, tek örnek)
        SettingsRegistry.Changed += OnSettingChanged;

        LocalizationManager.LoadSavedLanguage();
        SetupResolutions();
        SetupVolume();
        SetupLanguage();
        SetupFullscreen();
    }

    void OnDisable()
    {
        SettingsRegistry.Changed -= OnSettingChanged;
    }

    /// <summary>Ayar başka bir arayüzden değişirse slider'ları eşitle.</summary>
    private void OnSettingChanged(SettingsRegistry.Setting s)
    {
        if (s == null) return;
        _suppress = true;
        try
        {
            switch (s.Key)
            {
                case SettingsRegistry.K_MUSIC:
                    if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(SettingsRegistry.MusicVolume);
                    break;
                case SettingsRegistry.K_SFX:
                    if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(SettingsRegistry.SfxVolume);
                    break;
                case SettingsRegistry.K_UI_VOL:
                    if (uiVolumeSlider != null) uiVolumeSlider.SetValueWithoutNotify(SettingsRegistry.UiVolume);
                    break;
                case SettingsRegistry.K_MASTER:
                    if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(SettingsRegistry.MasterVolume);
                    break;
                case SettingsRegistry.K_MUTE_UNFOCUSED:
                    if (muteWhenUnfocusedToggle != null) muteWhenUnfocusedToggle.SetIsOnWithoutNotify(SettingsRegistry.MuteWhenUnfocused);
                    break;
            }
        }
        finally { _suppress = false; }
    }

    private void SetupResolutions()
    {
        if (resolutionDropdown == null) return;

        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        var options = new List<string>();
        int currentIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            options.Add($"{resolutions[i].width} x {resolutions[i].height}");
            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                currentIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentIndex;
        resolutionDropdown.RefreshShownValue();
        resolutionDropdown.onValueChanged.RemoveAllListeners();
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    private void SetResolution(int index)
    {
        if (resolutions == null || index < 0 || index >= resolutions.Length) return;
        var r = resolutions[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode);
    }

    private void SetupFullscreen()
    {
        if (fullscreenToggle == null) return;
        fullscreenToggle.isOn = Screen.fullScreen;
        fullscreenToggle.onValueChanged.RemoveAllListeners();
        fullscreenToggle.onValueChanged.AddListener(v => Screen.fullScreen = v);
    }

    private void SetupVolume()
    {
        // Müzik ve SFX: eski anahtarlar korunur ("MusicVolume" / "SfxVolume").
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = SettingsRegistry.MusicVolume;
            musicVolumeSlider.onValueChanged.RemoveAllListeners();
            musicVolumeSlider.onValueChanged.AddListener(v =>
            {
                if (_suppress) return;
                SettingsRegistry.SetFloat(SettingsRegistry.K_MUSIC, v);   // AudioManager'a anında uygulanır
            });
        }
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = SettingsRegistry.SfxVolume;
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider.onValueChanged.AddListener(v =>
            {
                if (_suppress) return;
                SettingsRegistry.SetFloat(SettingsRegistry.K_SFX, v);
            });
        }
        // YENİ kanallar: arayüz sesi (tık sesleri) ve ana ses.
        if (uiVolumeSlider != null)
        {
            uiVolumeSlider.value = SettingsRegistry.UiVolume;
            uiVolumeSlider.onValueChanged.RemoveAllListeners();
            uiVolumeSlider.onValueChanged.AddListener(v =>
            {
                if (_suppress) return;
                SettingsRegistry.SetFloat(SettingsRegistry.K_UI_VOL, v);
            });
        }
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = SettingsRegistry.MasterVolume;
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
            masterVolumeSlider.onValueChanged.AddListener(v =>
            {
                if (_suppress) return;
                SettingsRegistry.SetFloat(SettingsRegistry.K_MASTER, v);
            });
        }
        if (muteWhenUnfocusedToggle != null)
        {
            muteWhenUnfocusedToggle.SetIsOnWithoutNotify(SettingsRegistry.MuteWhenUnfocused);
            muteWhenUnfocusedToggle.onValueChanged.RemoveAllListeners();
            muteWhenUnfocusedToggle.onValueChanged.AddListener(v =>
            {
                if (_suppress) return;
                SettingsRegistry.SetBool(SettingsRegistry.K_MUTE_UNFOCUSED, v);
            });
        }
    }

    private void SetupLanguage()
    {
        if (languageDropdown == null) return;
        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(new List<string>(LanguageNames));
        languageDropdown.value = Mathf.Clamp((int)LocalizationManager.CurrentLanguage, 0, LanguageNames.Length - 1);
        languageDropdown.RefreshShownValue();
        languageDropdown.onValueChanged.RemoveAllListeners();
        languageDropdown.onValueChanged.AddListener(i =>
        {
            if (_suppress) return;
            if (i < 0 || i >= LanguageNames.Length) return;
            LocalizationManager.SetLanguage((Language)i);
        });
    }
}

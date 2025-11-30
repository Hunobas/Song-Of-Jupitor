using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class LocalizationManager : PersistentSingleton<LocalizationManager>
{
    public enum Language
    {
        KO = 0,
        EN = 1,
        // JA = 2,
    }

    [Header("Defaults")]
    [SerializeField] private Language _defaultLanguage = Language.KO;

    public Language Current { get; private set; }
    public event Action OnLocaleChanged;

    const string PlayerPrefsLang = "loc.lang";

    void Awake()
    {
        // 마지막 언어 복원
        if (PlayerPrefs.HasKey(PlayerPrefsLang))
        {
            int saved = PlayerPrefs.GetInt(PlayerPrefsLang, (int)_defaultLanguage);
            if (Enum.IsDefined(typeof(Language), saved)) _defaultLanguage = (Language)saved;
        }
        SetLocale(_defaultLanguage, invokeEvent: false);
    }

#if DEVBUILD
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.U)) CycleLanguage();
    }
#endif

    public void CycleLanguage()
    {
        var values = (Language[])Enum.GetValues(typeof(Language));
        int idx = Array.IndexOf(values, Current);
        int next = (idx + 1) % values.Length;
        SetLocale(values[next]);
    }

    public void SetLocale(Language lang, bool invokeEvent = true)
    {
        if (Current == lang) return;

        Current = lang;
        PlayerPrefs.SetInt(PlayerPrefsLang, (int)Current);
        PlayerPrefs.Save();

        if (invokeEvent) OnLocaleChanged?.Invoke();

        LocalizationRegistry.RescanSceneAndRegisterAll();
        LocalizationRegistry.RefreshAll();

        ConsoleLogger.Log($"[Localization] 현재 언어: {Current}");
    }

    public string GetString(string key, params object[] args)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        if (DBManager.Instance != null &&
            DBManager.Instance.TryGetPanelUIText(key, out var s))
            return Format(s, args);

        if (DBManager.Instance != null)
        {
            foreach (Dictionary<string, string> dict in DBManager.Instance.EnumerateAllPanelUIDicts())
            {
                if (dict != null && dict.TryGetValue(key, out var fs))
                    return Format(fs, args);
            }
        }

        ConsoleLogger.LogWarning($"[Localization] Missing key '{key}' for {Current}");
        return $"#{key}";

        static string Format(string raw, object[] a)
            => (a == null || a.Length == 0) ? raw : string.Format(raw, a);
    }
}

public static class L10n {
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static string T(string key) => LocalizationManager.Instance?.GetString(key) ?? $"#{key}";
    public static string T(string key, params object[] args) => LocalizationManager.Instance?.GetString(key, args) ?? $"#{key}";
}
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
[DisallowMultipleComponent]
public sealed class LocalizedTMP : MonoBehaviour, ILocalizable
{
    [Header("String Key")]
    [SerializeField] private string key;

    [SerializeField] private string[] initialArgs;

    [Header("Profiles")]
    [SerializeField] private LayoutProfile defaultProfile;

    [Serializable]
    public struct LocaleProfilePair
    {
        public LocalizationManager.Language language;
        public LayoutProfile profile;
    }
    [SerializeField] private List<LocaleProfilePair> perLocaleProfiles = new();

    [Header("Utilities")]
    [SerializeField] private bool safeAutoFitDown = false;
    [SerializeField, Min(8)] private int safeAutoFitMin = 10;

    TMP_Text _tmp;
    object[] _args;

    void Awake()
    {
        _tmp = GetComponent<TMP_Text>();
        _args = (initialArgs != null && initialArgs.Length > 0) ? Array.ConvertAll(initialArgs, a => (object)a) : null;

        // 레지스트리 등록 (비활성 오브젝트도 Awake는 호출되므로 누락 방지)
        LocalizationRegistry.Register(this);

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLocaleChanged += Refresh;
    }

    void OnEnable() => Refresh();

    void OnDestroy()
    {
        LocalizationRegistry.Unregister(this);
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLocaleChanged -= Refresh;
    }

    // ILocalizable
    public void RefreshLocale() => Refresh();

    public void SetArgs(params object[] args) { _args = args; Refresh(); }
    public void SetKey(string newKey, params object[] args) { key = newKey; _args = args; Refresh(); }

    public void Refresh()
    {
        if (_tmp == null) _tmp = GetComponent<TMP_Text>();

        string text = GetLocalizedString(key, _args);
        _tmp.text = text ?? string.Empty;

        ApplyProfileForCurrentLocale();

        if (safeAutoFitDown && !_tmp.enableAutoSizing)
            ShrinkToFitIfNeeded();
    }

    string GetLocalizedString(string k, object[] args)
    {
        if (string.IsNullOrEmpty(k)) return string.Empty;
        if (LocalizationManager.Instance == null) return $"#{k}";
        return LocalizationManager.Instance.GetString(k, args);
    }

    void ApplyProfileForCurrentLocale()
    {
        var prof = defaultProfile;
        if (LocalizationManager.Instance != null)
        {
            var lang = LocalizationManager.Instance.Current;
            foreach (var p in perLocaleProfiles)
                if (p.language.Equals(lang)) { prof = p.profile ?? prof; break; }
        }
        prof?.ApplyTo(_tmp);
    }

    void ShrinkToFitIfNeeded()
    {
        var rect = _tmp.rectTransform.rect;
        var preferred = _tmp.GetPreferredValues(_tmp.text);
        if (preferred.x <= rect.width && preferred.y <= rect.height) return;

        int fs = Mathf.RoundToInt(_tmp.fontSize);
        while (fs > safeAutoFitMin)
        {
            fs--;
            _tmp.fontSize = fs;
            preferred = _tmp.GetPreferredValues(_tmp.text);
            if (preferred.x <= rect.width && preferred.y <= rect.height) break;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying) return;
        if (!_tmp) _tmp = GetComponent<TMP_Text>();
        if (_tmp && !string.IsNullOrEmpty(key))
            _tmp.text = key.StartsWith("#") ? key : $"#{key}";
    }
#endif
}

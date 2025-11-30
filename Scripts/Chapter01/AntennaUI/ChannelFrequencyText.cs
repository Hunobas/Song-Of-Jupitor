using TMPro;
using UnityEngine;
using System.Text;
using System.Text.RegularExpressions;
using Sirenix.OdinInspector;

public class ChannelFrequencyText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _bodyText;
    [SerializeField] private TextMeshProUGUI _followText;
    [SerializeField] private RotationKnob _channelKnob;
    
    [HideIf("@_followText != null")] [Header("Glitch Settings")]
    [SerializeField] private int maxLength = 5;

    static readonly string[] GlitchTokens =
    {
        "!", "@", "#", "$", "%", "&", "?", "*", "+", "-", "=",
        "0","1","2","3","4","5","6","7","8","9",
        "null"
    };

    void Awake()
    {
        Debug.Assert(_bodyText != null);
    }

    void Start()
    {
        UpdateBodyText();
    }

    private void OnEnable()
    {
        UpdateBodyText();
        
        if (_channelKnob != null)
            _channelKnob.OnValueChanged += HandleKnobChanged;
    }

    private void OnDisable()
    {
        if (_channelKnob != null)
            _channelKnob.OnValueChanged -= HandleKnobChanged;
    }

    void HandleKnobChanged(float val)
    {
        if (_followText != null)
        {
            UpdateBodyText();
        }
        else
        {
            GenerateGlitchText();
        }
    }

    void UpdateBodyText()
    {
        if (_bodyText == null)
            return;

        if (_followText != null)
        {
            string src = _followText.text;
            Match m = Regex.Match(src, @"\d{1,3}");
            _bodyText.text = m.Success ? m.Value : "---";
        }
        else
        {
            _bodyText.text = "---";
        }
    }

    void GenerateGlitchText()
    {
        if (_bodyText == null)
            return;

        StringBuilder sb = new StringBuilder();
        while (sb.Length < maxLength)
        {
            string token = GlitchTokens[UnityEngine.Random.Range(0, GlitchTokens.Length)];
            int remaining = maxLength - sb.Length;

            if (token.Length > remaining)
                break;

            sb.Append(token);
        }

        _bodyText.text = sb.ToString();
    }
}

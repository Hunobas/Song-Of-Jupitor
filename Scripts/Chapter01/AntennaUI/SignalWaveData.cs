using System;
using UnityEngine;

[Serializable]
public struct WaveData
{
    [Range(WaveConstants.FrequencyMin, WaveConstants.FrequencyMax)]
    public float Frequency;

    [Range(WaveConstants.AmplitudeMin, WaveConstants.AmplitudeMax)]
    public float AmplitudeNoRender;

    [Range(WaveConstants.DisplacementMin, WaveConstants.DisplacementMax)]
    public float DisplacementNoRender;

    public static WaveData Default => new WaveData
    {
        Frequency = 1f,
        AmplitudeNoRender = 0.5f,
        DisplacementNoRender = 0f
    };
}

[CreateAssetMenu(fileName = "SignalWave", menuName ="Custom Assets/Signal Wave")]
public class SignalWaveData : ScriptableObject
{
    [SerializeField] public WaveData[] Current = new WaveData[WaveConstants.CctvCount];
    [SerializeField] public WaveData[] Answers = new WaveData[WaveConstants.CctvCount];

    private void OnEnable()
    {
        if (Current == null || Current.Length != WaveConstants.CctvCount)
            Current = new WaveData[WaveConstants.CctvCount];

        if (Answers == null || Answers.Length != WaveConstants.CctvCount)
            Answers = new WaveData[WaveConstants.CctvCount];

        for (int i = 0; i < Current.Length; i++)
        {
            if (Current[i].Frequency == 0f && Current[i].AmplitudeNoRender == 0f && Current[i].DisplacementNoRender == 0f)
                Current[i] = WaveData.Default;

            if (Answers[i].Frequency == 0f && Answers[i].AmplitudeNoRender == 0f && Answers[i].DisplacementNoRender == 0f)
                Answers[i] = WaveData.Default;
        }
    }

    public void SetWave(int index, WaveData data)
    {
        if (index < 0 || index >= Current.Length) return;
        Current[index] = data;
    }

    public WaveData GetWave(int index)
    {
        if (index < 0 || index >= Current.Length) return WaveData.Default;
        return Current[index];
    }

    public void SetAnswer(int index, WaveData data)
    {
        if (index < 0 || index >= Answers.Length) return;
        Answers[index] = data;
    }

    public WaveData GetAnswer(int index)
    {
        if (index < 0 || index >= Answers.Length) return WaveData.Default;
        return Answers[index];
    }

    public void SyncAnswers()
    {
        for (int i = 0; i < Current.Length && i < Answers.Length; i++)
        {
            Current[i] = Answers[i];
        }
    }

    public WaveData[] SnapshotCurrent() => (WaveData[])Current.Clone();
    public WaveData[] SnapshotAnswers() => (WaveData[])Answers.Clone();
}
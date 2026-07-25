using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CharacterAiPerfSettings",
    menuName = "DungeonStory/AI/Performance Settings")]
public sealed class CharacterAiPerfSettingsSO : ScriptableObject
{
    [Header("Profile Targets")]
    [SerializeField, Min(1)] private int realPlayActorTarget = 100;
    [SerializeField, Min(1)] private int stressActorTarget = 300;
    [SerializeField, Min(0.1f)] private float targetSchedulerAverageMs = 2f;
    [SerializeField, Min(0.1f)] private float targetSchedulerP95Ms = 4f;
    [SerializeField, Min(1f)] private float targetGcKbPerFrame = 64f;

    [Header("Tick LOD")]
    [SerializeField, Min(0.01f)] private float selectedTickInterval = 0.15f;
    [SerializeField, Min(0.01f)] private float visibleTickInterval = 0.35f;
    [SerializeField, Min(0.1f)] private float offscreenIdleTickInterval = 1.5f;
    [SerializeField, Min(0.1f)] private float offscreenLongWorkTickInterval = 4f;

    public int RealPlayActorTarget => realPlayActorTarget;
    public int StressActorTarget => stressActorTarget;
    public float TargetSchedulerAverageMs => targetSchedulerAverageMs;
    public float TargetSchedulerP95Ms => targetSchedulerP95Ms;
    public float TargetGcKbPerFrame => targetGcKbPerFrame;
    public float SelectedTickInterval => selectedTickInterval;
    public float VisibleTickInterval => visibleTickInterval;
    public float OffscreenIdleTickInterval => offscreenIdleTickInterval;
    public float OffscreenLongWorkTickInterval => offscreenLongWorkTickInterval;
}

[Serializable]
public sealed class CharacterAiPerformanceReport
{
    public bool valid;
    public int actorCount;
    public int sampleFrames;
    public CharacterAiPerformanceMetric scheduler = new CharacterAiPerformanceMetric("Scheduler");
    public CharacterAiPerformanceMetric behaviorTree = new CharacterAiPerformanceMetric("BT");
    public CharacterAiPerformanceMetric pathBroker = new CharacterAiPerformanceMetric("Grid.SearchPath");
    public CharacterAiPerformanceMetric garbageCollection = new CharacterAiPerformanceMetric("GC");
    public int brokerSearches;
    public int brokerCacheHits;
    public int brokerBudgetDeferrals;
    public string summary;
}

[Serializable]
public sealed class CharacterAiPerformanceMetric
{
    public string name;
    public double average;
    public double p95;
    public double max;
    public long gcBytes;

    public CharacterAiPerformanceMetric(string name)
    {
        this.name = name;
    }
}

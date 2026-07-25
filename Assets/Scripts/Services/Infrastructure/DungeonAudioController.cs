using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer.Unity;

public enum DungeonAudioCue
{
    UiClick,
    Confirm,
    Warning,
    Impact,
    Victory,
    Defeat
}

public interface IDungeonAudioService
{
    bool IsReady { get; }
    int PlayedCueCount { get; }
    DungeonAudioCue LastCue { get; }
    void Play(DungeonAudioCue cue);
}

public sealed class DungeonAudioController :
    IDungeonAudioService,
    IStartable,
    IDisposable
{
    private const string LibraryResourcePath = "Audio/DungeonAudioLibrary";
    private const string MixerResourcePath = "Audio/DungeonAudioMixer";
    private const int SampleRate = 22050;

    private readonly IDungeonUserSettingsService settingsService;
    private readonly IGameEventBus gameEventBus;
    private readonly IUiClock uiClock;
    private readonly IResourcesAssetLoader resourcesAssetLoader;
    private readonly Dictionary<int, ButtonBinding> buttonBindings = new Dictionary<int, ButtonBinding>();
    private readonly Dictionary<DungeonAudioCue, AudioClip> fallbackCues = new Dictionary<DungeonAudioCue, AudioClip>();
    private readonly List<AudioClip> ownedClips = new List<AudioClip>();
    private readonly List<GameObject> sceneRootBuffer = new List<GameObject>();
    private readonly List<Button> buttonScanBuffer = new List<Button>();

    private DungeonAudioLibrarySO library;
    private GameObject root;
    private AudioSource musicSource;
    private AudioSource ambienceSource;
    private AudioSource effectsSource;
    private AudioSource uiSource;
    private DungeonAudioRuntimeBehaviour runtimeBehaviour;
    private bool started;
    private float nextButtonScanAt;
    private float lastWarningAt = -10f;
    private IDisposable ownerRunEndedSubscription;
    private IDisposable researchCompletedSubscription;
    private IDisposable invasionStartedSubscription;
    private IDisposable bossInvasionStartedSubscription;
    private IDisposable invasionFacilityDamagedSubscription;

    public DungeonAudioController(
        IDungeonUserSettingsService settingsService,
        IGameEventBus gameEventBus,
        IUiClock uiClock,
        IResourcesAssetLoader resourcesAssetLoader)
    {
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        this.resourcesAssetLoader = resourcesAssetLoader
            ?? throw new ArgumentNullException(nameof(resourcesAssetLoader));
    }

    public bool IsReady => started && root != null;
    public int PlayedCueCount { get; private set; }
    public DungeonAudioCue LastCue { get; private set; }

    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        library = resourcesAssetLoader.LoadOptional<DungeonAudioLibrarySO>(LibraryResourcePath);
        CreateRuntimeAudio();
        DungeonUserSettingsRuntime.Changed += ApplyVolumes;
        researchCompletedSubscription =
            gameEventBus.Subscribe<BlueprintResearchCompletedEvent>(OnTriggerEvent);
        invasionStartedSubscription =
            gameEventBus.Subscribe<InvasionStartedEvent>(OnTriggerEvent);
        invasionFacilityDamagedSubscription =
            gameEventBus.Subscribe<InvasionFacilityDamagedEvent>(OnTriggerEvent);
        bossInvasionStartedSubscription =
            gameEventBus.Subscribe<BossInvasionStartedEvent>(OnTriggerEvent);
        ownerRunEndedSubscription = gameEventBus.Subscribe<OwnerRunEndedEvent>(OnTriggerEvent);
        ApplyVolumes();
        ScanButtons();
    }

    public void Dispose()
    {
        if (!started)
        {
            return;
        }

        DungeonUserSettingsRuntime.Changed -= ApplyVolumes;
        researchCompletedSubscription?.Dispose();
        researchCompletedSubscription = null;
        invasionStartedSubscription?.Dispose();
        invasionStartedSubscription = null;
        invasionFacilityDamagedSubscription?.Dispose();
        invasionFacilityDamagedSubscription = null;
        bossInvasionStartedSubscription?.Dispose();
        bossInvasionStartedSubscription = null;
        ownerRunEndedSubscription?.Dispose();
        ownerRunEndedSubscription = null;

        foreach (ButtonBinding binding in buttonBindings.Values)
        {
            binding.Dispose();
        }

        buttonBindings.Clear();
        if (root != null)
        {
            UnityEngine.Object.Destroy(root);
        }

        foreach (AudioClip clip in ownedClips.Where(clip => clip != null))
        {
            UnityEngine.Object.Destroy(clip);
        }

        ownedClips.Clear();
        fallbackCues.Clear();
        started = false;
    }

    public void Play(DungeonAudioCue cue)
    {
        if (!IsReady)
        {
            return;
        }

        AudioClip clip = library != null ? library.GetCue(cue) : null;
        clip ??= GetFallbackCue(cue);
        AudioSource source = cue == DungeonAudioCue.UiClick ? uiSource : effectsSource;
        if (clip != null && source != null && source.isActiveAndEnabled)
        {
            source.PlayOneShot(clip);
            LastCue = cue;
            PlayedCueCount++;
        }
    }

    public void OnTriggerEvent(BlueprintResearchCompletedEvent eventType)
    {
        Play(DungeonAudioCue.Confirm);
    }

    public void OnTriggerEvent(InvasionStartedEvent eventType)
    {
        PlayWarning();
    }

    public void OnTriggerEvent(BossInvasionStartedEvent eventType)
    {
        PlayWarning();
    }

    public void OnTriggerEvent(InvasionFacilityDamagedEvent eventType)
    {
        Play(DungeonAudioCue.Impact);
    }

    public void OnTriggerEvent(OwnerRunEndedEvent eventType)
    {
        Play(eventType.Outcome == DungeonRunOutcome.Victory
            ? DungeonAudioCue.Victory
            : DungeonAudioCue.Defeat);
    }

    internal void RuntimeUpdate()
    {
        if (uiClock.Time < nextButtonScanAt)
        {
            return;
        }

        nextButtonScanAt = uiClock.Time + 0.75f;
        ScanButtons();
    }

    private void CreateRuntimeAudio()
    {
        root = new GameObject("DungeonAudioRuntime", typeof(DungeonAudioRuntimeBehaviour));
        runtimeBehaviour = root.GetComponent<DungeonAudioRuntimeBehaviour>();
        runtimeBehaviour.Initialize(this);

        AudioMixer mixer = resourcesAssetLoader.LoadOptional<AudioMixer>(MixerResourcePath);
        musicSource = CreateSource("Music", true, FindGroup(mixer, "Music"));
        ambienceSource = CreateSource("Ambience", true, FindGroup(mixer, "Ambience"));
        effectsSource = CreateSource("Effects", false, FindGroup(mixer, "Effects"));
        uiSource = CreateSource("UI", false, FindGroup(mixer, "UI"));

        musicSource.clip = library != null && library.musicLoop != null
            ? library.musicLoop
            : Own(CreateMusicLoop());
        ambienceSource.clip = library != null && library.ambienceLoop != null
            ? library.ambienceLoop
            : Own(CreateAmbienceLoop());
        musicSource.Play();
        ambienceSource.Play();
    }

    private AudioSource CreateSource(string name, bool loop, AudioMixerGroup output)
    {
        GameObject sourceObject = new GameObject(name, typeof(AudioSource));
        sourceObject.transform.SetParent(root.transform, false);
        AudioSource source = sourceObject.GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = output;
        return source;
    }

    private void ApplyVolumes()
    {
        if (!IsReady)
        {
            return;
        }

        DungeonUserSettingsData settings = settingsService.Current;
        musicSource.volume = 0.16f * settings.musicVolume;
        ambienceSource.volume = 0.09f * settings.musicVolume;
        effectsSource.volume = settings.effectsVolume;
        uiSource.volume = 0.7f * settings.uiVolume;
    }

    private void ScanButtons()
    {
        foreach (int id in buttonBindings
                     .Where(pair => pair.Value.Button == null)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            buttonBindings.Remove(id);
        }

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            sceneRootBuffer.Clear();
            scene.GetRootGameObjects(sceneRootBuffer);
            foreach (GameObject sceneRoot in sceneRootBuffer)
            {
                if (sceneRoot == null)
                {
                    continue;
                }

                buttonScanBuffer.Clear();
                sceneRoot.GetComponentsInChildren(includeInactive: true, buttonScanBuffer);
                foreach (Button button in buttonScanBuffer)
                {
                    if (button == null)
                    {
                        continue;
                    }

                    int id = button.GetInstanceID();
                    if (!buttonBindings.ContainsKey(id))
                    {
                        buttonBindings.Add(id, new ButtonBinding(button, () => Play(DungeonAudioCue.UiClick)));
                    }
                }
            }
        }
    }

    private void PlayWarning()
    {
        if (uiClock.Time - lastWarningAt < 0.5f)
        {
            return;
        }

        lastWarningAt = uiClock.Time;
        Play(DungeonAudioCue.Warning);
    }

    private AudioClip GetFallbackCue(DungeonAudioCue cue)
    {
        if (fallbackCues.TryGetValue(cue, out AudioClip clip))
        {
            return clip;
        }

        clip = cue switch
        {
            DungeonAudioCue.UiClick => CreateTone("UI Click", 0.055f, 520f, 660f, 0.12f),
            DungeonAudioCue.Confirm => CreateTone("Confirm", 0.24f, 440f, 780f, 0.18f),
            DungeonAudioCue.Warning => CreatePulse("Warning", 0.62f, 190f, 0.22f),
            DungeonAudioCue.Impact => CreateNoiseBurst("Impact", 0.18f, 0.24f),
            DungeonAudioCue.Victory => CreateArpeggio("Victory", new[] { 392f, 494f, 587f, 784f }, 0.2f, 0.18f),
            DungeonAudioCue.Defeat => CreateArpeggio("Defeat", new[] { 294f, 247f, 196f }, 0.28f, 0.2f),
            _ => null
        };
        fallbackCues[cue] = Own(clip);
        return clip;
    }

    private AudioClip Own(AudioClip clip)
    {
        if (clip != null && !ownedClips.Contains(clip))
        {
            ownedClips.Add(clip);
        }

        return clip;
    }

    private static AudioMixerGroup FindGroup(AudioMixer mixer, string name)
    {
        return mixer != null ? mixer.FindMatchingGroups(name).FirstOrDefault() : null;
    }

    private static AudioClip CreateMusicLoop()
    {
        const float duration = 8f;
        float[] melody = { 220f, 261.63f, 293.66f, 261.63f, 196f, 220f, 246.94f, 220f };
        int sampleCount = Mathf.RoundToInt(duration * SampleRate);
        float[] data = new float[sampleCount];
        for (int index = 0; index < sampleCount; index++)
        {
            float time = index / (float)SampleRate;
            int noteIndex = Mathf.FloorToInt(time) % melody.Length;
            float phase = time - Mathf.Floor(time);
            float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, phase * 8f))
                * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, (1f - phase) * 5f));
            float frequency = melody[noteIndex];
            data[index] = (Mathf.Sin(2f * Mathf.PI * frequency * time)
                + 0.35f * Mathf.Sin(2f * Mathf.PI * frequency * 0.5f * time)) * envelope * 0.12f;
        }

        return CreateClip("Fallback Dungeon Music", data);
    }

    private static AudioClip CreateAmbienceLoop()
    {
        const float duration = 6f;
        int sampleCount = Mathf.RoundToInt(duration * SampleRate);
        float[] data = new float[sampleCount];
        System.Random random = new System.Random(1407);
        float filtered = 0f;
        for (int index = 0; index < sampleCount; index++)
        {
            float noise = (float)(random.NextDouble() * 2d - 1d);
            filtered = Mathf.Lerp(filtered, noise, 0.015f);
            float time = index / (float)SampleRate;
            data[index] = filtered * 0.16f + Mathf.Sin(2f * Mathf.PI * 55f * time) * 0.018f;
        }

        return CreateClip("Fallback Dungeon Ambience", data);
    }

    private static AudioClip CreateTone(string name, float duration, float startFrequency, float endFrequency, float amplitude)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        float[] data = new float[sampleCount];
        float phase = 0f;
        for (int index = 0; index < sampleCount; index++)
        {
            float normalized = index / (float)sampleCount;
            phase += 2f * Mathf.PI * Mathf.Lerp(startFrequency, endFrequency, normalized) / SampleRate;
            float envelope = Mathf.Sin(Mathf.PI * normalized);
            data[index] = Mathf.Sin(phase) * envelope * amplitude;
        }

        return CreateClip(name, data);
    }

    private static AudioClip CreatePulse(string name, float duration, float frequency, float amplitude)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        float[] data = new float[sampleCount];
        for (int index = 0; index < sampleCount; index++)
        {
            float time = index / (float)SampleRate;
            float pulse = Mathf.Sin(2f * Mathf.PI * 3.2f * time) > -0.2f ? 1f : 0.18f;
            float envelope = Mathf.Sin(Mathf.PI * index / sampleCount);
            data[index] = Mathf.Sin(2f * Mathf.PI * frequency * time) * pulse * envelope * amplitude;
        }

        return CreateClip(name, data);
    }

    private static AudioClip CreateNoiseBurst(string name, float duration, float amplitude)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        float[] data = new float[sampleCount];
        System.Random random = new System.Random(7331);
        for (int index = 0; index < sampleCount; index++)
        {
            float normalized = index / (float)sampleCount;
            data[index] = (float)(random.NextDouble() * 2d - 1d)
                * Mathf.Pow(1f - normalized, 2.5f)
                * amplitude;
        }

        return CreateClip(name, data);
    }

    private static AudioClip CreateArpeggio(string name, IReadOnlyList<float> notes, float noteDuration, float amplitude)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(notes.Count * noteDuration * SampleRate));
        float[] data = new float[sampleCount];
        for (int index = 0; index < sampleCount; index++)
        {
            float time = index / (float)SampleRate;
            int noteIndex = Mathf.Min(notes.Count - 1, Mathf.FloorToInt(time / noteDuration));
            float notePhase = time % noteDuration / noteDuration;
            float envelope = Mathf.Sin(Mathf.PI * notePhase);
            data[index] = Mathf.Sin(2f * Mathf.PI * notes[noteIndex] * time) * envelope * amplitude;
        }

        return CreateClip(name, data);
    }

    private static AudioClip CreateClip(string name, float[] data)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private sealed class ButtonBinding : IDisposable
    {
        private readonly UnityAction callback;

        public ButtonBinding(Button button, UnityAction callback)
        {
            Button = button;
            this.callback = callback;
            Button.onClick.AddListener(callback);
        }

        public Button Button { get; }

        public void Dispose()
        {
            if (Button != null)
            {
                Button.onClick.RemoveListener(callback);
            }
        }
    }
}

public sealed class DungeonAudioRuntimeBehaviour : MonoBehaviour
{
    private DungeonAudioController controller;

    public void Initialize(DungeonAudioController value)
    {
        controller = value;
    }

    private void Update()
    {
        controller?.RuntimeUpdate();
    }
}

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class CharacterMoodPersistenceDebugScenarios
{
    private const string MenuPath =
        "Tools/Dungeon Story/QA/Character Mood Persistence Exact Round Trip";

    [MenuItem(MenuPath)]
    public static void RunFromMenu()
    {
        VerifyExactRemainingDurationRoundTrip();
        Debug.Log(
            "[PASS] CHARACTER_MOOD_REMAINING_DURATION_EXACT_ROUND_TRIP");
    }

    internal static void VerifyExactRemainingDurationRoundTrip()
    {
        CharacterMoodFactorSnapshot authoredMinimum = new CharacterMoodMemory(
                "qa:mood-authored-minimum",
                "QA authored minimum",
                2f,
                0.0001f,
                1,
                0f)
            .CreateSnapshot(0f);
        if (BitConverter.SingleToInt32Bits(authoredMinimum.RemainingSeconds)
            != BitConverter.SingleToInt32Bits(0.25f))
        {
            throw new InvalidOperationException(
                "New gameplay mood effects must retain the authored 0.25 second minimum.");
        }

        float[] captureTimes = { 0f, 17.125f, 32.1f, 4096.03125f, 16384.125f };
        float[] restoreTimes = { 0f, 31.375f, 32.1f, 8192.0625f, 32768.25f };
        float[] durations =
        {
            0.0001f,
            0.00164794921875f,
            0.249999f,
            0.25f,
            1.001f,
            178.27427673339845f,
            1800.125f
        };

        for (int captureIndex = 0; captureIndex < captureTimes.Length; captureIndex++)
        for (int restoreIndex = 0; restoreIndex < restoreTimes.Length; restoreIndex++)
        for (int durationIndex = 0; durationIndex < durations.Length; durationIndex++)
        {
            CharacterMoodMemory original = CharacterMoodMemory.RestoreExact(
                "qa:mood-round-trip", "QA mood round trip", 2f,
                durations[durationIndex], captureTimes[captureIndex]);
            CharacterMoodFactorSnapshot captured =
                original.CreateSnapshot(captureTimes[captureIndex]);
            int expectedBits = BitConverter.SingleToInt32Bits(captured.RemainingSeconds);
            CharacterMoodFactorSnapshot current = captured;
            for (int cycle = 0; cycle < 8; cycle++)
            {
                float cycleNow = restoreTimes[restoreIndex] + (cycle * 32.1f);
                CharacterMoodMemory restored = CharacterMoodMemory.RestoreExact(
                    current.Id, current.Label, current.Value,
                    current.RemainingSeconds, cycleNow);
                current = restored.CreateSnapshot(cycleNow);
                int actualBits = BitConverter.SingleToInt32Bits(current.RemainingSeconds);
                if (expectedBits != actualBits)
                    throw new InvalidOperationException(
                        "Character mood remaining duration did not survive an exact save/restore round trip. "
                        + $"captureTime={captureTimes[captureIndex]:R};restoreTime={cycleNow:R};cycle={cycle};"
                        + $"duration={durations[durationIndex]:R};expectedBits={expectedBits};actualBits={actualBits}");
            }
        }
    }
}
#endif

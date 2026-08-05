using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ThermalProtectionProfile
{
    public float comfortMinimumOffset;
    public float comfortMaximumOffset;
    public float safeMinimumOffset;
    public float safeMaximumOffset;
    [Range(0.05f, 2f)] public float coldExposureMultiplier = 1f;
    [Range(0.05f, 2f)] public float heatExposureMultiplier = 1f;

    public static ThermalProtectionProfile None =>
        new ThermalProtectionProfile();

    public void Add(ThermalProtectionProfile other)
    {
        if (other == null)
        {
            return;
        }

        comfortMinimumOffset += other.comfortMinimumOffset;
        comfortMaximumOffset += other.comfortMaximumOffset;
        safeMinimumOffset += other.safeMinimumOffset;
        safeMaximumOffset += other.safeMaximumOffset;
        coldExposureMultiplier *= Mathf.Clamp(
            other.coldExposureMultiplier,
            0.05f,
            2f);
        heatExposureMultiplier *= Mathf.Clamp(
            other.heatExposureMultiplier,
            0.05f,
            2f);
    }

    public ThermalProtectionProfile Clone()
    {
        return new ThermalProtectionProfile
        {
            comfortMinimumOffset = comfortMinimumOffset,
            comfortMaximumOffset = comfortMaximumOffset,
            safeMinimumOffset = safeMinimumOffset,
            safeMaximumOffset = safeMaximumOffset,
            coldExposureMultiplier = coldExposureMultiplier,
            heatExposureMultiplier = heatExposureMultiplier
        };
    }
}

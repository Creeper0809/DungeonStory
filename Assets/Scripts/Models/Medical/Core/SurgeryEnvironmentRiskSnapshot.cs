using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct SurgeryEnvironmentRiskSnapshot
{
    public SurgeryEnvironmentRiskSnapshot(
        EnvironmentalCellSnapshot environment,
        EnvironmentalExposureBand doctorBand,
        EnvironmentalExposureBand patientBand,
        float successPenalty,
        float infectionAdded,
        float bleedingAdded,
        float organDamageAdded,
        float instabilityAdded,
        bool extreme,
        bool normal)
    {
        Environment = environment;
        DoctorBand = doctorBand;
        PatientBand = patientBand;
        SuccessPenalty = Mathf.Max(0f, successPenalty);
        InfectionAdded = Mathf.Max(0f, infectionAdded);
        BleedingAdded = Mathf.Max(0f, bleedingAdded);
        OrganDamageAdded = Mathf.Max(0f, organDamageAdded);
        InstabilityAdded = Mathf.Max(0f, instabilityAdded);
        Extreme = extreme;
        Normal = normal;
    }

    public EnvironmentalCellSnapshot Environment { get; }
    public EnvironmentalExposureBand DoctorBand { get; }
    public EnvironmentalExposureBand PatientBand { get; }
    public float SuccessPenalty { get; }
    public float InfectionAdded { get; }
    public float BleedingAdded { get; }
    public float OrganDamageAdded { get; }
    public float InstabilityAdded { get; }
    public bool Extreme { get; }
    public bool Normal { get; }
}

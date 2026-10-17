using System;
using UnityEngine;

[Serializable]
public struct FacilityNeedRecoveryData
{
    public float sleep;
    public float mood;
    public float fun;
    public float hunger;
    public float excretion;
    public float hygiene;

    public bool HasEffect => !Mathf.Approximately(sleep, 0f)
        || !Mathf.Approximately(mood, 0f)
        || !Mathf.Approximately(fun, 0f)
        || !Mathf.Approximately(hunger, 0f)
        || !Mathf.Approximately(excretion, 0f)
        || !Mathf.Approximately(hygiene, 0f);
}

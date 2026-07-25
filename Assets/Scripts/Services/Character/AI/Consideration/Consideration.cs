using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
[DrawWithUnity]
public abstract class Consideration : SerializedScriptableObject
{
    public string considerationName;
    public abstract float ScoreConsideration(CharacterActor actor);
}

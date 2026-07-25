using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Data<T>
{
    public event Action<T> OnValueChange;
    [SerializeField] private T v;
    public T Value
    {
        get => v;
        set
        {
            if (EqualityComparer<T>.Default.Equals(v, value)) return;
            v = value;
            OnValueChange?.Invoke(Value);
        }
    }
    public void Initialize(T t)
    {
        v = t;
        OnValueChange?.Invoke(Value);
    }
}

using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/GameData", order = 0)]
public sealed class GameData : ScriptableObject
{
    [Min(0)] [SerializeField] private int startingMoney = 5000;
    [Min(1)] [SerializeField] private int startingDay = 1;
    [Range(1, 5)] [SerializeField] private int startingGameSpeed = 1;
    [Min(1f)] [SerializeField] private float secondsPerDay = 180f;

    public int StartingMoney => startingMoney;
    public int StartingDay => startingDay;
    public int StartingGameSpeed => startingGameSpeed;
    public float SecondsPerDay => secondsPerDay;
}

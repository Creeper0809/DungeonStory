using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName = "DungeonStory/GameData", order = 0)]
public class GameData : ScriptableObject
{
    public Data<int> gameSpeed;
    public Data<int> holdingMoney;
    public Data<int> day;
    public Data<float> curTime;
    public Data<int> hour;

    public Data<TimeOfDay> timeOfDay;
}

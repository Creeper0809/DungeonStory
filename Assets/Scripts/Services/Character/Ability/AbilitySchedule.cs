using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AbilitySchedule : CharacterAbility
{
    private Schedule[] schedules = new Schedule[24];
    public Data<Schedule> nowSheduleData;
    public GameData gameData;
    protected override void Awake()
    {
        base.Awake();
        gameData.hour.OnValueChange += CheckTime;
        for (int i = 0; i < schedules.Length; i++)
        {
            schedules[i] = Schedule.WORK;
        }
        nowSheduleData.Value = Schedule.WORK;
    }
    private void CheckTime(int hour)
    {
        nowSheduleData.Value = schedules[hour];
    }
    private void OnDisable()
    {
        gameData.hour.OnValueChange -= CheckTime;
    }
}
public enum Schedule
{
    NONE,
    FREE,
    WORK,
    SLEEP,
}
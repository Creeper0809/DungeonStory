public enum WorkPriorityLevel
{
    Off = 0,
    Priority1 = 1,
    Priority2 = 2,
    Priority3 = 3
}

public static class WorkPriorityLevelExtensions
{
    public static WorkPriorityLevel Next(this WorkPriorityLevel priority)
    {
        return priority switch
        {
            WorkPriorityLevel.Priority1 => WorkPriorityLevel.Priority2,
            WorkPriorityLevel.Priority2 => WorkPriorityLevel.Priority3,
            WorkPriorityLevel.Priority3 => WorkPriorityLevel.Off,
            _ => WorkPriorityLevel.Priority1
        };
    }

    public static float GetBaseScore(this WorkPriorityLevel priority)
    {
        return priority switch
        {
            WorkPriorityLevel.Priority1 => 300f,
            WorkPriorityLevel.Priority2 => 200f,
            WorkPriorityLevel.Priority3 => 100f,
            _ => float.NegativeInfinity
        };
    }

    public static string ToDisplayText(this WorkPriorityLevel priority)
    {
        return priority switch
        {
            WorkPriorityLevel.Priority1 => "1",
            WorkPriorityLevel.Priority2 => "2",
            WorkPriorityLevel.Priority3 => "3",
            _ => "꺼짐"
        };
    }
}

using System;
using VContainer.Unity;

public sealed class FestivalExecutionDriverAdapter : ITickable
{
    private readonly IFestivalExecutionDriver driver;

    public FestivalExecutionDriverAdapter(IFestivalExecutionDriver driver) =>
        this.driver = driver ?? throw new ArgumentNullException(nameof(driver));

    public void Tick() => driver.Advance();
}

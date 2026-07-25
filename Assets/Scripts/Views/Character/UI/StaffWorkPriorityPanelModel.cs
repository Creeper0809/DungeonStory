using System;
using System.Collections.Generic;

public readonly struct StaffWorkPriorityRowModel
{
    public StaffWorkPriorityRowModel(CharacterActor character, AbilityWork work, string name)
    {
        Character = character ?? throw new ArgumentNullException(nameof(character));
        Work = work ?? throw new ArgumentNullException(nameof(work));
        Name = string.IsNullOrWhiteSpace(name) ? character.name : name;
    }

    public CharacterActor Character { get; }
    public AbilityWork Work { get; }
    public string Name { get; }
}

public interface IStaffWorkPriorityPanelModelBuilder
{
    IReadOnlyList<StaffWorkPriorityRowModel> BuildRows();
    int CalculateWorkerHash();
    int CalculateWorkerHash(IReadOnlyList<StaffWorkPriorityRowModel> workers);
    string GetDisplayName(CharacterActor character);
}

public sealed class StaffWorkPriorityPanelModelBuilder : IStaffWorkPriorityPanelModelBuilder
{
    private readonly IStaffWorkforceQueryService workforceQueryService;

    public StaffWorkPriorityPanelModelBuilder(IStaffWorkforceQueryService workforceQueryService)
    {
        this.workforceQueryService = workforceQueryService
            ?? throw new ArgumentNullException(nameof(workforceQueryService));
    }

    public IReadOnlyList<StaffWorkPriorityRowModel> BuildRows()
    {
        IReadOnlyList<CharacterActor> characters = workforceQueryService.FindActiveWorkers();
        List<StaffWorkPriorityRowModel> workers = new List<StaffWorkPriorityRowModel>(characters.Count);
        foreach (CharacterActor character in characters)
        {
            if (CharacterWorkRoleUtility.TryGetWork(character, out AbilityWork work))
            {
                workers.Add(new StaffWorkPriorityRowModel(
                    character,
                    work,
                    workforceQueryService.GetDisplayName(character)));
            }
        }

        return workers;
    }

    public int CalculateWorkerHash()
    {
        return CalculateWorkerHash(BuildRows());
    }

    public int CalculateWorkerHash(IReadOnlyList<StaffWorkPriorityRowModel> workers)
    {
        if (workers == null)
        {
            return 0;
        }

        unchecked
        {
            int hash = 17;
            foreach (StaffWorkPriorityRowModel worker in workers)
            {
                hash = (hash * 31) + worker.Character.GetInstanceID();
                hash = (hash * 31) + (int)worker.Work.CurrentDutyState;
                hash = (hash * 31) + (worker.Work.isWorking ? 1 : 0);
                hash = (hash * 31) + (worker.Character.Lifecycle != null
                    && worker.Character.Lifecycle.CurrentState == CharacterLifecycleState.OnExpedition
                        ? 1
                        : 0);
                hash = (hash * 31) + (worker.Character.Brain != null ? worker.Character.Brain.GetDebugHash() : 0);

                foreach (WorkTypeDefinition definition in WorkTaskCatalog.Definitions)
                {
                    hash = (hash * 31) + (int)worker.Work.WorkPriorities.GetPriority(
                        definition.WorkTypeId);
                }
            }

            return hash;
        }
    }

    public string GetDisplayName(CharacterActor character)
    {
        return workforceQueryService.GetDisplayName(character);
    }
}

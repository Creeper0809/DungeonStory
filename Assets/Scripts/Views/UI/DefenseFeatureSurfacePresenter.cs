using System;
using System.Collections.Generic;

public sealed class DefenseFeatureSurfaceModel
{
    public string DefenseHudSummary { get; set; } = string.Empty;
    public string ThreatSummary { get; set; } = string.Empty;
    public string ThreatFactors { get; set; } = string.Empty;
    public string CampaignSummary { get; set; } = string.Empty;
    public string ReinforcementSummary { get; set; } = string.Empty;
    public string OwnerEvacuationSummary { get; set; } = string.Empty;
    public IReadOnlyList<DefenseFeatureIntruderRow> Intruders { get; set; }
        = Array.Empty<DefenseFeatureIntruderRow>();
    public IReadOnlyList<DefenseFeaturePolicyRow> Policies { get; set; }
        = Array.Empty<DefenseFeaturePolicyRow>();
    public DefenseFeaturePolicyRow SelectedPolicy { get; set; }
    public IReadOnlyList<DefenseFeatureGuardRow> Guards { get; set; }
        = Array.Empty<DefenseFeatureGuardRow>();
    public IReadOnlyList<DefenseFeatureFacilityRow> Facilities { get; set; }
        = Array.Empty<DefenseFeatureFacilityRow>();
    public IReadOnlyList<DefenseFeatureReportRow> Reports { get; set; }
        = Array.Empty<DefenseFeatureReportRow>();
}

public sealed class DefenseFeatureIntruderRow
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class DefenseFeaturePolicyRow
{
    public int Index { get; set; }
    public string PolicyId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
    public bool IsCustom { get; set; }
    public bool AutoRespond { get; set; }
    public float MinimumDispatchHealthRatio { get; set; }
    public float RetreatHealthRatio { get; set; }
    public bool HoldWithoutReplacement { get; set; }
    public float RejoinHealthRatio { get; set; }
}

public sealed class DefenseFeatureGuardRow
{
    public int Index { get; set; }
    public int ActorRuntimeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool UsesSelectedPolicy { get; set; }
}

public sealed class DefenseFeatureFacilityRow
{
    public int Index { get; set; }
    public int RuntimeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DefenseArmingPolicy ArmingPolicy { get; set; }
    public DefenseFacilityOperationalState OperationalState { get; set; }
}

public sealed class DefenseFeatureReportRow
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public readonly struct DefenseFeatureCommandResult
{
    public DefenseFeatureCommandResult(bool succeeded, string message, string entityId = "")
    {
        Succeeded = succeeded;
        Message = message ?? string.Empty;
        EntityId = entityId ?? string.Empty;
    }

    public bool Succeeded { get; }
    public string Message { get; }
    public string EntityId { get; }
}

public interface IDefenseFeatureQueryService
{
    DefenseFeatureSurfaceModel Capture(string selectedPolicyId);
}

public interface IDefenseFeatureCommandService
{
    DefenseFeatureCommandResult ToggleAutoResponse(string policyId);
    DefenseFeatureCommandResult StepMinimumDispatchHealth(string policyId);
    DefenseFeatureCommandResult StepRetreatHealth(string policyId);
    DefenseFeatureCommandResult ToggleHoldWithoutReplacement(string policyId);
    DefenseFeatureCommandResult StepRejoinHealth(string policyId);
    DefenseFeatureCommandResult CreatePolicy();
    DefenseFeatureCommandResult DuplicatePolicy(string policyId);
    DefenseFeatureCommandResult DeletePolicy(string policyId);
    DefenseFeatureCommandResult AssignPolicy(int actorRuntimeId, string policyId);
    DefenseFeatureCommandResult CycleFacilityArmingPolicy(int facilityRuntimeId);
    DefenseFeatureCommandResult RequestFacilityService(int facilityRuntimeId);
}



public sealed class DefenseFeatureSurfacePresenter : IFeatureSurfaceTabPresenter
{
    private const float CompactCardHeight = 92f;
    private const float DetailCardHeight = 164f;

    private readonly IDefenseFeatureQueryService query;
    private readonly IDefenseFeatureCommandService commands;
    private readonly IDefenseUiTextQuery text;
    private string selectedPolicyId = DefenseResponsePolicyRuntime.StandardPolicyId;
    private string pendingDeletePolicyId = string.Empty;
    private int selectedReportIndex = -1;

    public DefenseFeatureSurfacePresenter(
        IDefenseFeatureQueryService query,
        IDefenseFeatureCommandService commands,
        IDefenseUiTextQuery text)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public TabId Id => TabId.Defense;

    public void Present(IFeatureSurfaceView view)
    {
        if (view == null)
        {
            throw new ArgumentNullException(nameof(view));
        }

        DefenseFeatureSurfaceModel model = query.Capture(selectedPolicyId);
        if (model.SelectedPolicy != null)
        {
            selectedPolicyId = model.SelectedPolicy.PolicyId;
        }

        view.AddSection(text.Get("Section.DefenseHud"), model.DefenseHudSummary);
        view.AddSection(text.Get("Section.InvasionThreat"), model.ThreatSummary);
        view.AddLabel(model.ThreatFactors, 17f, 44f);
        view.AddSection(text.Get("Section.Campaign"), model.CampaignSummary);
        view.AddSection(text.Get("Section.Reinforcements"), model.ReinforcementSummary);
        view.AddSection(
            text.Get("Section.IntruderTracking"),
            text.Get("ActiveIntruderCount", model.Intruders.Count));
        if (model.Intruders.Count == 0)
        {
            view.AddLabel(text.Get("DungeonSafe"), 18f, 44f);
        }

        foreach (DefenseFeatureIntruderRow row in model.Intruders)
        {
            DefenseFeatureIntruderRow captured = row;
            view.AddDataCard(
                $"P1Action_IntruderTrack_{captured.Index}",
                captured.Title,
                captured.Detail,
                text.Get("Action.Track"),
                () => view.ShowFeedback(captured.Detail.Replace("\n", " / ")),
                116f);
        }

        view.AddSection(text.Get("Section.OwnerEvacuation"), model.OwnerEvacuationSummary);
        AddPolicies(view, model);
        AddFacilities(view, model);
        AddReports(view, model);
    }

    private void AddPolicies(IFeatureSurfaceView view, DefenseFeatureSurfaceModel model)
    {
        view.AddSection(
            text.Get("Section.GuardResponsePolicy"),
            model.SelectedPolicy != null
                ? text.Get(
                    "PolicySelectionSummary",
                    model.Policies.Count,
                    model.SelectedPolicy.DisplayName)
                : text.Get("PolicyUnavailable"));
        foreach (DefenseFeaturePolicyRow row in model.Policies)
        {
            DefenseFeaturePolicyRow captured = row;
            view.AddDataCard(
                $"P1Action_DefensePolicySelect_{captured.Index}",
                captured.DisplayName,
                captured.Detail,
                text.Get(captured.IsSelected ? "Action.Selected" : "Action.Select"),
                () =>
                {
                    selectedPolicyId = captured.PolicyId;
                    pendingDeletePolicyId = string.Empty;
                    view.ShowFeedback(
                        text.Get("PolicySelected", captured.DisplayName));
                    view.RequestRefresh();
                },
                CompactCardHeight);
        }

        DefenseFeaturePolicyRow selected = model.SelectedPolicy;
        if (selected == null)
        {
            return;
        }

        AddPolicyCommand(
            view,
            "P1Action_DefensePolicyAuto",
            text.Get(selected.AutoRespond
                ? "Action.DisableAutoResponse"
                : "Action.EnableAutoResponse"),
            text.Get("Help.AutoResponse"),
            () => commands.ToggleAutoResponse(selected.PolicyId));
        AddPolicyCommand(
            view,
            "P1Action_DefensePolicyDispatchHealth",
            text.Get("Action.MinimumDispatchHealth", selected.MinimumDispatchHealthRatio),
            text.Get("Help.StepFivePercent"),
            () => commands.StepMinimumDispatchHealth(selected.PolicyId));
        AddPolicyCommand(
            view,
            "P1Action_DefensePolicyRetreatHealth",
            text.Get("Action.RetreatHealth", FormatRetreat(selected.RetreatHealthRatio)),
            text.Get("Help.NoAutomaticRetreat"),
            () => commands.StepRetreatHealth(selected.PolicyId));
        AddPolicyCommand(
            view,
            "P1Action_DefensePolicyHold",
            selected.HoldWithoutReplacement
                ? text.Get("Action.HoldWithoutReplacement")
                : text.Get("Action.RetreatWithoutReplacement"),
            text.Get("Help.HoldWithoutReplacement"),
            () => commands.ToggleHoldWithoutReplacement(selected.PolicyId));
        AddPolicyCommand(
            view,
            "P1Action_DefensePolicyRejoinHealth",
            text.Get("Action.RejoinHealth", selected.RejoinHealthRatio),
            text.Get("Help.RejoinHealth"),
            () => commands.StepRejoinHealth(selected.PolicyId));
        AddPolicyCommand(
            view,
            "P1Action_DefensePolicyCreate",
            text.Get("Action.NewPolicy"),
            text.Get("Help.NewPolicy"),
            commands.CreatePolicy,
            selectReturnedEntity: true);
        AddPolicyCommand(
            view,
            "P1Action_DefensePolicyDuplicate",
            text.Get("Action.DuplicatePolicy"),
            text.Get("Help.DuplicatePolicy"),
            () => commands.DuplicatePolicy(selected.PolicyId),
            selectReturnedEntity: true);

        if (selected.IsCustom)
        {
            bool confirming = string.Equals(
                pendingDeletePolicyId,
                selected.PolicyId,
                StringComparison.Ordinal);
            view.AddDataCard(
                "P1Action_DefensePolicyDelete",
                text.Get(confirming ? "Action.ConfirmDelete" : "Action.DeletePolicy"),
                confirming
                    ? text.Get("Help.DeletePolicyReassign")
                    : text.Get("Help.DeletePolicyConfirm"),
                text.Get("Action.Execute"),
                () =>
                {
                    if (!confirming)
                    {
                        pendingDeletePolicyId = selected.PolicyId;
                        view.ShowFeedback(text.Get("PolicyDeleteConfirmFeedback"));
                    }
                    else
                    {
                        DefenseFeatureCommandResult result = commands.DeletePolicy(selected.PolicyId);
                        selectedPolicyId = result.Succeeded
                            ? DefenseResponsePolicyRuntime.StandardPolicyId
                            : selectedPolicyId;
                        pendingDeletePolicyId = string.Empty;
                        view.ShowFeedback(result.Message);
                    }

                    view.RequestRefresh();
                },
                CompactCardHeight);
        }

        view.AddSection(
            text.Get("Section.GuardPolicyAssignment"),
            text.Get(
                "GuardPolicyAssignmentSummary",
                model.Guards.Count,
                selected.DisplayName));
        foreach (DefenseFeatureGuardRow guard in model.Guards)
        {
            DefenseFeatureGuardRow captured = guard;
            view.AddDataCard(
                $"P1Action_DefensePolicyAssign_{captured.Index}",
                captured.Name,
                captured.Detail,
                text.Get(captured.UsesSelectedPolicy
                    ? "Action.Assigned"
                    : "Action.AssignPolicy"),
                () =>
                {
                    DefenseFeatureCommandResult result = commands.AssignPolicy(
                        captured.ActorRuntimeId,
                        selected.PolicyId);
                    view.ShowFeedback(result.Message);
                    view.RequestRefresh();
                },
                CompactCardHeight);
        }
    }

    private void AddFacilities(
        IFeatureSurfaceView view,
        DefenseFeatureSurfaceModel model)
    {
        view.AddSection(
            text.Get("Section.DefenseFacilities"),
            text.Get("ActiveFacilityCount", model.Facilities.Count));
        foreach (DefenseFeatureFacilityRow facility in model.Facilities)
        {
            DefenseFeatureFacilityRow captured = facility;
            view.AddDataCard(
                $"P1Action_DefenseFacilityPolicy_{captured.Index}",
                captured.Name,
                captured.Detail,
                text.Get("Action.CycleArming"),
                () =>
                {
                    DefenseFeatureCommandResult result =
                        commands.CycleFacilityArmingPolicy(
                            captured.RuntimeId);
                    view.ShowFeedback(result.Message);
                    view.RequestRefresh();
                },
                CompactCardHeight);
            if (captured.OperationalState is
                    DefenseFacilityOperationalState.Empty
                    or DefenseFacilityOperationalState.Reloading
                    or DefenseFacilityOperationalState.Jammed)
            {
                view.AddDataCard(
                    $"P1Action_DefenseFacilityService_{captured.Index}",
                    captured.OperationalState
                        == DefenseFacilityOperationalState.Jammed
                            ? text.Get("Action.ClearJam")
                            : text.Get("Action.RequestReload"),
                    captured.Detail,
                    text.Get("Action.Execute"),
                    () =>
                    {
                        DefenseFeatureCommandResult result =
                            commands.RequestFacilityService(
                                captured.RuntimeId);
                        view.ShowFeedback(result.Message);
                        view.RequestRefresh();
                    },
                    CompactCardHeight);
            }
        }
    }

    private void AddReports(IFeatureSurfaceView view, DefenseFeatureSurfaceModel model)
    {
        view.AddSection(
            text.Get("Section.CombatReports"),
            text.Get("CompletedReportCount", model.Reports.Count));
        foreach (DefenseFeatureReportRow report in model.Reports)
        {
            DefenseFeatureReportRow captured = report;
            bool selected = selectedReportIndex == captured.Index;
            view.AddDataCard(
                $"P1Action_CombatReport_{captured.Index}",
                captured.Title,
                selected ? captured.Detail : captured.Summary,
                text.Get(selected ? "Action.Selected" : "Action.Detail"),
                () =>
                {
                    selectedReportIndex = captured.Index;
                    view.ShowFeedback(captured.Title);
                    view.RequestRefresh();
                },
                selected ? DetailCardHeight : CompactCardHeight);
        }
    }

    private void AddPolicyCommand(
        IFeatureSurfaceView view,
        string actionName,
        string title,
        string detail,
        Func<DefenseFeatureCommandResult> execute,
        bool selectReturnedEntity = false)
    {
        view.AddDataCard(
            actionName,
            title,
            detail,
            text.Get("Action.Execute"),
            () =>
            {
                DefenseFeatureCommandResult result = execute();
                if (selectReturnedEntity
                    && result.Succeeded
                    && !string.IsNullOrWhiteSpace(result.EntityId))
                {
                    selectedPolicyId = result.EntityId;
                }

                view.ShowFeedback(result.Message);
                view.RequestRefresh();
            },
            CompactCardHeight);
    }

    private string FormatRetreat(float ratio)
    {
        return ratio > 0f ? ratio.ToString("P0") : text.Get("None");
    }
}

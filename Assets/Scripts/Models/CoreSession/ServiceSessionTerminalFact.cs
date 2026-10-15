using System;
using System.Collections.Generic;

/// <summary>Frozen service result, before or after the owner's one-time payment.
/// No log schema, receipt, importance or key encoding is part of this fact.</summary>
public sealed class ServiceSessionTerminalFact
{
    private readonly ServiceSessionSnapshot state;
    public bool IsProposal { get; }
    public ServiceSessionStage TerminalStage { get; }
    public int AbsoluteDay { get; }
    public string HubName { get; }

    private ServiceSessionTerminalFact(ServiceSessionSnapshot source,
        ServiceSessionStage terminalStage, int absoluteDay, string hubName, bool proposal)
    {
        if (source == null || source.Contract == null) throw new ArgumentNullException(nameof(source));
        RequireText(source.SessionId); RequireText(source.HubId); RequireText(source.ProcessId); RequireText(hubName);
        if (source.ActorId == null || source.ActorId.Length > 0 && source.ActorId.Trim() != source.ActorId
            || !Enum.IsDefined(typeof(ServiceCategory), source.Category)
            || terminalStage is not (ServiceSessionStage.Completed or ServiceSessionStage.Cancelled)
            || absoluteDay < 0 || !Finite(source.StartedAt) || !Finite(source.StageStartedAt)
            || source.StageStartedAt < source.StartedAt || !Finite(source.Contract.serviceSeconds)
            || source.Contract.serviceSeconds <= 0 || source.Contract.price < 0
            || source.Contract.supportIds == null)
            throw new ArgumentException("Service terminal fact source is invalid.");
        var supports = new HashSet<string>(StringComparer.Ordinal);
        foreach (string support in source.Contract.supportIds)
        { RequireText(support); if (!supports.Add(support)) throw new ArgumentException("Duplicate service support."); }
        if (!proposal && (source.IsActive || source.Stage != terminalStage
            || source.TerminalOwnerRevision != ServiceSessionTerminalIdentity.OwnerRevision
            || !Finite(source.ProvidedServiceSeconds)
            || source.ProvidedServiceSeconds > source.Contract.serviceSeconds + 0.001f
            || terminalStage == ServiceSessionStage.Completed
                && (Math.Abs(source.ProvidedServiceSeconds - source.Contract.serviceSeconds) > 0.001f
                    || source.Contract.paymentRequired && source.Contract.price > 0 && !source.PaymentCommitted)
            || terminalStage == ServiceSessionStage.Cancelled && string.IsNullOrWhiteSpace(source.CancellationReason)))
            throw new ArgumentException("Only a payment-ready terminal result can be published.");
        state = ServiceRoomsSaveData.FromSnapshot(source).ToSnapshot();
        // Transport acknowledgements must not be part of the observed gameplay result.
        state.TerminalPublicationKey = state.TerminalPublicationId = state.TerminalPublicationFingerprint = string.Empty;
        IsProposal = proposal; TerminalStage = terminalStage; AbsoluteDay = absoluteDay; HubName = hubName;
    }

    public static ServiceSessionTerminalFact Propose(ServiceSessionSnapshot source,
        ServiceSessionStage terminalStage, int absoluteDay, string hubName) =>
        new(source, terminalStage, absoluteDay, hubName, true);
    public static ServiceSessionTerminalFact Actual(ServiceSessionSnapshot source) =>
        new(source, source?.Stage ?? default, source?.TerminalAbsoluteDay ?? -1, source?.TerminalHubName, false);
    public ServiceSessionSnapshot Snapshot() => ServiceRoomsSaveData.FromSnapshot(state).ToSnapshot();
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
    private static void RequireText(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() != value)
            throw new ArgumentException("Service terminal fact text is missing or noncanonical.");
    }
}

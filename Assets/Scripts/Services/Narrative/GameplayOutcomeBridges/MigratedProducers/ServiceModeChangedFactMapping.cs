using System;

public static class ServiceModeChangedFactMapping
{
    public static MigratedDomainFactCapture Capture(ServiceModeChangedFact fact)
    {
        var data = (fact ?? throw new ArgumentNullException(nameof(fact))).Snapshot();
        return new MigratedDomainFactCapture(MigratedProducerOutcomeKind.ServiceModeChangeResult,
            "service-mode-change:hub=" + data.hubId + ":command=set-mode:" + data.requestedMode,
            data.absoluteDay, GameplayOutcomeStatus.Succeeded,
            new MigratedProducerOutcomeDetails(new[]
            {
                new MigratedProducerOutcomeSubject(MigratedProducerOutcomeIds.FacilityKind,
                    data.hubId, data.hubName, MigratedProducerOutcomeIds.FacilityRole)
            }, Array.Empty<GameplayOutcomeMetric>(), Array.Empty<GameplayOutcomeFact>(),
                data.hubName + " 운영 모드를 " + data.previousMode + "에서 " + data.requestedMode + "으로 변경했습니다."));
    }
}

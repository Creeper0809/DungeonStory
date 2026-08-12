using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class WildlifeDiseaseVectorRuntime
{
    private readonly ICharacterAiWorldRegistry characterWorld;
    private readonly IGameCalendar calendar;
    private readonly IGameEventBus events;
    private readonly IDiseaseDefinitionCatalog diseases;

    public WildlifeDiseaseVectorRuntime(WildlifeWorldServices world)
    {
        if (world == null)
            throw new ArgumentNullException(nameof(world));
        characterWorld = world.WorldRegistry;
        calendar = world.Calendar;
        events = world.Events;
        diseases = world.Diseases;
    }

    public int PublishDailyExposure(
        IReadOnlyList<WildlifeActor> wildlife,
        int lastPublishedAbsoluteDay)
    {
        int absoluteDay = calendar.Day;
        if (absoluteDay <= 0 || absoluteDay == lastPublishedAbsoluteDay)
            return lastPublishedAbsoluteDay;

        CharacterActor[] living = characterWorld.Characters
            .Where(value => value != null && !value.IsDead)
            .OrderBy(value => CharacterPersistentIdentity.Require(value).Value,
                StringComparer.Ordinal)
            .ToArray();
        foreach (WildlifeActor vector in (wildlife ?? Array.Empty<WildlifeActor>())
                     .Where(value => value != null
                         && value.IsAlive
                         && value.Species?.DiseaseVectorIds.Count > 0)
                     .OrderBy(value => value.WildlifeId, StringComparer.Ordinal))
        {
            foreach (CharacterActor target in living)
            {
                int distance = Mathf.Abs(target.GetNowXY().x - vector.GridPosition.x)
                    + Mathf.Abs(target.GetNowXY().y - vector.GridPosition.y);
                if (distance > 2)
                    continue;

                CharacterId characterId = CharacterPersistentIdentity.Require(target);
                foreach (string diseaseId in vector.Species.DiseaseVectorIds)
                {
                    DiseaseDefinition disease = diseases.Require(diseaseId);
                    DiseaseTransmissionRoute route = ResolveVectorRoute(disease.Routes);
                    if (route == DiseaseTransmissionRoute.None)
                        continue;
                    events.Publish(new PopulationDiseaseRouteExposureEvent(
                        characterId,
                        diseaseId,
                        route,
                        exposureHours: distance == 0 ? 1f : 0.5f,
                        environmentCoefficient: 1f));
                }
            }
        }

        return absoluteDay;
    }

    private static DiseaseTransmissionRoute ResolveVectorRoute(
        DiseaseTransmissionRoute routes)
    {
        DiseaseTransmissionRoute[] priority =
        {
            DiseaseTransmissionRoute.Contact,
            DiseaseTransmissionRoute.Blood,
            DiseaseTransmissionRoute.Air,
            DiseaseTransmissionRoute.Droplet,
            DiseaseTransmissionRoute.Environment,
            DiseaseTransmissionRoute.Food,
            DiseaseTransmissionRoute.Water,
            DiseaseTransmissionRoute.ManaExposure
        };
        return priority.FirstOrDefault(value => (routes & value) != 0);
    }
}

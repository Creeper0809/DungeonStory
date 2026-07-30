using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class SurgicalFacilityQuery : ISurgicalFacilityQuery
{
    private readonly IBuildingWorldQuery buildings;
    private readonly IRoomLayoutCache rooms;

    public SurgicalFacilityQuery(
        IBuildingWorldQuery buildings,
        IRoomLayoutCache rooms)
    {
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
    }

    public SurgicalFacilitySnapshot Evaluate(
        BuildableObject primaryFacility,
        SurgeryFacilityTag requiredTags)
    {
        if (primaryFacility == null
            || primaryFacility.isDestroy
            || primaryFacility.IsDamaged)
        {
            return Blocked(primaryFacility, "집도 시설이 파괴되었거나 고장났습니다.");
        }

        ISurgicalFacilityAbility primaryAbility = primaryFacility.BuildingData?
            .Abilities?
            .OfType<ISurgicalFacilityAbility>()
            .FirstOrDefault(ability => ability.IsPrimaryOperatingFacility);
        if (primaryAbility == null)
        {
            return Blocked(primaryFacility, "수술 능력이 없는 시설입니다.");
        }

        if (!rooms.TryGetRoom(primaryFacility, out RoomInstance room)
            || room == null
            || !room.IsUsable)
        {
            return Blocked(primaryFacility, "닫힌 수술실이 필요합니다.");
        }

        List<BuildableObject> supports = room.Furniture
            .Where(candidate => candidate != null
                && candidate != primaryFacility
                && !candidate.isDestroy
                && !candidate.IsDamaged
                && candidate.BuildingData?.Abilities?
                    .OfType<ISurgicalFacilityAbility>()
                    .Any() == true)
            .ToList();
        SurgeryFacilityTag tags = primaryAbility.FacilityTags;
        float sterility = primaryAbility.SterilityBonus;
        float speed = primaryAbility.WorkSpeedMultiplier;
        float success = primaryAbility.SuccessBonus;
        float anesthesia = primaryAbility.AnesthesiaBonus;
        foreach (BuildableObject support in supports)
        {
            foreach (ISurgicalFacilityAbility ability in
                     support.BuildingData.Abilities.OfType<ISurgicalFacilityAbility>())
            {
                tags |= ability.FacilityTags;
                sterility += ability.SterilityBonus;
                speed *= Mathf.Max(0.25f, ability.WorkSpeedMultiplier);
                success += ability.SuccessBonus;
                anesthesia += ability.AnesthesiaBonus;
            }
        }

        SurgeryFacilityTag missing = requiredTags & ~tags;
        string blockReason = missing == SurgeryFacilityTag.None
            ? string.Empty
            : $"필요한 수술실 설비가 없습니다: {FormatTags(missing)}";
        return new SurgicalFacilitySnapshot(
            primaryFacility,
            tags,
            sterility,
            speed,
            success,
            anesthesia,
            supports,
            blockReason);
    }

    public bool TryFindBestFacility(
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        out SurgicalFacilitySnapshot facility,
        out string failureReason)
    {
        facility = default;
        failureReason = string.Empty;
        if (subject == null || !subject.IsValid || procedure == null)
        {
            failureReason = "수술 대상 또는 절차가 유효하지 않습니다.";
            return false;
        }

        List<SurgicalFacilitySnapshot> candidates =
            GetCandidateFacilities(procedure)
                .ToList();
        if (candidates.Count == 0)
        {
            failureReason = "요구 조건을 충족하는 수술 시설이 없습니다.";
            return false;
        }

        facility = candidates[0];
        return true;
    }

    public IReadOnlyList<SurgicalFacilitySnapshot> GetCandidateFacilities(
        SurgicalProcedureSO procedure,
        bool includeBlocked = false)
    {
        if (procedure == null)
        {
            return Array.Empty<SurgicalFacilitySnapshot>();
        }

        return buildings.Buildings
            .Where(candidate => candidate != null
                && !candidate.isDestroy
                && candidate.BuildingData?.Abilities?
                    .OfType<ISurgicalFacilityAbility>()
                    .Any(ability => ability.IsPrimaryOperatingFacility) == true)
            .Select(candidate => Evaluate(candidate, procedure.RequiredFacilityTags))
            .Where(snapshot => includeBlocked || snapshot.IsAvailable)
            .OrderByDescending(snapshot => snapshot.SuccessBonus)
            .ThenByDescending(snapshot => snapshot.Sterility)
            .ThenBy(snapshot => GetFacilityId(snapshot.PrimaryFacility), StringComparer.Ordinal)
            .ToList();
    }

    public string GetFacilityId(BuildableObject facility)
    {
        if (facility == null)
        {
            return string.Empty;
        }

        FacilityEvolutionStateComponent evolution =
            facility.GetComponent<FacilityEvolutionStateComponent>();
        if (evolution != null)
        {
            evolution.InitializeIfNeeded(facility);
            if (!string.IsNullOrWhiteSpace(evolution.FacilityPersistentId))
            {
                return evolution.FacilityPersistentId;
            }
        }

        return $"building:{facility.id}:{facility.centerPos.x}:{facility.centerPos.y}";
    }

    private static SurgicalFacilitySnapshot Blocked(
        BuildableObject facility,
        string reason)
    {
        return new SurgicalFacilitySnapshot(
            facility,
            SurgeryFacilityTag.None,
            0f,
            1f,
            0f,
            0f,
            Array.Empty<BuildableObject>(),
            reason);
    }

    public static string FormatTags(SurgeryFacilityTag tags)
    {
        List<string> labels = new List<string>();
        Add(SurgeryFacilityTag.Emergency, "응급");
        Add(SurgeryFacilityTag.Anatomy, "해부");
        Add(SurgeryFacilityTag.GeneralSurgery, "외과");
        Add(SurgeryFacilityTag.Sterilization, "세정");
        Add(SurgeryFacilityTag.Anesthesia, "마취");
        Add(SurgeryFacilityTag.Transplant, "순환 이식");
        Add(SurgeryFacilityTag.ImmuneControl, "면역 조절");
        Add(SurgeryFacilityTag.IsolationRecovery, "격리 회복");
        Add(SurgeryFacilityTag.ArcaneSurgery, "비전 개조");
        Add(SurgeryFacilityTag.RuneSuture, "룬 봉합");
        Add(SurgeryFacilityTag.Rehabilitation, "재활");
        Add(SurgeryFacilityTag.OrganStorage, "장기 보관");
        Add(SurgeryFacilityTag.ProstheticAssembly, "보철 조립");
        return string.Join(", ", labels);

        void Add(SurgeryFacilityTag tag, string label)
        {
            if ((tags & tag) != 0)
            {
                labels.Add(label);
            }
        }
    }
}

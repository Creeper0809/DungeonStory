using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CircusProgramRegistry
{
    private readonly Dictionary<string, ICircusProgramHandler> handlers =
        new Dictionary<string, ICircusProgramHandler>(StringComparer.Ordinal);

    public CircusProgramRegistry(IEnumerable<ICircusProgramHandler> handlers)
    {
        foreach (ICircusProgramHandler handler in handlers
                     ?? Array.Empty<ICircusProgramHandler>())
        {
            string id = handler?.Definition?.programId?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!this.handlers.TryAdd(id, handler))
            {
                throw new InvalidOperationException($"Duplicate circus program '{id}'.");
            }
        }
    }

    public IReadOnlyList<CircusProgramModule> Definitions =>
        handlers.Values.Select(handler => handler.Definition).ToArray();

    public bool TryGet(string programId, out ICircusProgramHandler handler)
    {
        return handlers.TryGetValue(programId?.Trim() ?? string.Empty, out handler);
    }
}

public abstract class CircusProgramHandlerBase : ICircusProgramHandler
{
    protected CircusProgramHandlerBase(CircusProgramModule definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public CircusProgramModule Definition { get; }

    public virtual bool Validate(
        CircusShowOrder order,
        IReadOnlyList<CaptiveState> performers,
        out string failureReason)
    {
        if (Definition.requiresCaptive && (performers == null || performers.Count == 0))
        {
            failureReason = "이 프로그램에는 포로 공연자가 필요합니다.";
            return false;
        }

        if (Definition.requiresWildlife
            && (order?.wildlifeIds == null || order.wildlifeIds.Count == 0))
        {
            failureReason = "이 프로그램에는 포획 동물이 필요합니다.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    public virtual CircusProgramSettlement Settle(
        CircusShowOrder order,
        IReadOnlyList<CaptiveState> performers)
    {
        float skill = performers?.Select(item => item.performerSkill).DefaultIfEmpty(0f).Average()
            ?? 0f;
        return new CircusProgramSettlement(
            Definition.baseAudienceSatisfaction + skill * 0.12f,
            Definition.basePerformerFame,
            Definition.publiclyCruel || Definition.usesCombat,
            Definition.usesCombat || Definition.baseAccidentRisk > 0f,
            $"{Definition.displayName} 공연이 끝났습니다.");
    }
}

public sealed class NonlethalActCircusProgram : CircusProgramHandlerBase
{
    public NonlethalActCircusProgram() : base(new CircusProgramModule
    {
        programId = "circus:nonlethal-act",
        displayName = "비살상 재주",
        requiresCaptive = true,
        baseAudienceSatisfaction = 62f,
        basePerformerFame = 3f
    })
    {
    }
}

public sealed class DangerousStuntCircusProgram : CircusProgramHandlerBase
{
    public DangerousStuntCircusProgram() : base(new CircusProgramModule
    {
        programId = "circus:dangerous-stunt",
        displayName = "위험 묘기",
        requiresCaptive = true,
        baseAccidentRisk = 0.18f,
        baseAudienceSatisfaction = 70f,
        basePerformerFame = 5f
    })
    {
    }
}

public sealed class CaptiveDuelCircusProgram : CircusProgramHandlerBase
{
    public CaptiveDuelCircusProgram() : base(new CircusProgramModule
    {
        programId = "circus:captive-duel",
        displayName = "포로 결투",
        requiresCaptive = true,
        usesCombat = true,
        publiclyCruel = true,
        baseAudienceSatisfaction = 76f,
        basePerformerFame = 7f
    })
    {
    }

    public override bool Validate(
        CircusShowOrder order,
        IReadOnlyList<CaptiveState> performers,
        out string failureReason)
    {
        if (!base.Validate(order, performers, out failureReason))
        {
            return false;
        }

        if (performers.Count < 2)
        {
            failureReason = "포로 결투에는 공연자 두 명이 필요합니다.";
            return false;
        }

        return true;
    }
}

public sealed class BeastShowCircusProgram : CircusProgramHandlerBase
{
    public BeastShowCircusProgram() : base(new CircusProgramModule
    {
        programId = "circus:beast-show",
        displayName = "야수 공연",
        requiresWildlife = true,
        baseAccidentRisk = 0.12f,
        baseAudienceSatisfaction = 68f,
        basePerformerFame = 4f
    })
    {
    }
}

public sealed class BeastArenaCircusProgram : CircusProgramHandlerBase
{
    public BeastArenaCircusProgram() : base(new CircusProgramModule
    {
        programId = "circus:beast-arena",
        displayName = "야수 투기",
        requiresWildlife = true,
        usesCombat = true,
        publiclyCruel = true,
        baseAudienceSatisfaction = 80f,
        basePerformerFame = 8f
    })
    {
    }
}

public sealed class PublicPunishmentCircusProgram : CircusProgramHandlerBase
{
    public PublicPunishmentCircusProgram() : base(new CircusProgramModule
    {
        programId = "circus:public-punishment",
        displayName = "공개 처벌",
        requiresCaptive = true,
        publiclyCruel = true,
        baseAudienceSatisfaction = 58f,
        basePerformerFame = 2f
    })
    {
    }
}

public sealed class ExecutionPlayCircusProgram : CircusProgramHandlerBase
{
    public ExecutionPlayCircusProgram() : base(new CircusProgramModule
    {
        programId = "circus:execution-play",
        displayName = "처형극",
        requiresCaptive = true,
        usesCombat = true,
        publiclyCruel = true,
        baseAudienceSatisfaction = 72f,
        basePerformerFame = 10f
    })
    {
    }
}

public sealed class PublicCorruptionRitualCircusProgram : CircusProgramHandlerBase
{
    public PublicCorruptionRitualCircusProgram() : base(new CircusProgramModule
    {
        programId = "circus:public-corruption",
        displayName = "타락 의식 공개",
        requiresCaptive = true,
        publiclyCruel = true,
        baseAudienceSatisfaction = 74f,
        basePerformerFame = 6f
    })
    {
    }
}

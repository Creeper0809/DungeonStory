using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityInteractionRegistry
{
    private readonly System.Collections.Generic.Dictionary<string, ICaptivityInteractionHandler>
        handlers = new System.Collections.Generic.Dictionary<string, ICaptivityInteractionHandler>(
            System.StringComparer.Ordinal);

    public CaptivityInteractionRegistry(
        System.Collections.Generic.IEnumerable<ICaptivityInteractionHandler> handlers)
    {
        foreach (ICaptivityInteractionHandler handler in handlers
                     ?? System.Array.Empty<ICaptivityInteractionHandler>())
        {
            if (handler == null || string.IsNullOrWhiteSpace(handler.InteractionId))
            {
                continue;
            }

            if (!this.handlers.TryAdd(handler.InteractionId, handler))
            {
                throw new System.InvalidOperationException(
                    $"Duplicate captivity interaction '{handler.InteractionId}'.");
            }
        }
    }

    public System.Collections.Generic.IReadOnlyCollection<ICaptivityInteractionHandler> All =>
        handlers.Values;

    public bool TryGet(string interactionId, out ICaptivityInteractionHandler handler)
    {
        return handlers.TryGetValue(interactionId?.Trim() ?? string.Empty, out handler);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public abstract class CaptivityInteractionHandlerBase : ICaptivityInteractionHandler
{
    private static readonly System.Collections.Generic.IReadOnlyDictionary<StockCategory, int>
        EmptyMaterials = new System.Collections.Generic.Dictionary<StockCategory, int>();

    public abstract string InteractionId { get; }
    public abstract string DisplayName { get; }
    public abstract CaptiveInteractionKind Kind { get; }
    public abstract float RequiredWork { get; }
    public virtual System.Collections.Generic.IReadOnlyDictionary<StockCategory, int>
        MaterialRequirements => EmptyMaterials;

    protected static System.Collections.Generic.IReadOnlyDictionary<StockCategory, int>
        Materials(params (StockCategory category, int amount)[] values)
    {
        System.Collections.Generic.Dictionary<StockCategory, int> result =
            new System.Collections.Generic.Dictionary<StockCategory, int>();
        foreach ((StockCategory category, int amount) in values)
        {
            if (amount > 0)
            {
                result[category] = amount;
            }
        }

        return result;
    }

    public virtual bool CanExecute(
        CaptivityInteractionContext context,
        out string failureReason)
    {
        if (context.Captive == null || !context.Captive.IsInCustody)
        {
            failureReason = "대상이 더 이상 수용 중이 아닙니다.";
            return false;
        }

        if (!context.SubjectAvailable)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }

        if (!context.WardenAvailable)
        {
            failureReason = "담당자가 작업할 수 없는 상태입니다.";
            return false;
        }

        if (!context.FacilityAvailable)
        {
            failureReason = "포로 관리 작업을 수행할 유효한 수용 시설이 필요합니다.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    public abstract CaptivityInteractionResult Execute(
        CaptivityInteractionContext context);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityPersuasionHandler : CaptivityInteractionHandlerBase
{
    public override string InteractionId => "captivity:persuasion";
    public override string DisplayName => "회유";
    public override CaptiveInteractionKind Kind => CaptiveInteractionKind.Persuasion;
    public override float RequiredWork => 14f;
    public override System.Collections.Generic.IReadOnlyDictionary<StockCategory, int>
        MaterialRequirements => Materials((StockCategory.Food, 1));

    public override CaptivityInteractionResult Execute(CaptivityInteractionContext context)
    {
        return new CaptivityInteractionResult(
            true,
            "차분한 대화가 경계를 조금 누그러뜨렸습니다.",
            willDelta: -3f,
            trustDelta: 9f,
            grudgeDelta: -4f);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityIsolationHandler : CaptivityInteractionHandlerBase
{
    public override string InteractionId => "captivity:isolation";
    public override string DisplayName => "격리";
    public override CaptiveInteractionKind Kind => CaptiveInteractionKind.Isolation;
    public override float RequiredWork => 10f;
    public override System.Collections.Generic.IReadOnlyDictionary<StockCategory, int>
        MaterialRequirements => Materials((StockCategory.General, 1));

    public override CaptivityInteractionResult Execute(CaptivityInteractionContext context)
    {
        return new CaptivityInteractionResult(
            true,
            "고립된 시간이 의지를 깎았지만 원한도 남겼습니다.",
            willDelta: -10f,
            fearDelta: 5f,
            trustDelta: -3f,
            grudgeDelta: 5f);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityCoercionHandler : CaptivityInteractionHandlerBase
{
    public override string InteractionId => "captivity:coercion";
    public override string DisplayName => "강압";
    public override CaptiveInteractionKind Kind => CaptiveInteractionKind.Coercion;
    public override float RequiredWork => 12f;
    public override System.Collections.Generic.IReadOnlyDictionary<StockCategory, int>
        MaterialRequirements => Materials((StockCategory.General, 1));

    public override CaptivityInteractionResult Execute(CaptivityInteractionContext context)
    {
        return new CaptivityInteractionResult(
            true,
            "공포는 빠르게 번졌고, 그만큼 깊은 원한이 남았습니다.",
            willDelta: -16f,
            fearDelta: 18f,
            trustDelta: -10f,
            grudgeDelta: 14f,
            healthDelta: -6f);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityInterrogationHandler : CaptivityInteractionHandlerBase
{
    public override string InteractionId => "captivity:interrogation";
    public override string DisplayName => "심문";
    public override CaptiveInteractionKind Kind => CaptiveInteractionKind.Interrogation;
    public override float RequiredWork => 18f;
    public override System.Collections.Generic.IReadOnlyDictionary<StockCategory, int>
        MaterialRequirements => Materials((StockCategory.General, 1));

    public override CaptivityInteractionResult Execute(CaptivityInteractionContext context)
    {
        bool unreliable = context.Captive.fear >= 75f;
        return new CaptivityInteractionResult(
            true,
            unreliable
                ? "포로는 원하는 답을 내놓았습니다. 진실인지는 알 수 없습니다."
                : "조각난 정보 하나를 확보했습니다.",
            willDelta: -8f,
            fearDelta: 9f,
            trustDelta: -4f,
            grudgeDelta: 7f);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityIndoctrinationHandler : CaptivityInteractionHandlerBase
{
    public override string InteractionId => "captivity:indoctrination";
    public override string DisplayName => "교화";
    public override CaptiveInteractionKind Kind => CaptiveInteractionKind.Indoctrination;
    public override float RequiredWork => 24f;
    public override System.Collections.Generic.IReadOnlyDictionary<StockCategory, int>
        MaterialRequirements => Materials((StockCategory.Mana, 1));

    public override CaptivityInteractionResult Execute(CaptivityInteractionContext context)
    {
        return new CaptivityInteractionResult(
            true,
            "반복된 교리가 의심의 자리를 차지하기 시작했습니다.",
            willDelta: -7f,
            trustDelta: 5f,
            grudgeDelta: -2f,
            corruptionDelta: 7f);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityBrandingHandler : CaptivityInteractionHandlerBase
{
    public override string InteractionId => "captivity:branding";
    public override string DisplayName => "각인";
    public override CaptiveInteractionKind Kind => CaptiveInteractionKind.Branding;
    public override float RequiredWork => 20f;
    public override System.Collections.Generic.IReadOnlyDictionary<StockCategory, int>
        MaterialRequirements => Materials(
            (StockCategory.General, 1),
            (StockCategory.Fuel, 1));

    public override CaptivityInteractionResult Execute(CaptivityInteractionContext context)
    {
        return new CaptivityInteractionResult(
            true,
            "지워지지 않는 표식이 공포와 원한을 함께 남겼습니다.",
            willDelta: -11f,
            fearDelta: 13f,
            trustDelta: -8f,
            grudgeDelta: 12f,
            corruptionDelta: 9f,
            healthDelta: -8f);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityBloodExtractionHandler : CaptivityInteractionHandlerBase
{
    public override string InteractionId => "captivity:blood-extraction";
    public override string DisplayName => "혈액 추출";
    public override CaptiveInteractionKind Kind => CaptiveInteractionKind.BloodExtraction;
    public override float RequiredWork => 16f;
    public override System.Collections.Generic.IReadOnlyDictionary<StockCategory, int>
        MaterialRequirements => Materials((StockCategory.Medicine, 1));

    public override CaptivityInteractionResult Execute(CaptivityInteractionContext context)
    {
        return new CaptivityInteractionResult(
            true,
            "혈액을 확보했습니다. 포로의 상태는 눈에 띄게 나빠졌습니다.",
            fearDelta: 10f,
            trustDelta: -9f,
            grudgeDelta: 11f,
            healthDelta: -18f,
            outputItemId: CaptivityItemDefinitions.ExtractedBloodItemId,
            outputAmount: 1);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityMemoryExtractionHandler : CaptivityInteractionHandlerBase
{
    public override string InteractionId => "captivity:memory-extraction";
    public override string DisplayName => "기억 추출";
    public override CaptiveInteractionKind Kind => CaptiveInteractionKind.MemoryExtraction;
    public override float RequiredWork => 28f;
    public override System.Collections.Generic.IReadOnlyDictionary<StockCategory, int>
        MaterialRequirements => Materials((StockCategory.Mana, 1));

    public override CaptivityInteractionResult Execute(CaptivityInteractionContext context)
    {
        return new CaptivityInteractionResult(
            true,
            "기억의 일부가 응결됐습니다. 남은 자아는 전보다 흐릿합니다.",
            willDelta: -14f,
            fearDelta: 12f,
            trustDelta: -12f,
            grudgeDelta: 8f,
            corruptionDelta: 13f,
            healthDelta: -10f,
            outputItemId: CaptivityItemDefinitions.MemoryResidueItemId,
            outputAmount: 1);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityForcedModificationHandler : CaptivityInteractionHandlerBase
{
    public override string InteractionId => "captivity:forced-modification";
    public override string DisplayName => "강제 개조";
    public override CaptiveInteractionKind Kind => CaptiveInteractionKind.ForcedModification;
    public override float RequiredWork => 36f;
    public override System.Collections.Generic.IReadOnlyDictionary<StockCategory, int>
        MaterialRequirements => Materials(
            (StockCategory.General, 2),
            (StockCategory.Medicine, 1));

    public override CaptivityInteractionResult Execute(CaptivityInteractionContext context)
    {
        return new CaptivityInteractionResult(
            true,
            "육체는 명령에 맞춰졌지만 원한까지 사라지지는 않았습니다.",
            willDelta: -15f,
            fearDelta: 15f,
            trustDelta: -15f,
            grudgeDelta: 18f,
            corruptionDelta: 20f,
            healthDelta: -16f);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityCorruptionRitualHandler : CaptivityInteractionHandlerBase
{
    public override string InteractionId => "captivity:corruption-ritual";
    public override string DisplayName => "타락 의식";
    public override CaptiveInteractionKind Kind => CaptiveInteractionKind.CorruptionRitual;
    public override float RequiredWork => 42f;
    public override System.Collections.Generic.IReadOnlyDictionary<StockCategory, int>
        MaterialRequirements => Materials(
            (StockCategory.Mana, 1),
            (StockCategory.Biological, 1));

    public override CaptivityInteractionResult Execute(CaptivityInteractionContext context)
    {
        return new CaptivityInteractionResult(
            true,
            "낯선 충동이 포로의 선택을 잠식하기 시작했습니다.",
            willDelta: -12f,
            fearDelta: 8f,
            trustDelta: -8f,
            grudgeDelta: 7f,
            corruptionDelta: 28f,
            healthDelta: -10f);
    }
}

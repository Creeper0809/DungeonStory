#if UNITY_EDITOR
using System;
using System.Collections;

/// <summary>
/// Typed live-scene seam owned by the PlayMode verifier. Preparation creates
/// or selects the real facility, worker, physical inputs and one-cycle bill,
/// but must not execute the cycle before returning its durable correlation.
/// Production then drives the real one-cycle bill until its completion receipt
/// is queryable. Clearance receives that actual receipt and drives scheduler-
/// owned AI haul while capturing the shared natural-clearance witness. Frozen
/// request data never acts as runtime state.
/// </summary>
public interface IProductionOutputClearanceRecipeNaturalScenarioDriver
{
    bool TryPrepare(
        ProductionOutputClearanceNaturalExecutionRequest request,
        out ProductionRecipeExecutionCorrelation correlation,
        out string failureReason);

    IEnumerator ExecutePreparedProduction(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionRecipeExecutionCorrelation correlation,
        ProductionOutputClearanceNaturalProductionStageResult result);

    IEnumerator ExecutePreparedClearance(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionRecipeExecutionCorrelation correlation,
        ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        ProductionOutputClearanceNaturalClearanceStageResult result);

    bool TryRollbackPrepared(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionRecipeExecutionCorrelation correlation,
        out string failureReason);

    bool TryFinalizeAccepted(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionRecipeExecutionCorrelation correlation,
        out string failureReason);
}

public sealed class ProductionOutputClearanceRecipeNaturalMeasurementExecutor :
    IProductionOutputClearanceNaturalMeasurementExecutor
{
    public const string Id = "natural-measurement-executor:recipe";
    public const int Version = 1;

    private readonly IProductionOutputClearanceRecipeNaturalScenarioDriver driver;
    private readonly IProductionRecipeExecutionCorrelationCommand correlations;
    private readonly ProductionOutputClearanceNaturalMeasurementHandlerRegistry
        handlers;

    public ProductionOutputClearanceRecipeNaturalMeasurementExecutor(
        IProductionOutputClearanceRecipeNaturalScenarioDriver driver,
        IProductionRecipeExecutionCorrelationCommand correlations,
        ProductionOutputClearanceNaturalMeasurementHandlerRegistry handlers)
    {
        this.driver = driver ?? throw new ArgumentNullException(nameof(driver));
        this.correlations = correlations
            ?? throw new ArgumentNullException(nameof(correlations));
        this.handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
    }

    public string ExecutorId => Id;
    public int ContractVersion => Version;
    public string PayloadKind => "recipe";

    public IEnumerator Execute(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceNaturalExecutionResult result) =>
        ExecuteCore(request, result);

    private IEnumerator ExecuteCore(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceNaturalExecutionResult result)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        if (result.IsTerminal)
        {
            throw new InvalidOperationException(
                "A terminal natural execution result cannot be reused.");
        }
        if (request.Descriptor.Payload is not
                ProductionOutputClearanceRecipeExecutablePayload payload)
        {
            result.Fail("recipe-natural-executor-payload-mismatch");
            yield break;
        }

        if (!driver.TryPrepare(
                request,
                out ProductionRecipeExecutionCorrelation correlation,
                out string prepareFailure)
            || correlation == null)
        {
            result.Fail(CanonicalFailure(
                prepareFailure,
                "recipe-natural-executor-prepare-failed"));
            yield break;
        }
        if (!string.Equals(
                correlation.RecipeId,
                payload.RecipeId,
                StringComparison.Ordinal))
        {
            RollbackOrThrow(request, correlation);
            result.Fail("recipe-natural-executor-correlation-recipe-mismatch");
            yield break;
        }
        if (!correlations.TryRegisterExecution(
                request.ActionId,
                correlation,
                out string registrationFailure))
        {
            RollbackOrThrow(request, correlation);
            result.Fail(CanonicalFailure(
                registrationFailure,
                "recipe-natural-executor-correlation-register-failed"));
            yield break;
        }

        ProductionOutputClearanceNaturalProductionStageResult production = new();
        IEnumerator execution = driver.ExecutePreparedProduction(
            request,
            correlation,
            production);
        if (execution == null)
        {
            CancelOrThrow(request, correlation);
            result.Fail("recipe-natural-executor-driver-returned-null");
            yield break;
        }
        bool productionEnumerationCompleted = false;
        try
        {
            while (true)
            {
                bool moved;
                object current = null;
                try
                {
                    moved = execution.MoveNext();
                    if (moved)
                        current = execution.Current;
                }
                catch
                {
                    CancelOrThrow(request, correlation);
                    productionEnumerationCompleted = true;
                    throw;
                }
                if (!moved)
                {
                    productionEnumerationCompleted = true;
                    break;
                }
                yield return current;
            }
        }
        finally
        {
            (execution as IDisposable)?.Dispose();
            if (!productionEnumerationCompleted)
                CancelOrThrow(request, correlation);
        }

        if (!production.IsTerminal || !production.Succeeded)
        {
            CancelOrThrow(request, correlation);
            result.Fail(CanonicalFailure(
                production.FailureReason,
                production.IsTerminal
                    ? "recipe-natural-executor-driver-failed"
                    : "recipe-natural-executor-driver-not-terminal"));
            yield break;
        }

        if (!handlers.TryCaptureCompleted(
                request.Descriptor,
                request.ActionId,
                out ProductionOutputClearanceExecutionReceiptSnapshot receipt,
                out string receiptFailure)
            || receipt == null)
        {
            UnityEngine.Debug.Log(
                "V27_NATURAL_RECIPE_RECEIPT_CAPTURE_FAILURE "
                + (string.IsNullOrEmpty(receiptFailure)
                    ? "<empty>"
                    : receiptFailure));
            CancelOrThrow(request, correlation);
            result.Fail(CanonicalFailure(
                receiptFailure,
                "recipe-natural-executor-runtime-receipt-missing"));
            yield break;
        }

        ProductionOutputClearanceNaturalClearanceStageResult clearance = new();
        IEnumerator clearanceExecution = driver.ExecutePreparedClearance(
            request,
            correlation,
            receipt,
            clearance);
        if (clearanceExecution == null)
        {
            CancelOrThrow(request, correlation);
            result.Fail("recipe-natural-executor-clearance-driver-returned-null");
            yield break;
        }
        bool clearanceEnumerationCompleted = false;
        try
        {
            while (true)
            {
                bool moved;
                object current = null;
                try
                {
                    moved = clearanceExecution.MoveNext();
                    if (moved)
                        current = clearanceExecution.Current;
                }
                catch
                {
                    CancelOrThrow(request, correlation);
                    clearanceEnumerationCompleted = true;
                    throw;
                }
                if (!moved)
                {
                    clearanceEnumerationCompleted = true;
                    break;
                }
                yield return current;
            }
        }
        finally
        {
            (clearanceExecution as IDisposable)?.Dispose();
            if (!clearanceEnumerationCompleted)
                CancelOrThrow(request, correlation);
        }
        if (!clearance.IsTerminal
            || !clearance.Succeeded
            || clearance.Witness == null)
        {
            CancelOrThrow(request, correlation);
            result.Fail(CanonicalFailure(
                clearance.FailureReason,
                clearance.IsTerminal
                    ? "recipe-natural-executor-clearance-failed"
                    : "recipe-natural-executor-clearance-not-terminal"));
            yield break;
        }

        ProductionOutputClearanceNaturalShardEvidence evidence;
        try
        {
            evidence = ProductionOutputClearanceNaturalEvidenceAssembler.Assemble(
                request,
                receipt,
                clearance.Witness);
        }
        catch
        {
            CancelOrThrow(request, correlation);
            throw;
        }
        bool acknowledged;
        string ackFailure;
        try
        {
            acknowledged = handlers.TryAcknowledgeAccepted(
                receipt,
                out ackFailure);
        }
        catch
        {
            CancelOrThrow(request, correlation);
            throw;
        }
        if (!acknowledged)
        {
            CancelOrThrow(request, correlation);
            result.Fail(CanonicalFailure(
                ackFailure,
                "recipe-natural-executor-receipt-acknowledgement-failed"));
            yield break;
        }
        if (!driver.TryFinalizeAccepted(
                request,
                correlation,
                out string finalizeFailure))
        {
            UnityEngine.Debug.Log(
                "V27_NATURAL_RECIPE_FINALIZE_FAILURE "
                + (string.IsNullOrEmpty(finalizeFailure)
                    ? "<empty>"
                    : finalizeFailure));
            result.Fail(CanonicalFailure(
                finalizeFailure,
                "recipe-natural-executor-finalize-failed"));
            yield break;
        }

        result.Complete(evidence);
    }

    private void CancelOrThrow(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionRecipeExecutionCorrelation correlation)
    {
        if (!correlations.TryCancelExecution(
                request.ActionId,
                correlation,
                out string correlationFailure))
        {
            throw new InvalidOperationException(
                "Recipe natural execution could not cancel its correlation: "
                + correlationFailure);
        }
        RollbackOrThrow(request, correlation);
    }

    private void RollbackOrThrow(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionRecipeExecutionCorrelation correlation)
    {
        if (!driver.TryRollbackPrepared(
                request,
                correlation,
                out string rollbackFailure))
        {
            throw new InvalidOperationException(
                "Recipe natural execution could not rollback its prepared live scenario: "
                + rollbackFailure);
        }
    }

    private static string CanonicalFailure(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return fallback;
        }
        for (int index = 0; index < value.Length; index++)
        {
            if (char.IsWhiteSpace(value[index]))
                return fallback;
        }
        return value;
    }
}
#endif

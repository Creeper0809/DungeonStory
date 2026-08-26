#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PreparedOutputExactDestinationAdmissionCandidateDebugScenarios
{
    private const string ParticipantId =
        "items.prepared-output-exact-destination-admission.v1";

    [MenuItem(
        "DungeonStory/Debug/Items/Run Prepared Output Admission Candidate Guards")]
    public static void RunAll()
    {
        PreparedOutputExactDestinationAdmissionRequest request = CreateRequest();
        PreparedOutputExactDestinationAdmissionCandidate registered =
            CreateCandidate(ParticipantId, request);
        Dictionary<string, PreparedOutputExactDestinationAdmissionCandidate>
            candidates = new(StringComparer.Ordinal)
            {
                [request.AdmissionOperationId] = registered
            };

        Require(
            PreparedOutputExactDestinationAdmissionParticipant
                .IsCandidateOwnedOrTerminal(
                    candidates,
                    ParticipantId,
                    registered),
            "the exact registered Prepared candidate must be accepted");

        PreparedOutputExactDestinationAdmissionCandidate forged =
            CreateCandidate(ParticipantId, request);
        Require(
            !PreparedOutputExactDestinationAdmissionParticipant
                .IsCandidateOwnedOrTerminal(
                    candidates,
                    ParticipantId,
                    forged),
            "a same-operation Prepared candidate with a different reference must fail");

        Require(
            !PreparedOutputExactDestinationAdmissionParticipant
                .IsCandidateOwnedOrTerminal(
                    candidates,
                    "items.wrong-participant.v1",
                    registered),
            "a candidate presented to another participant must fail");

        registered.Phase =
            PreparedOutputExactDestinationAdmissionPhase.Published;
        Require(
            PreparedOutputExactDestinationAdmissionParticipant
                .IsCandidateOwnedOrTerminal(
                    candidates,
                    ParticipantId,
                    registered),
            "the exact registered Published candidate must be accepted");

        candidates.Clear();
        Require(
            !PreparedOutputExactDestinationAdmissionParticipant
                .IsCandidateOwnedOrTerminal(
                    candidates,
                    ParticipantId,
                    registered),
            "an unregistered Published candidate must fail");

        registered.Phase =
            PreparedOutputExactDestinationAdmissionPhase.Completed;
        Require(
            PreparedOutputExactDestinationAdmissionParticipant
                .IsCandidateOwnedOrTerminal(
                    candidates,
                    ParticipantId,
                    registered),
            "a forgotten Completed candidate must remain idempotently accepted");

        registered.Phase =
            PreparedOutputExactDestinationAdmissionPhase.RolledBack;
        Require(
            PreparedOutputExactDestinationAdmissionParticipant
                .IsCandidateOwnedOrTerminal(
                    candidates,
                    ParticipantId,
                    registered),
            "a forgotten RolledBack candidate must remain idempotently accepted");

        Debug.Log("Prepared-output admission candidate guards PASS.");
    }

    private static PreparedOutputExactDestinationAdmissionRequest CreateRequest()
    {
        PreparedOutputExactDestinationAuthoritySnapshot authority = new(
            PreparedOutputExactDestinationTargetKind.Warehouse,
            "warehouse:test",
            new Vector2Int(3, 4),
            new string('a', 64),
            7L,
            11L,
            25_000L,
            0L);
        return new PreparedOutputExactDestinationAdmissionRequest(
            "admission:test-candidate-reference",
            "route:test-candidate-reference",
            new string('b', 64),
            new string('c', 64),
            new[]
            {
                new PreparedOutputExactDestinationLotSlice(
                    "stack:test-candidate-reference",
                    2,
                    13L,
                    new string('d', 64),
                    1_200L)
            },
            authority);
    }

    private static PreparedOutputExactDestinationAdmissionCandidate CreateCandidate(
        string participantId,
        PreparedOutputExactDestinationAdmissionRequest request)
    {
        PreparedOutputExactDestinationAdmissionHandle handle = new(
            PreparedOutputExactDestinationTargetKind.Warehouse,
            default,
            new string('e', 64),
            new string('f', 64));
        return new PreparedOutputExactDestinationAdmissionCandidate(
            participantId,
            request,
            handle);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif

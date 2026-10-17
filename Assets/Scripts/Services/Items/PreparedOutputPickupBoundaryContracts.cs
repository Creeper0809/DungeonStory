using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using UnityEngine;


internal enum PreparedOutputPickupBoundaryFailureCode
{
    None = 0,
    CustodyMalformed = 1,
    IntentMissing = 2,
    IntentProjectionStale = 3,
    QuantityLeaseStale = 4,
    SourceLotStale = 5,
    AdmissionMissing = 6,
    AdmissionNotReserved = 7,
    AdmissionProjectionStale = 8,
    DestinationAuthorityStale = 9
}

internal readonly struct PreparedOutputPickupBoundaryResult
{
    internal PreparedOutputPickupBoundaryResult(
        PreparedOutputPickupBoundaryFailureCode code,
        string detail = "")
    {
        Code = code;
        Detail = detail ?? string.Empty;
    }

    internal PreparedOutputPickupBoundaryFailureCode Code { get; }
    internal string Detail { get; }
    internal bool IsFailure => Code != PreparedOutputPickupBoundaryFailureCode.None;

    public override string ToString() => IsFailure
        ? $"items.haul.prepared_output_pickup_boundary:{Code}:{Detail}"
        : string.Empty;
}

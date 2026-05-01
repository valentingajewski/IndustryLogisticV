using System;
using System.Collections.Generic;

namespace IndustryLogisticV.Systems
{
    internal static class MarkerConstants
    {
        internal static readonly HashSet<string> PreserveConfiguredZMarkerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Morningwood",
            "Burton Mall",
            "US Route 15",
            "US Route 68 - Zancudo",
            "US Route 68 - Grand Senora Desert - East",
            "US Route 13",
            "Popular St",
            "Marina Dr",
            "Sandy Shores Marina Drive",
            "El Rancho Blvd",
        };
    }
}
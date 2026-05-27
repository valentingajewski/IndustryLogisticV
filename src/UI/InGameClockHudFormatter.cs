using System;
using System.Globalization;

namespace LSOL.UI
{
    internal static class InGameClockHudFormatter
    {
        public static string BuildCompactLabel(DateTime clockDateTime)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1:00}:{2:00}",
                clockDateTime.ToString("ddd", CultureInfo.InvariantCulture),
                clockDateTime.Hour,
                clockDateTime.Minute);
        }
    }
}
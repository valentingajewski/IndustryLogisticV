using System;
using System.Globalization;

namespace LSOL
{
    internal static class ModMath
    {
        public static float Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            if (value >= 1f)
            {
                return 1f;
            }

            return value;
        }
    }

    internal static class ModFormatting
    {
        private const string PlayerFacingNumberFormat = "#,##0.00";
        private static readonly CultureInfo PlayerFacingCulture = CultureInfo.InvariantCulture;

        public static string FormatNumber(double value)
        {
            return value.ToString(PlayerFacingNumberFormat, PlayerFacingCulture);
        }

        public static string FormatSignedNumber(double value)
        {
            return FormatSignedCore(value, FormatNumber(Math.Abs(value)));
        }

        public static string FormatMoney(double amount)
        {
            return FormatMoneyCore(amount, includePositiveSign: false);
        }

        public static string FormatSignedMoney(double amount)
        {
            return FormatMoneyCore(amount, includePositiveSign: true);
        }

        public static string FormatPercent(double value)
        {
            return string.Concat(FormatNumber(value), "%");
        }

        public static string FormatSignedPercent(double value)
        {
            return FormatSignedCore(value, FormatPercent(Math.Abs(value)));
        }

        public static string FormatTons(double value)
        {
            return string.Concat(FormatNumber(value), "t");
        }

        public static string FormatLiters(double value)
        {
            return string.Concat(FormatNumber(value), "L");
        }

        public static string FormatRatePerHour(double value, string unit)
        {
            return string.Format(PlayerFacingCulture, "{0} {1}/h", FormatNumber(value), unit ?? string.Empty);
        }

        public static string FormatPricePerTon(double value)
        {
            return string.Concat(FormatMoney(value), "/t");
        }

        public static string FormatSpeed(double value, string unit)
        {
            return string.Format(PlayerFacingCulture, "{0} {1}", FormatNumber(value), unit ?? string.Empty);
        }

        public static string FormatDistance(double meters, bool useMetric)
        {
            var distanceMeters = Math.Max(0d, meters);
            if (useMetric)
            {
                if (distanceMeters < 1000d)
                {
                    return string.Format(PlayerFacingCulture, "{0:0} m", distanceMeters);
                }

                var distanceKilometers = distanceMeters / 1000d;
                return distanceKilometers < 10d
                    ? string.Format(PlayerFacingCulture, "{0:0.0} km", distanceKilometers)
                    : string.Format(PlayerFacingCulture, "{0:0} km", distanceKilometers);
            }

            var distanceMiles = distanceMeters / 1609.344d;
            if (distanceMiles < 0.05d)
            {
                return "0 mi";
            }

            return distanceMiles < 10d
                ? string.Format(PlayerFacingCulture, "{0:0.0} mi", distanceMiles)
                : string.Format(PlayerFacingCulture, "{0:0} mi", distanceMiles);
        }

        public static string FormatRatio(double current, double capacity, string unitSuffix)
        {
            return string.Format(PlayerFacingCulture, "{0}/{1}{2}", FormatNumber(current), FormatNumber(capacity), unitSuffix ?? string.Empty);
        }

        private static string FormatMoneyCore(double amount, bool includePositiveSign)
        {
            var absolute = string.Concat("$", FormatNumber(Math.Abs(amount)));
            return FormatSignedCore(amount, absolute, includePositiveSign);
        }

        private static string FormatSignedCore(double value, string absolute)
        {
            return FormatSignedCore(value, absolute, includePositiveSign: true);
        }

        private static string FormatSignedCore(double value, string absolute, bool includePositiveSign)
        {
            if (value < 0d)
            {
                return string.Concat("-", absolute);
            }

            if (includePositiveSign && value > 0d)
            {
                return string.Concat("+", absolute);
            }

            return absolute;
        }
    }

    internal static class ModDiagnostics
    {
        public static string FormatFailure(string action, Exception exception)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                action = "Operation";
            }

            if (exception == null)
            {
                return action + " failed.";
            }

            var detail = string.IsNullOrWhiteSpace(exception.Message)
                ? exception.GetType().Name
                : string.Format("{0}: {1}", exception.GetType().Name, exception.Message.Trim());
            return string.Format("{0} failed. {1}", action, detail);
        }
    }
}
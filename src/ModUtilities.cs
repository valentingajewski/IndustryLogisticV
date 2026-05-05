using System;

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
        public static string FormatMoney(float amount)
        {
            var absolute = Math.Abs(amount).ToString("0,0");
            return amount < 0f ? string.Format("-${0}", absolute) : string.Format("${0}", absolute);
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
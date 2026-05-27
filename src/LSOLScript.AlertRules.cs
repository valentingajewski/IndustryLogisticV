using System;
using GTA;
using LSOL.Systems;
using LSOL.UI;

namespace LSOL
{
    public sealed partial class LSOLScript
    {
        private static readonly AlertLeadTimeMode[] AlertLeadTimeOptions =
        {
            AlertLeadTimeMode.Off,
            AlertLeadTimeMode.DueNow,
            AlertLeadTimeMode.Within60Minutes,
            AlertLeadTimeMode.Within180Minutes,
            AlertLeadTimeMode.WithinDay,
        };

        private static readonly FleetAlertMode[] FleetAlertModeOptions =
        {
            FleetAlertMode.Off,
            FleetAlertMode.CriticalOnly,
            FleetAlertMode.WatchAndCritical,
        };

        private static readonly TerritoryAlertMode[] TerritoryAlertModeOptions =
        {
            TerritoryAlertMode.Off,
            TerritoryAlertMode.ChargesOnly,
            TerritoryAlertMode.ChargesAndRisk,
        };

        private void EvaluateAmbientAlertRules(int gameTime)
        {
            if (_tabletStateStore == null || gameTime < _nextAlertRuleAllowedAtMs)
            {
                return;
            }

            if (_lastAlertRuleEvaluationMs > 0 && gameTime - _lastAlertRuleEvaluationMs < AlertRuleEvaluationIntervalMs)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(GetActiveStatusMessage()))
            {
                return;
            }

            _lastAlertRuleEvaluationMs = gameTime;
            var next = AlertRulesEvaluator.SelectNextNotification(
                EnsureAlertRules(),
                BuildAlertRulesEvaluationContext(),
                _alertRuleLastShownByKey,
                gameTime,
                AlertRuleNotificationCooldownMs);
            if (next == null || string.IsNullOrWhiteSpace(next.Key) || string.IsNullOrWhiteSpace(next.Message))
            {
                return;
            }

            _alertRuleLastShownByKey[next.Key] = gameTime;
            _nextAlertRuleAllowedAtMs = gameTime + AlertRuleGlobalCooldownMs;
            ShowStatus(next.Message, 4500);
        }

        private AlertRulesEvaluationContext BuildAlertRulesEvaluationContext()
        {
            var snapshot = _tabletStateStore.GetSnapshot();
            return new AlertRulesEvaluationContext
            {
                UpcomingBills = _tabletStateStore.GetUpcomingBills(),
                AcceptedPlayerContracts = _tabletStateStore.GetAcceptedPlayerContracts(),
                FleetSummary = _tabletStateStore.GetFleetAlertSummary(snapshot),
                DistrictComparisons = _tabletStateStore.GetDistrictComparisons(),
            };
        }

        private void ResetAlertRuleRuntimeState(int gameTime, bool useWarmup)
        {
            _alertRuleLastShownByKey.Clear();
            _lastAlertRuleEvaluationMs = 0;
            _nextAlertRuleAllowedAtMs = Math.Max(0, gameTime) + (useWarmup ? AlertRuleWarmupMs : 0);
        }

        private AlertRulesPersistenceSnapshot EnsureAlertRules()
        {
            if (_alertRules == null)
            {
                _alertRules = new AlertRulesPersistenceSnapshot();
            }

            return _alertRules;
        }

        private string CurrentRentAlertCaption()
        {
            return string.Format("Rent alerts: < {0} >", FormatAlertLeadTimeLabel(EnsureAlertRules().RentLeadTime));
        }

        private string CurrentRentAlertDetail()
        {
            var leadTime = EnsureAlertRules().RentLeadTime;
            return leadTime == AlertLeadTimeMode.Off
                ? "Disable office, apartment, and rental-vehicle charge reminders."
                : string.Format("Warn for office, apartment, and rental-vehicle charges due {0} or overdue.", DescribeLeadTimeWindow(leadTime));
        }

        private string CurrentContractAlertCaption()
        {
            return string.Format("Contract alerts: < {0} >", FormatAlertLeadTimeLabel(EnsureAlertRules().ContractLeadTime));
        }

        private string CurrentContractAlertDetail()
        {
            var leadTime = EnsureAlertRules().ContractLeadTime;
            return leadTime == AlertLeadTimeMode.Off
                ? "Disable accepted-contract expiry and NPC payroll reminders."
                : string.Format("Warn for accepted player contract expiry and NPC route payroll due {0} or overdue.", DescribeLeadTimeWindow(leadTime));
        }

        private string CurrentFleetAlertCaption()
        {
            return string.Format("Fleet alerts: < {0} >", FormatFleetAlertModeLabel(EnsureAlertRules().FleetMode));
        }

        private string CurrentFleetAlertDetail()
        {
            switch (EnsureAlertRules().FleetMode)
            {
                case FleetAlertMode.CriticalOnly:
                    return "Warn for urgent fuel, overdue inspection, and low-condition issues only.";
                case FleetAlertMode.WatchAndCritical:
                    return "Warn for fuel watch, urgent fuel, overdue inspection, and low-condition issues.";
                case FleetAlertMode.Off:
                default:
                    return "Disable low-fuel, inspection, and condition reminders.";
            }
        }

        private string CurrentTerritoryAlertCaption()
        {
            return string.Format("Territory alerts: < {0} >", FormatTerritoryAlertModeLabel(EnsureAlertRules().TerritoryMode));
        }

        private string CurrentTerritoryAlertDetail()
        {
            switch (EnsureAlertRules().TerritoryMode)
            {
                case TerritoryAlertMode.ChargesOnly:
                    return "Warn when territory charges are due within 3h or overdue.";
                case TerritoryAlertMode.ChargesAndRisk:
                    return "Warn when territory charges are due within 3h and when charter, corridor/service, or competition pressure turns risky.";
                case TerritoryAlertMode.Off:
                default:
                    return "Disable territory charge and district pressure reminders.";
            }
        }

        private void CycleRentAlertLeadTime(int delta)
        {
            EnsureAlertRules().RentLeadTime = CycleOption(EnsureAlertRules().RentLeadTime, AlertLeadTimeOptions, delta);
            ResetAlertRuleRuntimeState(Game.GameTime, false);
        }

        private void CycleContractAlertLeadTime(int delta)
        {
            EnsureAlertRules().ContractLeadTime = CycleOption(EnsureAlertRules().ContractLeadTime, AlertLeadTimeOptions, delta);
            ResetAlertRuleRuntimeState(Game.GameTime, false);
        }

        private void CycleFleetAlertMode(int delta)
        {
            EnsureAlertRules().FleetMode = CycleOption(EnsureAlertRules().FleetMode, FleetAlertModeOptions, delta);
            ResetAlertRuleRuntimeState(Game.GameTime, false);
        }

        private void CycleTerritoryAlertMode(int delta)
        {
            EnsureAlertRules().TerritoryMode = CycleOption(EnsureAlertRules().TerritoryMode, TerritoryAlertModeOptions, delta);
            ResetAlertRuleRuntimeState(Game.GameTime, false);
        }

        private static T CycleOption<T>(T current, T[] options, int delta)
        {
            if (options == null || options.Length == 0)
            {
                return current;
            }

            var currentIndex = Array.IndexOf(options, current);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            return options[(currentIndex + delta + options.Length) % options.Length];
        }

        private static string FormatAlertLeadTimeLabel(AlertLeadTimeMode mode)
        {
            switch (mode)
            {
                case AlertLeadTimeMode.DueNow:
                    return "Due now";
                case AlertLeadTimeMode.Within60Minutes:
                    return "1h";
                case AlertLeadTimeMode.Within180Minutes:
                    return "3h";
                case AlertLeadTimeMode.WithinDay:
                    return "1 day";
                case AlertLeadTimeMode.Off:
                default:
                    return "Off";
            }
        }

        private static string DescribeLeadTimeWindow(AlertLeadTimeMode mode)
        {
            switch (mode)
            {
                case AlertLeadTimeMode.DueNow:
                    return "now";
                case AlertLeadTimeMode.Within60Minutes:
                    return "within 1h";
                case AlertLeadTimeMode.Within180Minutes:
                    return "within 3h";
                case AlertLeadTimeMode.WithinDay:
                    return "within 1 day";
                case AlertLeadTimeMode.Off:
                default:
                    return "off";
            }
        }

        private static string FormatFleetAlertModeLabel(FleetAlertMode mode)
        {
            switch (mode)
            {
                case FleetAlertMode.CriticalOnly:
                    return "Critical";
                case FleetAlertMode.WatchAndCritical:
                    return "Watch + Critical";
                case FleetAlertMode.Off:
                default:
                    return "Off";
            }
        }

        private static string FormatTerritoryAlertModeLabel(TerritoryAlertMode mode)
        {
            switch (mode)
            {
                case TerritoryAlertMode.ChargesOnly:
                    return "Charges only";
                case TerritoryAlertMode.ChargesAndRisk:
                    return "Charge + Risk";
                case TerritoryAlertMode.Off:
                default:
                    return "Off";
            }
        }
    }
}
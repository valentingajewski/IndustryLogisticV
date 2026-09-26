using System;
using System.Collections.Generic;
using System.Linq;

namespace LSOL.Systems
{
    /// <summary>
    /// Result of awarding side job credit to a district. Produced by
    /// <c>TerritoryManager.RegisterSideJobCompletion</c> so the side job can report what happened
    /// in its status line without depending on the territory manager.
    /// </summary>
    public sealed class DistrictBonusAward
    {
        public DistrictBonusAward()
        {
            JobId = string.Empty;
            JobLabel = string.Empty;
            DistrictName = string.Empty;
        }

        /// <summary>False when nothing was awarded (unknown job, no district, zero units).</summary>
        public bool Applied { get; set; }

        public string JobId { get; set; }

        public string JobLabel { get; set; }

        public string DistrictName { get; set; }

        public float AppliedPoints { get; set; }

        /// <summary>District total after the award, already decayed and capped.</summary>
        public float TotalPercent { get; set; }

        public float CapPercent { get; set; }

        /// <summary>In-game hours left before the district bonus decays to zero if untouched.</summary>
        public float RemainingInGameHours { get; set; }
    }

    /// <summary>One side job's share of a district bonus.</summary>
    public sealed class DistrictBonusPoolEntry
    {
        public DistrictBonusPoolEntry()
        {
            JobId = string.Empty;
            Label = string.Empty;
        }

        public string JobId { get; set; }

        public string Label { get; set; }

        public float Points { get; set; }
    }

    /// <summary>
    /// Read-only snapshot of one district's perishable side job bonus, used by the Company Hub and
    /// the tablet so both render exactly the same numbers.
    /// </summary>
    public sealed class DistrictBonusBreakdown
    {
        public DistrictBonusBreakdown()
        {
            DistrictName = string.Empty;
            Pools = new List<DistrictBonusPoolEntry>();
        }

        public string DistrictName { get; set; }

        /// <summary>Capped district total in percentage points.</summary>
        public float TotalPercent { get; set; }

        public float CapPercent { get; set; }

        public float RemainingInGameHours { get; set; }

        public List<DistrictBonusPoolEntry> Pools { get; private set; }

        public bool HasAny
        {
            get { return TotalPercent > DistrictBonusCatalog.MinimumMeaningfulPoints; }
        }

        public string TopContributorLabel
        {
            get
            {
                var top = Pools
                    .Where(entry => entry != null && entry.Points > DistrictBonusCatalog.MinimumMeaningfulPoints)
                    .OrderByDescending(entry => entry.Points)
                    .ThenBy(entry => entry.JobId, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                return top != null ? top.Label : string.Empty;
            }
        }
    }

    /// <summary>
    /// Tuning and pure math for the perishable "district bonus" that side jobs feed. The pools are
    /// stored in percentage points and decay relatively, so the per-job breakdown always adds up to
    /// the district total. Deliberately free of GTA types so it can be unit tested without a game.
    /// </summary>
    internal static class DistrictBonusCatalog
    {
        /// <summary>Hard ceiling per district, in percentage points of revenue. Raise to buff the feature.</summary>
        public const float CapPercent = 20f;

        /// <summary>
        /// In-game hours for an untouched pool to decay from full to zero. At the default GTA time
        /// scale this is roughly 1.5 to 2 hours of real play, so the bonus must be topped up.
        /// </summary>
        public const float DecayHorizonInGameHours = 48f;

        public const float MinutesPerInGameHour = 60f;

        /// <summary>Pools below this are treated as empty so UIs do not show "+0.0%".</summary>
        public const float MinimumMeaningfulPoints = 0.01f;

        public const string TowingJobId = "Towing";
        public const string GarbageJobId = "Garbage";
        public const string FoodDeliveryJobId = "FoodDelivery";
        public const string BusJobId = "Bus";

        private static readonly string[] JobIdList =
        {
            TowingJobId,
            GarbageJobId,
            FoodDeliveryJobId,
            BusJobId,
        };

        /// <summary>
        /// Stable side job ids, matching LSOLScript's side job toggles and the job= vocabulary of
        /// JobCoordinates.xml. Trucking is deliberately absent: cargo hauling is not a side job.
        /// </summary>
        public static IReadOnlyList<string> JobIds
        {
            get { return JobIdList; }
        }

        public static string NormalizeJobId(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return string.Empty;
            }

            var trimmed = jobId.Trim();
            for (int i = 0; i < JobIdList.Length; i++)
            {
                if (string.Equals(JobIdList[i], trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return JobIdList[i];
                }
            }

            return string.Empty;
        }

        public static bool IsKnownJobId(string jobId)
        {
            return !string.IsNullOrWhiteSpace(NormalizeJobId(jobId));
        }

        /// <summary>Player-facing job label, used by the Company Hub and tablet breakdowns.</summary>
        public static string GetJobLabel(string jobId)
        {
            var normalized = NormalizeJobId(jobId);
            if (string.Equals(normalized, TowingJobId, StringComparison.Ordinal))
            {
                return "Towing";
            }

            if (string.Equals(normalized, GarbageJobId, StringComparison.Ordinal))
            {
                return "Garbage";
            }

            if (string.Equals(normalized, FoodDeliveryJobId, StringComparison.Ordinal))
            {
                return "Food Delivery";
            }

            if (string.Equals(normalized, BusJobId, StringComparison.Ordinal))
            {
                return "Bus";
            }

            return string.Empty;
        }

        /// <summary>
        /// Percentage points added to the district pool per completed unit of work (one finished
        /// route, one delivered towed vehicle, ...). FoodDelivery and Bus keep their
        /// values ready but are not wired to a job yet.
        /// </summary>
        public static float GetContributionPointsPerUnit(string jobId)
        {
            var normalized = NormalizeJobId(jobId);
            if (string.Equals(normalized, TowingJobId, StringComparison.Ordinal))
            {
                return 1.0f;
            }

            if (string.Equals(normalized, GarbageJobId, StringComparison.Ordinal))
            {
                return 2.0f;
            }

            if (string.Equals(normalized, BusJobId, StringComparison.Ordinal))
            {
                return 1.5f;
            }

            if (string.Equals(normalized, FoodDeliveryJobId, StringComparison.Ordinal))
            {
                return 0.5f;
            }

            return 0f;
        }

        /// <summary>
        /// Relative decay factor applied to every pool. Returning a factor (instead of an absolute
        /// amount) keeps the per-job share intact no matter how lopsided the pools are.
        /// Elapsed time of zero or less means "no decay" so a clock that moved backwards after
        /// loading an older save can never inflate or corrupt the bonus.
        /// </summary>
        public static float GetDecayFactor(float elapsedInGameMinutes)
        {
            if (elapsedInGameMinutes <= 0f)
            {
                return 1f;
            }

            var elapsedHours = elapsedInGameMinutes / MinutesPerInGameHour;
            if (elapsedHours >= DecayHorizonInGameHours)
            {
                return 0f;
            }

            return 1f - (elapsedHours / DecayHorizonInGameHours);
        }

        /// <summary>In-game hours left before an untouched pool reaches zero.</summary>
        public static float GetRemainingInGameHours(float elapsedInGameMinutes)
        {
            if (elapsedInGameMinutes <= 0f)
            {
                return DecayHorizonInGameHours;
            }

            var elapsedHours = elapsedInGameMinutes / MinutesPerInGameHour;
            return Math.Max(0f, DecayHorizonInGameHours - elapsedHours);
        }

        /// <summary>
        /// District total in percentage points: sum of every pool, clamped to the cap. Pass a job id
        /// to exclude that pool, which is how a side job's own payout avoids boosting itself.
        /// </summary>
        public static float GetTotalPercent(IEnumerable<KeyValuePair<string, float>> pools, string excludedJobId = null)
        {
            if (pools == null)
            {
                return 0f;
            }

            var excluded = NormalizeJobId(excludedJobId);
            var total = 0f;
            foreach (var pool in pools)
            {
                var jobId = NormalizeJobId(pool.Key);
                if (string.IsNullOrWhiteSpace(jobId) || jobId == excluded)
                {
                    continue;
                }

                if (pool.Value > 0f)
                {
                    total += pool.Value;
                }
            }

            return Math.Min(CapPercent, total);
        }

        /// <summary>Applies a relative decay factor to every pool and drops empty entries.</summary>
        public static void ApplyDecayFactor(IDictionary<string, float> pools, float factor)
        {
            if (pools == null || pools.Count == 0)
            {
                return;
            }

            var clamped = Math.Max(0f, Math.Min(1f, factor));
            if (clamped >= 1f)
            {
                return;
            }

            var keys = pools.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                var decayed = Math.Max(0f, pools[key]) * clamped;
                if (decayed <= MinimumMeaningfulPoints)
                {
                    pools.Remove(key);
                }
                else
                {
                    pools[key] = decayed;
                }
            }
        }

        /// <summary>Adds points to one job's pool, ignoring unknown jobs and non-positive amounts.</summary>
        public static float AddPoints(IDictionary<string, float> pools, string jobId, float points)
        {
            if (pools == null || points <= 0f)
            {
                return 0f;
            }

            var normalized = NormalizeJobId(jobId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return 0f;
            }

            float current;
            pools.TryGetValue(normalized, out current);
            var updated = Math.Max(0f, current) + points;
            pools[normalized] = updated;
            return updated;
        }

        /// <summary>
        /// Scales every pool down proportionally when their sum exceeds the cap. Doing this on write
        /// (not on read) keeps the per-job breakdown adding up to the displayed district total, and
        /// means work done at the cap is simply not banked.
        /// </summary>
        public static void ClampPoolsToCap(IDictionary<string, float> pools)
        {
            if (pools == null || pools.Count == 0)
            {
                return;
            }

            var total = 0f;
            foreach (var pool in pools)
            {
                if (pool.Value > 0f)
                {
                    total += pool.Value;
                }
            }

            if (total <= CapPercent)
            {
                return;
            }

            ApplyDecayFactor(pools, CapPercent / total);
        }

        /// <summary>Builds the display model for one district, including the per-job attribution.</summary>
        public static DistrictBonusBreakdown BuildBreakdown(
            string districtName,
            IEnumerable<KeyValuePair<string, float>> pools,
            float remainingInGameHours)
        {
            var breakdown = new DistrictBonusBreakdown
            {
                DistrictName = districtName ?? string.Empty,
                CapPercent = CapPercent,
                RemainingInGameHours = Math.Max(0f, remainingInGameHours),
                TotalPercent = GetTotalPercent(pools),
            };

            if (pools != null)
            {
                foreach (var pool in pools
                    .Where(entry => entry.Value > MinimumMeaningfulPoints)
                    .OrderByDescending(entry => entry.Value)
                    .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
                {
                    breakdown.Pools.Add(new DistrictBonusPoolEntry
                    {
                        JobId = NormalizeJobId(pool.Key),
                        Label = GetJobLabel(pool.Key),
                        Points = pool.Value,
                    });
                }
            }

            return breakdown;
        }
    }
}

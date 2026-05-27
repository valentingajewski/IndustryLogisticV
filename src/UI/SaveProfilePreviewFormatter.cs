using System;
using System.Collections.Generic;
using System.Linq;
using LSOL.Domain;
using LSOL.Systems;

namespace LSOL.UI
{
    internal static class SaveProfilePreviewFormatter
    {
        public static string BuildDetail(
            string action,
            DateTime? lastUpdated,
            IndustryPersistenceMetadata metadata,
            IEnumerable<OfficeDefinition> offices)
        {
            var lines = new List<string>();
            var header = string.IsNullOrWhiteSpace(action) ? string.Empty : action.Trim();
            if (lastUpdated.HasValue)
            {
                header = string.Format(
                    "{0}{1}Last updated {2:yyyy-MM-dd HH:mm}.",
                    header,
                    string.IsNullOrWhiteSpace(header) ? string.Empty : " ",
                    lastUpdated.Value);
            }

            if (!string.IsNullOrWhiteSpace(header))
            {
                lines.Add(header);
            }

            var balanceOfficeFleetLine = BuildBalanceOfficeFleetLine(metadata, offices);
            if (!string.IsNullOrWhiteSpace(balanceOfficeFleetLine))
            {
                lines.Add(balanceOfficeFleetLine);
            }

            var difficultyLine = BuildDifficultyLine(metadata);
            if (!string.IsNullOrWhiteSpace(difficultyLine))
            {
                lines.Add(difficultyLine);
            }

            var difficultyFlagsLine = BuildDifficultyFlagsLine(metadata);
            if (!string.IsNullOrWhiteSpace(difficultyFlagsLine))
            {
                lines.Add(difficultyFlagsLine);
            }

            return string.Join("\n", lines.ToArray());
        }

        private static string BuildBalanceOfficeFleetLine(
            IndustryPersistenceMetadata metadata,
            IEnumerable<OfficeDefinition> offices)
        {
            if (metadata == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (metadata.HasGameplayMetadata)
            {
                parts.Add(string.Format("Balance {0}", ModFormatting.FormatMoney(metadata.Profit)));
            }

            var hasPropertyContext = metadata.HasGameplayMetadata
                || metadata.PropertyOwnership != null
                || (metadata.OwnedFleet != null && metadata.OwnedFleet.HasData);

            if (!hasPropertyContext)
            {
                return string.Join(" | ", parts.ToArray());
            }

            parts.Add(string.Format("Office {0}", ResolveActiveOfficeLabel(metadata.PropertyOwnership, offices)));
            parts.Add(string.Format("Fleet {0}", ResolveFleetCount(metadata)));
            return string.Join(" | ", parts.ToArray());
        }

        private static string BuildDifficultyLine(IndustryPersistenceMetadata metadata)
        {
            if (metadata == null || !metadata.HasGameplayMetadata)
            {
                return string.Empty;
            }

            return DifficultySettingsSummaryFormatter.BuildPreviewLine(
                DifficultySettingsProfile.FromMetadata(metadata),
                metadata.DifficultySettingsLocked);
        }

        private static string BuildDifficultyFlagsLine(IndustryPersistenceMetadata metadata)
        {
            if (metadata == null || !metadata.HasGameplayMetadata)
            {
                return string.Empty;
            }

            var flags = BuildDifficultyFlags(metadata);
            return flags.Count == 0
                ? string.Empty
                : string.Format("Flags {0}", string.Join(", ", flags.ToArray()));
        }

        private static List<string> BuildDifficultyFlags(IndustryPersistenceMetadata metadata)
        {
            return DifficultySettingsSummaryFormatter.BuildPreviewFlags(DifficultySettingsProfile.FromMetadata(metadata));
        }

        private static string ResolveActiveOfficeLabel(
            PropertyOwnershipPersistenceSnapshot propertyOwnership,
            IEnumerable<OfficeDefinition> offices)
        {
            var activeOfficeId = propertyOwnership != null ? propertyOwnership.ActiveOfficeId : string.Empty;
            if (string.IsNullOrWhiteSpace(activeOfficeId))
            {
                return "No active office";
            }

            var office = offices != null
                ? offices.FirstOrDefault(candidate => candidate != null && string.Equals(candidate.OfficeId, activeOfficeId, StringComparison.OrdinalIgnoreCase))
                : null;
            if (office != null && !string.IsNullOrWhiteSpace(office.DisplayName))
            {
                return office.DisplayName;
            }

            return activeOfficeId;
        }

        private static int ResolveFleetCount(IndustryPersistenceMetadata metadata)
        {
            if (metadata == null)
            {
                return 0;
            }

            var propertyFleetCount = metadata.PropertyOwnership != null
                ? metadata.PropertyOwnership.CommercialVehicles.Count
                : -1;
            if (propertyFleetCount > 0)
            {
                return propertyFleetCount;
            }

            var ownedFleetCount = metadata.OwnedFleet != null
                ? metadata.OwnedFleet.Vehicles.Count
                : 0;
            if (ownedFleetCount > 0)
            {
                return ownedFleetCount;
            }

            return Math.Max(0, propertyFleetCount);
        }

    }
}
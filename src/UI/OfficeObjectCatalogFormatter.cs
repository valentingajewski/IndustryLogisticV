using System;
using System.Collections.Generic;
using LSOL.Domain;

namespace LSOL.UI
{
    internal static class OfficeObjectCatalogFormatter
    {
        public static string BuildCaption(OfficeObjectDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            return string.Format("[{0}] {1}", BuildKindLabel(definition), definition.DisplayName ?? string.Empty);
        }

        public static string BuildDetail(OfficeObjectDefinition definition, int placedCount, int pendingCount)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            var segments = new List<string>
            {
                BuildFunctionSegment(definition),
                BuildLimitSegment(definition),
                BuildHaulSegment(definition),
                string.Format("Price: {0}", ModFormatting.FormatMoney(Math.Max(0f, definition.Price))),
                string.Format("Placed: {0}", Math.Max(0, placedCount)),
                string.Format("Pending: {0}", Math.Max(0, pendingCount)),
            };

            var capacitySegment = BuildCapacitySegment(definition);
            if (!string.IsNullOrWhiteSpace(capacitySegment))
            {
                segments.Insert(1, capacitySegment);
            }

            var placementSegment = BuildPlacementSegment(definition);
            if (!string.IsNullOrWhiteSpace(placementSegment))
            {
                segments.Insert(Math.Min(segments.Count, 2), placementSegment);
            }

            var interactionSegment = BuildInteractionSegment(definition);
            if (!string.IsNullOrWhiteSpace(interactionSegment))
            {
                segments.Insert(Math.Min(segments.Count, 3), interactionSegment);
            }

            var staffSegment = BuildStaffSegment(definition);
            if (!string.IsNullOrWhiteSpace(staffSegment))
            {
                segments.Insert(Math.Min(segments.Count, 4), staffSegment);
            }

            var accessSegment = BuildAccessSegment(definition);
            if (!string.IsNullOrWhiteSpace(accessSegment))
            {
                segments.Insert(Math.Min(segments.Count, 5), accessSegment);
            }

            return string.Join(" | ", segments);
        }

        public static string BuildPurchaseActionDetail(OfficeObjectDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            if (definition.RequiresRoomAnchor)
            {
                return definition.IsFunctional
                    ? "Buy now, haul it from the port, then auto-install it at the matching office room."
                    : "Buy now and auto-install it at the matching office room.";
            }

            if (definition.UsesFacilityAnchor && definition.PlacementContext == OfficeObjectPlacementContext.Either)
            {
                return definition.IsFunctional
                    ? "Buy now, haul it from the port, then auto-install it at the matching office room when available or place it manually in the yard."
                    : "Buy now and auto-install it at the matching office room when available or place it manually in the yard.";
            }

            return definition.IsFunctional
                ? "Buy now, then haul it from the port to the active office before placement."
                : "Buy now and immediately enter placement mode at the active office.";
        }

        private static string BuildKindLabel(OfficeObjectDefinition definition)
        {
            switch (definition.Function)
            {
                case OfficeObjectFunction.Refuel:
                    return "Refuel";
                case OfficeObjectFunction.Repair:
                    return "Repair";
                case OfficeObjectFunction.Npc:
                    return "NPC";
                case OfficeObjectFunction.Headquarters:
                    return "HQ";
                default:
                    return definition.InteractionType != OfficeFacilityInteractionType.None || definition.AmbientStaffRole != OfficeAmbientStaffRole.None
                        ? "Facility"
                        : "Decor";
            }
        }

        private static string BuildFunctionSegment(OfficeObjectDefinition definition)
        {
            switch (definition.Function)
            {
                case OfficeObjectFunction.Refuel:
                    return "Function: diesel storage";
                case OfficeObjectFunction.Repair:
                    return "Function: repairs office trucks and trailers";
                case OfficeObjectFunction.Npc:
                    return "Function: hired NPC support";
                case OfficeObjectFunction.Headquarters:
                    return "Function: landmark HQ annex";
                default:
                    return definition.InteractionType != OfficeFacilityInteractionType.None || definition.AmbientStaffRole != OfficeAmbientStaffRole.None
                        ? "Function: office facility support"
                        : "Function: decorative placement";
            }
        }

        private static string BuildCapacitySegment(OfficeObjectDefinition definition)
        {
            switch (definition.Function)
            {
                case OfficeObjectFunction.Refuel:
                    return string.Format("Capacity: {0}", ModFormatting.FormatLiters(Math.Max(0f, definition.Capacity)));
                case OfficeObjectFunction.Npc:
                    return string.Format("Capacity: supports {0:0} hired NPCs", Math.Max(0f, definition.Capacity));
                default:
                    return string.Empty;
            }
        }

        private static string BuildLimitSegment(OfficeObjectDefinition definition)
        {
            if (definition.Function == OfficeObjectFunction.Headquarters)
            {
                return "Limit: 1 company-wide";
            }

            return definition.PerOfficeLimit > 0
                ? string.Format("Limit: {0} per office", definition.PerOfficeLimit)
                : "Limit: no office cap";
        }

        private static string BuildHaulSegment(OfficeObjectDefinition definition)
        {
            return definition.IsFunctional
                ? "Haul: required from port before placement"
                : "Haul: no haul required; immediate placement";
        }

        private static string BuildPlacementSegment(OfficeObjectDefinition definition)
        {
            if (definition == null || definition.AnchorType == OfficeFacilityAnchorType.None)
            {
                return string.Empty;
            }

            switch (definition.PlacementContext)
            {
                case OfficeObjectPlacementContext.Room:
                    return string.Format("Placement: {0} only", BuildAnchorLabel(definition.AnchorType));
                case OfficeObjectPlacementContext.Either:
                    return string.Format("Placement: {0} or yard fallback", BuildAnchorLabel(definition.AnchorType));
                default:
                    return string.Empty;
            }
        }

        private static string BuildInteractionSegment(OfficeObjectDefinition definition)
        {
            switch (definition != null ? definition.InteractionType : OfficeFacilityInteractionType.None)
            {
                case OfficeFacilityInteractionType.OfficeSummary:
                    return "Interaction: office summary";
                case OfficeFacilityInteractionType.HireNpc:
                    return "Interaction: staffing and route planning";
                case OfficeFacilityInteractionType.RepairVehicle:
                    return "Interaction: repair access";
                case OfficeFacilityInteractionType.FuelManagement:
                    return "Interaction: fuel management";
                case OfficeFacilityInteractionType.HeadquartersStatus:
                    return "Interaction: HQ status";
                default:
                    return string.Empty;
            }
        }

        private static string BuildStaffSegment(OfficeObjectDefinition definition)
        {
            if (definition == null || definition.AmbientStaffRole == OfficeAmbientStaffRole.None)
            {
                return string.Empty;
            }

            var count = Math.Max(1, definition.AmbientStaffCount);
            var roleLabel = BuildStaffRoleLabel(definition.AmbientStaffRole);
            return string.Format("Staff: {0} {1}{2}", count, roleLabel, count == 1 ? string.Empty : "s");
        }

        private static string BuildAccessSegment(OfficeObjectDefinition definition)
        {
            return definition != null && definition.RequiresOwnedOffice
                ? "Access: owned office only"
                : string.Empty;
        }

        private static string BuildAnchorLabel(OfficeFacilityAnchorType anchorType)
        {
            switch (anchorType)
            {
                case OfficeFacilityAnchorType.ReceptionDesk:
                    return "reception desk";
                case OfficeFacilityAnchorType.DispatchDesk:
                    return "dispatch desk";
                case OfficeFacilityAnchorType.Boardroom:
                    return "boardroom";
                case OfficeFacilityAnchorType.MaintenanceDesk:
                    return "maintenance desk";
                case OfficeFacilityAnchorType.FuelDesk:
                    return "fuel desk";
                case OfficeFacilityAnchorType.BreakRoom:
                    return "break-room nook";
                case OfficeFacilityAnchorType.WorkerFallback:
                    return "worker station";
                default:
                    return "facility anchor";
            }
        }

        private static string BuildStaffRoleLabel(OfficeAmbientStaffRole role)
        {
            switch (role)
            {
                case OfficeAmbientStaffRole.Receptionist:
                    return "receptionist";
                case OfficeAmbientStaffRole.Dispatcher:
                    return "dispatcher";
                case OfficeAmbientStaffRole.Mechanic:
                    return "mechanic";
                case OfficeAmbientStaffRole.SupportWorker:
                    return "support worker";
                case OfficeAmbientStaffRole.Security:
                    return "security guard";
                case OfficeAmbientStaffRole.Manager:
                    return "manager";
                case OfficeAmbientStaffRole.AdminClerk:
                    return "admin clerk";
                default:
                    return "staffer";
            }
        }
    }
}
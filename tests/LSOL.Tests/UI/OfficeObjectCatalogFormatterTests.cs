using LSOL.Domain;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class OfficeObjectCatalogFormatterTests
    {
        [TestMethod]
        public void BuildDetail_ForRefuelModule_ShowsFunctionCapacityLimitAndHaul()
        {
            var definition = new OfficeObjectDefinition
            {
                DisplayName = "Dieseltank",
                Function = OfficeObjectFunction.Refuel,
                Capacity = 15000f,
                PerOfficeLimit = 1,
                Price = 15000f,
            };

            Assert.AreEqual(
                "Function: diesel storage | Capacity: 15,000.00L | Limit: 1 per office | Haul: required from port before placement | Price: $15,000.00 | Placed: 0 | Pending: 0",
                OfficeObjectCatalogFormatter.BuildDetail(definition, 0, 0));
        }

        [TestMethod]
        public void BuildDetail_ForNpcModule_UsesHiredNpcCapacityWording()
        {
            var definition = new OfficeObjectDefinition
            {
                DisplayName = "Construction Site Cabin",
                Function = OfficeObjectFunction.Npc,
                Capacity = 3f,
                PerOfficeLimit = 4,
                Price = 25000f,
            };

            Assert.AreEqual(
                "Function: hired NPC support | Capacity: supports 3 hired NPCs | Limit: 4 per office | Haul: required from port before placement | Price: $25,000.00 | Placed: 1 | Pending: 1",
                OfficeObjectCatalogFormatter.BuildDetail(definition, 1, 1));
        }

        [TestMethod]
        public void BuildDetail_ForRepairModule_OmitsFakeCapacity()
        {
            var definition = new OfficeObjectDefinition
            {
                DisplayName = "Maintenance Bay",
                Function = OfficeObjectFunction.Repair,
                PerOfficeLimit = 1,
                Price = 8000f,
            };

            Assert.AreEqual(
                "Function: repairs office trucks and trailers | Limit: 1 per office | Haul: required from port before placement | Price: $8,000.00 | Placed: 0 | Pending: 0",
                OfficeObjectCatalogFormatter.BuildDetail(definition, 0, 0));
        }

        [TestMethod]
        public void BuildDetail_ForDecorativeItem_ShowsImmediatePlacementHaulBehavior()
        {
            var definition = new OfficeObjectDefinition
            {
                DisplayName = "Cone",
                Function = OfficeObjectFunction.Decorative,
                PerOfficeLimit = 20,
                Price = 250f,
            };

            Assert.AreEqual(
                "Function: decorative placement | Limit: 20 per office | Haul: no haul required; immediate placement | Price: $250.00 | Placed: 2 | Pending: 0",
                OfficeObjectCatalogFormatter.BuildDetail(definition, 2, 0));
        }

        [TestMethod]
        public void BuildDetail_ForFacilityModule_IncludesPlacementInteractionStaffAndAccess()
        {
            var definition = new OfficeObjectDefinition
            {
                DisplayName = "Dispatch Console",
                Function = OfficeObjectFunction.Npc,
                Capacity = 2f,
                PlacementContext = OfficeObjectPlacementContext.Either,
                AnchorType = OfficeFacilityAnchorType.DispatchDesk,
                InteractionType = OfficeFacilityInteractionType.HireNpc,
                AmbientStaffRole = OfficeAmbientStaffRole.Dispatcher,
                AmbientStaffCount = 1,
                RequiresOwnedOffice = true,
                Price = 18000f,
            };

            Assert.AreEqual(
                "Function: hired NPC support | Capacity: supports 2 hired NPCs | Placement: dispatch desk or yard fallback | Interaction: staffing and route planning | Staff: 1 dispatcher | Access: owned office only | Limit: no office cap | Haul: required from port before placement | Price: $18,000.00 | Placed: 0 | Pending: 0",
                OfficeObjectCatalogFormatter.BuildDetail(definition, 0, 0));
        }

        [TestMethod]
        public void BuildPurchaseActionDetail_ForRoomOnlyFacility_DescribesAutoInstall()
        {
            var definition = new OfficeObjectDefinition
            {
                DisplayName = "Landmark HQ Annex",
                Function = OfficeObjectFunction.Headquarters,
                PlacementContext = OfficeObjectPlacementContext.Room,
                AnchorType = OfficeFacilityAnchorType.Boardroom,
            };

            Assert.AreEqual(
                "Buy now, haul it from the port, then auto-install it at the matching office room.",
                OfficeObjectCatalogFormatter.BuildPurchaseActionDetail(definition));
        }
    }
}
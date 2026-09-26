using System.Collections.Generic;
using System.Reflection;
using GTA.Math;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class VehicleSpawnControllerTests
    {
        [TestMethod]
        public void CommercialDealershipSectionMode_CyclesTruckTractorTrailersAndTrucksVans()
        {
            var controller = CreateController(
                CreateVehicle("hauler", "Hauler", CommercialVehicleFleetRole.Tractor, VehicleCargoType.Unknown),
                CreateVehicle("packer", "Packer", CommercialVehicleFleetRole.Tractor, VehicleCargoType.Unknown),
                CreateVehicle("trailers4", "Container Trailer", CommercialVehicleFleetRole.Trailer, VehicleCargoType.Trailer),
                CreateVehicle("mule", "Mule Box", CommercialVehicleFleetRole.Rigid, VehicleCargoType.CraftedGoods));

            controller.SetCommercialDealershipSectionMode(true);

            Assert.AreEqual("Truck tractor", controller.CurrentCommercialDealershipSectionCaption);
            Assert.AreEqual("Hauler", controller.CurrentVehicleCaption);
            Assert.AreEqual("unavailable", controller.CurrentTractorCaption);

            controller.ChangeVehicleSelection(1);
            Assert.AreEqual("Packer", controller.CurrentVehicleCaption);

            controller.ChangeCommercialDealershipSection(1);
            Assert.AreEqual("Trailers", controller.CurrentCommercialDealershipSectionCaption);
            Assert.AreEqual("Container Trailer", controller.CurrentVehicleCaption);
            Assert.IsNull(controller.SelectedTractorDefinition);

            controller.ChangeCommercialDealershipSection(1);
            Assert.AreEqual("Trucks/Vans", controller.CurrentCommercialDealershipSectionCaption);
            Assert.AreEqual("Mule Box", controller.CurrentVehicleCaption);

            controller.ChangeCommercialDealershipSection(1);
            Assert.AreEqual("Truck tractor", controller.CurrentCommercialDealershipSectionCaption);
            Assert.AreEqual("Hauler", controller.CurrentVehicleCaption);
        }

        private static VehicleSpawnController CreateController(params VehicleDefinition[] definitions)
        {
            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.VehicleDefinitions), new List<VehicleDefinition>(definitions));
            return new VehicleSpawnController(
                new FleetManager(config),
                Vector3.Zero,
                0f,
                new[] { VehicleCargoType.CraftedGoods },
                VehicleCargoType.CraftedGoods);
        }

        private static VehicleDefinition CreateVehicle(string modelName, string displayName, CommercialVehicleFleetRole fleetRole, VehicleCargoType cargoType)
        {
            return new VehicleDefinition
            {
                Id = modelName,
                ModelName = modelName,
                DisplayName = displayName,
                FleetRole = fleetRole,
                CargoType = cargoType,
                IsEnabled = true,
                CapacityTons = fleetRole == CommercialVehicleFleetRole.Tractor ? 0f : 20f,
                FuelCapacityLiters = fleetRole == CommercialVehicleFleetRole.Trailer ? 0f : 300f,
            };
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value, null);
        }
    }
}
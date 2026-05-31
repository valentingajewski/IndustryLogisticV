using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using LSOL;
using LSOL.Config;
using LSOL.Domain;
using LSOL.Systems;
using LSOL.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.UI
{
    [TestClass]
    public sealed class OfficeMarkerMenuTests
    {
        [TestMethod]
        public void BuildOfficeMenuItems_ActiveOfficeOmitsHireNpc()
        {
            var office = new OfficeDefinition
            {
                OfficeId = "alpha",
                SiteName = "Alpha Yard",
                DistrictName = "Downtown",
                OfficePrice = 1200f,
                WeeklyOfficeRent = 120f,
                MaxCommercialVehicles = 2,
            };

            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.OfficeDefinitions), new[] { office }.ToList());
            SetProperty(config, nameof(ModConfig.OfficeObjectDefinitions), new List<OfficeObjectDefinition>());

            var propertyManager = new PropertyManager(config);
            propertyManager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                ActiveOfficeId = "alpha",
                Offices =
                {
                    new OfficeOwnershipPersistenceEntry { OfficeId = "alpha", IsOwned = true, LastChargedWeekIndex = 0 },
                },
            }, 0);

            var script = (LSOLScript)FormatterServices.GetUninitializedObject(typeof(LSOLScript));
            SetField(script, "_propertyManager", propertyManager);
            SetField(script, "_menuOffice", office);
            SetField(script, "_profit", 50000f);
            SetField(script, "_workerSpawnController", new WorkerSpawnController(new[] { "s_m_m_trucker_01" }));

            var method = typeof(LSOLScript).GetMethod("BuildOfficeMenuItems", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            var items = ((IEnumerable<MenuItem>)method.Invoke(script, null)).ToList();
            var captions = items
                .Where(item => item != null && !item.IsSeparator && item.CaptionFactory != null)
                .Select(item => item.CaptionFactory() ?? string.Empty)
                .ToList();

            CollectionAssert.DoesNotContain(captions, "Hire NPC");
            CollectionAssert.Contains(captions, "Garage");
            CollectionAssert.Contains(captions, "Objects");
            CollectionAssert.Contains(captions, "Fuel Management");
            CollectionAssert.Contains(captions, "Repair Vehicle");
        }

        [TestMethod]
        public void BuildOfficeMenuSubtitle_OwnedOfficeShowsPermanentAccess()
        {
            var office = new OfficeDefinition
            {
                OfficeId = "alpha",
                SiteName = "Alpha Yard",
                DistrictName = "Downtown",
                OfficePrice = 1200f,
                WeeklyOfficeRent = 120f,
                MaxCommercialVehicles = 2,
            };

            var config = new ModConfig();
            SetProperty(config, nameof(ModConfig.OfficeDefinitions), new[] { office }.ToList());
            SetProperty(config, nameof(ModConfig.OfficeObjectDefinitions), new List<OfficeObjectDefinition>());

            var propertyManager = new PropertyManager(config);
            propertyManager.ApplySnapshot(new PropertyOwnershipPersistenceSnapshot
            {
                ActiveOfficeId = "alpha",
                Offices =
                {
                    new OfficeOwnershipPersistenceEntry { OfficeId = "alpha", IsOwned = true, LastChargedWeekIndex = 0 },
                },
            }, 0);

            var script = (LSOLScript)FormatterServices.GetUninitializedObject(typeof(LSOLScript));
            SetField(script, "_propertyManager", propertyManager);
            SetField(script, "_menuOffice", office);

            var method = typeof(LSOLScript).GetMethod("BuildOfficeMenuSubtitle", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            var subtitle = (string)method.Invoke(script, null);

            Assert.AreEqual("Owned | Permanent access | No rent due", subtitle);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = typeof(LSOLScript).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value, null);
        }
    }
}
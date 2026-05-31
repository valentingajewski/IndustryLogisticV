using GTA.Math;
using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class OfficeObjectAmbientStaffResolverTests
    {
        [TestMethod]
        public void ResolveBackwardOffset_UsesMinimumClearanceForShallowModels()
        {
            var offset = OfficeObjectAmbientStaffResolver.ResolveBackwardOffset(
                new Vector3(-0.2f, -0.1f, 0f),
                new Vector3(0.2f, 0.15f, 1f));

            Assert.AreEqual(0.85f, offset, 0.001f);
        }

        [TestMethod]
        public void ResolveBackwardOffset_GrowsWithModelDepth()
        {
            var offset = OfficeObjectAmbientStaffResolver.ResolveBackwardOffset(
                new Vector3(-1f, -0.95f, 0f),
                new Vector3(1f, 0.55f, 1.5f));

            Assert.AreEqual(1.10f, offset, 0.001f);
        }

        [TestMethod]
        public void BuildPosition_PlacesAmbientStaffBehindDeskFootprint()
        {
            var position = OfficeObjectAmbientStaffResolver.BuildPosition(
                Vector3.Zero,
                0f,
                0,
                1,
                1.1f);

            Assert.AreEqual(0f, position.X, 0.001f);
            Assert.AreEqual(-1.1f, position.Y, 0.001f);
            Assert.AreEqual(0f, position.Z, 0.001f);
        }
    }
}
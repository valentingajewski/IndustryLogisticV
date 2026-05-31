using GTA.Math;
using LSOL.Domain;
using LSOL.Systems;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LSOL.Tests.Systems
{
    [TestClass]
    public sealed class OfficeObjectPlacementResolverTests
    {
        [TestMethod]
        public void ResolveFinalPosition_YardPlacementUsesGroundedZInsteadOfStaleCandidate()
        {
            var requested = new Vector3(12f, 24f, 18f);
            var grounded = new Vector3(12f, 24f, 6.25f);

            var resolved = OfficeObjectPlacementResolver.ResolveFinalPosition(
                OfficeObjectPlacementContext.Yard,
                false,
                requested,
                grounded);

            Assert.AreEqual(grounded.X, resolved.X, 0.001f);
            Assert.AreEqual(grounded.Y, resolved.Y, 0.001f);
            Assert.AreEqual(grounded.Z, resolved.Z, 0.001f);
        }

        [TestMethod]
        public void ResolveFinalPosition_YardPlacementRecomputesGroundAfterHorizontalMove()
        {
            var stalePosition = new Vector3(30f, 40f, 6.25f);
            var groundedAfterMove = new Vector3(30f, 40f, 8.75f);

            var resolved = OfficeObjectPlacementResolver.ResolveFinalPosition(
                OfficeObjectPlacementContext.Yard,
                false,
                stalePosition,
                groundedAfterMove);

            Assert.AreEqual(8.75f, resolved.Z, 0.001f);
        }

        [TestMethod]
        public void ResolveFinalPosition_RoomPlacementWithAnchorPreservesAnchorHeight()
        {
            var anchorPosition = new Vector3(5f, 6f, 11.5f);
            var outdoorGround = new Vector3(5f, 6f, 2.0f);

            var resolved = OfficeObjectPlacementResolver.ResolveFinalPosition(
                OfficeObjectPlacementContext.Room,
                true,
                anchorPosition,
                outdoorGround);

            Assert.IsFalse(OfficeObjectPlacementResolver.ShouldGroundPlacement(OfficeObjectPlacementContext.Room, true));
            Assert.AreEqual(anchorPosition.Z, resolved.Z, 0.001f);
        }

        [TestMethod]
        public void NeedsPlacementRepair_FloatingYardPlacementNormalizesToGroundedPosition()
        {
            var persisted = new Vector3(100f, 200f, 14f);
            var grounded = new Vector3(100f, 200f, 5.5f);
            var resolved = OfficeObjectPlacementResolver.ResolveFinalPosition(
                OfficeObjectPlacementContext.Yard,
                false,
                persisted,
                grounded);

            Assert.IsTrue(OfficeObjectPlacementResolver.NeedsPlacementRepair(persisted, resolved, 0.02f));
            Assert.AreEqual(grounded.Z, resolved.Z, 0.001f);
        }

        [TestMethod]
        public void ResolveFinalPosition_EitherPlacementUsesAnchorWhenAvailableAndGroundWhenNot()
        {
            var requested = new Vector3(1f, 2f, 10f);
            var grounded = new Vector3(1f, 2f, 4f);

            var anchored = OfficeObjectPlacementResolver.ResolveFinalPosition(
                OfficeObjectPlacementContext.Either,
                true,
                requested,
                grounded);
            var freePlaced = OfficeObjectPlacementResolver.ResolveFinalPosition(
                OfficeObjectPlacementContext.Either,
                false,
                requested,
                grounded);

            Assert.IsFalse(OfficeObjectPlacementResolver.ShouldGroundPlacement(OfficeObjectPlacementContext.Either, true));
            Assert.IsTrue(OfficeObjectPlacementResolver.ShouldGroundPlacement(OfficeObjectPlacementContext.Either, false));
            Assert.AreEqual(requested.Z, anchored.Z, 0.001f);
            Assert.AreEqual(grounded.Z, freePlaced.Z, 0.001f);
        }
    }
}
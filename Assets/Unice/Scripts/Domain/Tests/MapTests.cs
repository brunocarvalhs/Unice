using NUnit.Framework;

namespace Unice
{
    [TestFixture]
    public class MapTests
    {
        // Map.Furniture[0] is "bed" (x170,y40,w150,h175), first entry of the static furniture table — used below for RectDist.
        static Furn Bed => Map.Furniture[0];

        [Test]
        public void InWalk_PointInsideRoomInterior_ReturnsTrue() => Assert.IsTrue(Map.InWalk(100, 100, 0));

        [Test]
        public void InWalk_PointOutsideAnyRoomOrDoor_ReturnsFalse() => Assert.IsFalse(Map.InWalk(-100, -100, 0));

        [Test]
        public void InWalk_WithMargin_ShrinksTheWalkableArea()
        {
            // (40,40) is the exact top-left corner of the "master" room; a 14px margin excludes it.
            Assert.IsFalse(Map.InWalk(40, 40, 14));
            Assert.IsTrue(Map.InWalk(54, 54, 14));
        }

        [Test]
        public void InFurn_PointInsideFurnitureFootprint_ReturnsTrue() => Assert.IsTrue(Map.InFurn(245, 127, 0)); // inside the bed

        [Test]
        public void InFurn_PointAwayFromFurniture_ReturnsFalse() => Assert.IsFalse(Map.InFurn(100, 460, 0)); // open hall floor

        [Test]
        public void CanStand_OpenFloor_ReturnsTrue() => Assert.IsTrue(Map.CanStand(100, 460));

        [Test]
        public void CanStand_OnFurniture_ReturnsFalse() => Assert.IsFalse(Map.CanStand(245, 127)); // on top of the bed

        [Test]
        public void CanStand_OutsideTheHouse_ReturnsFalse() => Assert.IsFalse(Map.CanStand(-100, -100));

        [Test]
        public void WalkLine_ClearPathWithinARoom_ReturnsTrue() => Assert.IsTrue(Map.WalkLine(100, 460, 300, 460)); // open hall floor

        [Test]
        public void WalkLine_BlockedByFurniture_ReturnsFalse()
        {
            // Both endpoints are clear of the bed (x170-320), but the straight line between them at y=120 crosses it.
            Assert.IsTrue(Map.CanStand(150, 120));
            Assert.IsTrue(Map.CanStand(340, 120));
            Assert.IsFalse(Map.WalkLine(150, 120, 340, 120));
        }

        [Test]
        public void SightLine_IgnoresFurniture_UnlikeWalkLine()
        {
            // Same segment as the WalkLine test above: you can SEE across the bed even though you can't WALK through it.
            Assert.IsFalse(Map.WalkLine(150, 120, 340, 120));
            Assert.IsTrue(Map.SightLine(150, 120, 340, 120));
        }

        [Test]
        public void WalkLine_StartingOutsideTheHouse_ReturnsFalse() => Assert.IsFalse(Map.WalkLine(-100, -100, 100, 100));

        [Test]
        public void RoomOf_PointInsideARoom_ReturnsThatRoom() => Assert.AreEqual("master", Map.RoomOf(100, 100).id);

        [Test]
        public void RoomOf_PointFarOutsideAllRooms_ReturnsNearestByDistance() => Assert.AreEqual("master", Map.RoomOf(-1000, -1000).id);

        [Test]
        public void RoomOf_PointInTheGapBetweenTwoRooms_ReturnsTheCloserOne()
        {
            // (100,404) is in the 24px gap between "master" (bottom edge y=390, distance 14) and "hall" (top edge y=414, distance 10).
            Assert.AreEqual("hall", Map.RoomOf(100, 404).id);
        }

        [Test]
        public void RectDist_PointInsideTheRect_IsZero() => Assert.AreEqual(0f, Map.RectDist(Bed, 245, 127));

        [Test]
        public void RectDist_PointOutsideHorizontally_IsDistanceToNearestEdge() => Assert.AreEqual(80f, Map.RectDist(Bed, 400, 127));

        [Test]
        public void RectDist_PointOutsideDiagonally_IsEuclideanDistanceToNearestCorner() => Assert.AreEqual(116.73f, Map.RectDist(Bed, 400, 300), 0.01f);

        [Test]
        public void NearestNode_ReturnsAnIndexIntoNodes()
        {
            int i = Map.NearestNode(100, 100);
            Assert.That(i, Is.InRange(0, Map.Nodes.Count - 1));
        }

        [Test]
        public void NearestNode_PointFarOutsideTheHouse_FallsBackToRawDistance_DoesNotThrow()
        {
            // When no graph node is within reach *and* walkable from (x,y) (true for any point outside the house),
            // NearNodes falls back to the geometrically nearest node regardless of walkability.
            int i = Map.NearestNode(-5000, -5000);
            Assert.That(i, Is.InRange(0, Map.Nodes.Count - 1));
        }

        [Test]
        public void FindPath_SameStartAndTarget_ReturnsSinglePointPath()
        {
            var path = Map.FindPath(100, 460, 100, 460);
            Assert.AreEqual(1, path.Count);
            Assert.AreEqual(100f, path[0].x); Assert.AreEqual(460f, path[0].y);
        }

        [Test]
        public void FindPath_ClearDirectLine_ReturnsSinglePointPathToTarget()
        {
            var path = Map.FindPath(100, 460, 300, 460); // open hall floor, no furniture in between
            Assert.AreEqual(1, path.Count);
            Assert.AreEqual(300f, path[0].x); Assert.AreEqual(460f, path[0].y);
        }

        [Test]
        public void FindPath_AcrossRoomsWithNoDirectLine_RoutesThroughGraphNodesToTarget()
        {
            // "master" bedroom to "kitchen": no straight walkable line exists, so this must go through Dijkstra over Nodes.
            var path = Map.FindPath(100, 100, 1300, 700);
            Assert.Greater(path.Count, 1, "a blocked direct line must produce a multi-point route");
            var last = path[path.Count - 1];
            Assert.AreEqual(1300f, last.x); Assert.AreEqual(700f, last.y);
        }
    }
}

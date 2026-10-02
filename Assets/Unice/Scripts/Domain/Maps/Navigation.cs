using System.Collections.Generic;

namespace Unice
{
    /// <summary>
    /// Path finding on the current <see cref="Map"/>: a graph of walkable points (a grid, door crossings, hiding spots),
    /// linked when a character can walk straight between them. Rebuilt by <see cref="Map.Load"/>.
    /// </summary>
    public static class Navigation
    {
        /// <summary>Spacing of the grid of nodes, and where it starts.</summary>
        const float GridStep = 46, GridStart = 64;
        /// <summary>Nodes are linked when closer than this and a straight walk works.</summary>
        const float MaxLinkLength = 80;
        /// <summary>A point joins the graph through every node within this distance it can walk straight to.</summary>
        const float EntryReach = 110;
        /// <summary>Extra nodes on each side of a door, so paths go through doors straight.</summary>
        const float DoorApproach = 34;

        public static List<Point> Nodes { get; private set; } = new List<Point>();
        /// <summary>For each node: the nodes it links to and how far they are.</summary>
        static List<(int to, float distance)>[] links;

        public static void Build()
        {
            Nodes = new List<Point>();
            for (float y = GridStart; y < Map.WorldH; y += GridStep)
                for (float x = GridStart; x < Map.WorldW; x += GridStep)
                    if (Map.CanStand(x, y, Map.BodyRadius + 3)) Nodes.Add(new Point(x, y));
            foreach (var door in Map.Doors)
            {
                Nodes.Add(new Point(door.centerX, door.centerY));
                foreach (float offset in new[] { -DoorApproach, DoorApproach })
                {
                    float x = door.vertical ? door.centerX : door.centerX + offset, y = door.vertical ? door.centerY + offset : door.centerY;
                    if (Map.CanStand(x, y)) Nodes.Add(new Point(x, y));
                }
            }
            foreach (var spot in Map.Spots) Nodes.Add(new Point(spot.standX, spot.standY));

            links = new List<(int, float)>[Nodes.Count];
            for (int i = 0; i < Nodes.Count; i++) links[i] = new List<(int, float)>();
            for (int i = 0; i < Nodes.Count; i++)
                for (int j = i + 1; j < Nodes.Count; j++)
                {
                    var nodeA = Nodes[i]; var nodeB = Nodes[j]; float distance = Geometry.Distance(nodeA.x, nodeA.y, nodeB.x, nodeB.y);
                    if (distance <= MaxLinkLength && Map.CanWalkStraight(nodeA.x, nodeA.y, nodeB.x, nodeB.y)) { links[i].Add((j, distance)); links[j].Add((i, distance)); }
                }
        }

        /// <summary>The nodes (x, y) can walk straight to within <see cref="EntryReach"/>; if none, just the closest node.</summary>
        static List<(int node, float distance)> EntryNodes(float x, float y)
        {
            var entries = new List<(int, float)>();
            for (int i = 0; i < Nodes.Count; i++)
            {
                var node = Nodes[i]; float distance = Geometry.Distance(node.x, node.y, x, y);
                if (distance < EntryReach && Map.CanWalkStraight(x, y, node.x, node.y)) entries.Add((i, distance));
            }
            if (entries.Count == 0)
            {
                int closest = 0; float closestDistance = float.MaxValue;
                for (int i = 0; i < Nodes.Count; i++)
                {
                    var node = Nodes[i]; float distance = Geometry.Distance(node.x, node.y, x, y);
                    if (distance < closestDistance) { closestDistance = distance; closest = i; }
                }
                entries.Add((closest, closestDistance));
            }
            return entries;
        }

        /// <summary>Index of a node close to (x, y) (the first entry node).</summary>
        public static int NearestNode(float x, float y) => EntryNodes(x, y)[0].node;

        // scratch arrays for FindPath, reused between calls
        static float[] costTo; static int[] cameFrom; static bool[] visited;

        /// <summary>
        /// Shortest walkable route from (fromX, fromY) to (toX, toY), as waypoints ending at the destination. Empty when unreachable.
        /// ponytail: O(N²) Dijkstra over ~550 nodes; use a heap if the house grows a lot more.
        /// </summary>
        public static List<Point> FindPath(float fromX, float fromY, float toX, float toY)
        {
            var path = new List<Point>();
            if (Map.CanWalkStraight(fromX, fromY, toX, toY)) { path.Add(new Point(toX, toY)); return path; }

            int nodeCount = Nodes.Count;
            if (costTo == null || costTo.Length != nodeCount) { costTo = new float[nodeCount]; cameFrom = new int[nodeCount]; visited = new bool[nodeCount]; }
            for (int i = 0; i < nodeCount; i++) { costTo[i] = float.PositiveInfinity; cameFrom[i] = -1; visited[i] = false; }
            foreach (var (node, distance) in EntryNodes(fromX, fromY)) costTo[node] = distance;
            var exits = new Dictionary<int, float>();   // node → distance from it to the destination
            foreach (var (node, distance) in EntryNodes(toX, toY)) exits[node] = distance;

            float bestTotal = float.PositiveInfinity; int lastNode = -1;
            while (true)
            {
                // the unvisited node with the lowest cost so far
                int current = -1; float currentCost = float.PositiveInfinity;
                for (int i = 0; i < nodeCount; i++) if (!visited[i] && costTo[i] < currentCost) { currentCost = costTo[i]; current = i; }
                if (current < 0 || currentCost >= bestTotal) break;
                visited[current] = true;
                if (exits.TryGetValue(current, out float exitDistance) && currentCost + exitDistance < bestTotal) { bestTotal = currentCost + exitDistance; lastNode = current; }
                foreach (var (next, length) in links[current])
                    if (currentCost + length < costTo[next]) { costTo[next] = currentCost + length; cameFrom[next] = current; }
            }
            if (lastNode < 0) return path;
            for (int node = lastNode; node >= 0; node = cameFrom[node]) path.Insert(0, Nodes[node]);
            path.Add(new Point(toX, toY));
            return path;
        }
    }
}

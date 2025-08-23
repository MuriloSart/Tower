using System.Collections.Generic;
using System.Linq;


public class KruskalMST
{
    private Dictionary<RoomNode, RoomNode> parent = new();

    private RoomNode Find(RoomNode node)
    {
        if (parent[node] == node)
            return node;
        return parent[node] = Find(parent[node]);
    }

    private void Union(RoomNode a, RoomNode b)
    {
        RoomNode rootA = Find(a);
        RoomNode rootB = Find(b);
        if (rootA != rootB)
            parent[rootB] = rootA;
    }

    public List<Edge> GenerateMST(Graph graph)
    {
        List<Edge> result = new();

        foreach (var node in graph.Nodes)
            parent[node] = node;

        var sortedEdges = graph.Edges.OrderBy(e => e.Weight);

        foreach (var edge in sortedEdges)
        {
            RoomNode rootA = Find(edge.From);
            RoomNode rootB = Find(edge.To);

            if (rootA != rootB)
            {
                result.Add(edge);
                Union(rootA, rootB);
            }
        }

        return result;
    }
}

using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class Graph
{
    public List<RoomNode> Nodes = new();
    public List<Edge> Edges = new();

    public RoomNode AddNode(RoomNode node)
    {
        Nodes.Add(node);
        return node;
    }

    public void AddEdge(RoomNode from, RoomNode to, int weight)
    {
        if (!Edges.Any(e => (e.From == from && e.To == to) || (e.From == to && e.To == from)))
            Edges.Add(new Edge(from, to, weight));
    }
}

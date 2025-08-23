using System.Collections.Generic; 
using System.Linq;
using UnityEngine;

public class GraphManager : MonoBehaviour
{
    public GameObject roomPrefab;
    public int numberOfRooms = 10;
    public float mapSize = 10f;
    public float minDistanceBetweenRooms = 2f;
    public float maxNeighborDistance = 15f;

    private Graph graph = new Graph();

    // Garante no Inspector que maxNeighborDistance nunca fique abaixo de minDistanceBetweenRooms
    void OnValidate()
    {
        maxNeighborDistance = Mathf.Max(maxNeighborDistance, minDistanceBetweenRooms);
    }

    void Start()
    {
        // Garante também em runtime (caso valores venham de outro lugar)
        maxNeighborDistance = Mathf.Max(maxNeighborDistance, minDistanceBetweenRooms);

        GenerateRooms();
        AdjustRoomDistances();
        GenerateEdges();
        DrawMST();
    }

    void GenerateRooms()
    {
        int maxAttempts = 10000;
        int attempts = 0;

        for (int i = 0; i < numberOfRooms; i++)
        {
            bool validPosition = false;
            Vector3 pos = Vector3.zero;

            while (!validPosition && attempts < maxAttempts)
            {
                attempts++;
                pos = new Vector3(Random.Range(-mapSize, mapSize), 0, Random.Range(-mapSize, mapSize));
                validPosition = true;

                foreach (var existingRoom in graph.Nodes)
                {
                    if (Vector3.Distance(pos, existingRoom.transform.position) < minDistanceBetweenRooms)
                    {
                        validPosition = false;
                        break;
                    }
                }
            }

            if (!validPosition) Debug.LogWarning("Não foi possível posicionar a sala sem sobreposição.");

            GameObject roomGO = Instantiate(roomPrefab, pos, Quaternion.identity);
            roomGO.name = $"Room {i + 1}";
            RoomNode node = roomGO.AddComponent<RoomNode>();
            node.Id = i + 1;
            graph.AddNode(node);
        }
    }

    void GenerateEdges()
    {
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            var node = graph.Nodes[i];
            RoomNode nearest = null;
            float nearestDist = float.MaxValue;

            for (int j = 0; j < graph.Nodes.Count; j++)
            {
                if (i == j) continue;

                var other = graph.Nodes[j];
                float dist = Vector3.Distance(node.transform.position, other.transform.position);

                int weight = Mathf.CeilToInt(dist);
                graph.AddEdge(node, other, weight);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = other;
                }
            }

            if (!graph.Edges.Any(e => e.From == node || e.To == node) && nearest != null)
            {
                int weight = Mathf.CeilToInt(nearestDist);
                graph.AddEdge(node, nearest, weight);
            }
        }
    }

    void DrawMST()
    {
        KruskalMST kruskal = new();
        List<Edge> mst = kruskal.GenerateMST(graph);

        foreach (var edge in mst)
            DrawEdgeLine(edge.From, edge.To);
    }

    void AdjustRoomDistances()
    {
        // Para cada sala
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            var node = graph.Nodes[i];

            // Encontra a sala mais próxima
            RoomNode nearest = null;
            float nearestDist = float.MaxValue;

            for (int j = 0; j < graph.Nodes.Count; j++)
            {
                if (i == j) continue;

                var other = graph.Nodes[j];
                float dist = Vector3.Distance(node.transform.position, other.transform.position);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = other;
                }
            }

            // Se a distância da sala mais próxima exceder o limite, aproxima
            if (nearestDist > maxNeighborDistance && nearest != null)
            {
                Vector3 direction = (nearest.transform.position - node.transform.position).normalized;
                float moveAmount = nearestDist - maxNeighborDistance;
                node.transform.position += direction * moveAmount;
            }
        }
    }

    void DrawEdgeLine(RoomNode from, RoomNode to)
    {
        GameObject lineGO = new GameObject($"Edge_{from.Id}_{to.Id}");
        lineGO.transform.parent = this.transform;
        lineGO.transform.position = Vector3.zero;

        LineRenderer lr = lineGO.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, from.transform.position);
        lr.SetPosition(1, to.transform.position);

        lr.startWidth = 0.3f;
        lr.endWidth = 0.3f;

        Material lineMat = new Material(Shader.Find("Unlit/Color"));
        lineMat.color = Color.red;
        lr.material = lineMat;

        lr.useWorldSpace = true;
    }
}

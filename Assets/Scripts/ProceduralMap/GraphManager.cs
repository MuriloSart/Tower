using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GraphManager : MonoBehaviour
{
    [Header("Rooms Configuration")]
    public GameObject roomPrefab;
    public int numberOfRooms = 10;
    public float mapWeight = 10f;
    public float minDistanceBetweenRooms = 2f;
    public float maxNeighborDistance = 15f;
    public int numberOfFloors = 4;

    private float floorsHeight = 25;
    private List<float> floorsPositions;

    [Header("Overlap Solver")]
    [Tooltip("Maximum number of iterations for the overlap solver")]
    public int maxRelaxIterations = 40;

    [Tooltip("Push factor (0.5 pushes half of the deficit to reach the minimum distance)")]
    public float separationStrength = 0.5f;

    [Tooltip("Tolerance margin to consider 'no overlap' and avoid jitter")]
    [Range(0.001f, 0.01f)]
    public float epsilon = 0.001f;

    [Space]

    [SerializeField]
    private ProceduralRoom proceduralRoom;


    private Graph graph = new Graph();
    private List<Vector3> roomPositions;
    private List<(Vector3 a, Vector3 b)> corridorsPoints;

    void OnValidate()
    {
        maxNeighborDistance = Mathf.Max(maxNeighborDistance, minDistanceBetweenRooms);
        separationStrength = Mathf.Clamp01(separationStrength);
    }

    private void Awake()
    {
        roomPositions = new List<Vector3>();
        corridorsPoints = new List<(Vector3 a, Vector3 b)>();
        floorsPositions = new();
    }

    void Start()
    {
        Random.InitState(System.DateTime.Now.Millisecond);
        maxNeighborDistance = Mathf.Max(maxNeighborDistance, minDistanceBetweenRooms);

        GenerateRooms();
        ResolveOverlaps();
        AdjustRoomDistances();
        GenerateEdges();
        DrawMST();

        roomPositions.Clear();
        foreach (var n in graph.Nodes)
            roomPositions.Add(n.transform.position);

        if (proceduralRoom != null)
        {
            foreach (var c in roomPositions)
            {
                proceduralRoom.Build(c);
            }
        }
        else
            Debug.LogWarning("[GraphManager] ProceduralMap não atribuído no Inspector.");

        for (int i = 0; i < numberOfFloors; i++)
        {
            floorsPositions.Add(floorsHeight * i);
        }
    }

    private void LateUpdate()
    {
        foreach(var edge in DrawMST())
        {
            Debug.DrawLine(edge.From.transform.position, edge.To.transform.position, Color.yellow, 5f);
        }
    }

    /// <summary>
    /// Defines the layout in which the rooms (nodes) will be arranged within the graph.
    /// </summary>
    public virtual void GenerateRooms()
    {

        for (int i = 0; i < numberOfFloors; i++)
        {
            floorsPositions.Add(floorsHeight * i);
        }

        for (int i = 0; i < numberOfRooms; i++)
        {
            var pos = new Vector3(
                Random.Range(-mapWeight, mapWeight),
                floorsPositions[Random.Range(0, floorsPositions.Count - 1)],
                Random.Range(-mapWeight, mapWeight) 
            );

            GameObject roomGO = Instantiate(roomPrefab, pos, Quaternion.identity, this.transform);
            roomGO.name = $"Room {i + 1}";
            RoomNode node = roomGO.AddComponent<RoomNode>();
            node.Id = i + 1;
            graph.AddNode(node);
            roomPositions.Add(pos);
        }
    }

    private void ResolveOverlaps()
    {
        if (graph.Nodes.Count <= 1) return;

        for (int iter = 0; iter < maxRelaxIterations; iter++)
        {
            bool anyPushed = false;

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                var a = graph.Nodes[i];
                for (int j = i + 1; j < graph.Nodes.Count; j++)
                {
                    var b = graph.Nodes[j];

                    Vector3 delta = b.transform.position - a.transform.position;
                    float dist = delta.magnitude;

                    
                    if (dist < (minDistanceBetweenRooms - epsilon))
                    {
                        Vector3 dir;
                        if (dist < 1e-6f)
                        {
                            
                            dir = Random.insideUnitSphere;
                            dir.y = 0f;
                            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.right;
                            dir.Normalize();
                        }
                        else
                        {
                            dir = delta / dist;
                        }

                        float deficit = (minDistanceBetweenRooms - dist);
                        
                        float push = deficit * 0.5f * Mathf.Max(0.05f, separationStrength);

                        a.transform.position -= dir * push;
                        b.transform.position += dir * push;

                        anyPushed = true;
                    }
                }
            }

            if (!anyPushed) break;
        }
    }

    /// <summary>
    /// Generates the distribution rule for edges and their respective weights.
    /// </summary>
    public virtual void GenerateEdges()
    {
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            var a = graph.Nodes[i];
            RoomNode nearest = null;
            float nearestDist = float.MaxValue;

            for (int j = i + 1; j < graph.Nodes.Count; j++)
            {
                var b = graph.Nodes[j];
                float dist = Vector3.Distance(a.transform.position, b.transform.position);
                int weight = Mathf.CeilToInt(dist);
                graph.AddEdge(a, b, weight);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = b;
                }
            }

            if (!graph.Edges.Any(e => e.From == a || e.To == a) && nearest != null)
            {
                int w = Mathf.CeilToInt(nearestDist);
                graph.AddEdge(a, nearest, w);
            }
        }
    }


    private void AdjustRoomDistances()
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
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = other;
                }
            }

            if (nearest != null && nearestDist > maxNeighborDistance)
            {
                Vector3 direction = (nearest.transform.position - node.transform.position).normalized;
                float desiredMove = nearestDist - maxNeighborDistance;

                float low = 0f, high = desiredMove, safeMove = 0f;
                for (int it = 0; it < 8; it++)
                {
                    float mid = (low + high) * 0.5f;
                    Vector3 testPos = node.transform.position + direction * mid;

                    bool ok = true;
                    for (int k = 0; k < graph.Nodes.Count; k++)
                    {
                        if (k == i) continue;
                        var other = graph.Nodes[k];
                        float d = Vector3.Distance(testPos, other.transform.position);
                        if (d < (minDistanceBetweenRooms - epsilon))
                        {
                            ok = false; break;
                        }
                    }

                    if (ok)
                    {
                        safeMove = mid;
                        low = mid;
                    }
                    else
                    {
                        high = mid;
                    }
                }

                if (safeMove > 0f)
                {
                    node.transform.position += direction * safeMove;
                }
            }
        }
    }
    private List<Edge> DrawMST()
    {
        corridorsPoints.Clear();

        KruskalMST kruskal = new();
        List<Edge> mst = kruskal.GenerateMST(graph);

        foreach (var edge in mst)
        {
            corridorsPoints.Add((edge.From.transform.position, edge.To.transform.position));
        }

        return mst;
    }
}

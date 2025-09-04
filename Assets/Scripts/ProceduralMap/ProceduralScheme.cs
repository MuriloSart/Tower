using System;
using System.Collections.Generic;
using UnityEngine;

public class ProceduralScheme : MonoBehaviour
{
    [Header("Geometry")]
    [Tooltip("Modules of the Room Wall: First One will be ever the normal wall and will be the mesh most instanced")]
    public List<Wall> wallModules;

    [Header("Floor")]
    public GameObject floorMesh;

    private readonly List<List<List<Matrix4x4>>> _batches = new();

    private Vector3 _moduleSize;
    private Vector2 _roomSize;

    void Awake()
    {
        _moduleSize = wallModules[0].mesh.bounds.size;

        for (int index = 0; index < wallModules.Count; index++)
        {
            _batches.Add(new List<List<Matrix4x4>>());
        }


        var rend = floorMesh.GetComponent<Renderer>();
        if (rend)
        {
            var s = rend.bounds.size;
            _roomSize = new Vector2(s.x, s.z);
        }
        else
        {
            var mf = floorMesh.GetComponent<MeshFilter>();

            if (!mf) { Debug.LogError("[" + this.name + "]" + " floorMesh sem Renderer/MeshFilter."); enabled = false; return; }

            Vector3 local = mf.sharedMesh.bounds.size;
            Vector3 sc = floorMesh.transform.lossyScale;

            _roomSize = new Vector2(local.x * sc.x, local.z * sc.z);
        }

        foreach (var module in wallModules) module.material.enableInstancing = true;
    }

    private void Start()
    {
        foreach (var c in GraphManager.Instance.roomPositions)
            Build(c);
    }

    public void Build(Vector3 roomCenter)
    {
        AddRoomPerimeter(roomCenter, _roomSize);
        foreach (var module in wallModules)
        {
            SplitIntoBatches(module.matrices, 1023, _batches[wallModules.IndexOf(module)]);
        }
    }

    private void LateUpdate()
    {
        foreach(List<List<Matrix4x4>> batchList in _batches)
            foreach(List<Matrix4x4> batch in batchList)
                Graphics.DrawMeshInstanced(wallModules[_batches.IndexOf(batchList)].mesh, 0, wallModules[_batches.IndexOf(batchList)].material, batch);
    }

    void AddRoomPerimeter(Vector3 center, Vector2 sizeXZ)
    {
        float halfX = sizeXZ.x * 0.5f;
        float halfZ = sizeXZ.y * 0.5f;

        AddWallSegment(center + new Vector3(-halfX, 0, +halfZ),
                       center + new Vector3(+halfX, 0, +halfZ),
                       center); // frente

        AddWallSegment(center + new Vector3(-halfX, 0, -halfZ),
                       center + new Vector3(+halfX, 0, -halfZ),
                       center); // trás

        AddWallSegment(center + new Vector3(-halfX, 0, -halfZ),
                       center + new Vector3(-halfX, 0, +halfZ),
                       center); // esquerda

        AddWallSegment(center + new Vector3(+halfX, 0, -halfZ),
                       center + new Vector3(+halfX, 0, +halfZ),
                       center); // direita
    }

    private void AddWallSegment(Vector3 a, Vector3 b, Vector3 interiorHint)
    {
        Vector3 delta = b - a;
        float vectorLenght = delta.magnitude;
        if (vectorLenght < 1e-4f) return;

        Vector3 L = delta.normalized;
        Vector3 mid = (a + b) / 2;

        float rotationAdjust;
        if (interiorHint.z < mid.z || interiorHint.x > mid.x)
            rotationAdjust = 90f;
        else
            rotationAdjust = -90f;

        Quaternion rot = Quaternion.AngleAxis(rotationAdjust, Vector3.up) * Quaternion.LookRotation(L, Vector3.up);

        float wallLenght = _moduleSize.x;

        float quantity = Mathf.Round(vectorLenght / wallLenght);
        float rest = vectorLenght - quantity * wallLenght;

        float targetProporcionalSize = vectorLenght / quantity;

        targetProporcionalSize = targetProporcionalSize / wallLenght;

        float cursor = 0f;
        for (int i = 0; i < quantity; i++)
        {
            float along = cursor + wallLenght * targetProporcionalSize * 0.5f;
            Vector3 cpos = a + L * along;

            Matrix4x4 transformMatrix = Matrix4x4.TRS(
                new Vector3(cpos.x, a.y, cpos.z),
                rot,
                new Vector3(targetProporcionalSize, 1, 1)
            );

            AddInstance(transformMatrix);

            cursor += (wallLenght * targetProporcionalSize);
        }
    }

    private void AddInstance(Matrix4x4 transformMatrix)
    {
        int totalWeight = 0;
        foreach (var module in wallModules)
            totalWeight += module.weight;

        totalWeight = Mathf.Max(0, totalWeight);
        int pick = UnityEngine.Random.Range(0, totalWeight);

        int count = 0;
        foreach (var module in wallModules)
        {
            count += module.weight;
            if (count > pick)
            {
                module.matrices.Add(transformMatrix);
                break;
            }
        }
    }

    static void SplitIntoBatches(List<Matrix4x4> src, int batchSize, List<List<Matrix4x4>> dst)
    {
        dst.Clear();
        for (int i = 0; i < src.Count; i += batchSize)
        {
            int count = Mathf.Min(batchSize, src.Count - i);
            var slice = new List<Matrix4x4>(count);
            for (int k = 0; k < count; k++) slice.Add(src[i + k]);
            dst.Add(slice);
        }
    }
}

[Serializable]
public class Wall
{
    public Mesh mesh;
    public Material material;
    public int weight = 1;

    [HideInInspector] public List<Matrix4x4> matrices;

}

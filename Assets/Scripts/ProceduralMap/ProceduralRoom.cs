using System.Collections.Generic;
using UnityEngine;

public class ProceduralRoom : MonoBehaviour
{
    [Header("Geometry")]
    public Mesh wallMesh;
    public Material wallMaterial;
    public GameObject floorMesh;


    private readonly List<Matrix4x4> _matrices = new();
    private readonly List<List<Matrix4x4>> _batches = new();

    private Vector3 _wallSize;
    private Vector2 _roomSize;

    void Awake()
    {
        if (!wallMesh) { Debug.LogError("[ProceduralRoom] wallMesh não setado."); enabled = false; return; }
        _wallSize = wallMesh.bounds.size;

        if (!floorMesh) { Debug.LogError("[ProceduralRoom] floorMesh não setado."); enabled = false; return; }
        var rend = floorMesh.GetComponent<Renderer>();
        if (rend)
        {
            var s = rend.bounds.size;
            _roomSize = new Vector2(s.x, s.z);
        }
        else
        {
            var mf = floorMesh.GetComponent<MeshFilter>();
            if (!mf) { Debug.LogError("[ProceduralRoom] floorMesh sem Renderer/MeshFilter."); enabled = false; return; }
            Vector3 local = mf.sharedMesh.bounds.size;
            Vector3 sc = floorMesh.transform.lossyScale;
            _roomSize = new Vector2(local.x * sc.x, local.z * sc.z);
        }

        if (!wallMaterial) { Debug.LogError("[ProceduralRoom] wallMaterial não setado."); enabled = false; return; }
        wallMaterial.enableInstancing = true;
        if (!wallMaterial.enableInstancing) { Debug.LogError("[ProceduralRoom] Material/Shader sem GPU Instancing."); enabled = false; return; }
    }

    public void Build(Vector3 roomCenter)
    {
        _matrices.Clear();
        _batches.Clear();

        AddRoomPerimeter(roomCenter, _roomSize);
        SplitIntoBatches(_matrices, 1023, _batches);
    }

    void LateUpdate()
    {
        if (_batches.Count == 0) return;
        foreach (var batch in _batches)
            Graphics.DrawMeshInstanced(wallMesh, 0, wallMaterial, batch);
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

    void AddWallSegment(Vector3 a, Vector3 b, Vector3 interiorHint)
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

        float wallLenght = _wallSize.x;

        int full = Mathf.FloorToInt(vectorLenght / wallLenght);
        float rest = vectorLenght - full * wallLenght;

        float cursor = 0f;
        for (int i = 0; i < full; i++)
        {
            float along = cursor + wallLenght * 0.5f;
            Vector3 cpos = a + L * along;

            _matrices.Add(Matrix4x4.TRS(
                new Vector3(cpos.x, a.y, cpos.z),
                rot,
                Vector3.one
            ));

            cursor += wallLenght;
        }

        if (rest > 1e-3f)
        {
            float partial = rest / wallLenght;
            float along = cursor + rest * 0.5f;
            Vector3 cpos = a + L * along;

            Vector3 scale = Vector3.one;
            scale.x = partial;

            _matrices.Add(Matrix4x4.TRS(
                new Vector3(cpos.x, a.y, cpos.z),
                rot,
                scale
            ));
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

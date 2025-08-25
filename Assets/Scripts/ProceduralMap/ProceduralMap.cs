using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renderiza paredes de salas (ret�ngulos) e corredores (duas laterais por aresta)
/// via Graphics.DrawMeshInstanced.
/// </summary>
public class ProceduralMap : MonoBehaviour
{
    [Header("Geometry")]
    public Mesh wallMesh;                // Mesh da parede (bloco). Comprimento ficar� no eixo escolhido abaixo.
    public Material wallMaterial;        // Material base (precisa suportar GPU Instancing)

    [Header("Rooms")]
    public Vector2 roomSize = new Vector2(6, 4); // Largura (X) e Profundidade (Z) de cada sala
    public float wallHeight = 3f;                // Altura da parede
    public float wallThickness = 0.2f;           // Espessura da parede

    [Header("Corridors")]
    public float corridorWidth = 2f;             // Largura do corredor (dist�ncia entre paredes laterais)
    public float corridorMargin = 0.5f;          // Folga para n�o colar a parede na sala

    public enum LengthAxis { X, Z }

    [Header("Mesh Orientation")]
    [Tooltip("Qual eixo do wallMesh representa o 'comprimento' da parede")]
    public LengthAxis meshLengthAxis = LengthAxis.Z; // Muitos meshes usam Z como frente

    [Header("Debug")]
    public int seed = 0;

    // buffers
    private readonly List<Matrix4x4> _matrices = new();
    private readonly List<List<Matrix4x4>> _batches = new();

    // cache do tamanho do mesh (para escalar corretamente)
    private Vector3 _meshSize; // wallMesh.bounds.size

    // dados externos
    private List<Vector3> _roomCenters;              // posi��es das salas (centro)
    private List<(Vector3 A, Vector3 B)> _corridors; // segmentos dos corredores

    void Awake()
    {
        // Checagens b�sicas
        if (wallMesh == null)
        {
            Debug.LogError("[ProceduralMap] wallMesh n�o setado.");
            enabled = false;
            return;
        }

        _meshSize = wallMesh.bounds.size;
        if (_meshSize.x <= 0f || _meshSize.y <= 0f || _meshSize.z <= 0f)
            Debug.LogWarning("[ProceduralMap] wallMesh.bounds.size parece inv�lido; confira o mesh.");

        if (wallMaterial == null)
        {
            Debug.LogError("[ProceduralMap] wallMaterial n�o setado.");
            enabled = false;
            return;
        }

        // Tenta habilitar instancing (shader precisa suportar)
        wallMaterial.enableInstancing = true;
        if (!wallMaterial.enableInstancing)
        {
            Debug.LogError("[ProceduralMap] Shader do wallMaterial n�o suporta instancing. Habilite no material/shader ou troque o material.");
            enabled = false;
            return;
        }
    }

    /// <summary>
    /// Chame isto a partir do seu GraphManager, depois que as salas e a MST/arestas estiverem prontas.
    /// Ex.: proceduralMap.Build(positions, edges);
    /// </summary>
    public void Build(List<Vector3> roomCenters, List<(Vector3 A, Vector3 B)> corridors)
    {
        _roomCenters = roomCenters;
        _corridors = corridors;

        _matrices.Clear();
        _batches.Clear();

        // 1) paredes das salas (per�metro)
        if (_roomCenters != null)
        {
            foreach (var c in _roomCenters)
                AddRoomPerimeter(c, roomSize);
        }

        // 2) paredes dos corredores (duas laterais por segmento)
        if (_corridors != null)
        {
            foreach (var seg in _corridors)
                AddCorridorWalls(seg.A, seg.B);
        }

        // 3) quebra em lotes de at� 1023 inst�ncias
        SplitIntoBatches(_matrices, 1023, _batches);
    }

    void LateUpdate()
    {
        // desenha (se j� fizemos Build)
        if (_batches.Count == 0) return;

        foreach (var batch in _batches)
            Graphics.DrawMeshInstanced(wallMesh, 0, wallMaterial, batch);
    }

    // ========= helpers de constru��o =========

    void AddRoomPerimeter(Vector3 center, Vector2 sizeXZ)
    {
        float halfX = sizeXZ.x * 0.5f;
        float halfZ = sizeXZ.y * 0.5f;

        // Frente (no +Z)
        AddWallSegment(
            center + new Vector3(-halfX, 0, +halfZ),
            center + new Vector3(+halfX, 0, +halfZ));

        // Tr�s (no -Z)
        AddWallSegment(
            center + new Vector3(-halfX, 0, -halfZ),
            center + new Vector3(+halfX, 0, -halfZ));

        // Esquerda (no -X)
        AddWallSegment(
            center + new Vector3(-halfX, 0, -halfZ),
            center + new Vector3(-halfX, 0, +halfZ));

        // Direita (no +X)
        AddWallSegment(
            center + new Vector3(+halfX, 0, -halfZ),
            center + new Vector3(+halfX, 0, +halfZ));
    }

    void AddCorridorWalls(Vector3 a, Vector3 b)
    {
        Vector3 dir = (b - a);
        float length = dir.magnitude;
        if (length < 1e-4f) return;

        Vector3 fwd = dir / length;
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

        // recua uma margem para n�o colar na sala
        Vector3 p0 = a + fwd * corridorMargin;
        Vector3 p1 = b - fwd * corridorMargin;

        float usableLen = Vector3.Distance(p0, p1);
        if (usableLen <= 1e-3f) return;

        // duas paredes laterais
        Vector3 leftOffset = -right * (corridorWidth * 0.5f);
        Vector3 rightOffset = right * (corridorWidth * 0.5f);

        AddWallSegment(p0 + leftOffset, p1 + leftOffset);   // lateral esquerda
        AddWallSegment(p0 + rightOffset, p1 + rightOffset);  // lateral direita
    }

    // Cria uma inst�ncia de parede (um �tijolo� escalado) entre dois pontos no plano XZ
    void AddWallSegment(Vector3 a, Vector3 b)
    {
        Vector3 delta = b - a;
        float length = delta.magnitude;
        if (length < 1e-4f) return;

        Vector3 mid = (a + b) * 0.5f;

        // Rota��o que alinha o eixo de COMPRIMENTO do mesh com o segmento (b - a)
        Quaternion rot = (meshLengthAxis == LengthAxis.X)
            ? Quaternion.FromToRotation(Vector3.right, delta.normalized)   // alinha eixo X do mesh
            : Quaternion.FromToRotation(Vector3.forward, delta.normalized); // alinha eixo Z do mesh

        // Escalas coerentes com o eixo de comprimento escolhido
        float sy = Mathf.Max(0.0001f, wallHeight / Mathf.Max(1e-4f, _meshSize.y));
        float sx, sz;

        if (meshLengthAxis == LengthAxis.X)
        {
            sx = Mathf.Max(0.0001f, length / Mathf.Max(1e-4f, _meshSize.x)); // comprimento no X
            sz = Mathf.Max(0.0001f, wallThickness / Mathf.Max(1e-4f, _meshSize.z)); // espessura no Z
        }
        else // LengthAxis.Z
        {
            sx = Mathf.Max(0.0001f, wallThickness / Mathf.Max(1e-4f, _meshSize.x)); // espessura no X
            sz = Mathf.Max(0.0001f, length / Mathf.Max(1e-4f, _meshSize.z)); // comprimento no Z
        }

        Matrix4x4 m = Matrix4x4.TRS(
            new Vector3(mid.x, wallHeight * 0.5f, mid.z), // eleva metade da altura
            rot,
            new Vector3(sx, sy, sz)
        );

        _matrices.Add(m);
    }

    // quebra em lotes de at� 1023 inst�ncias
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

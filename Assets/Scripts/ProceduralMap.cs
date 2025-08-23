using System.Collections.Generic;
using UnityEngine;

namespace ProceduralGenetaion
{
    public class ProceduralMap : MonoBehaviour
    {
        public Vector2 roomSize;
        public int seed;

        public Mesh wallMesh;
        public Material wallMaterial1;
        public Material wallMaterial0;

        private Vector2 wallSize;

        private List<Matrix4x4> wallMatricesN;
        private List<Matrix4x4> wallMatricesNB;
        private List<Matrix4x4> wallMatricesNC;

        private void Start()
        {
            createWalls();
        }
        void createWalls()
        {
            Random.InitState(seed);
 
            var wallMatricesN = new List<Matrix4x4>();
            var wallMatricesNB = new List<Matrix4x4>();
            var wallMatricesNC = new List<Matrix4x4>();

            int wallCount = Mathf.Max(1, (int)(roomSize.x / wallSize.x));
            float scale = (roomSize.x / wallCount) / wallSize.x;

            for (int i = 0; i < wallCount; i++)
            {
                var t = transform.position + new Vector3(
                    -roomSize.x / 2 + wallSize.x * scale / 2 + i * scale * wallSize.x,
                    0,
                    -roomSize.y / 2
                );

                var r = transform.rotation;
                var s = new Vector3(scale, 1, 1);

                var mat = Matrix4x4.TRS(t, r, s);

                var rand = Random.Range(0, 3);
                if (rand < 1)
                {
                    wallMatricesN.Add(mat);
                }
                else if (rand < 2)
                {
                    wallMatricesNB.Add(mat);
                }
                else
                {
                    wallMatricesNC.Add(mat);
                }
            }
        }

        void renderWalls()
        {
            if(wallMatricesN != null)
            {
                Graphics.DrawMeshInstanced(wallMesh, 0, wallMaterial0, wallMatricesN.ToArray(), wallMatricesN.Count);
                Graphics.DrawMeshInstanced(wallMesh, 0, wallMaterial1, wallMatricesN.ToArray(), wallMatricesN.Count);
            }
        }
    }
}

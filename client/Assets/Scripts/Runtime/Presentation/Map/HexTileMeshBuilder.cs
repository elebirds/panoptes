using UnityEngine;

namespace Panoptes.Presentation.Map
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    public sealed class HexTileMeshBuilder : MonoBehaviour
    {
        [SerializeField] private float radius = 1f;

        private static Mesh _sharedPointyHexMesh;

        private void Awake()
        {
            ApplyMesh();
        }

        private void OnEnable()
        {
            ApplyMesh();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _sharedPointyHexMesh = null;
            ApplyMesh();
        }
#endif

        private void ApplyMesh()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                return;
            }

            var mesh = GetOrCreateMesh(Mathf.Max(0.0001f, radius));
            meshFilter.sharedMesh = mesh;

            if (TryGetComponent<MeshCollider>(out var meshCollider))
            {
                meshCollider.sharedMesh = mesh;
            }
        }

        private static Mesh GetOrCreateMesh(float meshRadius)
        {
            if (_sharedPointyHexMesh != null)
            {
                return _sharedPointyHexMesh;
            }

            var vertices = new Vector3[7];
            var uvs = new Vector2[7];
            var triangles = new int[18];
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (var i = 0; i < 6; i++)
            {
                var angle = Mathf.Deg2Rad * (30f + 60f * i);
                var x = Mathf.Cos(angle) * meshRadius;
                var y = Mathf.Sin(angle) * meshRadius;
                vertices[i + 1] = new Vector3(x, y, 0f);
                uvs[i + 1] = new Vector2(x / (meshRadius * 2f) + 0.5f, y / (meshRadius * 2f) + 0.5f);

                var tri = i * 3;
                triangles[tri] = 0;
                triangles[tri + 1] = i + 1;
                triangles[tri + 2] = i == 5 ? 1 : i + 2;
            }

            var mesh = new Mesh
            {
                name = "PointyTopHexTile"
            };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _sharedPointyHexMesh = mesh;
            return _sharedPointyHexMesh;
        }
    }
}

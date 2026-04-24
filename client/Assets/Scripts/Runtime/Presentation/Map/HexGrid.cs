using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public static class HexGrid
    {
        public static readonly Vector2Int[] AxialDirections =
        {
            new Vector2Int(1, 0),
            new Vector2Int(1, -1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(0, 1)
        };

        public static Vector3 AxialToWorld(int q, int r, float size)
        {
            var safeSize = Mathf.Max(0.0001f, size);
            var x = safeSize * (Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) * 0.5f * r);
            var z = safeSize * (1.5f * r);
            return new Vector3(x, 0f, z);
        }

        public static Vector2Int WorldToAxial(Vector3 world, float size)
        {
            var safeSize = Mathf.Max(0.0001f, size);
            var q = (Mathf.Sqrt(3f) / 3f * world.x - 1f / 3f * world.z) / safeSize;
            var r = (2f / 3f * world.z) / safeSize;
            return CubeRound(q, r);
        }

        public static int AxialDistance(Vector2Int a, Vector2Int b)
        {
            var dq = a.x - b.x;
            var dr = a.y - b.y;
            return (Mathf.Abs(dq) + Mathf.Abs(dq + dr) + Mathf.Abs(dr)) / 2;
        }

        public static Vector2Int OffsetToAxial(int col, int row)
        {
            return new Vector2Int(col - (row - (row & 1)) / 2, row);
        }

        public static Vector2Int AxialToOffset(int q, int r)
        {
            return new Vector2Int(q + (r - (r & 1)) / 2, r);
        }

        private static Vector2Int CubeRound(float q, float r)
        {
            var x = q;
            var z = r;
            var y = -x - z;

            var rx = Mathf.Round(x);
            var ry = Mathf.Round(y);
            var rz = Mathf.Round(z);

            var xDiff = Mathf.Abs(rx - x);
            var yDiff = Mathf.Abs(ry - y);
            var zDiff = Mathf.Abs(rz - z);

            if (xDiff > yDiff && xDiff > zDiff)
            {
                rx = -ry - rz;
            }
            else if (yDiff > zDiff)
            {
                ry = -rx - rz;
            }
            else
            {
                rz = -rx - ry;
            }

            return new Vector2Int(Mathf.RoundToInt(rx), Mathf.RoundToInt(rz));
        }
    }
}

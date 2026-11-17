// Claude.ai changed: New helper class with the 2D (XZ-plane) polygon maths used by the Prefab Scatter tool: Catmull-Rom smoothing, point-in-polygon, distance to edge, area and triangulation.
using System.Collections.Generic;
using UnityEngine;

namespace TheSourceGame.PrefabScatter
{
    public static class ScatterMath
    {
        // Claude.ai changed: Converts a world/local point to a 2D point on the XZ plane.
        public static Vector2 ToXZ(Vector3 point)
        {
            return new Vector2(point.x, point.z);
        }

        // Claude.ai changed: Builds a closed, smooth outline through the control points (uniform Catmull-Rom). With subdivisions <= 1 the control points are returned as-is.
        public static void BuildClosedCurve(IList<Vector3> controlPoints, int subdivisions, List<Vector3> result)
        {
            result.Clear();
            int count = controlPoints.Count;
            if (count < 3)
            {
                for (int i = 0; i < count; i++)
                {
                    result.Add(controlPoints[i]);
                }
                return;
            }

            if (subdivisions <= 1)
            {
                for (int i = 0; i < count; i++)
                {
                    result.Add(controlPoints[i]);
                }
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 p0 = controlPoints[(i - 1 + count) % count];
                Vector3 p1 = controlPoints[i];
                Vector3 p2 = controlPoints[(i + 1) % count];
                Vector3 p3 = controlPoints[(i + 2) % count];

                for (int s = 0; s < subdivisions; s++)
                {
                    float t = s / (float)subdivisions;
                    result.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }
        }

        public static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1)
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // Claude.ai changed: Positive = counter-clockwise on the XZ plane.
        public static float SignedAreaXZ(IList<Vector3> polygon)
        {
            float area = 0f;
            int count = polygon.Count;
            for (int i = 0; i < count; i++)
            {
                Vector3 a = polygon[i];
                Vector3 b = polygon[(i + 1) % count];
                area += a.x * b.z - b.x * a.z;
            }
            return area * 0.5f;
        }

        public static bool IsInsideXZ(IList<Vector3> polygon, Vector2 point)
        {
            bool inside = false;
            int count = polygon.Count;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector3 a = polygon[i];
                Vector3 b = polygon[j];
                if (((a.z > point.y) != (b.z > point.y))
                    && (point.x < (b.x - a.x) * (point.y - a.z) / (b.z - a.z) + a.x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        public static float DistanceToEdgeXZ(IList<Vector3> polygon, Vector2 point)
        {
            float best = float.MaxValue;
            int count = polygon.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = ToXZ(polygon[i]);
                Vector2 b = ToXZ(polygon[(i + 1) % count]);
                float distance = DistancePointSegment(point, a, b);
                if (distance < best)
                {
                    best = distance;
                }
            }
            return best;
        }

        public static float DistancePointSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            if (lengthSqr < 0.000001f)
            {
                return Vector2.Distance(point, a);
            }
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSqr);
            return Vector2.Distance(point, a + ab * t);
        }

        // Claude.ai changed: Ear-clipping triangulation on the XZ plane. Works for concave (organic) shapes as long as the outline does not cross itself.
        public static void TriangulateXZ(IList<Vector3> polygon, List<int> triangles)
        {
            triangles.Clear();
            int count = polygon.Count;
            if (count < 3)
            {
                return;
            }

            List<int> indices = new List<int>(count);
            if (SignedAreaXZ(polygon) > 0f)
            {
                for (int i = 0; i < count; i++)
                {
                    indices.Add(i);
                }
            }
            else
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    indices.Add(i);
                }
            }

            int guard = count * count;
            while (indices.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                int n = indices.Count;
                for (int i = 0; i < n; i++)
                {
                    int ia = indices[(i - 1 + n) % n];
                    int ib = indices[i];
                    int ic = indices[(i + 1) % n];

                    Vector2 a = ToXZ(polygon[ia]);
                    Vector2 b = ToXZ(polygon[ib]);
                    Vector2 c = ToXZ(polygon[ic]);

                    if (Cross(b - a, c - b) <= 0f)
                    {
                        continue;
                    }

                    bool containsOther = false;
                    for (int j = 0; j < n; j++)
                    {
                        int ip = indices[j];
                        if (ip == ia || ip == ib || ip == ic)
                        {
                            continue;
                        }
                        if (PointInTriangle(ToXZ(polygon[ip]), a, b, c))
                        {
                            containsOther = true;
                            break;
                        }
                    }

                    if (containsOther)
                    {
                        continue;
                    }

                    triangles.Add(ia);
                    triangles.Add(ib);
                    triangles.Add(ic);
                    indices.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (!clipped)
                {
                    break;
                }
            }

            if (indices.Count == 3)
            {
                triangles.Add(indices[0]);
                triangles.Add(indices[1]);
                triangles.Add(indices[2]);
            }
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a);
            float d2 = Cross(c - b, p - b);
            float d3 = Cross(a - c, p - c);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }
    }
}

// Claude.ai changed: v1.0.1 - translated all UI text, tooltips and log messages to English.
// Claude.ai changed: New component that holds all settings of one scatter area. It only stores data and draws a gizmo; the actual spawning lives in the Editor assembly (PrefabScatterGenerator).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheSourceGame.PrefabScatter
{
    [Serializable]
    public class ScatterPrefabEntry
    {
        [Tooltip("The prefab to spawn. Its pivot should be at the bottom.")]
        public GameObject prefab;

        [Tooltip("Relative chance that this prefab is picked. 2 = twice as often as a prefab with 1.")]
        [Min(0f)]
        public float weight = 1f;

        [Tooltip("Random uniform scale between min (x) and max (y), multiplied by the prefab's own scale.")]
        public Vector2 scaleRange = new Vector2(0.9f, 1.1f);

        [Tooltip("0 = always straight up, 1 = fully follows the slope of the ground.")]
        [Range(0f, 1f)]
        public float alignToNormal = 0f;

        [Tooltip("Offset along the up axis. Negative = sink slightly into the ground (prevents floating edges on slopes).")]
        public float yOffset = 0f;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("The Source Game/Prefab Scatter Area")]
    public class PrefabScatterArea : MonoBehaviour
    {
        // ---------- Prefabs ----------
        public List<ScatterPrefabEntry> prefabs = new List<ScatterPrefabEntry>();

        // ---------- Ground ----------
        [Tooltip("Only colliders on these layers count as ground.")]
        public LayerMask groundLayers = 1;

        [Tooltip("Maximum slope in degrees. Steeper spots are skipped.")]
        [Range(0f, 90f)]
        public float maxSlope = 45f;

        [Tooltip("How far above the highest point of the area the raycast starts.")]
        [Min(0.1f)]
        public float raycastHeight = 50f;

        // ---------- Area ----------
        [Tooltip("Control points of the area, local to this object.")]
        public List<Vector3> points = new List<Vector3>();

        [Tooltip("Creates a smooth (organic) outline through the control points.")]
        public bool smoothOutline = true;

        [Tooltip("Number of sub-points per segment of the smooth outline.")]
        [Range(2, 16)]
        public int smoothSubdivisions = 8;

        // ---------- Distribution ----------
        [Tooltip("Maximum number of objects per square metre.")]
        [Min(0f)]
        public float density = 0.5f;

        [Tooltip("Minimum distance between two spawned objects (metres). 0 = off.")]
        [Min(0f)]
        public float minDistance = 0.5f;

        [Tooltip("Within this distance from the border the density fades to 0, for a natural edge. 0 = off.")]
        [Min(0f)]
        public float edgeFalloff = 1f;

        [Tooltip("Uses Perlin noise to create clumps and clearings.")]
        public bool useNoise = false;

        [Tooltip("Noise frequency. Smaller = larger clumps.")]
        [Min(0.001f)]
        public float noiseScale = 0.15f;

        [Tooltip("Higher = more clearings.")]
        [Range(0f, 1f)]
        public float noiseThreshold = 0.45f;

        // ---------- Transform ----------
        [Tooltip("Random rotation around the Y axis (0-360).")]
        public bool randomYRotation = true;

        // ---------- Output ----------
        [Tooltip("Random seed. Same seed + same settings = same result.")]
        public int seed = 12345;

        [Tooltip("Safety limit for the number of objects in this area.")]
        [Min(1)]
        public int maxInstances = 5000;

        [Tooltip("Name of the child object that holds all spawned objects.")]
        public string containerName = "Scattered";

        [Tooltip("The child object holding all spawned objects. Created automatically.")]
        public Transform container;

        // ---------- Visuals (editor only) ----------
        public bool showPreview = true;
        public Color areaColor = new Color(0.35f, 0.9f, 0.45f, 1f);

        // Claude.ai changed: Reusable buffers so gizmo drawing does not allocate every frame.
        private readonly List<Vector3> _localCurveBuffer = new List<Vector3>();
        private readonly List<Vector3> _worldCurveBuffer = new List<Vector3>();

        // Claude.ai changed: Called by Unity when the component is added; creates a slightly irregular circle so the area looks organic straight away.
        private void Reset()
        {
            ResetPointsToCircle(5f, 8);
        }

        public void ResetPointsToCircle(float radius, int count)
        {
            points.Clear();
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                float r = radius * (0.85f + 0.3f * Mathf.PerlinNoise(i * 0.73f, 0.5f));
                points.Add(new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r));
            }
        }

        // Claude.ai changed: Fills 'result' with the (smoothed) outline of the area in world space.
        public void GetWorldOutline(List<Vector3> result)
        {
            ScatterMath.BuildClosedCurve(points, smoothOutline ? smoothSubdivisions : 1, _localCurveBuffer);
            result.Clear();
            for (int i = 0; i < _localCurveBuffer.Count; i++)
            {
                result.Add(transform.TransformPoint(_localCurveBuffer[i]));
            }
        }

        // Claude.ai changed: Faint outline when the object is not selected, so you can still see where areas are in the scene.
        private void OnDrawGizmos()
        {
            if (points == null || points.Count < 3)
            {
                return;
            }

            GetWorldOutline(_worldCurveBuffer);
            Color color = areaColor;
            color.a = 0.35f;
            Gizmos.color = color;
            for (int i = 0; i < _worldCurveBuffer.Count; i++)
            {
                Gizmos.DrawLine(_worldCurveBuffer[i], _worldCurveBuffer[(i + 1) % _worldCurveBuffer.Count]);
            }
        }
    }
}

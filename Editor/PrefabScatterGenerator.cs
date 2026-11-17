// Claude.ai changed: New editor-only class that calculates placements (also used for the scene preview) and spawns/clears the prefabs with full Undo support.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TheSourceGame.PrefabScatter
{
    public struct ScatterPlacement
    {
        public int prefabIndex;
        public Vector3 position;
        public Quaternion rotation;
        public float scale;
    }

    public static class PrefabScatterGenerator
    {
        private const int _candidateMultiplier = 4;
        private const int _insideAttempts = 20;
        private const float _noiseSoftness = 0.12f;

        private static readonly RaycastHit[] _hitBuffer = new RaycastHit[32];
        private static readonly List<Vector3> _outlineBuffer = new List<Vector3>();
        private static readonly List<ScatterPlacement> _placementBuffer = new List<ScatterPlacement>();

        // ---------------------------------------------------------------
        // Placement calculation
        // ---------------------------------------------------------------

        // Claude.ai changed: Calculates where prefabs would be placed without spawning anything. Deterministic for a given seed + settings.
        public static void ComputePlacements(PrefabScatterArea area, List<ScatterPlacement> result)
        {
            result.Clear();
            if (area == null || area.points == null || area.points.Count < 3)
            {
                return;
            }

            area.GetWorldOutline(_outlineBuffer);
            List<Vector3> outline = _outlineBuffer;

            float areaSize = Mathf.Abs(ScatterMath.SignedAreaXZ(outline));
            if (areaSize < 0.0001f || area.density <= 0f)
            {
                return;
            }

            // Bounds of the outline.
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector3 p = outline[i];
                min = Vector2.Min(min, ScatterMath.ToXZ(p));
                max = Vector2.Max(max, ScatterMath.ToXZ(p));
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            float rayStartY = maxY + area.raycastHeight;
            float rayLength = area.raycastHeight * 2f + (maxY - minY);

            // Prefab weights.
            float totalWeight = 0f;
            for (int i = 0; i < area.prefabs.Count; i++)
            {
                ScatterPrefabEntry entry = area.prefabs[i];
                if (entry != null && entry.prefab != null && entry.weight > 0f)
                {
                    totalWeight += entry.weight;
                }
            }

            System.Random random = new System.Random(area.seed);
            float noiseOffsetX = (float)random.NextDouble() * 1000f;
            float noiseOffsetZ = (float)random.NextDouble() * 1000f;

            long candidateCount = (long)Mathf.Round(areaSize * area.density);
            long candidateCap = (long)area.maxInstances * _candidateMultiplier;
            if (candidateCount > candidateCap)
            {
                candidateCount = candidateCap;
            }

            float cellSize = Mathf.Max(area.minDistance, 0.01f);
            float minDistanceSqr = area.minDistance * area.minDistance;
            Dictionary<Vector2Int, List<Vector2>> grid = area.minDistance > 0f ? new Dictionary<Vector2Int, List<Vector2>>() : null;

            for (long c = 0; c < candidateCount; c++)
            {
                if (result.Count >= area.maxInstances)
                {
                    break;
                }

                // Random point inside the outline.
                Vector2 point = Vector2.zero;
                bool inside = false;
                for (int attempt = 0; attempt < _insideAttempts; attempt++)
                {
                    point = new Vector2(
                        Mathf.Lerp(min.x, max.x, (float)random.NextDouble()),
                        Mathf.Lerp(min.y, max.y, (float)random.NextDouble()));
                    if (ScatterMath.IsInsideXZ(outline, point))
                    {
                        inside = true;
                        break;
                    }
                }

                // Always consume the same amount of random numbers per candidate, so changing one setting does not reshuffle everything.
                double maskRoll = random.NextDouble();
                double prefabRoll = random.NextDouble();
                double yawRoll = random.NextDouble();
                double scaleRoll = random.NextDouble();

                if (!inside)
                {
                    continue;
                }

                // Density mask: edge falloff + noise.
                float mask = 1f;
                if (area.edgeFalloff > 0f)
                {
                    float distance = ScatterMath.DistanceToEdgeXZ(outline, point);
                    mask *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / area.edgeFalloff));
                }
                if (area.useNoise)
                {
                    float noise = Mathf.PerlinNoise(point.x * area.noiseScale + noiseOffsetX, point.y * area.noiseScale + noiseOffsetZ);
                    mask *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(area.noiseThreshold - _noiseSoftness, area.noiseThreshold + _noiseSoftness, noise));
                }
                if (maskRoll > mask)
                {
                    continue;
                }

                if (grid != null && IsTooClose(grid, point, cellSize, minDistanceSqr))
                {
                    continue;
                }

                RaycastHit hit;
                if (!TryFindGround(area, new Vector3(point.x, rayStartY, point.y), rayLength, out hit))
                {
                    continue;
                }

                if (Vector3.Angle(hit.normal, Vector3.up) > area.maxSlope)
                {
                    continue;
                }

                int prefabIndex = PickPrefab(area, totalWeight, (float)prefabRoll);
                ScatterPrefabEntry entry = prefabIndex >= 0 ? area.prefabs[prefabIndex] : null;

                Quaternion rotation = area.randomYRotation
                    ? Quaternion.Euler(0f, (float)yawRoll * 360f, 0f)
                    : Quaternion.identity;

                float scale = 1f;
                Vector3 position = hit.point;

                if (entry != null)
                {
                    if (entry.alignToNormal > 0f)
                    {
                        Quaternion toNormal = Quaternion.FromToRotation(Vector3.up, hit.normal);
                        rotation = Quaternion.Slerp(Quaternion.identity, toNormal, entry.alignToNormal) * rotation;
                    }
                    scale = Mathf.Lerp(entry.scaleRange.x, entry.scaleRange.y, (float)scaleRoll);
                    position += rotation * Vector3.up * entry.yOffset;
                }

                if (grid != null)
                {
                    AddToGrid(grid, point, cellSize);
                }

                ScatterPlacement placement;
                placement.prefabIndex = prefabIndex;
                placement.position = position;
                placement.rotation = rotation;
                placement.scale = scale;
                result.Add(placement);
            }
        }

        // Claude.ai changed: Raycasts down and returns the nearest hit on the ground layers, ignoring triggers and everything that was already scattered by this area.
        public static bool TryFindGround(PrefabScatterArea area, Vector3 origin, float length, out RaycastHit result)
        {
            result = default(RaycastHit);
            int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, _hitBuffer, length, area.groundLayers, QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                if (area.container != null && hit.transform.IsChildOf(area.container))
                {
                    continue;
                }
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    result = hit;
                    found = true;
                }
            }
            return found;
        }

        private static int PickPrefab(PrefabScatterArea area, float totalWeight, float roll)
        {
            if (totalWeight <= 0f)
            {
                return -1;
            }

            float target = roll * totalWeight;
            float accumulated = 0f;
            int lastValid = -1;
            for (int i = 0; i < area.prefabs.Count; i++)
            {
                ScatterPrefabEntry entry = area.prefabs[i];
                if (entry == null || entry.prefab == null || entry.weight <= 0f)
                {
                    continue;
                }
                lastValid = i;
                accumulated += entry.weight;
                if (target <= accumulated)
                {
                    return i;
                }
            }
            return lastValid;
        }

        private static Vector2Int GetCell(Vector2 point, float cellSize)
        {
            return new Vector2Int(Mathf.FloorToInt(point.x / cellSize), Mathf.FloorToInt(point.y / cellSize));
        }

        private static bool IsTooClose(Dictionary<Vector2Int, List<Vector2>> grid, Vector2 point, float cellSize, float minDistanceSqr)
        {
            Vector2Int cell = GetCell(point, cellSize);
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    List<Vector2> list;
                    if (!grid.TryGetValue(new Vector2Int(cell.x + x, cell.y + y), out list))
                    {
                        continue;
                    }
                    for (int i = 0; i < list.Count; i++)
                    {
                        if ((list[i] - point).sqrMagnitude < minDistanceSqr)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static void AddToGrid(Dictionary<Vector2Int, List<Vector2>> grid, Vector2 point, float cellSize)
        {
            Vector2Int cell = GetCell(point, cellSize);
            List<Vector2> list;
            if (!grid.TryGetValue(cell, out list))
            {
                list = new List<Vector2>();
                grid.Add(cell, list);
            }
            list.Add(point);
        }

        // ---------------------------------------------------------------
        // Spawning
        // ---------------------------------------------------------------

        // Claude.ai changed: Clears the container and spawns all placements as prefab instances (one Undo step). Returns the number of spawned objects.
        public static int Scatter(PrefabScatterArea area)
        {
            if (area == null)
            {
                return 0;
            }

            Physics.SyncTransforms();

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Scatter Prefabs");

            Transform container = GetOrCreateContainer(area);
            ClearChildren(container);

            ComputePlacements(area, _placementBuffer);

            int spawned = 0;
            for (int i = 0; i < _placementBuffer.Count; i++)
            {
                ScatterPlacement placement = _placementBuffer[i];
                if (placement.prefabIndex < 0)
                {
                    continue;
                }

                GameObject prefab = area.prefabs[placement.prefabIndex].prefab;
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, container) as GameObject;
                if (instance == null)
                {
                    // Not a prefab asset (e.g. a scene object): fall back to a normal copy.
                    instance = Object.Instantiate(prefab, container);
                    instance.name = prefab.name;
                }

                Transform t = instance.transform;
                t.SetPositionAndRotation(placement.position, placement.rotation);

                Vector3 parentScale = container.lossyScale;
                Vector3 baseScale = prefab.transform.localScale * placement.scale;
                t.localScale = new Vector3(
                    SafeDivide(baseScale.x, parentScale.x),
                    SafeDivide(baseScale.y, parentScale.y),
                    SafeDivide(baseScale.z, parentScale.z));

                Undo.RegisterCreatedObjectUndo(instance, "Scatter Prefabs");
                spawned++;
            }

            Undo.CollapseUndoOperations(undoGroup);
            return spawned;
        }

        // Claude.ai changed: Removes everything under the container (undoable). The container itself stays.
        public static void Clear(PrefabScatterArea area)
        {
            if (area == null || area.container == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Clear Scattered Prefabs");
            ClearChildren(area.container);
            Undo.CollapseUndoOperations(undoGroup);
        }

        public static int CountSpawned(PrefabScatterArea area)
        {
            return area != null && area.container != null ? area.container.childCount : 0;
        }

        private static void ClearChildren(Transform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(container.GetChild(i).gameObject);
            }
        }

        private static Transform GetOrCreateContainer(PrefabScatterArea area)
        {
            if (area.container != null)
            {
                return area.container;
            }

            string containerName = string.IsNullOrEmpty(area.containerName) ? "Scattered" : area.containerName;
            Transform existing = area.transform.Find(containerName);

            Undo.RecordObject(area, "Scatter Prefabs");
            if (existing != null)
            {
                area.container = existing;
            }
            else
            {
                GameObject go = new GameObject(containerName);
                Undo.RegisterCreatedObjectUndo(go, "Scatter Prefabs");
                go.transform.SetParent(area.transform, false);
                area.container = go.transform;
            }
            return area.container;
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) < 0.00001f ? value : value / divisor;
        }
    }
}

// Claude.ai changed: New component that holds all settings of one scatter area. It only stores data and draws a gizmo; the actual spawning lives in the Editor assembly (PrefabScatterGenerator).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheSourceGame.PrefabScatter
{
    [Serializable]
    public class ScatterPrefabEntry
    {
        [Tooltip("De prefab die gespawnd wordt. De pivot hoort onderaan te zitten.")]
        public GameObject prefab;

        [Tooltip("Relatieve kans dat deze prefab gekozen wordt. 2 = twee keer zo vaak als een prefab met 1.")]
        [Min(0f)]
        public float weight = 1f;

        [Tooltip("Willekeurige uniforme schaal tussen min (x) en max (y), vermenigvuldigd met de schaal van de prefab.")]
        public Vector2 scaleRange = new Vector2(0.9f, 1.1f);

        [Tooltip("0 = altijd recht omhoog, 1 = volledig mee met de helling van de grond.")]
        [Range(0f, 1f)]
        public float alignToNormal = 0f;

        [Tooltip("Verschuiving langs de omhoog-as. Negatief = iets in de grond zakken (handig tegen zwevende randen op hellingen).")]
        public float yOffset = 0f;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("The Source Game/Prefab Scatter Area")]
    public class PrefabScatterArea : MonoBehaviour
    {
        // ---------- Prefabs ----------
        public List<ScatterPrefabEntry> prefabs = new List<ScatterPrefabEntry>();

        // ---------- Ground ----------
        [Tooltip("Alleen colliders op deze layers tellen als grond.")]
        public LayerMask groundLayers = 1;

        [Tooltip("Maximale helling in graden. Steilere plekken worden overgeslagen.")]
        [Range(0f, 90f)]
        public float maxSlope = 45f;

        [Tooltip("Hoe ver boven het hoogste punt van het gebied de raycast start.")]
        [Min(0.1f)]
        public float raycastHeight = 50f;

        // ---------- Area ----------
        [Tooltip("Controlepunten van het gebied, lokaal ten opzichte van dit object.")]
        public List<Vector3> points = new List<Vector3>();

        [Tooltip("Maakt een vloeiende (organische) rand door de controlepunten.")]
        public bool smoothOutline = true;

        [Tooltip("Aantal tussenpunten per segment van de vloeiende rand.")]
        [Range(2, 16)]
        public int smoothSubdivisions = 8;

        // ---------- Distribution ----------
        [Tooltip("Maximaal aantal objecten per vierkante meter.")]
        [Min(0f)]
        public float density = 0.5f;

        [Tooltip("Minimale afstand tussen twee gespawnde objecten (meters). 0 = uit.")]
        [Min(0f)]
        public float minDistance = 0.5f;

        [Tooltip("Binnen deze afstand tot de rand loopt de dichtheid af naar 0. Geeft een natuurlijke rand. 0 = uit.")]
        [Min(0f)]
        public float edgeFalloff = 1f;

        [Tooltip("Gebruikt Perlin noise om plukken/open plekken te maken.")]
        public bool useNoise = false;

        [Tooltip("Grootte van de noise. Kleiner = grotere plukken.")]
        [Min(0.001f)]
        public float noiseScale = 0.15f;

        [Tooltip("Hoger = meer open plekken.")]
        [Range(0f, 1f)]
        public float noiseThreshold = 0.45f;

        // ---------- Transform ----------
        [Tooltip("Willekeurige rotatie rond de Y-as (0-360).")]
        public bool randomYRotation = true;

        // ---------- Output ----------
        [Tooltip("Seed voor de willekeur. Zelfde seed + zelfde instellingen = zelfde resultaat.")]
        public int seed = 12345;

        [Tooltip("Veiligheidslimiet voor het aantal objecten in dit gebied.")]
        [Min(1)]
        public int maxInstances = 5000;

        [Tooltip("Naam van het child-object waar alle gespawnde objecten onder komen.")]
        public string containerName = "Scattered";

        [Tooltip("Het child-object met alle gespawnde objecten. Wordt automatisch aangemaakt.")]
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

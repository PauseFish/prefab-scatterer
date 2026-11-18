// Claude.ai changed: v1.0.1 - translated all UI text, tooltips and log messages to English.
// Claude.ai changed: New custom inspector + scene tool for PrefabScatterArea: prefab list with drag & drop, area editing with handles, filled area + outline projected on the ground, and a live placement preview.
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace TheSourceGame.PrefabScatter
{
    [CustomEditor(typeof(PrefabScatterArea))]
    public class PrefabScatterAreaEditor : Editor
    {
        private const int _maxPreviewPlacements = 4000;
        private const float _groundLift = 0.03f;
        private const float _deletePickDistance = 15f;

        private static bool _editMode;
        private static bool _showPoints;

        // Serialized properties
        private SerializedProperty _prefabsProp;
        private SerializedProperty _groundLayersProp;
        private SerializedProperty _maxSlopeProp;
        private SerializedProperty _raycastHeightProp;
        private SerializedProperty _pointsProp;
        private SerializedProperty _smoothOutlineProp;
        private SerializedProperty _smoothSubdivisionsProp;
        private SerializedProperty _densityProp;
        private SerializedProperty _minDistanceProp;
        private SerializedProperty _edgeFalloffProp;
        private SerializedProperty _useNoiseProp;
        private SerializedProperty _noiseScaleProp;
        private SerializedProperty _noiseThresholdProp;
        private SerializedProperty _randomYRotationProp;
        private SerializedProperty _seedProp;
        private SerializedProperty _maxInstancesProp;
        private SerializedProperty _containerNameProp;
        private SerializedProperty _containerProp;
        private SerializedProperty _showPreviewProp;
        private SerializedProperty _areaColorProp;

        private ReorderableList _prefabList;

        // Cached scene visuals
        private readonly List<Vector3> _worldOutline = new List<Vector3>();
        private readonly List<Vector3> _projectedOutline = new List<Vector3>();
        private readonly List<int> _fillTriangles = new List<int>();
        private Vector3[] _outlineLine = new Vector3[0];
        private readonly Vector3[] _triangleBuffer = new Vector3[3];

        private readonly List<ScatterPlacement> _previewPlacements = new List<ScatterPlacement>();
        private readonly List<Vector3[]> _previewSegments = new List<Vector3[]>();
        private readonly List<Color> _previewColors = new List<Color>();

        private bool _outlineDirty = true;
        private bool _previewDirty = true;
        private Matrix4x4 _lastMatrix;
        private int _selectedIndex = -1;
        private float _areaSize;

        // ---------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------

        private void OnEnable()
        {
            _prefabsProp = serializedObject.FindProperty("prefabs");
            _groundLayersProp = serializedObject.FindProperty("groundLayers");
            _maxSlopeProp = serializedObject.FindProperty("maxSlope");
            _raycastHeightProp = serializedObject.FindProperty("raycastHeight");
            _pointsProp = serializedObject.FindProperty("points");
            _smoothOutlineProp = serializedObject.FindProperty("smoothOutline");
            _smoothSubdivisionsProp = serializedObject.FindProperty("smoothSubdivisions");
            _densityProp = serializedObject.FindProperty("density");
            _minDistanceProp = serializedObject.FindProperty("minDistance");
            _edgeFalloffProp = serializedObject.FindProperty("edgeFalloff");
            _useNoiseProp = serializedObject.FindProperty("useNoise");
            _noiseScaleProp = serializedObject.FindProperty("noiseScale");
            _noiseThresholdProp = serializedObject.FindProperty("noiseThreshold");
            _randomYRotationProp = serializedObject.FindProperty("randomYRotation");
            _seedProp = serializedObject.FindProperty("seed");
            _maxInstancesProp = serializedObject.FindProperty("maxInstances");
            _containerNameProp = serializedObject.FindProperty("containerName");
            _containerProp = serializedObject.FindProperty("container");
            _showPreviewProp = serializedObject.FindProperty("showPreview");
            _areaColorProp = serializedObject.FindProperty("areaColor");

            BuildPrefabList();

            Undo.undoRedoPerformed += OnUndoRedo;
            MarkDirty();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            _editMode = false;
            Tools.hidden = false;
        }

        private void OnUndoRedo()
        {
            MarkDirty();
            Repaint();
            SceneView.RepaintAll();
        }

        private void MarkDirty()
        {
            _outlineDirty = true;
            _previewDirty = true;
        }

        // ---------------------------------------------------------------
        // Inspector
        // ---------------------------------------------------------------

        public override void OnInspectorGUI()
        {
            PrefabScatterArea area = (PrefabScatterArea)target;
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            // Prefabs
            _prefabList.DoLayoutList();
            DrawPrefabDropArea();
            if (_prefabsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Add at least one prefab (or drag them into the box above).", MessageType.Info);
            }

            // Ground
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ground", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_groundLayersProp);
            EditorGUILayout.PropertyField(_maxSlopeProp);
            EditorGUILayout.PropertyField(_raycastHeightProp);
            if (_groundLayersProp.intValue == 0)
            {
                EditorGUILayout.HelpBox("No ground layer selected: nothing will be placed.", MessageType.Warning);
            }

            // Area
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Area", EditorStyles.boldLabel);
            DrawEditModeButton();
            EditorGUILayout.PropertyField(_smoothOutlineProp);
            using (new EditorGUI.DisabledScope(!_smoothOutlineProp.boolValue))
            {
                EditorGUILayout.PropertyField(_smoothSubdivisionsProp);
            }
            EditorGUILayout.LabelField("Surface", _areaSize.ToString("0.0") + " m²   (" + _pointsProp.arraySize + " points)");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset To Circle"))
            {
                Undo.RecordObject(area, "Reset Scatter Area");
                area.ResetPointsToCircle(5f, 8);
                serializedObject.Update();
                MarkDirty();
            }
            if (GUILayout.Button("Snap Points To Ground"))
            {
                SnapPointsToGround(area);
                serializedObject.Update();
            }
            EditorGUILayout.EndHorizontal();

            _showPoints = EditorGUILayout.Foldout(_showPoints, "Points (local)", true);
            if (_showPoints)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < _pointsProp.arraySize; i++)
                {
                    EditorGUILayout.PropertyField(_pointsProp.GetArrayElementAtIndex(i), new GUIContent("Point " + i));
                }
                EditorGUI.indentLevel--;
            }

            // Distribution
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Distribution", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_densityProp, new GUIContent("Density (per m²)", _densityProp.tooltip));
            EditorGUILayout.PropertyField(_minDistanceProp);
            EditorGUILayout.PropertyField(_edgeFalloffProp);
            EditorGUILayout.PropertyField(_useNoiseProp);
            using (new EditorGUI.DisabledScope(!_useNoiseProp.boolValue))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_noiseScaleProp);
                EditorGUILayout.PropertyField(_noiseThresholdProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(_randomYRotationProp);

            // Output
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(_seedProp);
            if (GUILayout.Button("Random", GUILayout.Width(60f)))
            {
                _seedProp.intValue = Random.Range(int.MinValue, int.MaxValue);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(_maxInstancesProp);
            EditorGUILayout.PropertyField(_containerNameProp);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(_containerProp);
            }

            // Visuals
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Visuals", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_showPreviewProp, new GUIContent("Show Preview", "Shows in the Scene view where objects will be placed, before you press Scatter."));
            EditorGUILayout.PropertyField(_areaColorProp);

            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty();
            }
            serializedObject.ApplyModifiedProperties();

            // Actions
            EditorGUILayout.Space();
            string previewInfo = area.showPreview ? "Preview: " + _previewPlacements.Count + " objects" : "Preview off";
            EditorGUILayout.HelpBox(previewInfo + "   |   Spawned: " + PrefabScatterGenerator.CountSpawned(area), MessageType.None);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scatter", GUILayout.Height(30f)))
            {
                DoScatter(area);
            }
            if (GUILayout.Button("Clear", GUILayout.Height(30f), GUILayout.Width(80f)))
            {
                PrefabScatterGenerator.Clear(area);
                MarkDirty();
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            if (GUI.changed)
            {
                SceneView.RepaintAll();
            }
        }

        private void BuildPrefabList()
        {
            _prefabList = new ReorderableList(serializedObject, _prefabsProp, true, true, true, true);
            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            _prefabList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Prefabs");
            _prefabList.elementHeight = line * 5f + spacing * 6f;

            _prefabList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                SerializedProperty element = _prefabsProp.GetArrayElementAtIndex(index);
                Rect r = new Rect(rect.x, rect.y + spacing, rect.width, line);

                EditorGUI.PropertyField(r, element.FindPropertyRelative("prefab"));
                r.y += line + spacing;
                EditorGUI.PropertyField(r, element.FindPropertyRelative("weight"));
                r.y += line + spacing;
                DrawScaleRange(r, element.FindPropertyRelative("scaleRange"));
                r.y += line + spacing;
                EditorGUI.PropertyField(r, element.FindPropertyRelative("alignToNormal"));
                r.y += line + spacing;
                EditorGUI.PropertyField(r, element.FindPropertyRelative("yOffset"));
            };

            _prefabList.onAddCallback = list =>
            {
                int index = _prefabsProp.arraySize;
                _prefabsProp.arraySize++;
                SetEntryDefaults(_prefabsProp.GetArrayElementAtIndex(index), null);
                MarkDirty();
            };

            _prefabList.onChangedCallback = list => MarkDirty();
        }

        private static void SetEntryDefaults(SerializedProperty element, GameObject prefab)
        {
            element.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            element.FindPropertyRelative("weight").floatValue = 1f;
            element.FindPropertyRelative("scaleRange").vector2Value = new Vector2(0.9f, 1.1f);
            element.FindPropertyRelative("alignToNormal").floatValue = 0f;
            element.FindPropertyRelative("yOffset").floatValue = 0f;
        }

        private static void DrawScaleRange(Rect rect, SerializedProperty scaleRange)
        {
            GUIContent label = new GUIContent("Scale (min / max)", "Random uniform scale between min and max.");
            Rect fieldRect = EditorGUI.PrefixLabel(rect, label);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            float half = (fieldRect.width - 4f) * 0.5f;
            Vector2 value = scaleRange.vector2Value;
            EditorGUI.BeginChangeCheck();
            value.x = EditorGUI.FloatField(new Rect(fieldRect.x, fieldRect.y, half, fieldRect.height), value.x);
            value.y = EditorGUI.FloatField(new Rect(fieldRect.x + half + 4f, fieldRect.y, half, fieldRect.height), value.y);
            if (EditorGUI.EndChangeCheck())
            {
                value.x = Mathf.Max(0.001f, value.x);
                value.y = Mathf.Max(value.x, value.y);
                scaleRange.vector2Value = value;
            }

            EditorGUI.indentLevel = indent;
        }

        // Claude.ai changed: Drop zone so you can drag a whole selection of prefabs from the Project window in one go.
        private void DrawPrefabDropArea()
        {
            Rect dropRect = GUILayoutUtility.GetRect(0f, 32f, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drag prefabs here", EditorStyles.helpBox);

            Event e = Event.current;
            if (!dropRect.Contains(e.mousePosition))
            {
                return;
            }

            if (e.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                e.Use();
            }
            else if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (Object dragged in DragAndDrop.objectReferences)
                {
                    GameObject go = dragged as GameObject;
                    if (go == null)
                    {
                        continue;
                    }
                    int index = _prefabsProp.arraySize;
                    _prefabsProp.arraySize++;
                    SetEntryDefaults(_prefabsProp.GetArrayElementAtIndex(index), go);
                }
                MarkDirty();
                e.Use();
            }
        }

        private void DrawEditModeButton()
        {
            Color previous = GUI.backgroundColor;
            if (_editMode)
            {
                GUI.backgroundColor = new Color(1f, 0.8f, 0.3f);
            }
            bool newEditMode = GUILayout.Toggle(_editMode, _editMode ? "Edit Area (active - press Esc to stop)" : "Edit Area", "Button", GUILayout.Height(24f));
            GUI.backgroundColor = previous;

            if (newEditMode != _editMode)
            {
                SetEditMode(newEditMode);
            }

            if (_editMode)
            {
                EditorGUILayout.HelpBox(
                    "Drag point: move across the ground\n" +
                    "Shift + click: add point\n" +
                    "Ctrl/Cmd + click on point: remove point\n" +
                    "Esc: stop editing",
                    MessageType.None);
            }
        }

        private void SetEditMode(bool value)
        {
            _editMode = value;
            Tools.hidden = value;
            _selectedIndex = -1;
            SceneView.RepaintAll();
            Repaint();
        }

        private void DoScatter(PrefabScatterArea area)
        {
            int count = PrefabScatterGenerator.Scatter(area);
            serializedObject.Update();
            MarkDirty();
            SceneView.RepaintAll();
            if (count == 0)
            {
                Debug.LogWarning("Prefab Scatter: nothing was placed. Check the prefabs, the ground layers and whether the ground has a collider.", area);
            }
        }

        private void SnapPointsToGround(PrefabScatterArea area)
        {
            Physics.SyncTransforms();
            Undo.RecordObject(area, "Snap Scatter Area To Ground");
            for (int i = 0; i < area.points.Count; i++)
            {
                Vector3 world = area.transform.TransformPoint(area.points[i]);
                RaycastHit hit;
                if (PrefabScatterGenerator.TryFindGround(area, world + Vector3.up * area.raycastHeight, area.raycastHeight * 2f, out hit))
                {
                    area.points[i] = area.transform.InverseTransformPoint(hit.point);
                }
            }
            MarkDirty();
        }

        // ---------------------------------------------------------------
        // Scene view
        // ---------------------------------------------------------------

        private void OnSceneGUI()
        {
            PrefabScatterArea area = (PrefabScatterArea)target;
            if (area.points == null)
            {
                return;
            }

            Event e = Event.current;

            Matrix4x4 matrix = area.transform.localToWorldMatrix;
            if (matrix != _lastMatrix)
            {
                _lastMatrix = matrix;
                MarkDirty();
            }

            if (_outlineDirty)
            {
                RebuildOutline(area);
            }

            // Rebuilding the preview costs many raycasts, so wait until no handle is being dragged.
            if (_previewDirty && GUIUtility.hotControl == 0 && e.type == EventType.Layout)
            {
                RebuildPreview(area);
                Repaint();
            }

            if (e.type == EventType.Repaint)
            {
                DrawArea(area);
                if (area.showPreview)
                {
                    DrawPreview();
                }
            }

            if (_editMode)
            {
                HandleEditing(area, e);
            }
            else if (e.type == EventType.Repaint)
            {
                DrawControlPointDots(area);
            }

            DrawSceneOverlay(area);
        }

        private void RebuildOutline(PrefabScatterArea area)
        {
            _outlineDirty = false;
            Physics.SyncTransforms();

            area.GetWorldOutline(_worldOutline);
            _areaSize = _worldOutline.Count >= 3 ? Mathf.Abs(ScatterMath.SignedAreaXZ(_worldOutline)) : 0f;

            // Project the outline on the ground so it follows the terrain.
            _projectedOutline.Clear();
            for (int i = 0; i < _worldOutline.Count; i++)
            {
                Vector3 p = _worldOutline[i];
                RaycastHit hit;
                if (PrefabScatterGenerator.TryFindGround(area, p + Vector3.up * area.raycastHeight, area.raycastHeight * 2f, out hit))
                {
                    p = hit.point;
                }
                _projectedOutline.Add(p + Vector3.up * _groundLift);
            }

            ScatterMath.TriangulateXZ(_projectedOutline, _fillTriangles);

            if (_projectedOutline.Count > 0)
            {
                _outlineLine = new Vector3[_projectedOutline.Count + 1];
                for (int i = 0; i < _projectedOutline.Count; i++)
                {
                    _outlineLine[i] = _projectedOutline[i];
                }
                _outlineLine[_projectedOutline.Count] = _projectedOutline[0];
            }
            else
            {
                _outlineLine = new Vector3[0];
            }
        }

        private void RebuildPreview(PrefabScatterArea area)
        {
            _previewDirty = false;
            _previewPlacements.Clear();
            _previewSegments.Clear();
            _previewColors.Clear();

            if (!area.showPreview)
            {
                return;
            }

            Physics.SyncTransforms();
            PrefabScatterGenerator.ComputePlacements(area, _previewPlacements);

            // Group line segments per prefab so each prefab gets its own colour.
            int groupCount = area.prefabs.Count + 1;
            List<Vector3>[] groups = new List<Vector3>[groupCount];
            int drawCount = Mathf.Min(_previewPlacements.Count, _maxPreviewPlacements);

            for (int i = 0; i < drawCount; i++)
            {
                ScatterPlacement placement = _previewPlacements[i];
                int group = placement.prefabIndex < 0 ? groupCount - 1 : placement.prefabIndex;
                if (groups[group] == null)
                {
                    groups[group] = new List<Vector3>();
                }

                Vector3 up = placement.rotation * Vector3.up;
                Vector3 right = placement.rotation * Vector3.right;
                Vector3 forward = placement.rotation * Vector3.forward;
                float height = 0.6f * placement.scale;
                float cross = 0.15f * placement.scale;

                List<Vector3> list = groups[group];
                list.Add(placement.position);
                list.Add(placement.position + up * height);
                list.Add(placement.position - right * cross);
                list.Add(placement.position + right * cross);
                list.Add(placement.position - forward * cross);
                list.Add(placement.position + forward * cross);
            }

            for (int g = 0; g < groupCount; g++)
            {
                if (groups[g] == null)
                {
                    continue;
                }
                _previewSegments.Add(groups[g].ToArray());
                _previewColors.Add(g == groupCount - 1 ? Color.gray : GetPrefabColor(g));
            }
        }

        private static Color GetPrefabColor(int index)
        {
            float hue = (0.12f + index * 0.618034f) % 1f;
            return Color.HSVToRGB(hue, 0.65f, 1f);
        }

        private void DrawArea(PrefabScatterArea area)
        {
            if (_projectedOutline.Count < 3)
            {
                return;
            }

            Color fill = area.areaColor;
            fill.a = _editMode ? 0.18f : 0.1f;
            Handles.color = fill;
            for (int i = 0; i + 2 < _fillTriangles.Count; i += 3)
            {
                _triangleBuffer[0] = _projectedOutline[_fillTriangles[i]];
                _triangleBuffer[1] = _projectedOutline[_fillTriangles[i + 1]];
                _triangleBuffer[2] = _projectedOutline[_fillTriangles[i + 2]];
                Handles.DrawAAConvexPolygon(_triangleBuffer);
            }

            Color line = area.areaColor;
            line.a = 1f;
            Handles.color = line;
            Handles.DrawAAPolyLine(_editMode ? 5f : 3f, _outlineLine);
        }

        private void DrawPreview()
        {
            for (int i = 0; i < _previewSegments.Count; i++)
            {
                Handles.color = _previewColors[i];
                Handles.DrawLines(_previewSegments[i]);
            }
        }

        private void DrawControlPointDots(PrefabScatterArea area)
        {
            Handles.color = area.areaColor;
            for (int i = 0; i < area.points.Count; i++)
            {
                Vector3 world = area.transform.TransformPoint(area.points[i]);
                float size = HandleUtility.GetHandleSize(world) * 0.04f;
                Handles.DotHandleCap(0, world, Quaternion.identity, size, EventType.Repaint);
            }
        }

        private void HandleEditing(PrefabScatterArea area, Event e)
        {
            // Claude.ai changed: Default control so clicking in empty space does not deselect the area while editing.
            int defaultControl = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(defaultControl);
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                SetEditMode(false);
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                if (EditorGUI.actionKey)
                {
                    TryRemovePoint(area, e.mousePosition);
                    e.Use();
                }
                else if (e.shift && HandleUtility.nearestControl == defaultControl)
                {
                    AddPoint(area, e.mousePosition);
                    e.Use();
                }
            }

            // Point handles
            for (int i = 0; i < area.points.Count; i++)
            {
                Vector3 world = area.transform.TransformPoint(area.points[i]);
                float size = HandleUtility.GetHandleSize(world) * 0.09f;
                Handles.color = i == _selectedIndex ? Color.yellow : Color.white;

                EditorGUI.BeginChangeCheck();
                Handles.FreeMoveHandle(world, Quaternion.identity, size, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 newWorld = MouseToGround(area, e.mousePosition, world.y);
                    Undo.RecordObject(area, "Move Scatter Area Point");
                    area.points[i] = area.transform.InverseTransformPoint(newWorld);
                    _selectedIndex = i;
                    MarkDirty();
                    Repaint();
                }
            }
        }

        private void AddPoint(PrefabScatterArea area, Vector2 mousePosition)
        {
            Vector3 world = MouseToGround(area, mousePosition, area.transform.position.y);
            Vector2 world2D = ScatterMath.ToXZ(world);

            // Insert in the segment closest to the click, so the shape stays intact.
            int insertIndex = area.points.Count;
            float best = float.MaxValue;
            for (int i = 0; i < area.points.Count; i++)
            {
                Vector2 a = ScatterMath.ToXZ(area.transform.TransformPoint(area.points[i]));
                Vector2 b = ScatterMath.ToXZ(area.transform.TransformPoint(area.points[(i + 1) % area.points.Count]));
                float distance = ScatterMath.DistancePointSegment(world2D, a, b);
                if (distance < best)
                {
                    best = distance;
                    insertIndex = i + 1;
                }
            }

            Undo.RecordObject(area, "Add Scatter Area Point");
            area.points.Insert(insertIndex, area.transform.InverseTransformPoint(world));
            _selectedIndex = insertIndex;
            MarkDirty();
            Repaint();
        }

        private void TryRemovePoint(PrefabScatterArea area, Vector2 mousePosition)
        {
            int closest = -1;
            float best = _deletePickDistance;
            for (int i = 0; i < area.points.Count; i++)
            {
                Vector2 guiPoint = HandleUtility.WorldToGUIPoint(area.transform.TransformPoint(area.points[i]));
                float distance = Vector2.Distance(guiPoint, mousePosition);
                if (distance < best)
                {
                    best = distance;
                    closest = i;
                }
            }

            if (closest < 0)
            {
                return;
            }
            if (area.points.Count <= 3)
            {
                Debug.LogWarning("Prefab Scatter: an area needs at least 3 points.", area);
                return;
            }

            Undo.RecordObject(area, "Remove Scatter Area Point");
            area.points.RemoveAt(closest);
            _selectedIndex = -1;
            MarkDirty();
            Repaint();
        }

        // Claude.ai changed: Mouse position -> point on the ground layers. Falls back to a horizontal plane when nothing is hit.
        private static Vector3 MouseToGround(PrefabScatterArea area, Vector2 mousePosition, float fallbackHeight)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 10000f, area.groundLayers, QueryTriggerInteraction.Ignore)
                && (area.container == null || !hit.transform.IsChildOf(area.container)))
            {
                return hit.point;
            }

            Plane plane = new Plane(Vector3.up, new Vector3(0f, fallbackHeight, 0f));
            float enter;
            if (plane.Raycast(ray, out enter))
            {
                return ray.GetPoint(enter);
            }
            return ray.GetPoint(10f);
        }

        private void DrawSceneOverlay(PrefabScatterArea area)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10f, 10f, 240f, 200f));
            GUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label(area.name, EditorStyles.boldLabel);

            bool newEditMode = GUILayout.Toggle(_editMode, _editMode ? "Stop Editing (Esc)" : "Edit Area", "Button");
            if (newEditMode != _editMode)
            {
                SetEditMode(newEditMode);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Scatter"))
            {
                DoScatter(area);
            }
            if (GUILayout.Button("Clear"))
            {
                PrefabScatterGenerator.Clear(area);
                MarkDirty();
            }
            if (GUILayout.Button("New Seed"))
            {
                Undo.RecordObject(area, "New Scatter Seed");
                area.seed = Random.Range(int.MinValue, int.MaxValue);
                MarkDirty();
                Repaint();
            }
            GUILayout.EndHorizontal();

            string info = _areaSize.ToString("0.0") + " m²";
            if (area.showPreview)
            {
                info += "  |  preview: " + _previewPlacements.Count;
            }
            info += "  |  spawned: " + PrefabScatterGenerator.CountSpawned(area);
            GUILayout.Label(info, EditorStyles.miniLabel);

            if (_editMode)
            {
                GUILayout.Label("Shift+click: add point\nCtrl/Cmd+click: remove point", EditorStyles.miniLabel);
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        // ---------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------

        // Claude.ai changed: GameObject menu entry that creates a new area at the scene view pivot, snapped to the ground.
        [MenuItem("GameObject/The Source Game/Prefab Scatter Area", false, 10)]
        private static void CreateScatterArea(MenuCommand menuCommand)
        {
            GameObject go = new GameObject("Prefab Scatter Area");
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (menuCommand.context == null && sceneView != null)
            {
                Vector3 position = sceneView.pivot;
                RaycastHit hit;
                if (Physics.Raycast(position + Vector3.up * 500f, Vector3.down, out hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
                {
                    position = hit.point;
                }
                go.transform.position = position;
            }

            PrefabScatterArea area = go.AddComponent<PrefabScatterArea>();
            if (area.points.Count < 3)
            {
                area.ResetPointsToCircle(5f, 8);
            }
            Undo.RegisterCreatedObjectUndo(go, "Create Prefab Scatter Area");
            Selection.activeObject = go;
        }
    }
}

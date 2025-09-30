using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class PDSEditor : EditorWindow
{
    [Header("Spawn Settings")]
    public GameObject prefab;
    public float radius = 10f;
    public float minDistance = 1.5f;
    public int maxAttemptsPerPoint = 30;
    public int seed = 12345;

    [Header("Placement")]
    public LayerMask placementMask = ~0; // everything by default
    public Vector3 upAxis = Vector3.up;  // normal for the disc
    public Transform parent;

    [Header("Input")]
    public KeyCode spawnModifier = KeyCode.LeftShift; // Hold + LMB to spawn

    private static readonly Color DiscColor = new Color(0.3f, 0.7f, 1f, 1f);
    private Vector3 lastHitPoint;
    private Vector3 lastHitNormal;

    [Header("Randomize")]
    public bool randomizeYaw = true;
    public Vector2 yawRange = new Vector2(0f, 360f);
    public bool randomizeScale = false;
    public bool perAxisScale = false;
    public Vector2 uniformScaleRange = new Vector2(1f, 1f);
    public Vector2 xScaleRange = new Vector2(1f, 1f);
    public Vector2 yScaleRange = new Vector2(1f, 1f);
    public Vector2 zScaleRange = new Vector2(1f, 1f);

    [Header("Preview")]
    public bool showPreview = true;
    public float previewPointSize = 0.05f;
    public Color previewColor = new Color(0.2f, 0.9f, 0.6f, 0.9f);
    public int maxPreviewSamples = 5000;
    private readonly List<Vector3> previewPositions = new List<Vector3>();


    [MenuItem("Tools/Poisson Spawner")]
    public static void Open()
    {
        var win = GetWindow<PDSEditor>("Poisson Spawner");
        win.minSize = new Vector2(340, 360);
        SceneView.duringSceneGui -= win.OnSceneGUI;
        SceneView.duringSceneGui += win.OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
            radius = EditorGUILayout.Slider("Area Radius", radius, 0.1f, 200f);
            minDistance = EditorGUILayout.Slider("Min Distance", minDistance, 0.05f, 50f);
            maxAttemptsPerPoint = EditorGUILayout.IntSlider("Attempts / Point", maxAttemptsPerPoint, 5, 100);
            seed = EditorGUILayout.IntSlider("Seed", seed, 1, 10000);
            parent = (Transform)EditorGUILayout.ObjectField("Parent", parent, typeof(Transform), true);
            placementMask = LayerMaskField("Placement Mask", placementMask);
            upAxis = EditorGUILayout.Vector3Field("Up Axis", upAxis);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Randomize", EditorStyles.boldLabel);
            randomizeYaw = EditorGUILayout.Toggle("Randomize Yaw", randomizeYaw);
            using (new EditorGUI.DisabledScope(!randomizeYaw))
            {
                yawRange = EditorGUILayout.Vector2Field("Yaw Range (deg)", yawRange);
            }

            randomizeScale = EditorGUILayout.Toggle("Randomize Scale", randomizeScale);
            using (new EditorGUI.DisabledScope(!randomizeScale))
            {
                perAxisScale = EditorGUILayout.Toggle("Per-Axis Scale", perAxisScale);
                if (!perAxisScale)
                {
                    uniformScaleRange = EditorGUILayout.Vector2Field("Uniform Scale [min,max]", uniformScaleRange);
                }
                else
                {
                    xScaleRange = EditorGUILayout.Vector2Field("X Scale [min,max]", xScaleRange);
                    yScaleRange = EditorGUILayout.Vector2Field("Y Scale [min,max]", yScaleRange);
                    zScaleRange = EditorGUILayout.Vector2Field("Z Scale [min,max]", zScaleRange);
                }
            }
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            showPreview = EditorGUILayout.Toggle("Show Preview", showPreview);
            previewPointSize = EditorGUILayout.Slider("Preview Point Size", previewPointSize, 0.005f, 0.5f);
            previewColor = EditorGUILayout.ColorField("Preview Color", previewColor);
            maxPreviewSamples = EditorGUILayout.IntSlider("Max Preview Samples", maxPreviewSamples, 100, 20000);
            spawnModifier = (KeyCode)EditorGUILayout.EnumPopup("Spawn Modifier", spawnModifier);

            EditorGUILayout.HelpBox("Live preview updates as you move the mouse or change settings.\nSpawn with modifier + Left Click.", MessageType.Info);
        }

        if (GUI.changed)
        {
            SceneView.RepaintAll();
        }
    }

    private void OnSceneGUI(SceneView view)
    {
        Event e = Event.current;

        // Raycast from mouse to scene
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (Physics.Raycast(ray, out var hit, 10000f, placementMask))
        {
            lastHitPoint = hit.point;
            lastHitNormal = hit.normal;

            // Draw the disc at the hit point
            Handles.color = DiscColor;
            Handles.DrawWireDisc(lastHitPoint, GetUpVector(lastHitNormal), radius);

            // Live regenerate preview
            RegeneratePreview(lastHitPoint, GetUpVector(lastHitNormal));

            // Render preview samples
            if (showPreview && previewPositions.Count > 0)
            {
                Handles.color = previewColor;
                foreach (var p in previewPositions)
                {
                    float size = HandleUtility.GetHandleSize(p) * previewPointSize;
                    Handles.SphereHandleCap(0, p, Quaternion.identity, size, EventType.Repaint);
                }
            }

            // HUD
            Handles.BeginGUI();
            var rect = new Rect(10, 10, 460, 40);
            GUI.Label(rect, $"Samples: {previewPositions.Count} | Radius: {radius:0.##} | Dist ≥ {minDistance:0.##} | Spawn: hold {spawnModifier} + LMB");
            Handles.EndGUI();

            // Spawn on click + modifier (ignore Alt so we don't fight camera orbit)
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && IsSpawnModifierHeld(e, spawnModifier))
            {
                SpawnFromPreview(GetUpVector(lastHitNormal));
                e.Use();
            }
        }

        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        view.Repaint();
    }

    private static bool IsSpawnModifierHeld(Event e, KeyCode key)
    {
        switch (key)
        {
            case KeyCode.LeftShift:
            case KeyCode.RightShift: return e.shift;
            case KeyCode.LeftControl:
            case KeyCode.RightControl: return e.control;
#if UNITY_EDITOR_OSX
            case KeyCode.LeftCommand:
            case KeyCode.RightCommand: return e.command;
#endif
            case KeyCode.LeftAlt:
            case KeyCode.RightAlt: return e.alt;
            default:
                // Fallback to Input for any other key
                return UnityEngine.Input.GetKey(key);
        }
    }

    private Vector3 GetUpVector(Vector3 hitNormal)
    {
        if (upAxis.sqrMagnitude > 0.0001f) return upAxis.normalized;
        if (hitNormal.sqrMagnitude > 0.0001f) return hitNormal.normalized;
        return Vector3.up;
    }

    private void RegeneratePreview(Vector3 center, Vector3 up)
    {
        // Build a local tangent basis for the disc plane
        Vector3 normal = up;
        Vector3 tangent = Vector3.Cross(normal, Vector3.up);
        if (tangent.sqrMagnitude < 1e-4f /* 1 - 10^4 -> 0.0001*/) tangent = Vector3.Cross(normal, Vector3.right);
        tangent.Normalize();
        Vector3 build = Vector3.Cross(normal, tangent);

        previewPositions.Clear();

        // Generate 2D samples then map to world, limited for performance
        var samples2D = PoissonDisc2DInCircle(radius, minDistance, maxAttemptsPerPoint, seed);
        int count = Mathf.Min(samples2D.Count, maxPreviewSamples);
        for (int i = 0; i < count; i++)
        {
            var point = samples2D[i];
            Vector3 world = center + tangent * point.x + build * point.y;

            // Reproject to surface for exact ground contact
            if (Physics.Raycast(world + normal * 10f, -normal, out var rayHit, 1000f, placementMask))
                world = rayHit.point;

            previewPositions.Add(world);
        }
    }

    private void SpawnFromPreview(Vector3 up)
    {
        if (prefab == null)
        {
            Debug.LogWarning("Poisson Spawner: Assign a Prefab first.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var worldPointPos in previewPositions)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null) instance = Instantiate(prefab); 
            Undo.RegisterCreatedObjectUndo(instance, "Spawn Poisson Prefab");

            // Rotation: align up to surface, then add yaw around that normal
            Quaternion alignUp = Quaternion.FromToRotation(Vector3.up, up);
            float yaw = 0f;
            if (randomizeYaw)
            {
                yaw = Random.Range(yawRange.x, yawRange.y);
            }
            Quaternion yawRot = Quaternion.AngleAxis(yaw, up);
            instance.transform.rotation = yawRot * alignUp;

            // Scale
            if (randomizeScale)
            {
                if (!perAxisScale)
                {
                    float s = Random.Range(uniformScaleRange.x, uniformScaleRange.y);
                    instance.transform.localScale = Vector3.one * s;
                }
                else
                {
                    float sx = Random.Range(xScaleRange.x, xScaleRange.y);
                    float sy = Random.Range(yScaleRange.x, yScaleRange.y);
                    float sz = Random.Range(zScaleRange.x, zScaleRange.y);
                    instance.transform.localScale = new Vector3(sx, sy, sz);
                }
            }

            instance.transform.position = worldPointPos;
            if (parent != null && instance.transform.parent != parent) instance.transform.SetParent(parent, true);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkAllScenesDirty();
    }

    private static List<Vector2> PoissonDisc2DInCircle(float radius, float minDist, int maxAttemts, int seed)
    {
        var rng = new System.Random(seed);
        float cellSize = minDist / Mathf.Sqrt(2f);

        // Grid bounds in 2D covering the circle's AABB
        int gridW = Mathf.CeilToInt((radius * 2f) / cellSize);
        int gridH = Mathf.CeilToInt((radius * 2f) / cellSize);
        var grid = new int[gridW, gridH];
        for (int y = 0; y < gridH; y++)
            for (int x = 0; x < gridW; x++)
                grid[x, y] = -1;

        var samples = new List<Vector2>();
        var active = new List<int>();

        // Convert world pos to grid index
        Vector2 Origin = new Vector2(-radius, -radius);
        System.Func<Vector2, Vector2Int> worldToGridIndex = (Vector2 p) =>
        {
            Vector2 rel = p - Origin; // move to [0, 2R]
            int ix = Mathf.Clamp((int)(rel.x / cellSize), 0, gridW - 1);
            int iy = Mathf.Clamp((int)(rel.y / cellSize), 0, gridH - 1);
            return new Vector2Int(ix, iy);
        };

        // Start with a random point in the circle
        Vector2 first = RandomPointInCircle(rng, radius);
        samples.Add(first);
        active.Add(0);
        var firstIndex = worldToGridIndex(first);
        grid[firstIndex.x, firstIndex.y] = 0;

        // Grow set
        while (active.Count > 0)
        {
            int activeIndex = active[rng.Next(active.Count)];
            Vector2 activeSample = samples[activeIndex];
            bool found = false;

            for (int attempt = 0; attempt < maxAttemts; attempt++)
            {
                Vector2 cand = activeSample + RandomAnnulus(rng, minDist, 2f * minDist);
                if (cand.sqrMagnitude > radius * radius) continue; // outside circle

                var gridIndex = worldToGridIndex(cand);
                bool valid = true;

                // Check neighbors (within 2 cells in each direction)
                for (int y = Mathf.Max(0, gridIndex.y - 2); y <= Mathf.Min(gridH - 1, gridIndex.y + 2) && valid; y++)
                    for (int x = Mathf.Max(0, gridIndex.x - 2); x <= Mathf.Min(gridW - 1, gridIndex.x + 2) && valid; x++)
                    {
                        int sIdx = grid[x, y];
                        if (sIdx != -1)
                        {
                            float d2 = (samples[sIdx] - cand).sqrMagnitude;
                            if (d2 < minDist * minDist) valid = false;
                        }
                    }

                if (valid)
                {
                    samples.Add(cand);
                    active.Add(samples.Count - 1);
                    grid[gridIndex.x, gridIndex.y] = samples.Count - 1;
                    found = true;
                    break;
                }
            }

            if (!found)
                active.RemoveAt(active.Count - 1);
        }

        return samples;
    }

    private static Vector2 RandomPointInCircle(System.Random rng, float r)
    {
        double u = rng.NextDouble();
        double ang = rng.NextDouble() * Mathf.PI * 2.0;
        float rr = Mathf.Sqrt((float)u) * r;
        return new Vector2(Mathf.Cos((float)ang), Mathf.Sin((float)ang)) * rr;
    }

    private static Vector2 RandomAnnulus(System.Random rng, float rMin, float rMax)
    {
        double u = rng.NextDouble();
        double ang = rng.NextDouble() * Mathf.PI * 2.0;
        float rr = Mathf.Sqrt(Mathf.Lerp(rMin * rMin, rMax * rMax, (float)u));
        return new Vector2(Mathf.Cos((float)ang), Mathf.Sin((float)ang)) * rr;
    }

    // Unity has no built-in LayerMask field in Editor GUI outside inspectors
    // Custom LayerMask field
    private static LayerMask LayerMaskField(string label, LayerMask mask)
    {
        var layers = InternalEditorUtilityLayers();
        int maskVal = 0;
        for (int i = 0; i < 32; i++)
            if (((1 << i) & mask.value) != 0)
                maskVal |= (1 << System.Array.IndexOf(layers.indices, i));

        maskVal = EditorGUILayout.MaskField(label, maskVal, layers.names);

        int newMask = 0;
        for (int i = 0; i < layers.names.Length; i++)
            if ((maskVal & (1 << i)) != 0)
                newMask |= (1 << layers.indices[i]);

        mask.value = newMask;
        return mask;
    }

    private struct LayerData { public string[] names; public int[] indices; }
    private static LayerData InternalEditorUtilityLayers()
    {
        var names = new List<string>();
        var indices = new List<int>();
        for (int i = 0; i < 32; i++)
        {
            string n = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(n))
            {
                names.Add(n);
                indices.Add(i);
            }
        }
        return new LayerData { names = names.ToArray(), indices = indices.ToArray() };
    }


}

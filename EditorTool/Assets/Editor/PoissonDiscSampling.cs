using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PoissonDiscSampling : EditorWindow
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

    public static List<Vector2> PoissonDiscCircle(float radius, float maxPossibleTrys, Vector2 sampleSize, int seed)
    {
        float cellSize = radius / Mathf.Sqrt(2f);

        int[,] grid = new int[Mathf.CeilToInt(sampleSize.x /cellSize), Mathf.CeilToInt(sampleSize.y/cellSize)];

        List<Vector2> points = new List<Vector2>();


        return points;
    }
}

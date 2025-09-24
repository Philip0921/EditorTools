using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public static class PoissonDiscSampling
{
 
    public static List<Vector2> CreatePoints(float radius, float maxPossibleTrys, Vector2 sampleSize)
    {
        float cellSize = radius / Mathf.Sqrt(2);

        int[,] grid = new int[Mathf.CeilToInt(sampleSize.x /cellSize), Mathf.CeilToInt(sampleSize.y/cellSize)];

        List<Vector2> points = new List<Vector2>();


        return points;
    }
}

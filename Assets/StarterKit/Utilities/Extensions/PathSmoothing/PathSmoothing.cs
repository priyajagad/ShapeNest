using System.Collections.Generic;
using UnityEngine;

public static class PathSmoothing
{
    public static List<Vector3> SmoothPath(Vector3[] points, int numPointsBetween = 10)
    {
        List<Vector3> smoothedPoints = new List<Vector3>();

        if (points.Length < 2)
        {
            // Not enough points to smooth
            return new List<Vector3>(points);
        }

        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 p0 = i == 0 ? points[i] : points[i - 1];
            Vector3 p1 = points[i];
            Vector3 p2 = points[i + 1];
            Vector3 p3 = (i + 2) < points.Length ? points[i + 2] : points[i + 1];

            for (int j = 0; j < numPointsBetween; j++)
            {
                float t = j / (float)numPointsBetween;
                Vector3 newPoint = SplineUtils.CatmullRom(p0, p1, p2, p3, t);
                smoothedPoints.Add(newPoint);
            }
        }

        // Ensure the last point is added
        smoothedPoints.Add(points[points.Length - 1]);

        return smoothedPoints;
    }
}

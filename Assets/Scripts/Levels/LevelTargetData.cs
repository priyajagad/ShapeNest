using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LevelTargetData
{
    public ShapeType shapeType;
    public Vector2Int gridPosition;

    [Tooltip("Local cells relative to gridPosition. Empty means a single cell at (0,0) using Shape Type.")]
    public List<ShapeCellData> cells = new List<ShapeCellData>();

    [Tooltip("Simple = cells only. ShapeInShape also requires Outer Shape to match.")]
    public PieceComposition composition = PieceComposition.Simple;

    [Tooltip("Outer nest identity for ShapeInShape. Ignored for Simple targets.")]
    public ShapeType outerShape = ShapeType.Square;

    [Tooltip("Reserved for future rotation. 0 = authored orientation. Not applied at runtime.")]
    public int orientationSteps;

    public LevelTargetData Clone()
    {
        return new LevelTargetData
        {
            shapeType = shapeType,
            gridPosition = gridPosition,
            cells = ShapeLayout.Clone(cells, shapeType),
            composition = composition,
            outerShape = outerShape,
            orientationSteps = orientationSteps
        };
    }
}

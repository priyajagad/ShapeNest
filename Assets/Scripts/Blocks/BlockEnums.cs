using UnityEngine;

public enum ShapeType
{
    Square,
    Circle,
    Triangle
}

public enum MoveDirection
{
    Any,
    Up,
    Down,
    Left,
    Right
}

/// <summary>
/// How a piece or nest is composed. Simple uses only <see cref="ShapeCellData"/> cells.
/// ShapeInShape also requires a matching outer shape plus the inner cell configuration.
/// </summary>
public enum PieceComposition
{
    Simple = 0,
    ShapeInShape = 1
}

/// <summary>
/// Presentation-only sprite lookup. Gameplay identity remains ShapeType.
/// </summary>
public static class ShapeVisuals
{
    public static Sprite SpriteFor(ShapeType shapeType, Sprite square, Sprite circle, Sprite triangle)
    {
        switch (shapeType)
        {
            case ShapeType.Circle:
                return circle;
            case ShapeType.Triangle:
                return triangle;
            default:
                return square;
        }
    }

    public static Sprite First(Sprite preferred, Sprite fallback)
    {
        return preferred != null ? preferred : fallback;
    }
}

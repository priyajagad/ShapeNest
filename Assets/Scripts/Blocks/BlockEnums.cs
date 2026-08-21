using UnityEngine;

public enum ShapeType
{
    Square = 0,
    Circle = 1,
    Triangle = 2,
    Diamond = 3,
    Hexagon = 4,
    Star = 5
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
    public static Sprite SpriteFor(
        ShapeType shapeType,
        Sprite square,
        Sprite circle,
        Sprite triangle,
        Sprite diamond = null,
        Sprite hexagon = null,
        Sprite star = null)
    {
        switch (shapeType)
        {
            case ShapeType.Circle:
                return First(circle, square);
            case ShapeType.Triangle:
                return First(triangle, square);
            case ShapeType.Diamond:
                return First(diamond, square);
            case ShapeType.Hexagon:
                return First(hexagon, square);
            case ShapeType.Star:
                return First(star, square);
            default:
                return square;
        }
    }

    public static Sprite First(Sprite preferred, Sprite fallback)
    {
        return preferred != null ? preferred : fallback;
    }
}

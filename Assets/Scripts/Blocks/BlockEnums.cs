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
}

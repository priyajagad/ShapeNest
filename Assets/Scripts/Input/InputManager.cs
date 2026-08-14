using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Grid-cell drag input. Selects one Block, locks a cardinal direction, and
/// asks BlockMover to move only as far as the drag requests.
/// </summary>
public class InputManager : MonoBehaviour
{
    [SerializeField]
    [Min(1f)]
    [Tooltip("Screen-pixel distance before a drag direction is locked.")]
    private float dragThresholdPixels = 30f;

    [SerializeField]
    private bool debugDrag;

    [SerializeField]
    private LevelManager levelManager;

    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private PointerEventData pointerEventData;

    private Block pressedBlock;
    private BlockMover pressedMover;
    private BoardManager cachedBoard;
    private RectTransform cachedBoardRect;
    private Camera cachedEventCamera;
    private Vector2 cachedPressLocal;
    private float cachedAxisSize;
    private Vector2 pressScreenPosition;
    private Vector2Int pressGridPosition;
    private bool isPressing;
    private bool directionLocked;
    private Vector2Int lockedDirection;

    private void Update()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null)
        {
            return;
        }

        if (levelManager != null && !levelManager.IsGameplayInputAllowed)
        {
            if (isPressing)
            {
                if (directionLocked && pressedMover != null)
                {
                    pressedMover.EndDrag();
                }

                ClearPress();
            }

            return;
        }

        Vector2 screenPosition = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame)
        {
            OnPointerPressed(screenPosition);
        }

        if (!isPressing)
        {
            return;
        }

        OnPointerDragged(screenPosition);

        if (pointer.press.wasReleasedThisFrame)
        {
            OnPointerReleased();
        }
    }

    private void OnPointerPressed(Vector2 screenPosition)
    {
        pressedBlock = FindBlockAt(screenPosition);
        pressScreenPosition = screenPosition;
        isPressing = pressedBlock != null;
        directionLocked = false;
        lockedDirection = Vector2Int.zero;
        pressedMover = null;
        cachedBoard = null;
        cachedBoardRect = null;
        cachedEventCamera = null;
        cachedAxisSize = 0f;

        if (pressedBlock == null)
        {
            return;
        }

        if (pressedBlock.IsSettled)
        {
            if (debugDrag)
            {
                LogDrag("Ignored settled block");
            }

            ClearPress();
            return;
        }

        pressedMover = pressedBlock.GetComponent<BlockMover>();
        if (pressedMover == null || pressedMover.IsMoving || pressedMover.IsDragging)
        {
            if (debugDrag)
            {
                LogDrag("Ignored press: no mover or already moving");
            }

            ClearPress();
            return;
        }

        pressGridPosition = pressedBlock.GridPosition;
        CacheBoardForPress();
        pressedBlock.ShowDragSelection();
        if (debugDrag)
        {
            LogDrag("Selected block");
        }
    }

    private void OnPointerDragged(Vector2 screenPosition)
    {
        if (pressedBlock == null || pressedMover == null)
        {
            return;
        }

        Vector2 delta = screenPosition - pressScreenPosition;

        if (!directionLocked)
        {
            if (delta.sqrMagnitude < dragThresholdPixels * dragThresholdPixels)
            {
                return;
            }

            Vector2Int direction = GetCardinalDirection(delta);
            if (!pressedMover.IsDirectionAllowed(direction))
            {
                return;
            }

            if (!pressedMover.TryBeginDrag(direction))
            {
                ClearPress();
                return;
            }

            directionLocked = true;
            lockedDirection = direction;
            CacheAxisSize();
        }

        Vector2Int requested = ComputeRequestedCell(screenPosition);
        pressedMover.SetDragRequest(requested);
    }

    private void OnPointerReleased()
    {
        if (directionLocked && pressedMover != null)
        {
            pressedMover.EndDrag();
        }

        ClearPress();
    }

    private Vector2Int ComputeRequestedCell(Vector2 screenPosition)
    {
        Vector2Int origin = pressGridPosition;
        if (cachedBoardRect == null || cachedAxisSize <= 0.01f)
        {
            return origin;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                cachedBoardRect,
                screenPosition,
                cachedEventCamera,
                out Vector2 currentLocal))
        {
            return origin;
        }

        Vector2 localDelta = currentLocal - cachedPressLocal;
        float along = (localDelta.x * lockedDirection.x) + (localDelta.y * lockedDirection.y);
        int steps = Mathf.RoundToInt(along / cachedAxisSize);
        if (steps < 0)
        {
            steps = 0;
        }

        return origin + (lockedDirection * steps);
    }

    private void CacheBoardForPress()
    {
        cachedBoard = pressedBlock.Board;
        if (cachedBoard == null)
        {
            return;
        }

        cachedBoardRect = (RectTransform)cachedBoard.transform;
        Canvas canvas = cachedBoard.GetComponentInParent<Canvas>();
        cachedEventCamera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cachedEventCamera = canvas.worldCamera;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            cachedBoardRect,
            pressScreenPosition,
            cachedEventCamera,
            out cachedPressLocal);
    }

    private void CacheAxisSize()
    {
        if (cachedBoard == null)
        {
            cachedAxisSize = 0f;
            return;
        }

        Vector2 cellSize = cachedBoard.VisualCellSize;
        cachedAxisSize = lockedDirection.x != 0 ? cellSize.x : cellSize.y;
    }

    private static Vector2Int GetCardinalDirection(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            return delta.x > 0f ? Vector2Int.right : Vector2Int.left;
        }

        return delta.y > 0f ? Vector2Int.up : Vector2Int.down;
    }

    private Block FindBlockAt(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return null;
        }

        if (pointerEventData == null)
        {
            pointerEventData = new PointerEventData(eventSystem);
        }

        pointerEventData.Reset();
        pointerEventData.position = screenPosition;

        raycastResults.Clear();
        eventSystem.RaycastAll(pointerEventData, raycastResults);

        for (int i = 0; i < raycastResults.Count; i++)
        {
            Block block = raycastResults[i].gameObject.GetComponentInParent<Block>();
            if (block != null)
            {
                return block;
            }
        }

        return null;
    }

    private void ClearPress()
    {
        if (pressedBlock != null)
        {
            pressedBlock.HideDragSelection();
        }

        isPressing = false;
        pressedBlock = null;
        pressedMover = null;
        cachedBoard = null;
        cachedBoardRect = null;
        cachedEventCamera = null;
        cachedAxisSize = 0f;
        directionLocked = false;
        lockedDirection = Vector2Int.zero;
    }

    private void LogDrag(string message)
    {
        Debug.Log($"InputManager: {message}", this);
    }
}

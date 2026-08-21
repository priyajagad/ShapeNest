using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class LevelEditorWindow : EditorWindow
{
    private enum EditMode
    {
        Block,
        Target,
        Shutter,
        Eraser
    }

    private const string LevelsFolder = "Assets/Levels";
    private const float EditorGameplayAreaWidth = 440f;
    private const float EditorGameplayAreaHeight = 440f;
    private const float EditorGameplayAreaPadding = 12f;

    private LevelEditorSession session;
    private EditMode editMode = EditMode.Block;
    private ShapeType selectedShape = ShapeType.Square;
    private MoveDirection selectedDirection = MoveDirection.Any;
    private PieceComposition selectedComposition = PieceComposition.Simple;
    private ShapeType selectedOuterShape = ShapeType.Square;
    private bool extendSelectedFootprint;
    private int selectedShutterDurability = 1;
    private LevelShutterData selectedShutter;
    private Vector2Int? selectedCell;
    private Vector2 scrollPosition;
    private LevelEditorValidationResult lastValidation;
    private bool showValidationDetails;

    [MenuItem("Tools/Shape Nest/Level Editor")]
    public static void Open()
    {
        LevelEditorWindow window = GetWindow<LevelEditorWindow>("Level Editor");
        window.minSize = new Vector2(420f, 640f);
        window.Show();
    }

    private void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
        EnsureSession();
        if (string.IsNullOrEmpty(session.levelName) && session.blocks.Count == 0 && session.targets.Count == 0)
        {
            BeginNewLevel(markDirty: false);
        }
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        if (session != null)
        {
            DestroyImmediate(session);
            session = null;
        }
    }

    private void OnUndoRedo()
    {
        Repaint();
    }

    private void EnsureSession()
    {
        if (session != null)
        {
            return;
        }

        session = CreateInstance<LevelEditorSession>();
        session.hideFlags = HideFlags.HideAndDontSave;
        GetDefaultBoardSize(out session.columns, out session.rows);
        session.levelName = GetNextLevelName();
    }

    private void OnGUI()
    {
        EnsureSession();
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawHeader();
        DrawLevelSettings();
        DrawToolbarButtons();
        DrawDatabaseSection();
        DrawTools();
        DrawGrid();
        DrawSelectionInfo();
        DrawValidationPanel();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("SHAPE NEST LEVEL EDITOR", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        if (session.isDirty)
        {
            EditorGUILayout.HelpBox("Unsaved Changes", MessageType.Warning);
        }
        else if (session.sourceAsset != null)
        {
            EditorGUILayout.HelpBox($"Loaded: {session.sourceAsset.name}", MessageType.Info);
        }
    }

    private void DrawLevelSettings()
    {
        EditorGUI.BeginChangeCheck();
        session.levelName = EditorGUILayout.TextField("Level Name", session.levelName);
        session.columns = Mathf.Max(1, EditorGUILayout.IntField("Columns", session.columns));
        session.rows = Mathf.Max(1, EditorGUILayout.IntField("Rows", session.rows));
        if (EditorGUI.EndChangeCheck())
        {
            MarkDirty();
        }
    }

    private void DrawToolbarButtons()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("New Level"))
        {
            RequestNewLevel();
        }

        if (GUILayout.Button("Load Level"))
        {
            RequestLoadLevel();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Level"))
        {
            SaveLevel();
        }

        if (GUILayout.Button("Validate"))
        {
            lastValidation = ValidateCurrent();
            showValidationDetails = true;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawDatabaseSection()
    {
        EditorGUILayout.Space(8f);
        LevelDatabase database = FindLevelDatabase();
        if (database == null)
        {
            EditorGUILayout.HelpBox("No LevelDatabase found.", MessageType.Warning);
            if (GUILayout.Button("Create LevelDatabase"))
            {
                CreateLevelDatabase();
            }
        }
        else
        {
            EditorGUILayout.LabelField("Level Database", AssetDatabase.GetAssetPath(database));
            EditorGUILayout.LabelField("Levels in database", database.Count.ToString());
        }
    }

    private void DrawTools()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("TOOLS", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("Mode");
        EditorGUILayout.BeginHorizontal();
        DrawModeToggle(EditMode.Block, "Block");
        DrawModeToggle(EditMode.Target, "Target");
        DrawModeToggle(EditMode.Shutter, "Shutter");
        DrawModeToggle(EditMode.Eraser, "Eraser");
        EditorGUILayout.EndHorizontal();

        if (editMode == EditMode.Shutter)
        {
            EditorGUI.BeginChangeCheck();
            selectedShutterDurability = Mathf.Max(1, EditorGUILayout.IntField("Shutter Count", selectedShutterDurability));
            if (EditorGUI.EndChangeCheck() && selectedShutter != null)
            {
                selectedShutter.durability = selectedShutterDurability;
                session.isDirty = true;
            }

            if (selectedShutter != null)
            {
                EditorGUILayout.LabelField($"Active Shutter: {selectedShutter.cells.Count} cell(s)");
                if (GUILayout.Button("Deselect (Start New Shutter)"))
                {
                    selectedShutter = null;
                }
            }

            EditorGUILayout.HelpBox(
                "Click cells on the grid to paint a shutter region. Consecutive clicks automatically expand the active shutter. Switch tools or click 'Deselect' to start a new shutter.",
                MessageType.None);
            return;
        }

        if (editMode != EditMode.Eraser)
        {
            selectedShape = (ShapeType)EditorGUILayout.EnumPopup("Shape", selectedShape);
        }

        if (editMode == EditMode.Block)
        {
            selectedDirection = (MoveDirection)EditorGUILayout.EnumPopup("Move Direction", selectedDirection);
        }

        if (editMode != EditMode.Eraser)
        {
            selectedComposition = (PieceComposition)EditorGUILayout.EnumPopup("Composition", selectedComposition);
            if (selectedComposition == PieceComposition.ShapeInShape)
            {
                selectedOuterShape = (ShapeType)EditorGUILayout.EnumPopup("Outer Shape", selectedOuterShape);
            }

            extendSelectedFootprint = EditorGUILayout.Toggle("Add cell to selected piece", extendSelectedFootprint);
            EditorGUILayout.HelpBox(
                "Place an anchor first. Enable Add cell to selected piece, then click extra cells to grow the footprint. Local (0,0) is the first cell.",
                MessageType.None);
        }
    }

    private void DrawModeToggle(EditMode mode, string label)
    {
        bool selected = editMode == mode;
        if (GUILayout.Toggle(selected, label, EditorStyles.miniButton) && !selected)
        {
            editMode = mode;
            selectedCell = null;
            selectedShutter = null;
        }
    }

    private void DrawGrid()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("GRID", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Cell (0,0) is bottom-left, matching the runtime board.");

        float cellSize = ComputeEditorCellSize();
        float width = session.columns * cellSize;
        float height = session.rows * cellSize;
        Rect gridRect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));

        for (int y = session.rows - 1; y >= 0; y--)
        {
            for (int x = 0; x < session.columns; x++)
            {
                var cell = new Vector2Int(x, y);
                Rect cellRect = new Rect(
                    gridRect.x + x * cellSize,
                    gridRect.y + (session.rows - 1 - y) * cellSize,
                    cellSize - 2f,
                    cellSize - 2f);

                DrawCell(cellRect, cell);

                if (Event.current.type == EventType.MouseDown
                    && Event.current.button == 0
                    && cellRect.Contains(Event.current.mousePosition))
                {
                    HandleCellClick(cell);
                    Event.current.Use();
                    GUI.changed = true;
                    Repaint();
                }
            }
        }
    }

    private void DrawCell(Rect rect, Vector2Int cell)
    {
        LevelBlockData block = session.FindBlock(cell);
        LevelTargetData target = session.FindTarget(cell);
        LevelShutterData shutter = session.FindShutter(cell);
        bool selected = selectedCell.HasValue && selectedCell.Value == cell;

        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.2f));
        if (selected)
        {
            EditorGUI.DrawRect(rect, new Color(0.28f, 0.36f, 0.22f));
        }

        Handles.BeginGUI();
        Handles.color = new Color(0.35f, 0.35f, 0.38f);
        Handles.DrawSolidRectangleWithOutline(rect, Color.clear, new Color(0.45f, 0.45f, 0.48f));
        Handles.EndGUI();

        if (target != null)
        {
            ShapeType nestShape = ShapeLayout.ShapeAtLocal(target.cells, target.shapeType, cell - target.gridPosition);
            Rect nestRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
            DrawShape(nestRect, nestShape, ShapeColor(nestShape) * 0.45f, filled: false);
        }

        if (block != null)
        {
            ShapeType pieceShape = ShapeLayout.ShapeAtLocal(block.cells, block.shapeType, cell - block.gridPosition);
            Rect pieceRect = new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 22f);
            DrawShape(pieceRect, pieceShape, ShapeColor(pieceShape), filled: true);
            if (block.gridPosition == cell)
            {
                string arrow = DirectionGlyph(block.moveDirection);
                if (!string.IsNullOrEmpty(arrow))
                {
                    GUI.Label(new Rect(rect.x, rect.yMax - 18f, rect.width, 16f), arrow, CenteredMiniLabel());
                }
            }
        }
        else if (target != null)
        {
            ShapeType nestShape = ShapeLayout.ShapeAtLocal(target.cells, target.shapeType, cell - target.gridPosition);
            GUI.Label(rect, ShapeGlyph(nestShape, outlined: true), CenteredMiniLabel());
        }

        if (shutter != null)
        {
            EditorGUI.DrawRect(rect, new Color(0.22f, 0.12f, 0.32f, 0.88f));
            Handles.BeginGUI();
            Handles.color = new Color(0.58f, 0.42f, 0.76f, 0.95f);
            Handles.DrawSolidRectangleWithOutline(rect, Color.clear, Handles.color);
            Handles.EndGUI();
            if (shutter.cells != null && shutter.cells.Count > 0 && shutter.cells[0] == cell)
            {
                GUI.Label(rect, $"S {shutter.durability}", CenteredMiniLabel());
            }
        }

        GUI.Label(
            new Rect(rect.x + 2f, rect.y + 1f, rect.width, 14f),
            $"{cell.x},{cell.y}",
            EditorStyles.miniLabel);
    }

    private void DrawShape(Rect rect, ShapeType shape, Color color, bool filled)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.Label(rect, ShapeGlyph(shape, outlined: !filled), CenteredLabel());
        GUI.color = previous;
    }

    private void DrawSelectionInfo()
    {
        if (!selectedCell.HasValue)
        {
            return;
        }

        Vector2Int cell = selectedCell.Value;
        LevelBlockData block = session.FindBlock(cell);
        LevelTargetData target = session.FindTarget(cell);
        LevelShutterData shutter = session.FindShutter(cell);
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField($"Selected cell: ({cell.x},{cell.y})");
        EditorGUI.BeginChangeCheck();
        if (shutter != null)
        {
            selectedShutter = shutter;
            selectedShutterDurability = Mathf.Max(1, EditorGUILayout.IntField("Shutter Count", shutter.durability));
            shutter.durability = selectedShutterDurability;
            EditorGUILayout.LabelField($"Shutter cells: {shutter.cells.Count}");
            EditorGUILayout.LabelField("Shutter may cover any existing blocks/targets.");
        }

        if (block != null)
        {
            EditorGUILayout.LabelField($"Block anchor: ({block.gridPosition.x},{block.gridPosition.y})");
            block.moveDirection = (MoveDirection)EditorGUILayout.EnumPopup("Block direction", block.moveDirection);
            block.composition = (PieceComposition)EditorGUILayout.EnumPopup("Block composition", block.composition);
            if (block.composition == PieceComposition.ShapeInShape)
            {
                block.outerShape = (ShapeType)EditorGUILayout.EnumPopup("Block outer shape", block.outerShape);
            }

            if (editMode == EditMode.Block)
            {
                block.hasIce = EditorGUILayout.Toggle("Has Ice", block.hasIce);
                block.iceDurability = Mathf.Max(1, EditorGUILayout.IntField("Ice Durability", block.iceDurability));
            }

            EditorGUILayout.LabelField($"Block cells: {DescribeCells(block.cells, block.shapeType)}");
        }

        if (target != null)
        {
            EditorGUILayout.LabelField($"Target anchor: ({target.gridPosition.x},{target.gridPosition.y})");
            target.composition = (PieceComposition)EditorGUILayout.EnumPopup("Target composition", target.composition);
            if (target.composition == PieceComposition.ShapeInShape)
            {
                target.outerShape = (ShapeType)EditorGUILayout.EnumPopup("Target outer shape", target.outerShape);
            }

            EditorGUILayout.LabelField($"Target cells: {DescribeCells(target.cells, target.shapeType)}");
        }

        if (EditorGUI.EndChangeCheck())
        {
            MarkDirty();
        }

        if (block == null && target == null)
        {
            EditorGUILayout.LabelField("Empty cell");
        }
    }

    private void DrawValidationPanel()
    {
        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("VALIDATION", EditorStyles.boldLabel);

        LevelEditorValidationResult result = ValidateCurrent();
        lastValidation = result;
        if (result.IsValid)
        {
            EditorGUILayout.HelpBox("LEVEL VALID ✓", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("LEVEL INVALID ✗", MessageType.Error);
        }

        DrawCheck("Level name", result.NameValid);
        DrawCheck("Block count", result.BlockCountValid);
        DrawCheck("Target count", result.TargetCountValid);
        DrawCheck("Matchable layer counts match", result.CountsMatch);
        DrawCheck("Positions", result.PositionsValid);
        DrawCheck("Shape matching", result.ShapesMatch);
        DrawCheck("Shutters", result.ShuttersValid);

        if (showValidationDetails || !result.IsValid)
        {
            for (int i = 0; i < result.Errors.Count; i++)
            {
                EditorGUILayout.LabelField("✗ " + result.Errors[i]);
            }

            for (int i = 0; i < result.Warnings.Count; i++)
            {
                EditorGUILayout.LabelField("⚠ " + result.Warnings[i]);
            }
        }
    }

    private static void DrawCheck(string label, bool valid)
    {
        EditorGUILayout.LabelField((valid ? "✓ " : "✗ ") + label);
    }

    private void HandleCellClick(Vector2Int cell)
    {
        LevelBlockData previousBlock = selectedCell.HasValue ? session.FindBlock(selectedCell.Value) : null;
        LevelTargetData previousTarget = selectedCell.HasValue ? session.FindTarget(selectedCell.Value) : null;
        LevelShutterData previousShutter = session.FindShutter(cell);
        selectedCell = cell;
        Undo.RegisterCompleteObjectUndo(session, "Edit Level Cell");

        switch (editMode)
        {
            case EditMode.Block:
                selectedShutter = null;
                PlaceOrUpdateBlock(cell, previousBlock);
                break;
            case EditMode.Target:
                selectedShutter = null;
                PlaceOrUpdateTarget(cell, previousTarget);
                break;
            case EditMode.Shutter:
                PlaceOrUpdateShutter(cell, previousShutter);
                break;
            case EditMode.Eraser:
                bool removedBlock = session.RemoveBlockAt(cell);
                bool removedTarget = session.RemoveTargetAt(cell);
                bool removedShutter = session.RemoveShutterAt(cell);
                if (removedBlock || removedTarget || removedShutter)
                {
                    session.isDirty = true;
                    if (removedShutter)
                    {
                        selectedShutter = null;
                    }
                }

                break;
        }

        EditorUtility.SetDirty(session);
        lastValidation = ValidateCurrent();
    }

    private void PlaceOrUpdateShutter(Vector2Int cell, LevelShutterData existing)
    {
        if (existing != null)
        {
            selectedShutter = existing;
            selectedShutterDurability = existing.durability;
            return;
        }

        if (selectedShutter != null)
        {
            if (selectedShutter.cells == null)
            {
                selectedShutter.cells = new List<Vector2Int>();
            }

            if (!selectedShutter.cells.Contains(cell))
            {
                selectedShutter.cells.Add(cell);
            }
            selectedShutter.durability = selectedShutterDurability;
        }
        else
        {
            selectedShutter = new LevelShutterData
            {
                durability = selectedShutterDurability,
                cells = new List<Vector2Int> { cell }
            };
            session.shutters.Add(selectedShutter);
        }

        session.isDirty = true;
    }

    private void PlaceOrUpdateBlock(Vector2Int cell, LevelBlockData previousBlock)
    {
        LevelBlockData existing = session.FindBlock(cell);
        if (existing != null)
        {
            ApplyPaintedCell(existing, cell);
            existing.moveDirection = selectedDirection;
            existing.composition = selectedComposition;
            existing.outerShape = selectedOuterShape;
        }
        else if (extendSelectedFootprint && previousBlock != null)
        {
            ApplyPaintedCell(previousBlock, cell);
            previousBlock.moveDirection = selectedDirection;
            previousBlock.composition = selectedComposition;
            previousBlock.outerShape = selectedOuterShape;
        }
        else
        {
            session.blocks.Add(new LevelBlockData
            {
                shapeType = selectedShape,
                moveDirection = selectedDirection,
                gridPosition = cell,
                composition = selectedComposition,
                outerShape = selectedOuterShape,
                cells = new List<ShapeCellData>
                {
                    new ShapeCellData { localPosition = Vector2Int.zero, shapeType = selectedShape }
                }
            });
        }

        session.isDirty = true;
    }

    private void PlaceOrUpdateTarget(Vector2Int cell, LevelTargetData previousTarget)
    {
        LevelTargetData existing = session.FindTarget(cell);
        if (existing != null)
        {
            ApplyPaintedTargetCell(existing, cell);
            existing.composition = selectedComposition;
            existing.outerShape = selectedOuterShape;
        }
        else if (extendSelectedFootprint && previousTarget != null)
        {
            ApplyPaintedTargetCell(previousTarget, cell);
            previousTarget.composition = selectedComposition;
            previousTarget.outerShape = selectedOuterShape;
        }
        else
        {
            session.targets.Add(new LevelTargetData
            {
                shapeType = selectedShape,
                gridPosition = cell,
                composition = selectedComposition,
                outerShape = selectedOuterShape,
                cells = new List<ShapeCellData>
                {
                    new ShapeCellData { localPosition = Vector2Int.zero, shapeType = selectedShape }
                }
            });
        }

        session.isDirty = true;
    }

    private void ApplyPaintedCell(LevelBlockData block, Vector2Int worldCell)
    {
        if (block.cells == null)
        {
            block.cells = new List<ShapeCellData>();
        }

        EnsureCells(block.cells, block.shapeType);
        Vector2Int local = worldCell - block.gridPosition;
        SetCellShape(block.cells, local, selectedShape);
        if (local == Vector2Int.zero)
        {
            block.shapeType = selectedShape;
        }
    }

    private void ApplyPaintedTargetCell(LevelTargetData target, Vector2Int worldCell)
    {
        if (target.cells == null)
        {
            target.cells = new List<ShapeCellData>();
        }

        EnsureCells(target.cells, target.shapeType);
        Vector2Int local = worldCell - target.gridPosition;
        SetCellShape(target.cells, local, selectedShape);
        if (local == Vector2Int.zero)
        {
            target.shapeType = selectedShape;
        }
    }

    private static void EnsureCells(List<ShapeCellData> cells, ShapeType fallback)
    {
        if (cells == null)
        {
            return;
        }

        if (cells.Count == 0)
        {
            cells.Add(new ShapeCellData
            {
                localPosition = Vector2Int.zero,
                shapeType = fallback
            });
        }
    }

    private static void SetCellShape(List<ShapeCellData> cells, Vector2Int local, ShapeType shape)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] != null && cells[i].localPosition == local)
            {
                cells[i].shapeType = shape;
                return;
            }
        }

        cells.Add(new ShapeCellData
        {
            localPosition = local,
            shapeType = shape
        });
    }

    private static string DescribeCells(IReadOnlyList<ShapeCellData> cells, ShapeType fallback)
    {
        int count = ShapeLayout.EffectiveCount(cells);
        var parts = new string[count];
        for (int i = 0; i < count; i++)
        {
            Vector2Int local = ShapeLayout.EffectiveLocal(cells, i);
            parts[i] = $"({local.x},{local.y}) {ShapeLayout.EffectiveShape(cells, i, fallback)}";
        }

        return string.Join(", ", parts);
    }

    private void RequestNewLevel()
    {
        if (!ConfirmDiscardIfDirty())
        {
            return;
        }

        BeginNewLevel(markDirty: false);
    }

    private void BeginNewLevel(bool markDirty)
    {
        Undo.RegisterCompleteObjectUndo(session, "New Level");
        GetDefaultBoardSize(out int columns, out int rows);
        session.ResetNew(GetNextLevelName(), columns, rows);
        selectedCell = null;
        selectedShutter = null;
        lastValidation = null;
        showValidationDetails = false;
        if (markDirty)
        {
            session.isDirty = true;
        }

        EditorUtility.SetDirty(session);
        Repaint();
    }

    private void RequestLoadLevel()
    {
        if (!ConfirmDiscardIfDirty())
        {
            return;
        }

        string startFolder = Directory.Exists(LevelsFolder) ? LevelsFolder : "Assets";
        string absolutePath = EditorUtility.OpenFilePanel("Load Level", startFolder, "asset");
        if (string.IsNullOrEmpty(absolutePath))
        {
            return;
        }

        string assetPath = AbsolutePathToAssetPath(absolutePath);
        if (string.IsNullOrEmpty(assetPath))
        {
            EditorUtility.DisplayDialog("Load Level", "Please choose a LevelData asset inside this project.", "OK");
            return;
        }

        LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(assetPath);
        if (level == null)
        {
            EditorUtility.DisplayDialog("Load Level", "That asset is not a LevelData asset.", "OK");
            return;
        }

        LoadLevel(level);
    }

    private void LoadLevel(LevelData level)
    {
        Undo.RegisterCompleteObjectUndo(session, "Load Level");
        session.ResetNew(level.name, level.ResolvedGridWidth, level.ResolvedGridHeight);
        session.sourceAsset = level;
        CopyBlocks(level.blocks, session.blocks);
        CopyTargets(level.targets, session.targets);
        CopyShutters(level.shutters, session.shutters);
        ExpandBoardToFit();
        selectedCell = null;
        selectedShutter = null;
        lastValidation = ValidateCurrent();
        showValidationDetails = false;
        EditorUtility.SetDirty(session);
        Repaint();
    }

    private void SaveLevel()
    {
        if (string.IsNullOrWhiteSpace(session.levelName))
        {
            session.levelName = GetNextLevelName();
        }

        session.levelName = SanitizeAssetName(session.levelName);
        lastValidation = ValidateCurrent();
        showValidationDetails = true;
        if (!lastValidation.IsValid)
        {
            EditorUtility.DisplayDialog("Save Level", "The level is invalid. Fix the errors shown in the editor before saving.", "OK");
            Repaint();
            return;
        }

        EnsureLevelsFolder();
        string assetPath = $"{LevelsFolder}/{session.levelName}.asset";
        LevelData asset = AssetDatabase.LoadAssetAtPath<LevelData>(assetPath);
        bool created = false;
        if (asset == null)
        {
            asset = CreateInstance<LevelData>();
            AssetDatabase.CreateAsset(asset, assetPath);
            created = true;
        }

        Undo.RecordObject(asset, "Save Level");
        if (asset.blocks == null)
        {
            asset.blocks = new List<LevelBlockData>();
        }

        if (asset.targets == null)
        {
            asset.targets = new List<LevelTargetData>();
        }

        if (asset.shutters == null)
        {
            asset.shutters = new List<LevelShutterData>();
        }

        CopyBlocks(session.blocks, asset.blocks);
        CopyTargets(session.targets, asset.targets);
        CopyShutters(session.shutters, asset.shutters);
        asset.gridWidth = Mathf.Max(1, session.columns);
        asset.gridHeight = Mathf.Max(1, session.rows);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        session.sourceAsset = asset;
        session.isDirty = false;
        AddToLevelDatabase(asset);

        //Debug.Log(created
        //    ? $"Level Editor: Created {assetPath} and registered it in the LevelDatabase."
         //   : $"Level Editor: Updated {assetPath}.");
        Repaint();
    }

    private LevelEditorValidationResult ValidateCurrent()
    {
        return LevelEditorValidation.Validate(
            session.levelName,
            session.blocks,
            session.targets,
            session.shutters,
            session.columns,
            session.rows);
    }

    private bool ConfirmDiscardIfDirty()
    {
        if (!session.isDirty)
        {
            return true;
        }

        return EditorUtility.DisplayDialog(
            "Unsaved Changes",
            "Unsaved changes will be lost. Continue?",
            "Discard",
            "Cancel");
    }

    private void MarkDirty()
    {
        session.isDirty = true;
        EditorUtility.SetDirty(session);
    }

    private void ExpandBoardToFit()
    {
        int maxX = session.columns - 1;
        int maxY = session.rows - 1;
        for (int i = 0; i < session.blocks.Count; i++)
        {
            if (session.blocks[i] == null)
            {
                continue;
            }

            maxX = Mathf.Max(maxX, session.blocks[i].gridPosition.x);
            maxY = Mathf.Max(maxY, session.blocks[i].gridPosition.y);
            int cellCount = ShapeLayout.EffectiveCount(session.blocks[i].cells);
            for (int c = 0; c < cellCount; c++)
            {
                Vector2Int world = session.blocks[i].gridPosition + ShapeLayout.EffectiveLocal(session.blocks[i].cells, c);
                maxX = Mathf.Max(maxX, world.x);
                maxY = Mathf.Max(maxY, world.y);
            }
        }

        for (int i = 0; i < session.targets.Count; i++)
        {
            if (session.targets[i] == null)
            {
                continue;
            }

            maxX = Mathf.Max(maxX, session.targets[i].gridPosition.x);
            maxY = Mathf.Max(maxY, session.targets[i].gridPosition.y);
            int cellCount = ShapeLayout.EffectiveCount(session.targets[i].cells);
            for (int c = 0; c < cellCount; c++)
            {
                Vector2Int world = session.targets[i].gridPosition + ShapeLayout.EffectiveLocal(session.targets[i].cells, c);
                maxX = Mathf.Max(maxX, world.x);
                maxY = Mathf.Max(maxY, world.y);
            }
        }

        for (int i = 0; i < session.shutters.Count; i++)
        {
            LevelShutterData shutter = session.shutters[i];
            if (shutter == null || shutter.cells == null)
            {
                continue;
            }

            for (int c = 0; c < shutter.cells.Count; c++)
            {
                maxX = Mathf.Max(maxX, shutter.cells[c].x);
                maxY = Mathf.Max(maxY, shutter.cells[c].y);
            }
        }

        session.columns = Mathf.Max(session.columns, maxX + 1);
        session.rows = Mathf.Max(session.rows, maxY + 1);
    }

    private static void CopyBlocks(IList<LevelBlockData> source, List<LevelBlockData> destination)
    {
        destination.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            LevelBlockData block = source[i];
            if (block == null)
            {
                continue;
            }

            destination.Add(source[i].Clone());
        }
    }

    private static void CopyTargets(IList<LevelTargetData> source, List<LevelTargetData> destination)
    {
        destination.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            LevelTargetData target = source[i];
            if (target == null)
            {
                continue;
            }

            destination.Add(target.Clone());
        }
    }

    private static void CopyShutters(IList<LevelShutterData> source, List<LevelShutterData> destination)
    {
        destination.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            LevelShutterData shutter = source[i];
            if (shutter == null)
            {
                continue;
            }

            destination.Add(shutter.Clone());
        }
    }

    private static void GetDefaultBoardSize(out int columns, out int rows)
    {
        columns = 5;
        rows = 5;
    }

    private float ComputeEditorCellSize()
    {
        return BoardLayoutMath.ComputeSquareCellSize(
            session.columns,
            session.rows,
            EditorGameplayAreaWidth,
            EditorGameplayAreaHeight,
            EditorGameplayAreaPadding,
            EditorGameplayAreaPadding);
    }

    private static string GetNextLevelName()
    {
        int next = 1;
        var existing = new HashSet<string>();
        if (AssetDatabase.IsValidFolder(LevelsFolder))
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelData", new[] { LevelsFolder });
            for (int i = 0; i < (guids != null ? guids.Length : 0); i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string name = Path.GetFileNameWithoutExtension(path);
                existing.Add(name);
                Match match = Regex.Match(name, @"^Level(\d+)$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    next = Mathf.Max(next, int.Parse(match.Groups[1].Value) + 1);
                }
            }
        }

        string candidate = "Level" + next;
        while (existing.Contains(candidate))
        {
            next++;
            candidate = "Level" + next;
        }

        return candidate;
    }

    private static string SanitizeAssetName(string name)
    {
        string trimmed = name.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            trimmed = trimmed.Replace(invalid.ToString(), string.Empty);
        }

        return trimmed.Replace("/", string.Empty).Replace("\\", string.Empty);
    }

    private static LevelDatabase FindLevelDatabase()
    {
        const string preferredPath = LevelsFolder + "/LevelDatabase.asset";
        LevelDatabase preferred = AssetDatabase.LoadAssetAtPath<LevelDatabase>(preferredPath);
        if (preferred != null)
        {
            return preferred;
        }

        string[] guids = AssetDatabase.FindAssets("t:LevelDatabase");
        if (guids == null || guids.Length == 0)
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<LevelDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static void CreateLevelDatabase()
    {
        if (FindLevelDatabase() != null)
        {
            return;
        }

        EnsureLevelsFolder();
        LevelDatabase database = CreateInstance<LevelDatabase>();
        AssetDatabase.CreateAsset(database, LevelsFolder + "/LevelDatabase.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void AddToLevelDatabase(LevelData level)
    {
        LevelDatabase database = FindLevelDatabase();
        if (database == null)
        {
            CreateLevelDatabase();
            database = FindLevelDatabase();
        }

        if (database == null || level == null)
        {
            return;
        }

        var serialized = new SerializedObject(database);
        SerializedProperty levels = serialized.FindProperty("levels");
        if (levels == null)
        {
            Debug.LogError("Level Editor: Could not find the levels list on LevelDatabase.");
            return;
        }

        for (int i = 0; i < levels.arraySize; i++)
        {
            if (levels.GetArrayElementAtIndex(i).objectReferenceValue == level)
            {
                return;
            }
        }

        Undo.RecordObject(database, "Add Level to Database");
        int index = levels.arraySize;
        levels.arraySize++;
        levels.GetArrayElementAtIndex(index).objectReferenceValue = level;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
    }

    private static string AbsolutePathToAssetPath(string absolutePath)
    {
        string projectRelative = FileUtil.GetProjectRelativePath(absolutePath);
        if (!string.IsNullOrEmpty(projectRelative))
        {
            return projectRelative;
        }

        string dataPath = Application.dataPath.Replace('\\', '/');
        string normalized = absolutePath.Replace('\\', '/');
        if (normalized.StartsWith(dataPath))
        {
            return "Assets" + normalized.Substring(dataPath.Length);
        }

        return null;
    }

    private static void EnsureLevelsFolder()
    {
        if (AssetDatabase.IsValidFolder(LevelsFolder))
        {
            return;
        }

        AssetDatabase.CreateFolder("Assets", "Levels");
    }

    private static Color ShapeColor(ShapeType shape)
    {
        switch (shape)
        {
            case ShapeType.Square:
                return new Color(0.95f, 0.78f, 0.22f);
            case ShapeType.Circle:
                return new Color(0.35f, 0.78f, 0.95f);
            case ShapeType.Triangle:
                return new Color(0.88f, 0.45f, 0.88f);
            case ShapeType.Diamond:
                return new Color(0.31f, 0.82f, 0.55f);
            case ShapeType.Hexagon:
                return new Color(1f, 0.58f, 0.28f);
            case ShapeType.Star:
                return new Color(1f, 0.84f, 0.38f);
            default:
                return Color.white;
        }
    }

    private static string ShapeGlyph(ShapeType shape, bool outlined)
    {
        switch (shape)
        {
            case ShapeType.Square:
                return outlined ? "□" : "■";
            case ShapeType.Circle:
                return outlined ? "○" : "●";
            case ShapeType.Triangle:
                return outlined ? "△" : "▲";
            case ShapeType.Diamond:
                return outlined ? "◇" : "◆";
            case ShapeType.Hexagon:
                return outlined ? "⬡" : "⬢";
            case ShapeType.Star:
                return outlined ? "☆" : "★";
            default:
                return "?";
        }
    }

    private static string DirectionGlyph(MoveDirection direction)
    {
        switch (direction)
        {
            case MoveDirection.Up:
                return "↑";
            case MoveDirection.Down:
                return "↓";
            case MoveDirection.Left:
                return "←";
            case MoveDirection.Right:
                return "→";
            default:
                return string.Empty;
        }
    }

    private static GUIStyle CenteredLabel()
    {
        return new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18
        };
    }

    private static GUIStyle CenteredMiniLabel()
    {
        return new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };
    }
}

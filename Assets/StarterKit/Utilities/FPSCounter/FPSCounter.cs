using StarterKit;
using UnityEngine;

public enum FPSCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public class FPSCounter : Singleton<FPSCounter>
{
    [SerializeField] private bool _isEnabled = true;
    [SerializeField] private FPSCorner _corner = FPSCorner.TopRight;
    [SerializeField] private float _hudRefreshRate = 1f;
    [SerializeField] private int _fontSize = 24;
    [SerializeField] private Color _textColor = Color.white;
    [SerializeField] private Vector2 _padding = new(10, 10);

    [Header("Target FPS Settings")]
    [SerializeField] private bool _useTargetFPS = true;
    [SerializeField] private int _targetFPS = 60;
    [SerializeField] private Color _colorAboveTarget = Color.green;
    [SerializeField] private Color _colorBelowTarget = Color.red;

    private float _timer;
    private int _currentFPS;
    private string _fpsText = "FPS: 0";

    private int _frameCount;
    private float _fpsSum;
    private float _averageFPS;

    private GUIStyle _guiStyle;

    private void Start()
    {
        if (!_isEnabled)
        {
            Destroy(gameObject);
            return;
        }

        _guiStyle = new GUIStyle
        {
            fontSize = _fontSize
        };
        _guiStyle.normal.textColor = _textColor;
        _guiStyle.fontStyle = FontStyle.Bold;
    }

    private void Update()
    {
        if (!_isEnabled)
        {
            return;
        }

        _frameCount++;
        _fpsSum += 1f / Time.unscaledDeltaTime;

        if (Time.unscaledTime > _timer)
        {
            _currentFPS = (int)(1f / Time.unscaledDeltaTime);
            _fpsText = "FPS: " + _currentFPS;
            _timer = Time.unscaledTime + _hudRefreshRate;

            // Calculate average FPS
            _averageFPS = _fpsSum / _frameCount;
        }
    }

    private void OnGUI()
    {
        if (!_isEnabled)
        {
            return;
        }

        if (_guiStyle == null)
        {
            _guiStyle = new GUIStyle
            {
                fontSize = _fontSize
            };
            _guiStyle.normal.textColor = _textColor;
            _guiStyle.fontStyle = FontStyle.Bold;
        }

        // Update color based on target FPS
        if (_useTargetFPS)
        {
            _guiStyle.normal.textColor = _currentFPS >= _targetFPS ? _colorAboveTarget : _colorBelowTarget;
        }
        else
        {
            _guiStyle.normal.textColor = _textColor;
        }

        Vector2 textSize = _guiStyle.CalcSize(new GUIContent(_fpsText));
        Vector2 position = GetCornerPosition(textSize);

        GUI.Label(new Rect(position.x, position.y, textSize.x, textSize.y), _fpsText, _guiStyle);
    }

    private Vector2 GetCornerPosition(Vector2 textSize)
    {
        if (!_isEnabled)
        {
            return Vector2.zero;
        }

        float x = 0;
        float y = 0;

        switch (_corner)
        {
            case FPSCorner.TopLeft:
                x = _padding.x;
                y = _padding.y;
                break;
            case FPSCorner.TopRight:
                x = Screen.width - textSize.x - _padding.x;
                y = _padding.y;
                break;
            case FPSCorner.BottomLeft:
                x = _padding.x;
                y = Screen.height - textSize.y - _padding.y;
                break;
            case FPSCorner.BottomRight:
                x = Screen.width - textSize.x - _padding.x;
                y = Screen.height - textSize.y - _padding.y;
                break;
        }

        return new Vector2(x, y);
    }

    public float GetAverageFPS()
    {
        if (!_isEnabled)
        {
            return 0f;
        }

        return _averageFPS;
    }
}
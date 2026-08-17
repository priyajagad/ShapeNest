using StarterKit.UIKit;
using UnityEditor;



[CustomEditor(typeof(UIConfigration))]
public class UIConfigrationEditor : Editor
{

    private UIConfigration _UIconfig;

    public override void OnInspectorGUI()
    {
        //DrawDefaultInspector();
        base.OnInspectorGUI();

        _UIconfig = (UIConfigration)target;
    }
}


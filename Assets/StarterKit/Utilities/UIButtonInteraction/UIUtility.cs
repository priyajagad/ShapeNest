using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public static class UIUtility
{
    //Returns 'true' if we touched or hovering on Unity UI element.
    public static bool IsPointerOverUIElement()
    {
        if (Input.touchSupported)
        {
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        }
        else
        {
            return EventSystem.current.IsPointerOverGameObject();
        }
    }
}

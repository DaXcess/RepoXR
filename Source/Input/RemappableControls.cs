using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RepoXR.Input;

public class RemappableControls : MonoBehaviour
{
    public RemappableControl[] controls;
    public InputActionReference[] additionalBindings;
    public string defaultLeftHandedBindings;
}

[Serializable]
public class RemappableControl
{
    public string controlName;
    public string headerName;
    public InputActionReference currentInput;
    public int bindingIndex = -1;
    public bool toggleable;
    public bool defaultToggle;
}
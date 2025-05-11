﻿using RepoXR.Input;
using UnityEngine;

namespace RepoXR.UI;

// TODO: Move to UI.Controls namespace;
public class VRMenuKeybindToggle: MonoBehaviour
{
    public string inputAction;

    public void EnableToggle()
    {
        VRInputSystem.Instance.InputToggleRebind(inputAction, true);
    }

    public void DisableToggle()
    {
        VRInputSystem.Instance.InputToggleRebind(inputAction, false);
    }
    
    public void FetchSetting()
    {
        GetComponent<MenuTwoOptions>().startSettingFetch = VRInputSystem.Instance.InputToggleGet(inputAction);
    }
}
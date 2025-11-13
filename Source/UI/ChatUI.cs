using RepoXR.Assets;
using RepoXR.ThirdParty.MRTK;
using UnityEngine;

namespace RepoXR.UI;

public class ChatUI : MonoBehaviour
{
    private ChatManager chatManager;

    private NonNativeKeyboard keyboard;
    private Vector3 keyboardScale;
    private float keyboardLerp;

    private ChatManager.ChatState prevState;

    private void Awake()
    {
        chatManager = ChatManager.instance;

        keyboard = Instantiate(AssetCollection.Keyboard,
            transform.TransformPoint(new Vector3(360, -100)),
            transform.rotation * Quaternion.Euler(15, 0, 0)).GetComponent<NonNativeKeyboard>();
        keyboard.SubmitOnEnter = false;
        
        keyboard.OnKeyboardValueKeyPressed += OnKeyPressed;
        keyboard.OnKeyboardFunctionKeyPressed += OnFunctionKeyPressed;

        keyboardScale = Vector3.one *
                        (RunManager.instance.levelCurrent == RunManager.instance.levelLobbyMenu ? 0.0175f : 0.005f);

        prevState = chatManager.chatState;
    }

    private void OnDestroy()
    {
        keyboard.OnKeyboardValueKeyPressed -= OnKeyPressed;
        keyboard.OnKeyboardFunctionKeyPressed -= OnFunctionKeyPressed;
    }

    private void Update()
    {
        // Disable during loading
        if (global::LoadingUI.instance.isActiveAndEnabled &&
            ChatManager.instance.chatState == ChatManager.ChatState.Active)
            chatManager.StateSet(ChatManager.ChatState.Inactive);

        // Disable during pause menu
        if (!SemiFunc.MenuLevel() && MenuManager.instance.currentMenuPage &&
            ChatManager.instance.chatState == ChatManager.ChatState.Active)
            chatManager.StateSet(ChatManager.ChatState.Inactive);

        // Reset UI position if chat became active or is possessed
        if ((chatManager.chatState == ChatManager.ChatState.Active &&
             chatManager.chatState != prevState) || chatManager.chatState == ChatManager.ChatState.Possessed)
            PauseUI.instance?.ResetPosition(true);

        // The ray interactor needs to always stay active if we're in the lobby menu
        if (RunManager.instance.levelCurrent != RunManager.instance.levelLobbyMenu)
        {
            // Enable the XR Ray interactor visuals if the chat has become active
            if (chatManager.chatState == ChatManager.ChatState.Active && chatManager.chatState != prevState)
                XRRayInteractorManager.Instance?.SetVisible(true);

            // Disable the XR Ray interactor visuals if the chat has become inactive or possessed
            if (chatManager.chatState != ChatManager.ChatState.Active && chatManager.chatState != prevState)
                XRRayInteractorManager.Instance?.SetVisible(false);
        }

        prevState = chatManager.chatState;

        if (chatManager.chatState == ChatManager.ChatState.Active)
        {
            PhysGrabber.instance.ReleaseObject(-1); // Drop items while chat is active

            if (!keyboard.gameObject.activeSelf)
                keyboard.PresentKeyboard();

            keyboardLerp += 8 * Time.deltaTime;
            keyboardLerp = Mathf.Clamp01(keyboardLerp);
            keyboard.transform.localScale = Vector3.Slerp(Vector3.zero, keyboardScale, keyboardLerp);
        }
        else
        {
            if (!keyboard.gameObject.activeSelf)
                return;

            keyboardLerp -= 8 * Time.deltaTime;
            keyboardLerp = Mathf.Clamp01(keyboardLerp);
            keyboard.transform.localScale = Vector3.Slerp(Vector3.zero, keyboardScale, keyboardLerp);

            if (keyboardLerp == 0)
                keyboard.Close();
        }
    }

    private void LateUpdate()
    {
        keyboard.transform.position = transform.TransformPoint(new Vector3(360, -100, 0));
        keyboard.transform.rotation = transform.rotation * Quaternion.Euler(15, 0, 0);
    }

    private void AppendText(string text)
    {
        var originalText = chatManager.chatMessage;
        chatManager.chatMessage += text;

        if (chatManager.chatMessage.Length > 50)
        {
            global::ChatUI.instance.SemiUITextFlashColor(Color.red, 0.2f);
            global::ChatUI.instance.SemiUISpringShakeX(10f, 10f, 0.3f);
            global::ChatUI.instance.SemiUISpringScale(0.05f, 5f, 0.2f);
            MenuManager.instance.MenuEffectClick(MenuManager.MenuClickEffectType.Deny, null, 1f, 1f, true);

            chatManager.chatMessage = originalText;
            
            return;
        }
        
        chatManager.TypeEffect(Color.yellow);
    }

    private void OnKeyPressed(KeyboardValueKey key)
    {
        AppendText(keyboard.IsShifted ? key.ShiftValue : key.Value);
    }

    private void ChatHistoryCycle(bool previous)
    {
        if (chatManager.chatHistory.Count == 0)
            return;

        if (previous)
        {
            if (chatManager.chatHistoryIndex > 0)
                chatManager.chatHistoryIndex--;
            else
                chatManager.chatHistoryIndex = chatManager.chatHistory.Count - 1;
        }
        else
        {
            if (chatManager.chatHistoryIndex < chatManager.chatHistory.Count - 1)
                chatManager.chatHistoryIndex++;
            else
                chatManager.chatHistoryIndex = 0;
        }

        chatManager.chatMessage = chatManager.chatHistory[chatManager.chatHistoryIndex];
        chatManager.chatText.text = chatManager.chatMessage;
        global::ChatUI.instance.SemiUITextFlashColor(Color.cyan, 0.2f);
        global::ChatUI.instance.SemiUISpringShakeY(2f, 5f, 0.2f);
        MenuManager.instance.MenuEffectClick(MenuManager.MenuClickEffectType.Tick, null, 1f, 0.2f, true);
    }

    private void OnFunctionKeyPressed(KeyboardKeyFunc key)
    {
        switch (key.ButtonFunction)
        {
            case KeyboardKeyFunc.Function.Backspace:
                if (chatManager.chatMessage.Length == 0)
                    break;

                var message = chatManager.chatMessage;

                message = message.Remove(Mathf.Max(message.Length - 1, 0));

                chatManager.chatMessage = message;
                chatManager.chatText.text = message;
                chatManager.CharRemoveEffect();

                break;

            case KeyboardKeyFunc.Function.Enter:
                chatManager.StateSet(chatManager.chatMessage == ""
                    ? ChatManager.ChatState.Inactive
                    : ChatManager.ChatState.Send);

                break;

            case KeyboardKeyFunc.Function.Space:
                AppendText(" ");

                break;

            case KeyboardKeyFunc.Function.Previous:
                ChatHistoryCycle(true);

                break;
            
            case KeyboardKeyFunc.Function.Next:
                ChatHistoryCycle(false);

                break;
        }
    }
}
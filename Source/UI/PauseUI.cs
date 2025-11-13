using RepoXR.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RepoXR.UI;

public class PauseUI : MonoBehaviour
{
    public static PauseUI? instance;

    public Vector3 positionOffset;

    private Vector3 targetPos;
    private Quaternion targetRot;

    private Transform camera;
    private XRRayInteractorManager interactor;

    private bool isOpen;
    private float darkness;

    private void Awake()
    {
        instance = this;
        camera = Camera.main!.transform;
        interactor = camera.transform.parent.gameObject.AddComponent<XRRayInteractorManager>();
        interactor.SetVisible(false);

        var box = FindObjectOfType<MenuSelectionBoxTop>();
        box.transform.parent.SetParent(transform, false);
        box.transform.parent.localPosition = Vector3.zero;
        box.transform.parent.localRotation = Quaternion.identity;
        box.transform.parent.localScale = Vector3.one;
    }

    private void OnEnable()
    {
        Actions.Instance["ResetHeight"].performed += OnResetHeight;
    }

    private void OnDisable()
    {
        Actions.Instance["ResetHeight"].performed -= OnResetHeight;
    }

    private void OnDestroy()
    {
        instance = null;
    }

    private void Update()
    {
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, 8 * Time.deltaTime);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, 8 * Time.deltaTime);
    }

    private void LateUpdate()
    {
        darkness = Mathf.Lerp(darkness, isOpen ? 0.75f : 0, 6 * Time.deltaTime);

        if (darkness < 0.01f)
            darkness = 0;

        if (darkness > 0)
        {
            FadeOverlay.Instance.Image.color = new Color(0, 0, 0, darkness);

            interactor.SetLineSortingOrder(6);
        }
        else
            interactor.SetLineSortingOrder(4);
    }

    public void Show()
    {
        isOpen = true;
        ResetPosition(true);

        interactor.SetVisible(true);
    }

    public void Hide(bool instant)
    {
        isOpen = false;
        interactor.SetVisible(false);

        if (instant)
        {
            MenuManager.instance.PageCloseAll();
            darkness = 0;
        }
    }

    public void ResetPosition(bool instant = false)
    {
        var fwd = (camera.localRotation * Vector3.forward).normalized;
        fwd.y = 0;
        fwd.Normalize();

        var pos = camera.transform.localPosition + fwd * 5 + Vector3.up * 0.15f;
        var cameraPos = new Vector3(camera.localPosition.x, pos.y, camera.localPosition.z);

        targetRot = Quaternion.LookRotation(-(cameraPos - pos).normalized);
        targetPos = pos + targetRot * positionOffset;

        if (instant)
        {
            transform.localPosition = targetPos;
            transform.localRotation = targetRot;
        }
    }

    private void OnResetHeight(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || !isOpen)
            return;

        ResetPosition();
    }
}
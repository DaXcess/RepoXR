using System.Collections;
using UnityEngine;

namespace RepoXR.UI;

public class LoadingUI : MonoBehaviour
{
    public static LoadingUI instance;
    
    private static Vector3 lastLocalPosition;
    private static float lastLocalRotation;
    
    private Transform camera;

    private void Awake()
    {
        instance = this;
        camera = Camera.main!.transform;

        RestorePosition();
    }

    private void OnDestroy()
    {
        instance = null!;
    }

    private IEnumerator Start()
    {
        RestorePosition();
        
        yield return null;

        RestorePosition();
    }

    private void OnDisable()
    {
        lastLocalPosition = transform.localPosition;
        lastLocalRotation = transform.localEulerAngles.y;
    }

    public void ResetPosition()
    {
        var fwd = (camera.localRotation * Vector3.forward).normalized;
        fwd.y = 0;
        fwd.Normalize();

        var pos = camera.transform.localPosition + fwd * 5 + Vector3.up * 0.15f;
        var targetPos = new Vector3(camera.localPosition.x, pos.y, camera.localPosition.z);
        var dirToCam = -(targetPos - pos).normalized;

        transform.localPosition = pos;
        transform.localRotation = Quaternion.LookRotation(dirToCam);
    }

    private void RestorePosition()
    {
        transform.localPosition = lastLocalPosition;
        transform.localEulerAngles = lastLocalRotation * Vector3.up;
    }
}
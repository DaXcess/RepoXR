using RepoXR.Assets;
using RepoXR.Input;
using RepoXR.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RepoXR.Player;

public class VREyeTracking : MonoBehaviour
{
    private Transform debugCubeHmdRel;
    private Transform debugCubeSpaceRel;

    public bool Supported { get; private set; }
    public Vector3 FocusPosition => debugCubeHmdRel.position;
    public Vector3 GazeDirection { get; private set; }

    private void Awake()
    {
        Actions.Instance.EyeGazeTracked.performed += OnEyeTrackingDetected;

        debugCubeHmdRel = Instantiate(AssetCollection.Cube).transform;
        debugCubeSpaceRel = Instantiate(AssetCollection.Cube).transform;

        debugCubeHmdRel.GetComponent<MeshRenderer>().material.color = Color.blue;
        debugCubeSpaceRel.GetComponent<MeshRenderer>().material.color = Color.red;

        debugCubeHmdRel.GetComponent<Collider>().enabled = false;
        debugCubeSpaceRel.GetComponent<Collider>().enabled = false;

        debugCubeHmdRel.position = Vector3.down * 1000;
        debugCubeSpaceRel.position = Vector3.down * 1000;
    }

    private void OnDestroy()
    {
        Actions.Instance.EyeGazeTracked.performed -= OnEyeTrackingDetected;
    }

    private void OnEyeTrackingDetected(InputAction.CallbackContext obj)
    {
        if (!obj.performed)
            return;

        Supported = true;
    }

    private void Update()
    {
        if (!Supported)
            return;

        if (!!false) // TODO: If disabled by config
            return;

        var gazePosition = Actions.Instance.EyeGazePosition.ReadValue<Vector3>();
        var gazeRotation = Actions.Instance.EyeGazeRotation.ReadValue<Quaternion>();

        UpdateHmdRelative(gazePosition, gazeRotation);
        UpdateSpaceRelative(gazePosition, gazeRotation);

        // var ray = new Ray(transform.TransformPoint(gazePosition),
        //     transform.TransformDirection(gazeRotation * Vector3.forward));
        //
        // GazeDirection = ray.direction;
        //
        // if (Physics.Raycast(ray, out var hit, 5, SemiFunc.LayerMaskGetShouldHits()))
        //     debugCubeHmdRel.transform.position = hit.point;
        // else
        //     debugCubeHmdRel.transform.position = ray.origin + ray.direction * 5;
        //
        // NetworkSystem.instance.UpdateEyeTracking(debugCubeHmdRel.transform.position);
        //
        // debugCubeHmdRel.transform.rotation = Quaternion.LookRotation(ray.direction);
    }

    private void UpdateHmdRelative(Vector3 position, Quaternion rotation)
    {
        var ray = new Ray(transform.TransformPoint(position), transform.TransformDirection(rotation * Vector3.forward));

        if (Physics.Raycast(ray, out var hit, 5, SemiFunc.LayerMaskGetShouldHits()))
            debugCubeHmdRel.transform.position = hit.point;
        else
            debugCubeHmdRel.transform.position = ray.origin + ray.direction * 5;

        debugCubeHmdRel.transform.rotation = Quaternion.LookRotation(ray.direction);
    }

    private void UpdateSpaceRelative(Vector3 position, Quaternion rotation)
    {
        var ray = new Ray(transform.parent.TransformPoint(position),
            transform.parent.TransformDirection(rotation * Vector3.forward));

        if (Physics.Raycast(ray, out var hit, 5, SemiFunc.LayerMaskGetShouldHits()))
            debugCubeSpaceRel.transform.position = hit.point;
        else
            debugCubeSpaceRel.transform.position = ray.origin + ray.direction * 5;

        debugCubeSpaceRel.transform.rotation = Quaternion.LookRotation(ray.direction);
    }
}
using UnityEngine;

public class CameraScript : MonoBehaviour {

    public Camera CameraObj;
    public Transform Parent;
    public Material WallMat;

    private CustomMaterialInfo[] _filters;

    private void Awake()
    {
        _filters = new CustomMaterialInfo[0];
    }

    public void UpdateChildren()
    {
        _filters = Parent.GetComponentsInChildren<CustomMaterialInfo>();
    }

    private void LateUpdate()
    {
        foreach (CustomMaterialInfo f in _filters)
        {
            Graphics.DrawMesh(f.MeshFilter.sharedMesh, f.transform.localToWorldMatrix, f.Color, 10, CameraObj);
        }
    }
}

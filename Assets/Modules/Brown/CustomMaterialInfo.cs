using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class CustomMaterialInfo : MonoBehaviour
{
    public Material Color { get; set; }
    public MeshFilter MeshFilter { get; private set; }

    private void Start()
    {
        MeshFilter = GetComponent<MeshFilter>();
    }
}

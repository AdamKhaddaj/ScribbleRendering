using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor.VersionControl;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

public class MeshDecimator : MonoBehaviour
{
    // Simple class that uses the MeshSimplifier package to decimate a mesh to below 1000 triangles
    // so that tangent solver can run quicker

    Mesh inputMesh;
    Mesh decimateMesh;
    void Start()
    {
        inputMesh = GetComponent<MeshFilter>().mesh;
        decimateMesh = Instantiate(inputMesh);
        Decimate();
    }

    private void Decimate()
    {

        if (inputMesh.vertices.Length > 1000) // Get number of vertices to around 1000 at most
        {
            MeshSimplifier decimator = new MeshSimplifier();
            decimator.Initialize(decimateMesh);
            decimator.SimplifyMesh(0.2f);
            decimateMesh = decimator.ToMesh();
        }

        AssetDatabase.CreateAsset(decimateMesh, "Assets/TestDecimate.asset");
        AssetDatabase.SaveAssets();
    }

}

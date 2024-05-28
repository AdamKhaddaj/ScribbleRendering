using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Complex;
using UnityMeshSimplifier;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine.Rendering;

// This class computes tangents for each face of a mesh and stores it in the mesh
public class TangentSolver : MonoBehaviour
{

    public Mesh inputMesh;
    private Mesh decimateMesh;

    struct Curve
    {
        public Vector3 direction;
        public float residual;
        public Vector<int> adjacents;
    }

    Curve[] curves;

    public TangentSolver(Mesh m)
    {
        inputMesh = m;
        decimateMesh = inputMesh;
        /*
        decimateMesh = Instantiate(inputMesh);

        if(inputMesh.vertices.Length > 1000) // Get number of vertices to around 1000 at most
        {
            MeshSimplifier decimator = new MeshSimplifier();
            decimator.Initialize(decimateMesh);
            decimator.SimplifyMesh((decimateMesh.vertices.Length + 1000f) / 1000f);
            decimateMesh = decimator.ToMesh();
        }
        */
        curves = new Curve[decimateMesh.triangles.Length/3];
    }
    public void CreateDirectionField()
    {
        Dictionary<int, int[]> faces = new Dictionary<int, int[]>();

        int[] triangles = decimateMesh.triangles;
        Vector4[] tangents = decimateMesh.tangents;
        
        //Create a dictionary where key = face, value = index of three vertices for that face
        for(int i = 0; i < triangles.Length / 3; i++)
        {
            faces[i] = new int[] { triangles[i*3], triangles[i * 3 + 1], triangles[i * 3 + 2] };
        }

        // Get best fit for vertices adjacent to each face

        for(int i = 0; i < faces.Count; i++)
        {
            List<int> adjacentIndices = new List<int>();
            // For every face, gather all adjacent vertices
            for(int j = 0; j < faces.Count; j++)
            {
                for(int k = 0; k < 3; k++)
                {
                    if (faces[i].Contains<int>(faces[j][k]))
                    {
                        //curves[i].adjacents.Add(j);
                        if (!adjacentIndices.Contains<int>(faces[j][0]))
                        {
                            adjacentIndices.Add(faces[j][0]);
                        }
                        if (!adjacentIndices.Contains<int>(faces[j][1]))
                        {
                            adjacentIndices.Add(faces[j][1]);
                        }
                        if (!adjacentIndices.Contains<int>(faces[j][2]))
                        {
                            adjacentIndices.Add(faces[j][2]);
                        }
                    }
                }
            }

            var fit = FindBestFit(adjacentIndices);
            //curves[i].direction = fit.Item1;
            //curves[i].residual = fit.Item2;

            tangents[faces[i][0]] = fit.Item1;
            tangents[faces[i][1]] = fit.Item1;
            tangents[faces[i][2]] = fit.Item1;

        }

        decimateMesh.tangents = tangents;

        Debug.Log(decimateMesh.tangents[10]);

        Mesh newmesh = Instantiate(decimateMesh);

        AssetDatabase.CreateAsset(newmesh, "Assets/Objects/TestCurves.asset");
        AssetDatabase.SaveAssets();

        // Now, iterate through each curve, and smooth out based on ratio of adjacent curvatures, as well as the residuals **(FINISH THIS)**

    }

    private (Vector3, float) FindBestFit(List<int> adjacentIndices)
    {
        // Get coords from all indices
        Vector3[] vertices = new Vector3[adjacentIndices.Count];
        for (int i = 0; i < adjacentIndices.Count; i++)
        {
            vertices[i] = decimateMesh.vertices[adjacentIndices[i]];
        }

        // Solve linear system to get coefficients for the quadratic

        Matrix<float> A = CreateMatrix.Dense<float>(vertices.Length, 6);
        Vector<float> B = CreateVector.Dense<float>(vertices.Length);
        for (int i = 0; i < vertices.Length; i++)
        {
            A[i, 0] = Mathf.Pow(vertices[i][0], 2);
            A[i, 1] = Mathf.Pow(vertices[i][1], 2);
            A[i, 2] = vertices[i][0] * vertices[i][1];
            A[i, 3] = vertices[i][0];
            A[i, 4] = vertices[i][1];
            A[i, 5] = 1;

            B[i] = vertices[i][2];
        }

        Matrix<float> Ap = A.PseudoInverse();

        Vector<float> coefficients = Ap * B;

        // Get the norm of residuals
        float residual = (float)(A * coefficients - B).L2Norm();

        // Get our quadratic equation so that we can take derivaitves of it
        Func<double, double, double> quadratic = (x, y) => coefficients[0] * Mathf.Pow((float)x, 2) + coefficients[1] + Mathf.Pow((float)y, 2) + coefficients[2] * x * y + coefficients[3] * x + coefficients[4] * y + coefficients[5];

        // Step one, get critical point by finding x and y values when derivative is 0

        Matrix<double> C = Matrix<double>.Build.DenseOfArray(new double[,] {
            { 2 * coefficients[0], coefficients[2] }, //dx
            { coefficients[2], 2 * coefficients[1] } //dy
        });

        Vector<double> D = Vector<double>.Build.Dense(new double[] {
            -coefficients[3], //-d
            -coefficients[4], //-e
        });
        Vector<double> criticalPoint = C.Solve(D);
        Vector3 criticalPointCoords = new Vector3((float)criticalPoint[0], (float)criticalPoint[1], (float)quadratic(criticalPoint[0], criticalPoint[1])); // plug back into quadratic to get z value of critical point

        // Step two, get second order derivatives

        double dxx = 2 * coefficients[0];
        double dyy = 2 * coefficients[1];
        double dxy = coefficients[2];
        
        // Step three, use hessian matrix to get eigenvector in principle curvature directions

        Matrix<double> H = CreateMatrix.Dense<double>(2,2);
        H[0, 0] = dxx;
        H[0, 1] = dxy;
        H[1, 0] = dyy;
        H[1, 1] = dxy;

        Vector3 eigenvector = new Vector3((float)H.Evd().EigenVectors[0,0], (float)H.Evd().EigenVectors[0, 1]); 

        double fx = eigenvector[0];
        double fy = eigenvector[1]; 

        // Z value is the magnitude of the eigenvector
        double fz = eigenvector.magnitude;

        // Final vector we want placed on the face should have origin at critical point
        Vector3 curvature = new Vector3((float)(fx - criticalPointCoords[0]), (float)(fy - criticalPointCoords[1]), (float)(fz - criticalPointCoords[2])).normalized;

        return (curvature, residual);

    }

    public void DerivativeTester()
    {
        Func<double, double, double> quadratic = (x, y) => 2 * Mathf.Pow((float)x, 2) + 6 * Mathf.Pow((float)y, 2) + (-9) * x * y + 5 * y + 8 * x + 0;

        Func<double,double,double> dx = Differentiate.PartialDerivative2Func(quadratic,0,1);
        double dxy = Differentiate.PartialDerivative2(dx, 0, 0, 1, 1);


        Debug.Log(dxy);
    }

}

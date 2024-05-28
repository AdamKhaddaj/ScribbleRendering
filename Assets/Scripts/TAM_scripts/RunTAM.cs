using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class RunTAM : MonoBehaviour
{
    // This class just runs the TAM generator since its not a monobehaviour

    public Texture2D strokeTex;
    public bool skew;

    TAMGeneratorQuick tamgen;

    bool first = true;
    void Start()
    {
        tamgen = new TAMGeneratorQuick(strokeTex, skew);
    }

    private void Update()
    {
        if (first)
        {
            
            first = false;
            Profiler.BeginSample("TAM generation");
            tamgen.RunTAM();
            Profiler.EndSample();

        }
    }

}

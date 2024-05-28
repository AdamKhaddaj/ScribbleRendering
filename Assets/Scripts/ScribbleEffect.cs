using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This class switches out the TAM textures used to create the scribbly effect, and applies noise to the sobel outlines
public class ScribbleEffect : MonoBehaviour
{
    public Material scribble;
    public Material Hatching;
    public Material handHatching;
    public Material bunnyHatching;

    int hatchTexToggle = 1;
    public Texture brightTam1, brightTam2, brightTam3, darkTam1, darkTam2, darkTam3;

    int frames = 0;
    int texChangeFrames = 0;

    [Range(1.1f, 2f)]  [SerializeField] float wiggleRange = 1.3f;

    [Range(0.002f, 0.02f)] [SerializeField] float protrusionRange = 0.006f;

    [Range(20f, 80f)] [SerializeField] float levelRange = 40f;

    [Range(1f, 60f)] [SerializeField] float frameRange = 20f;

    [Range(1f, 60f)] [SerializeField] float texFrameRange = 20f;

    [Range(1f, 1.5f)] [SerializeField] float offsetChangeX = 1.2f;
    [Range(1f, 1.5f)] [SerializeField] float offsetChangeY = 1.2f;

    [Range(-0.1f, 0.1f)] [SerializeField] float lightChange = 0f;


    void Update()
    {
        // Outline Noise
        frames++;
        if (frames >= frameRange)
        {

            float newWiggle = Random.Range(1.1f, wiggleRange);
            //float newProtrusion = Random.Range(0.002f, protrusionRange);
            float newLevel = Random.Range(20f, levelRange);
            float newOffsetX = Random.Range(1f, offsetChangeX);
            float newOffsetY = Random.Range(1f, offsetChangeY);

            //float newLight = Mathf.Clamp(Random.Range(-0.1f, 0.1f), 0, 1);

            scribble.SetFloat("_NoiseWiggleLevel", newWiggle);
            //scribble.SetFloat("_NoiseProtrusionLevel", newProtrusion);
            scribble.SetFloat("_NoiseLevel", newLevel);


            frames = 0;
        }

        // TAM Noise
        texChangeFrames++;
        if (texChangeFrames >= texFrameRange)
        {
            if (hatchTexToggle == 1)
            {
                Hatching.SetTexture("_BrightTAM", brightTam1);
                Hatching.SetTexture("_DarkTAM", darkTam1);
                handHatching.SetTexture("_BrightTAM", brightTam1);
                handHatching.SetTexture("_DarkTAM", darkTam1);
                bunnyHatching.SetTexture("_BrightTAM", brightTam1);
                bunnyHatching.SetTexture("_DarkTAM", darkTam1);
                hatchTexToggle++;
            }
            else if (hatchTexToggle == 2)
            {
                Hatching.SetTexture("_BrightTAM", brightTam2);
                Hatching.SetTexture("_DarkTAM", darkTam2);
                handHatching.SetTexture("_BrightTAM", brightTam2);
                handHatching.SetTexture("_DarkTAM", darkTam2);
                bunnyHatching.SetTexture("_BrightTAM", brightTam2);
                bunnyHatching.SetTexture("_DarkTAM", darkTam2);
                hatchTexToggle++;
            }
            else
            {
                Hatching.SetTexture("_BrightTAM", brightTam3);
                Hatching.SetTexture("_DarkTAM", darkTam3);
                handHatching.SetTexture("_BrightTAM", brightTam3);
                handHatching.SetTexture("_DarkTAM", darkTam3);
                bunnyHatching.SetTexture("_BrightTAM", brightTam3);
                bunnyHatching.SetTexture("_DarkTAM", darkTam3);
                hatchTexToggle = 1;
            }
            texChangeFrames = 0;
        }

        // Move light direction around
        float t = (Mathf.Sin(2f * Mathf.PI * Time.time / 7) + 1f) / 2f;
        float currentValue = Mathf.Lerp(-5, 5, t);
        Hatching.SetVector("_LightDir", new Vector4(currentValue, 1, 1, 0));
        handHatching.SetVector("_LightDir", new Vector4(currentValue, 1, 1, 0));
        bunnyHatching.SetVector("_LightDir", new Vector4(currentValue, 1, 1, 0));
    }
}

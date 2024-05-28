using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;

public class TAMPackerQuick : MonoBehaviour
{
    string folderPath = "C:\\Users\\khadd\\Unity Projects\\HandDrawnShaderTest\\Assets\\Textures\\CurvedTAMs\\Curved_TAMs3";
    int mipLevels = 4;
    int toneLevels = 6;
    int resolution = 256;

    Texture2D[,] textures;

    private void Start()
    {
        textures = new Texture2D[mipLevels, toneLevels];
        GetTextures();
        PackTAMS();
    }
    public void GetTextures()
    {
        string[] imagePaths = Directory.GetFiles(folderPath, "*.png");

        for(int i = 0; i < imagePaths.Length; i++)
        {
            int res = GetMipResolution(i % mipLevels);
            Texture2D tex = new Texture2D(res, res);
            tex.LoadImage(File.ReadAllBytes(imagePaths[i]));
            textures[i % mipLevels, i/mipLevels] = tex;
        }
    }

    public void PackTAMS()
    {
        // This will output a total of eight packed textures. The first four are the combined first three tone levels for mips 1,2,3,4
        // and the last 4 and the combined last three tone levels for mips 1,2,3,4
        for (int i = 0; i < mipLevels; i++)
        {
            int res = GetMipResolution(i);
            Texture2D t1 = new Texture2D(res, res); //First three tones
            Texture2D t2 = new Texture2D(res, res); //Second three tones

            Color[] t1pixels = new Color[res * res];
            Color[] t2pixels = new Color[res * res];


            // first tex
            for (int j = 0; j < 3; j++)
            {
                Color[] texPixels = textures[i, j].GetPixels();
                for (int k = 0; k < res * res; k++)
                {
                    if (texPixels[k].a != 0) // if there is a stroke there
                    {
                        t1pixels[k].a += texPixels[k].a;

                        if (j == 0) // pack into red
                        {
                            t1pixels[k].r = 1;
                        }
                        if (j == 1) // pack into green
                        {
                            t1pixels[k].g = 1;
                        }
                        if (j == 2) // [ack into blue
                        {
                            t1pixels[k].b = 1;
                        }
                    }
                }
            }

            // second tex
            for (int j = 3; j < 6; j++)
            {
                Color[] texPixels = textures[i, j].GetPixels();
                for (int k = 0; k < res * res; k++)
                {
                    if (texPixels[k].a != 0) // if there is a stroke there
                    {
                        t2pixels[k].a += texPixels[k].a;
                        if (j == 3) // pack into red
                        {
                            t2pixels[k].r = 1;
                        }
                        if (j == 4) // pack into green
                        {
                            t2pixels[k].g = 1;
                        }
                        if (j == 5) // [ack into blue
                        {
                            t2pixels[k].b = 1;
                        }
                    }
                }
            }

            t1.SetPixels(t1pixels);
            t1.Apply();
            t2.SetPixels(t2pixels);
            t2.Apply();

            byte[] bytes = t1.EncodeToPNG();
            string path = @"C:\\Users\\khadd\\Unity Projects\\HandDrawnShaderTest\\Assets\\Textures\\CurvedTAMs\\Curved_TAMs3_packed\\Curved_TAM_package_bright_mip + " + i +".png";
            File.WriteAllBytes(path, bytes);

            bytes = t2.EncodeToPNG();
            path = @"C:\\Users\\khadd\\Unity Projects\\HandDrawnShaderTest\\Assets\\Textures\\CurvedTAMs\\Curved_TAMs3_packed\\Curved_TAM_package_dark_mip + " + i + ".png";
            File.WriteAllBytes(path, bytes);
            Debug.Log("Wrote TAMs");
        }
    }

    private int GetMipResolution(int mipLvl)
    {
        return (int)(resolution * Mathf.Pow(2, -(mipLevels - mipLvl) + 1));
    }
}

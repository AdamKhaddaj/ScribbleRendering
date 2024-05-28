using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class TAMGeneratorQuick
{
    struct Stroke
    {
        public int x, y, width, height, angle;
        public bool vert;
    }

    // These can all be changed depending on what you need (make sure resolution is a power of 2)
    int resolution = 256;
    int mipLevels = 4;
    int toneLevels = 6;
    float minTone = 0.2f;
    float maxTone = 0.95f;

    int baseStrokeHeight = 11; // Note! This should be changed depending on what base stroke texture you use
    int baseStrokeWidth = 256; // This one too, except stroke texture width should really be resolution size

    // Storing what tone values we want at each tone level
    float[] tones;

    // Determines if we add small random rotations to stroke textures as we draw them (generally makes it look better, except if your base stroke texture is already kinda curved)
    bool skew;

    List<List<Color[]>> TAMtextures = new List<List<Color[]>>();

    public Texture2D strokeTex;
    public Color[] strokeTexPixels;

    System.Random rnd = new System.Random();

    public TAMGeneratorQuick(Texture2D strokeTex, bool skew)
    {
        this.strokeTex = strokeTex;
        strokeTexPixels = strokeTex.GetPixels();
        tones = new float[toneLevels];
        // Set tones at each tone level to be evenly incremented
        float toneIncrement = (maxTone - minTone) / (toneLevels - 1);
        for (int i = 0; i < toneLevels; i++)
        {
            tones[i] = minTone + i * toneIncrement;
        }
        this.skew = skew;
    }

    public void RunTAM()
    {
        MakeTAM();
    }

    private void MakeTAM()
    {
        // This will iterate such that strokes are first drawn in the lightest tone, smallest mip, to keep TAM stroke hierarchy
        for (int i = 0; i < toneLevels; i++)
        {
            if (i != 0) // Copy strokes from previous tone textures if we're not starting at the first tone level
            {
                TAMtextures.Add(new List<Color[]>());
                for (int j = 0; j < mipLevels; j++)
                {
                    TAMtextures[i].Add((Color[])TAMtextures[i - 1][j].Clone());
                }
            }
            // Otherwise, add empty textures
            else
            {
                TAMtextures.Add(new List<Color[]>());
                for (int j = 0; j < mipLevels; j++)
                {
                    TAMtextures[i].Add(new Color[GetMipResolution(j) * GetMipResolution(j)]);
                }
            }
            for (int j = 0; j < mipLevels; j++)
            {
                // Get tone before we add any candidate strokes
                float currentTone = GetTone(TAMtextures[i][j]);
                List<Stroke> candidateStrokes = new List<Stroke>();
                int count = 0;
                while (currentTone < tones[i])
                {
                    // At every iteration, make a number of candidate strokes and take the one with the most "goodness" determined by how much darkness it adds. 
                    // Lighter tones should have more candidates, where stroke placement matters more. Darker tones can have less.
                    float numCandidates = 1000 - (150 * i);
                    for (int k = 0; k < numCandidates; k++)
                    {
                        // Once we're halfway through our tones, start adding vertical lines
                        if (i >= toneLevels / 2)
                        {
                            candidateStrokes.Add(CreateCandidateStroke(j, true));
                        }
                        else
                        {
                            candidateStrokes.Add(CreateCandidateStroke(j, false));
                        }
                    }

                    // Now, while the tone value is under the desired amount, take highest "goodness" value, put that in the image, repeat until
                    // desired tone is achieved.
                    float highestGoodness = -1;
                    int bestStroke = -1;
                    for (int k = 0; k < candidateStrokes.Count; k++)
                    {
                        float goodness = GetStrokeGoodnessPixels(candidateStrokes[k], j, i);
                        if (goodness > highestGoodness)
                        {
                            bestStroke = k;
                            highestGoodness = goodness;
                        }
                    }
                    // Should never happen
                    if (bestStroke == -1)
                    {
                        Debug.Log("Error on mip level" + j + " and tone level: " + i);
                        break;
                    }
                    else
                    {
                        // Actually add the stroke to the TAM texture for this mip and tone level
                        DrawStrokes(candidateStrokes[bestStroke], j, i);
                    }
                    candidateStrokes.Clear();
                    currentTone = GetTone(TAMtextures[i][j]);
                    count++;
                    if(count > 100)
                    {
                        Debug.Log("out!");
                        break;
                    }
                }
            }
        }
        // Finally, draw Texture2D's to images
        for (int i = 0; i < toneLevels; i++)
        {
            for (int j = 0; j < mipLevels; j++)
            {
                SaveTexToPNG(TAMtextures[i][j], j, i);
            }
        }

    }

    private Stroke CreateCandidateStroke(int mipLvl, bool vert)
    {
        int mipResolution = GetMipResolution(mipLvl);
        Stroke candidate = new Stroke();

        //Set random position, width, angle
        candidate.x = rnd.Next(0, mipResolution + 1);
        candidate.y = rnd.Next(0, mipResolution + 1);
        candidate.width = rnd.Next(mipResolution / 3, mipResolution + 1);
        candidate.height = baseStrokeHeight;
        candidate.vert = vert;
        if (skew)
        {
            candidate.angle = 0;
        }
        else
        {
            candidate.angle = rnd.Next(-2, 3);
        }
        return candidate;
    }

    private void DrawStrokes(Stroke stroke, int mipLvl, int toneLvl)
    {
        // Draw strokes to all TAM textures in this mip level and lower mip levels
        for (int i = mipLvl; i < mipLevels; i++)
        {
            Stroke newStroke = stroke;
            // Adjust stroke for mip level
            if (i > mipLvl)
            {
                newStroke.x = (int)(newStroke.x * Mathf.Pow(2, (i - mipLvl)));
                newStroke.y = (int)(newStroke.y * Mathf.Pow(2, (i - mipLvl)));
                newStroke.width = (int)(newStroke.width * Mathf.Pow(2, i - mipLvl));
                DrawStroke(TAMtextures[toneLvl][i], newStroke, i);
            }
            else
            {
                DrawStroke(TAMtextures[toneLvl][i], newStroke, i);
            }
        }
    }

    private void DrawStroke(Color[] tam, Stroke stroke, int mipLvl)
    {

        // Adjust width of stroke tex
        Color[] newStroke = Resize(strokeTexPixels, stroke.width);

        // Rotate it if its vertical or has skew or both
        if (stroke.vert)
        {
            
            newStroke = Rotate(newStroke, 90 + stroke.angle, stroke.width);
            float theta = Mathf.Deg2Rad * (90 + stroke.angle);
            int oldWidth = stroke.width;
            int oldHeight = stroke.height;
            stroke.width = Mathf.CeilToInt(Mathf.Abs(oldWidth * Mathf.Cos(theta)) + Mathf.Abs(oldHeight * Mathf.Sin(theta)));
            stroke.height = Mathf.CeilToInt(Mathf.Abs(oldWidth * Mathf.Sin(theta)) + Mathf.Abs(oldHeight * Mathf.Cos(theta)));

        }
        else if (stroke.angle != 0)
        {
            
            newStroke = Rotate(newStroke, stroke.angle, stroke.width);
            float theta = Mathf.Deg2Rad * stroke.angle;
            int oldWidth = stroke.width;
            int oldHeight = stroke.height;
            stroke.width = Mathf.CeilToInt(Mathf.Abs(oldWidth * Mathf.Cos(theta)) + Mathf.Abs(oldHeight * Mathf.Sin(theta)));
            stroke.height = Mathf.CeilToInt(Mathf.Abs(oldWidth * Mathf.Sin(theta)) + Mathf.Abs(oldHeight * Mathf.Cos(theta)));
            
        }

        int res = GetMipResolution(mipLvl);

        for (int i = 0; i < stroke.height; i++)
        {
            for (int j = 0; j < stroke.width; j++)
            {
                int strokeIndex = i * stroke.width + j;
                int tamIndex = ((stroke.y + i) % res) * res + ((stroke.x + j) % res);

                if (newStroke[strokeIndex].a != 0)
                {
                    tam[tamIndex] = new Vector4(0f, 0f, 0f, Mathf.Clamp(newStroke[strokeIndex].a + tam[tamIndex].a, 0, 1));
                }
            }
        }
    }

    private float GetStrokeGoodnessPixels(Stroke stroke, int mipLvl, int toneLvl) // Using pixel vals
    {
        
        float goodness = 0;
        Stroke newStroke = stroke;

        // Adjust width of stroke tex
        Color[] candidatePixels = Resize(strokeTexPixels, newStroke.width);

        //if vertical texture, or if it has a skew, rotate it

        if (newStroke.vert)
        {
            candidatePixels = Rotate(candidatePixels, 90 + newStroke.angle, newStroke.width);
            float theta = Mathf.Deg2Rad * (90 + newStroke.angle);
            int oldWidth = newStroke.width;
            int oldHeight = newStroke.height;
            newStroke.width = Mathf.CeilToInt(Mathf.Abs(oldWidth * Mathf.Cos(theta)) + Mathf.Abs(oldHeight * Mathf.Sin(theta)));
            newStroke.height = Mathf.CeilToInt(Mathf.Abs(oldWidth * Mathf.Sin(theta)) + Mathf.Abs(oldHeight * Mathf.Cos(theta)));
        }
        else if (newStroke.angle != 0)
        {
            candidatePixels = Rotate(candidatePixels, newStroke.angle, newStroke.width);
            float theta = Mathf.Deg2Rad * newStroke.angle;
            int oldWidth = newStroke.width;
            int oldHeight = newStroke.height;
            newStroke.width = Mathf.CeilToInt(Mathf.Abs(oldWidth * Mathf.Cos(theta)) + Mathf.Abs(oldHeight * Mathf.Sin(theta)));
            newStroke.height = Mathf.CeilToInt(Mathf.Abs(oldWidth * Mathf.Sin(theta)) + Mathf.Abs(oldHeight * Mathf.Cos(theta)));
        }

        Color[] tam = (Color[])TAMtextures[toneLvl][mipLvl].Clone();
        int res = GetMipResolution(mipLvl);

        // get tone before we add the stroke
        float preTone = GetTone(tam);

        for (int j = 0; j < newStroke.height; j++)
        {
            for (int k = 0; k < newStroke.width; k++)
            {
                int strokeIndex = j * newStroke.width + k;
                int tamIndex = ((j + newStroke.y) % res) * res + ((k + newStroke.x) % res);

                // For the first two tone levels, only accept strokes with no overlap

                if(toneLvl <= 1 && tam[tamIndex].a > 0)
                {
                    return 0;
                }

                if (candidatePixels[strokeIndex].a != 0)
                {
                    tam[tamIndex] = new Vector4(0f, 0f, 0f, Mathf.Clamp(candidatePixels[strokeIndex].a + tam[tamIndex].a, 0, 1));
                }
            }
        }
        float posttone = GetTone(tam);
        goodness = posttone - preTone;

        return goodness;
    }
    public float GetTone(Color[] pixels)
    {
        // Sum black pixels 
        float tone = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            //since 0 is black
            tone += pixels[i].a;
        }
        // Normalize it to be a value between 0 and 1
        return tone / (float)pixels.Length;

    }

    private int GetMipResolution(int mipLvl)
    {
        return (int)(resolution * Mathf.Pow(2, -(mipLevels - mipLvl) + 1));
    }

    private void SaveTexToPNG(Color[] tex, int mipLvl, int toneLvl)
    {
        Texture2D TAM = new Texture2D(GetMipResolution(mipLvl), GetMipResolution(mipLvl));
        TAM.SetPixels(tex);
        TAM.Apply();
        byte[] bytes = TAM.EncodeToPNG();
        string path = @"C:\\Users\\khadd\\Unity Projects\\HandDrawnShaderTest\\Assets\\Textures\\CurvedTAMs\\Curved_TAMs_Test\\TAM_Tone" + toneLvl + "_Mip" + mipLvl + ".png";
        File.WriteAllBytes(path, bytes);
        Debug.Log("Wrote TAM mip level: " + mipLvl + " and Tone level: " + toneLvl);
    }

    private Color[] Rotate(Color[] tex, int angle, int width)
    {
        //Fix angle
        float theta = Mathf.Deg2Rad * angle;

        // Calculate new size of the rotated texture
        int newWidth = Mathf.CeilToInt(Mathf.Abs(width * Mathf.Cos(theta)) + Mathf.Abs(baseStrokeHeight * Mathf.Sin(theta)));
        int newHeight = Mathf.CeilToInt(Mathf.Abs(width * Mathf.Sin(theta)) + Mathf.Abs(baseStrokeHeight * Mathf.Cos(theta)));

        Color[] result = new Color[newWidth * newHeight];

        int newCenterX = (newWidth-1) / 2;
        int newCenterY = (newHeight-1) / 2;
        int oldCenterX = (width-1) / 2;
        int oldCenterY = (baseStrokeHeight - 1) / 2;


        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                float x0 = x - newCenterX;
                float y0 = y - newCenterY;
                // apply  rotation matrix
                float rotatedX = Mathf.Cos(theta) * x0 + Mathf.Sin(theta) * y0;
                float rotatedY = Mathf.Cos(theta) * y0 - Mathf.Sin(theta) * x0;

                //Translate back
                float originalX = rotatedX + oldCenterX;
                float originalY = rotatedY + oldCenterY;

                // check bounds
                if (originalX >= 0 && originalX < width && originalY >= 0 && originalY < baseStrokeHeight)
                {
                    // Get pixel color from the input texture, do interpolation
                    Color pixelColor = BilinearInterpolation(tex, width, originalX, originalY);
                    result[y * newWidth + x] = pixelColor;
                }
                else
                {
                    // If the pixel is out of bounds, set it to transparent
                    result[y * newWidth + x] = Color.clear;
                }
            }
        }
        return result;
    }


    private Color BilinearInterpolation(Color[] source, int width, float x, float y)
    {
        // Partially my code here, partially from an online forum post

        int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, width - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt(x), 0, width - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, baseStrokeHeight - 1);
        int y1 = Mathf.Clamp(Mathf.CeilToInt(y), 0, baseStrokeHeight - 1);
        float dx = x - x0;
        float dy = y - y0;

        Color c00 = source[y0 * width + x0];
        Color c10 = source[y0 * width + x1];
        Color c01 = source[y1 * width + x0];
        Color c11 = source[y1 * width + x1];

        // Try not counting low alpha strokes

        if(c00.a < 0.3)
        {
            c00.a = 0;
        }
        if (c10.a < 0.3)
        {
            c10.a = 0;
        }
        if (c01.a < 0.3)
        {
            c01.a = 0;
        }
        if (c11.a < 0.3)
        {
            c11.a = 0;
        }

        Color interpolatedColor = Color.Lerp(Color.Lerp(c00, c10, dx), Color.Lerp(c01, c11, dx), dy);

        return interpolatedColor;
    }

    private Color[] Resize(Color[] source, int targetWidth)
    {
        // Partially my code here, partially from an online forum post
        Color[] result = new Color[targetWidth * baseStrokeHeight];
        float incX = (1.0f / ((float)targetWidth / (baseStrokeWidth - 1)));
        float incY = (1.0f / ((float)strokeTex.height / (baseStrokeHeight - 1)));

        for (int i = 0; i < baseStrokeHeight; i++)
        {
            for (int j = 0; j < targetWidth; j++)
            {
                float sourceX = j * incX;
                float sourceY = i * incY;

                Color newColor = BilinearInterpolation(source, baseStrokeWidth, sourceX, sourceY);

                result[i * targetWidth + j] = newColor;
            }
        }
        return result;
    }
}
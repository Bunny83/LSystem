#region License and Information
/*****
* HilbertCurve.cs
* ---------------
* 
* This is just a simple example usage of the LSystem to generate a texture
* filled with a Hilbert curve. The whole definition of the Hilbert curve as
* a Lindenmayer system is just:
* 
* Axiom: A
* Rules:
*     A --> L; B; F; R; A; F; A; R; F; B; L
*     B --> R; A; F; L; B; F; B; L; F; A; R
*
* 
* 
* [License]
* Copyright (c) 2017 Markus Göbel (Bunny83)
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to
* deal in the Software without restriction, including without limitation the
* rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
* sell copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
* FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
* IN THE SOFTWARE.
* 
*****/
#endregion License and Information

using B83.LSystem.StringBased;
using System.Collections.Generic;
using UnityEngine;

public class HilbertCurve : MonoBehaviour
{
    public int textureWidth = 256;
    public int textureHeight = 256;
    public int iterations = 7;

    /* ----------
     * Axiom: A
     * Rules:
     * A --> L B F R A F A R F B L
     * B --> R A F L B F B L F A R 
     * ---------
     * F=forward, L=turn left, R=turn right
     * 
     **/
    public static List<Module> Generate(int aOrder)
    {
        LSystemParser parser = new LSystemParser();
        LSystem sys = parser.ParseSystem(@"
Axiom: A
Rules:
    A --> L; B; F; R; A; F; A; R; F; B; L
    B --> R; A; F; L; B; F; B; L; F; A; R
            ");
        sys.Iterate(aOrder);

        string last = "";
        List<Module> result = new List<Module>(sys.symbols.Count);
        // Filter out the symbols "A" and "B" and also remove pointless rotations
        // So an "L" followed by a "R" is pointless so we can remove both.
        foreach (var m in sys.symbols)
        {
            if (m.Name == "L" || m.Name == "R" || m.Name == "F")
            {
                if ((last == "L" && m.Name == "R") || (last == "R" && m.Name == "L"))
                {
                    result.RemoveAt(result.Count - 1);
                    if (result.Count > 0)
                        last = result[result.Count - 1].Name;
                    else
                        last = "";
                }
                else
                {
                    result.Add(m);
                    last = m.Name;
                }
            }
        }
        return result;
    }
    // draw the resulting commands into a texture.
    public static void DrawCurve(List<Module> aCommandString, Texture2D aTex)
    {
        int width = aTex.width;
        int height = aTex.height;
        var data = new Color32[width * height];
        // fill image white
        for (int i = 0; i < data.Length; i++)
            data[i] = new Color32(255, 255, 255, 255);
        // turtle graphic cursor
        var p = Vector2.zero;
        var dir = Vector2.right;
        float tmp;
        foreach (var c in aCommandString)
        {
            switch (c.Name)
            {
                case "L": // rotate counter clockwise
                    tmp = dir.x;
                    dir.x = -dir.y;
                    dir.y = tmp;
                    break;
                case "R": // rotate clockwise
                    tmp = dir.x;
                    dir.x = dir.y;
                    dir.y = -tmp;
                    break;
                case "F": // draw forward two pixels at a time.
                    for (int i = 0; i < 2; i++)
                    {
                        int x = Mathf.RoundToInt(p.x);
                        int y = Mathf.RoundToInt(p.y);
                        if (x >= 0 && x < width && y >= 0 && y < height)
                            data[x + y * width] = new Color32(0, 0, 0, 255);
                        p += dir;
                    }
                    break;
            }
        }
        aTex.SetPixels32(data);
        aTex.Apply();
    }

    void Start()
    {
        var tex = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);

        var commands = Generate(iterations);
        DrawCurve(commands, tex);

        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.GetComponent<Renderer>().material.mainTexture = tex;
        q.transform.localScale = new Vector3(10,10,1);
    }
}

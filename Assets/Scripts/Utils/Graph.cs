using UnityEngine;
using System.Collections.Generic;
using System;


/*
 * Todo: Generalize this thing for use with arbitrary variables changing over time. Use reflection to hook it up in inspector?
 */

public class DebugGraph {
    private IList<IList<double>> _data;
    private readonly int _bufferSize;
    private int _index;

    private IList<Color> _colors;
    private float _hScale = 50f;

    public DebugGraph(IList<IList<double>> data, IList<Color> colors) {
        if (data.Count != colors.Count) {
            throw new ArgumentException("The number of specified colors needs to match the number of lines in the graph");
        }
        _data = data;
        _colors = colors;
    }

    public void Draw() {
        const float width = 900f;
        const float height = 400f;
        float wStep = width / _data[0].Count;
        float halfHeight = height * 0.5f;
        const int sampleStep = 1;

        // Generate a single pixel texture if it doesn't exist
        if (!_lineTex) {
            _lineTex = new Texture2D(1, 1);
            _lineTex.SetPixel(0, 0, Color.white);
            _lineTex.Apply();
        }
    
        GUILayout.BeginArea(new Rect(0f, 0f, width, height), GUI.skin.box);
        {
            DrawLine(new Vector2(0f, halfHeight), new Vector2(width, halfHeight), Color.white, 1f);
    
            for (int i = 0; i < _data.Count; i++) {
                var data = _data[i];
                for (int j = 0; j < data.Count - sampleStep; j += sampleStep) {
                    DrawLine(
                        new Vector2(j * wStep, halfHeight + (float)data[j] * _hScale),
                        new Vector2((j + (float)sampleStep) * wStep, halfHeight + (float)data[j + sampleStep] * _hScale),
                        _colors[i],
                        1f);
                }
            }
        }
        _hScale = GUILayout.HorizontalSlider(_hScale, 0.01f, 1000f);
        
        GUILayout.EndArea();
    }

    private static Texture2D _lineTex;

    static void DrawLine(Vector2 pointA, Vector2 pointB, Color color, float width) {
        // Save the current GUI matrix, since we're going to make changes to it.
        var matrix = GUI.matrix;

        // Store current GUI color, so we can switch it back later,
        // and set the GUI color to the color parameter
        var savedColor = GUI.color;
        GUI.color = color;

        // Determine the angle of the line.
        var angle = Vector3.Angle(pointB - pointA, Vector2.right);

        // Vector3.Angle always returns a positive number.
        // If pointB is above pointA, then angle needs to be negative.
        if (pointA.y > pointB.y) { angle = -angle; }

        // Use ScaleAroundPivot to adjust the size of the line.
        // We could do this when we draw the texture, but by scaling it here we can use
        //  non-integer values for the width and length (such as sub 1 pixel widths).
        // Note that the pivot point is at +.5 from pointA.y, this is so that the width of the line
        //  is centered on the origin at pointA.
        GUIUtility.ScaleAroundPivot(new Vector2((pointB - pointA).magnitude, width), new Vector2(pointA.x, pointA.y + 0.5f));

        // Set the rotation for the line.
        //  The angle was calculated with pointA as the origin.
        GUIUtility.RotateAroundPivot(angle, pointA);

        // Finally, draw the actual line.
        // We're really only drawing a 1x1 texture from pointA.
        // The matrix operations done with ScaleAroundPivot and RotateAroundPivot will make this
        //  render with the proper width, length, and angle.
        GUI.DrawTexture(new Rect(pointA.x, pointA.y, 1f, 1f), _lineTex);

        // We're done.  Restore the GUI matrix and GUI color to whatever they were before.
        GUI.matrix = matrix;
        GUI.color = savedColor;
    }
}
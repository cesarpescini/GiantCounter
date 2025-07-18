using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using SharpDX;
using Color = SharpDX.Color;

namespace GiantCounter
{
    internal static class Util
    {
        private static Random random = new Random(Guid.NewGuid().GetHashCode());

        public static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        public static Color HslToRgb(float h, float s = 1f, float l = 0.6f)
        {
            h = h % 360.0f; // Ensure hue is within 0-360 degrees
            s = Clamp(s, 0f, 1f); // Clamp saturation between 0 and 1
            l = Clamp(l, 0f, 1f); // Clamp lightness between 0 and 1

            float c = (1 - Math.Abs(2 * l - 1)) * s;
            float x = c * (1 - Math.Abs((h / 60f) % 2 - 1));
            float m = l - c / 2f;

            float r, g, b;

            if (h < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (h < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (h < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (h < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (h < 300)
            {
                r = x; g = 0; b = c;
            }
            else
            {
                r = c; g = 0; b = x;
            }

            // Adjust RGB values to be between 0 and 1
            r += m;
            g += m;
            b += m;

            return new Color(r, g, b, 1.0f); // Full opacity
        }


        public static int Random(int min, int max)
        {
            return random.Next(min, max);
        }

        public static SharpDX.Vector2 OffCenter(int radius)
        {
            return new SharpDX.Vector2(-radius, radius);
        }

        public static System.Drawing.Point Vec2ToPoint(SharpDX.Vector2 vec2)
        {
            return new System.Drawing.Point((int)vec2.X, (int)vec2.Y);
        }

        public static System.Drawing.Point CalculateLinearPointWithCurve(float t, System.Drawing.Point p0, System.Drawing.Point p1, float curveFactor)
        {
            // Linear interpolation
            int x = (int)(p0.X + t * (p1.X - p0.X));
            int y = (int)(p0.Y + t * (p1.Y - p0.Y));

            // Subtract curvature to move upwards in screen coordinates
            int curveOffset = (int)(Math.Sin(t * Math.PI) * curveFactor);
            y -= curveOffset;

            // Ensure y does not go below the linear path
            if (p1.Y > p0.Y)
            {
                // Moving downwards, y increases, so y with curve should not be greater than linear y
                y = Math.Min(y, (int)(p0.Y + t * (p1.Y - p0.Y)));
            }
            else
            {
                // Moving upwards, y decreases, so y with curve should not be less than linear y
                y = Math.Max(y, (int)(p0.Y + t * (p1.Y - p0.Y)));
            }

            return new System.Drawing.Point(x, y);
        }

   
    }
}

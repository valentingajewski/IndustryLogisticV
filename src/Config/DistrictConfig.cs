using System.Collections.Generic;
using GTA.Math;

namespace LSOL.Config
{
    public sealed class DistrictConfig
    {
        public DistrictConfig()
        {
            PolygonVertices = new List<Vector2>();
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public List<Vector2> PolygonVertices { get; }

        public Vector2 GetCentroid()
        {
            if (PolygonVertices.Count == 0)
            {
                return Vector2.Zero;
            }

            float sumX = 0f;
            float sumY = 0f;
            for (int i = 0; i < PolygonVertices.Count; i++)
            {
                sumX += PolygonVertices[i].X;
                sumY += PolygonVertices[i].Y;
            }

            return new Vector2(sumX / PolygonVertices.Count, sumY / PolygonVertices.Count);
        }

        public bool Contains(Vector2 point)
        {
            if (PolygonVertices.Count < 3)
            {
                return false;
            }

            var inside = false;
            for (int i = 0, j = PolygonVertices.Count - 1; i < PolygonVertices.Count; j = i++)
            {
                var pi = PolygonVertices[i];
                var pj = PolygonVertices[j];
                var intersects = ((pi.Y > point.Y) != (pj.Y > point.Y))
                    && (point.X < ((pj.X - pi.X) * (point.Y - pi.Y) / ((pj.Y - pi.Y) == 0f ? 0.0001f : (pj.Y - pi.Y)) + pi.X));
                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
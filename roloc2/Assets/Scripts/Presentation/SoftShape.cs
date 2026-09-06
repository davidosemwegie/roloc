using UnityEngine;
using UnityEngine.UI;

namespace Roloc.Presentation
{
    /// <summary>Resolution-independent UI artwork. No textures, shaders or post-processing required.</summary>
    [AddComponentMenu("ROLOC/Soft Shape")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SoftShape : MaskableGraphic
    {
        public enum Shape { Disc, Ring, Panel, Arc }
        public Shape kind;
        [Range(0.05f, 0.9f)] public float thickness = 0.25f;
        [Range(0, 1)] public float progress = 1;
        public float cornerRadius = 18;
        public bool shaded = true;
        public bool shadow = true;
        [Range(0, 12)] public float depth = 6;
        const int Segments = 96;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = GetPixelAdjustedRect();
            if (kind == Shape.Panel) { Panel(mesh, r); return; }
            float radius = Mathf.Min(r.width, r.height) * 0.5f - (shadow ? 5 : 1);
            var center = r.center;
            bool ring = kind == Shape.Ring || kind == Shape.Arc;
            float inner = ring ? radius * (1 - thickness) : 0;
            float sweep = kind == Shape.Arc ? progress : 1;
            if (sweep <= 0) return;
            if (shadow)
            {
                Band(mesh, center + Vector2.down * 5, radius + 3, ring ? inner - 2 : 0,
                    new Color(0.14f, 0.17f, 0.22f, color.a * 0.035f), false, sweep);
                Band(mesh, center + Vector2.down * 4, radius + 1, ring ? inner : 0,
                    new Color(0.14f, 0.17f, 0.22f, color.a * 0.08f), false, sweep);
            }
            if (shaded && depth > 0)
                Band(mesh, center + Vector2.down * depth, radius, inner,
                    new Color(color.r * .65f, color.g * .65f, color.b * .72f, color.a), false, sweep);
            Band(mesh, center, radius, inner, color, shaded, sweep);
            if (shaded)
            {
                Color rim = Color.Lerp(color, Color.white, .12f); rim.a = color.a;
                Band(mesh, center, radius, radius - .8f, rim, false, sweep);
            }
        }

        static void Band(VertexHelper mesh, Vector2 center, float outer, float inner, Color tint, bool gradient, float sweep)
        {
            int count = Mathf.Max(2, Mathf.CeilToInt(Segments * sweep));
            int start = mesh.currentVertCount;
            for (int i = 0; i <= count; i++)
            {
                float angle = Mathf.PI / 2 - i / (float)count * Mathf.PI * 2 * sweep;
                Vector2 normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                // Evaluate lighting at each vertex's actual height. Every center vertex
                // gets the same color, so solid pucks have a flat face, not a cone.
                mesh.AddVert(center + normal * outer, Shade(tint, gradient, normal.y), Vector2.zero);
                mesh.AddVert(center + normal * inner, Shade(tint, gradient, normal.y * inner / outer), Vector2.zero);
                if (i == count) continue;
                int a = start + i * 2;
                mesh.AddTriangle(a, a + 2, a + 1);
                mesh.AddTriangle(a + 1, a + 2, a + 3);
            }
        }

        static Color Shade(Color tint, bool gradient, float height)
        {
            if (!gradient) return tint;
            return Color.Lerp(tint * new Color(.95f, .95f, .98f, 1),
                Color.Lerp(tint, new Color(1, 1, 1, tint.a), .035f), height * .5f + .5f);
        }

        void Panel(VertexHelper mesh, Rect r)
        {
            float radius = Mathf.Min(cornerRadius, Mathf.Min(r.width, r.height) / 2);
            if (shadow)
            {
                Rounded(mesh, new Rect(r.x, r.y - depth - 3, r.width, r.height), radius,
                    new Color(.16f, .21f, .34f, .06f * color.a));
                Rounded(mesh, new Rect(r.x, r.y - depth, r.width, r.height), radius,
                    Color.Lerp(color, new Color(.15f, .18f, .28f, color.a), .22f));
            }
            Rounded(mesh, r, radius, color);
        }

        static void Rounded(VertexHelper mesh, Rect r, float radius, Color tint)
        {
            int centerIndex = mesh.currentVertCount;
            mesh.AddVert(r.center, tint, Vector2.zero);
            const int steps = 12;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 center = new Vector2(corner < 2 ? r.xMax - radius : r.xMin + radius,
                    corner == 0 || corner == 3 ? r.yMax - radius : r.yMin + radius);
                for (int j = 0; j <= steps; j++)
                {
                    float a = (90 - corner * 90 - j * 90f / steps) * Mathf.Deg2Rad;
                    mesh.AddVert(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, tint, Vector2.zero);
                }
            }
            int vertices = 4 * (steps + 1);
            for (int i = 0; i < vertices; i++) mesh.AddTriangle(centerIndex, centerIndex + 1 + i,
                centerIndex + 1 + (i + 1) % vertices);
        }
    }
}

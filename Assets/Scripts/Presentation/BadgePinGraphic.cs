using UnityEngine;
using UnityEngine.UI;

namespace Roloc.Presentation
{
    /// <summary>Flat enamel pin artwork: strong silhouettes, inset enamel and a ring-shaped face.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BadgePinGraphic : MaskableGraphic
    {
        public int Tier;
        public bool Locked;
        public bool Lifetime;
        static readonly Color Navy = new Color32(32, 35, 68, 255);
        static readonly Color Cream = new Color32(255, 248, 227, 255);
        static readonly Color[] Enamels = {
            new Color32(255, 120, 31, 255), new Color32(49, 93, 255, 255),
            new Color32(197, 237, 50, 255), new Color32(255, 62, 135, 255) };

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / 100;
            Color enamel = Locked ? (Color)new Color32(199, 207, 218, 255) : Enamels[Tier % 4];
            Color edge = Locked ? new Color32(147, 157, 174, 255) : Navy;
            Color face = Locked ? new Color32(228, 233, 240, 255) : Cream;
            int shape = Lifetime ? 1 : Tier < 2 ? 0 : Tier < 5 ? 1 : Tier < 8 ? 2 : 3;
            if (Tier >= (Lifetime ? 8 : 10))
            {
                Polygon(mesh, new[] { new Vector2(-31, 27), new Vector2(-36, 48), new Vector2(-14, 38),
                    new Vector2(0, 55), new Vector2(14, 38), new Vector2(36, 48), new Vector2(31, 27) }, edge, scale);
                Polygon(mesh, new[] { new Vector2(-26, 29), new Vector2(-30, 42), new Vector2(-12, 34),
                    new Vector2(0, 48), new Vector2(12, 34), new Vector2(30, 42), new Vector2(26, 29) }, enamel, scale);
            }
            Silhouette(mesh, shape, 44, Vector2.down * 3, edge, scale);
            Silhouette(mesh, shape, 44, Vector2.zero, edge, scale);
            Silhouette(mesh, shape, 40, Vector2.zero, enamel, scale);
            Disc(mesh, 31, Vector2.zero, edge, scale);
            Disc(mesh, 27, Vector2.zero, face, scale);
            // Rivets and enamel ring mark, shared across the collection.
            Disc(mesh, 2, new Vector2(-35, 0), face, scale);
            Disc(mesh, 2, new Vector2(35, 0), face, scale);
            Disc(mesh, 6, new Vector2(0, 17), enamel, scale);
            Disc(mesh, 3, new Vector2(0, 17), face, scale);
            if (Lifetime)
            {
                Disc(mesh, 4, new Vector2(6, 17), enamel, scale);
                Disc(mesh, 2, new Vector2(6, 17), face, scale);
            }
            if (Tier >= 2)
                for (int i = 0; i < Mathf.Min(5, 1 + Tier / 2); i++)
                    Disc(mesh, 1.5f, new Vector2((i - (Mathf.Min(5, 1 + Tier / 2) - 1) * .5f) * 6, -18), enamel, scale);
        }

        static void Silhouette(VertexHelper mesh, int kind, float r, Vector2 offset, Color tint, float scale)
        {
            int n = kind == 1 ? 6 : kind == 2 ? 12 : kind == 3 ? 20 : 64;
            var points = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = (90 + i * 360f / n) * Mathf.Deg2Rad;
                float radius = r * (kind >= 2 && i % 2 != 0 ? .85f : 1);
                points[i] = offset + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            Polygon(mesh, points, tint, scale);
        }
        static void Disc(VertexHelper mesh, float radius, Vector2 center, Color tint, float scale)
        {
            var points = new Vector2[48];
            for (int i = 0; i < points.Length; i++)
            {
                float a = i * Mathf.PI * 2 / points.Length;
                points[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            Polygon(mesh, points, tint, scale);
        }
        static void Polygon(VertexHelper mesh, Vector2[] points, Color tint, float scale)
        {
            int start = mesh.currentVertCount;
            Vector2 center = Vector2.zero;
            foreach (var p in points) center += p;
            center /= points.Length;
            mesh.AddVert(center * scale, tint, Vector2.zero);
            foreach (var p in points) mesh.AddVert(p * scale, tint, Vector2.zero);
            for (int i = 0; i < points.Length; i++) mesh.AddTriangle(start, start + i + 1, start + (i + 1) % points.Length + 1);
        }
    }
}

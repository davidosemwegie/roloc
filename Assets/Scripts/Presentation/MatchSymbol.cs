using UnityEngine;
using UnityEngine.UI;

namespace Roloc.Presentation
{
    /// <summary>Vector symbols remain recognizable without hue or font coverage.</summary>
    public sealed class MatchSymbol : MaskableGraphic
    {
        public int Symbol;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float r = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .45f;
            if (Symbol == 0) Polygon(mesh, r, 3, 90);
            else if (Symbol == 1) Polygon(mesh, r, 4, 45);
            else if (Symbol == 2) { Quad(mesh, -r, -r * .25f, r, r * .25f); Quad(mesh, -r * .25f, -r, r * .25f, r); }
            else Polygon(mesh, r * .8f, 24, 0);
        }
        void Polygon(VertexHelper mesh, float r, int sides, float angle)
        {
            mesh.AddVert(Vector3.zero, color, Vector2.zero);
            for (int i = 0; i < sides; i++)
            {
                float a = (angle + i * 360f / sides) * Mathf.Deg2Rad;
                mesh.AddVert(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, color, Vector2.zero);
            }
            for (int i = 0; i < sides; i++) mesh.AddTriangle(0, i + 1, (i + 1) % sides + 1);
        }
        void Quad(VertexHelper mesh, float left, float bottom, float right, float top)
        {
            int i = mesh.currentVertCount;
            mesh.AddVert(new Vector2(left, bottom), color, Vector2.zero); mesh.AddVert(new Vector2(left, top), color, Vector2.zero);
            mesh.AddVert(new Vector2(right, top), color, Vector2.zero); mesh.AddVert(new Vector2(right, bottom), color, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
    }
}

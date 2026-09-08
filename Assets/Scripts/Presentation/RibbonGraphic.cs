using UnityEngine;
using UnityEngine.UI;

namespace Roloc.Presentation
{
    /// <summary>A reusable, short-lived UI ribbon with a matching collection preview.</summary>
    [AddComponentMenu("ROLOC/Ribbon Graphic")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RibbonGraphic : MaskableGraphic
    {
        const int Capacity = 64;
        const int PreviewSamples = 40;
        const float Lifetime = .26f;
        const float SampleDistance = 6f;
        const float HalfWidth = 5f;
        readonly Vector2[] points = new Vector2[Capacity];
        readonly float[] times = new float[Capacity];
        readonly Vector2[] renderPoints = new Vector2[Capacity];
        readonly float[] widths = new float[Capacity];
        readonly float[] opacity = new float[Capacity];
        int count;
        bool preview;

        public int PointCount => count;
        public bool Preview
        {
            get => preview;
            set
            {
                if (preview == value) return;
                preview = value;
                Clear();
                SetVerticesDirty();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        protected override void OnDisable()
        {
            Clear();
            base.OnDisable();
        }

        public void Clear()
        {
            if (count == 0) return;
            count = 0;
            SetVerticesDirty();
        }

        public void AddPoint(Vector2 localPosition)
        {
            if (preview || !isActiveAndEnabled || !Finite(localPosition)) return;
            float now = Time.unscaledTime;
            Prune(now);
            if (count == 0)
            {
                Append(localPosition, now);
                SetVerticesDirty();
                return;
            }
            Vector2 previous = points[count - 1];
            float distance = Vector2.Distance(previous, localPosition);
            if (distance < .1f || float.IsInfinity(distance)) return;
            float previousTime = times[count - 1];
            // Bound work for teleports as well as ordinary fast drags.
            int steps = Mathf.CeilToInt(Mathf.Min(distance / SampleDistance, Capacity - 1));
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                Append(Vector2.Lerp(previous, localPosition, t), Mathf.Lerp(previousTime, now, t));
            }
            SetVerticesDirty();
        }

        static bool Finite(Vector2 point) => !float.IsNaN(point.x) && !float.IsNaN(point.y)
            && !float.IsInfinity(point.x) && !float.IsInfinity(point.y);

        void Append(Vector2 point, float time)
        {
            if (count == Capacity)
            {
                for (int i = 1; i < count; i++)
                {
                    points[i - 1] = points[i];
                    times[i - 1] = times[i];
                }
                count--;
            }
            points[count] = point;
            times[count++] = time;
        }

        void Prune(float now)
        {
            int expired = 0;
            while (expired < count && now - times[expired] >= Lifetime) expired++;
            if (expired == 0) return;
            count -= expired;
            for (int i = 0; i < count; i++)
            {
                points[i] = points[i + expired];
                times[i] = times[i + expired];
            }
        }

        void Update()
        {
            if (preview || count == 0) return;
            Prune(Time.unscaledTime);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            int samples = preview ? PreviewSamples : count;
            if (samples < 2) return;
            Rect rect = GetPixelAdjustedRect();
            float now = Time.unscaledTime;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / (samples - 1);
                if (preview)
                {
                    // An S-shaped strip leaves room for its full width at every edge.
                    renderPoints[i] = rect.center + new Vector2(Mathf.Sin(t * Mathf.PI * 2) * rect.width * .27f,
                        (.5f - t) * rect.height * .76f);
                    widths[i] = Mathf.Min(rect.width, rect.height) * .09f
                        * Mathf.Sqrt(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)));
                    opacity[i] = 1;
                }
                else
                {
                    renderPoints[i] = points[i];
                    float life = Mathf.Clamp01(1 - (now - times[i]) / Lifetime);
                    widths[i] = HalfWidth * Mathf.Sqrt(t) * Mathf.Sqrt(life);
                    opacity[i] = life;
                }
            }
            for (int i = 0; i < samples; i++)
            {
                Vector2 incoming = i > 0 ? renderPoints[i] - renderPoints[i - 1] : renderPoints[1] - renderPoints[0];
                Vector2 outgoing = i < samples - 1 ? renderPoints[i + 1] - renderPoints[i] : incoming;
                Vector2 tangent = incoming.normalized + outgoing.normalized;
                // Averaged normals, never a miter: reversals cannot produce long spikes.
                if (tangent.sqrMagnitude < .0001f) tangent = outgoing.sqrMagnitude > .0001f ? outgoing : incoming;
                if (tangent.sqrMagnitude < .0001f) tangent = Vector2.up;
                tangent.Normalize();
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                for (int column = 0; column < 5; column++)
                {
                    float across = column == 0 ? -1 : column == 1 ? -.65f : column == 2 ? 0 : column == 3 ? .65f : 1;
                    Color tint = column == 2 ? Color.Lerp(color, Color.white, .3f) : color;
                    tint.a = color.a * opacity[i] * (column == 0 || column == 4 ? 0 : 1);
                    mesh.AddVert(renderPoints[i] + normal * (widths[i] * across), tint,
                        new Vector2((across + 1) * .5f, (float)i / (samples - 1)));
                }
                if (i == 0) continue;
                int start = (i - 1) * 5;
                for (int column = 0; column < 4; column++)
                {
                    int v = start + column;
                    mesh.AddTriangle(v, v + 5, v + 1);
                    mesh.AddTriangle(v + 1, v + 5, v + 6);
                }
            }
        }
    }
}

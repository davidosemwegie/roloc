using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Roloc.Presentation
{
    public sealed class PuckView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, ICancelHandler
    {
        public int ColorIndex { get; private set; }
        public bool IsDragging { get; private set; }
        public bool ReduceEffects { get; set; }
        public bool MotionPaused { get; set; }
        public bool BoardTransitioning { get; set; }
        public bool FollowHomeExactly { get; set; }
        public Vector2 IdleOffset { get; set; }
        public Vector2 Home { get; set; }
        public RectTransform Rect => (RectTransform)transform;
        public Func<bool> CanDrag;
        public Action<PuckView> Released;
        public Action<PuckView> Moved;
        int pointerId;
        Vector2 grabOffset;
        CanvasGroup group;
        bool highlighted, highlightInitialized;
        Color baseTint;
        SoftShape face;
        float activeDepth;
        bool activeShading;
        float returnVelocityX, returnVelocityY;

        public void Configure(int color, Color tint)
        {
            ColorIndex = color; baseTint = tint; highlightInitialized = false;
            face = GetComponent<SoftShape>();
            activeDepth = face.depth; activeShading = face.shaded; face.color = tint;
            group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        }

        public void SetTint(Color tint)
        {
            baseTint = tint; highlightInitialized = false; SetHighlighted(highlighted);
        }

        public void SetHighlighted(bool active)
        {
            if (highlightInitialized && highlighted == active) return;
            highlighted = active; highlightInitialized = true;
            if (group) group.alpha = 1;
            // Inactive pucks keep only a faint color hint; full color and depth identify the correct puck.
            var tint = baseTint; tint.a = active ? 1f : .18f;
            face.shaded = active && activeShading;
            face.depth = active ? activeDepth : 0;
            face.color = tint;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (IsDragging || CanDrag == null || !CanDrag()) return;
            pointerId = e.pointerId;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform.parent,
                e.position, e.pressEventCamera, out var point);
            grabOffset = Rect.anchoredPosition - point;
            IsDragging = true;
            transform.SetAsLastSibling();
            returnVelocityX = returnVelocityY = 0;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!IsDragging || e.pointerId != pointerId) return;
            if (CanDrag == null || !CanDrag()) { CancelDrag(); return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform.parent,
                e.position, e.pressEventCamera, out var point);
            Rect.anchoredPosition = point + grabOffset;
            Moved?.Invoke(this);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!IsDragging || e.pointerId != pointerId) return;
            if (e is ExtendedPointerEventData touchEvent && touchEvent.device is Touchscreen screen)
            {
                foreach (var touch in screen.touches)
                    if (touch.touchId.ReadValue() == touchEvent.touchId &&
                        touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
                    { CancelDrag(); return; }
            }
            // Apply the release location even when no final drag event was dispatched.
            OnDrag(e);
            if (!IsDragging) return;
            IsDragging = false;
            Released?.Invoke(this);
        }

        public void OnCancel(BaseEventData e) => CancelDrag();
        public void CancelDrag() { IsDragging = false; }
        public void SnapHome()
        {
            CancelDrag();
            Rect.anchoredPosition = Home + IdleOffset;
            returnVelocityX = returnVelocityY = 0;
        }
        void OnDisable() { CancelDrag(); }

        void Update()
        {
            if (MotionPaused) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            if (!IsDragging && !BoardTransitioning)
            {
                if (FollowHomeExactly) Rect.anchoredPosition = Home + IdleOffset;
                Vector2 p = Rect.anchoredPosition;
                Vector2 target = Home + IdleOffset;
                Rect.anchoredPosition = new Vector2(
                    Mathf.SmoothDamp(p.x, target.x, ref returnVelocityX, .075f, Mathf.Infinity, dt),
                    Mathf.SmoothDamp(p.y, target.y, ref returnVelocityY, .075f, Mathf.Infinity, dt));
            }
            float targetScale = IsDragging ? 1.12f : highlighted ? 1.02f + (ReduceEffects ? 0 : Mathf.Sin(Time.unscaledTime * 3.5f) * .025f) : .88f;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, dt * 18);
        }
    }
}

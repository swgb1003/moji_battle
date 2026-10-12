using UnityEngine;
using UnityEngine.EventSystems;

namespace MojiBattle
{
    /// <summary>Capture the initiating pointer until release, including when it leaves the preview.</summary>
    public sealed class GripDragHandle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        CustomizeScreen screen;
        int? pointer;
        bool moved;
        public void Bind(CustomizeScreen owner) { screen = owner; }
        public void OnPointerEnter(PointerEventData e) => screen.SetPreviewPointerOver(true);
        public void OnPointerExit(PointerEventData e) => screen.SetPreviewPointerOver(false);
        public void OnPointerDown(PointerEventData e)
        {
            if (pointer.HasValue || e.button != PointerEventData.InputButton.Left) return;
            if (screen.BeginGripDrag(e.position, e.pressEventCamera)) { pointer = e.pointerId; moved = false; }
        }
        public void OnBeginDrag(PointerEventData e)
        {
            if (pointer != e.pointerId) return;
            moved = true; screen.DragGrip(e.position, e.pressEventCamera);
        }
        public void OnDrag(PointerEventData e)
        {
            if (pointer != e.pointerId) return;
            screen.DragGrip(e.position, e.pressEventCamera);
        }
        public void OnPointerUp(PointerEventData e) => Release(e);
        public void OnEndDrag(PointerEventData e) => Release(e);
        void Release(PointerEventData e)
        {
            if (pointer != e.pointerId) return;
            pointer = null;
            screen.EndGripDrag(e.position, e.pressEventCamera, moved);
        }
        void OnDisable()
        {
            pointer = null;
            if (screen != null) { screen.CancelGripDrag(); screen.SetPreviewPointerOver(false); }
        }
    }
}

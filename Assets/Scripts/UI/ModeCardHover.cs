using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MojiBattle
{
    // Pointer events belong to the entire card, including its nested action button.
    public sealed class ModeCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        ModeSelectScreen screen;
        public RectTransform Rect => (RectTransform)transform;
        public void Bind(ModeSelectScreen owner, UguiKit.ChoiceButton hit, UguiKit.ChoiceButton start)
        {
            screen = owner;
            hit.button.transition = Selectable.Transition.None;
            hit.image.canvasRenderer.SetColor(Color.white);
            hit.image.color = Color.clear;
            hit.image.GetComponent<Outline>().enabled = false;
            hit.image.GetComponent<RetroButtonMotion>().enabled = false;
            var navigation = hit.button.navigation; navigation.mode = Navigation.Mode.None; hit.button.navigation = navigation;
            // Preserve button colors and pressed feedback, while the shared frame supplies focus feedback.
            PaperUi.Card(Rect);
            foreach (var button in new[] { hit.button, start.button })
            {
                var trigger = button.gameObject.AddComponent<EventTrigger>();
                var select = new EventTrigger.Entry { eventID = EventTriggerType.Select };
                select.callback.AddListener(_ => screen.SelectCard(this)); trigger.triggers.Add(select);
                var deselect = new EventTrigger.Entry { eventID = EventTriggerType.Deselect };
                deselect.callback.AddListener(_ => screen.DeselectCard(this)); trigger.triggers.Add(deselect);
            }
        }
        public void OnPointerEnter(PointerEventData e) => screen.HoverCard(this);
        public void OnPointerExit(PointerEventData e) => screen.ExitCard(this);
        void OnDisable()
        {
            if (screen == null) return;
            screen.ExitCard(this); screen.DeselectCard(this);
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MojiBattle
{
    // The card owns its hover state, so entering its nested start button keeps the same highlight.
    public sealed class ChallengeCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        ChallengeScreen screen;
        Image plate;
        Text label;
        bool pointerInside;
        float emphasis;
        public Vector2 Position=>((RectTransform)transform).anchoredPosition;

        public void Bind(ChallengeScreen owner,UguiKit.ChoiceButton hit,UguiKit.ChoiceButton start)
        {
            screen=owner; plate=start.image; label=start.label;
            PaperUi.Card((RectTransform)transform);
            Configure(hit); Configure(start);
            var nav=hit.button.navigation;nav.mode=Navigation.Mode.None;hit.button.navigation=nav;
            Apply();
        }
        void Configure(UguiKit.ChoiceButton choice)
        {
            // This card animates color and the shared frame; avoid resizing the hit area or start button.
            choice.button.transition=Selectable.Transition.None;
            choice.image.canvasRenderer.SetColor(Color.white);
            choice.image.GetComponent<Outline>().enabled=false;
            choice.image.GetComponent<RetroButtonMotion>().enabled=false;
            var trigger=choice.button.gameObject.AddComponent<EventTrigger>();
            var select=new EventTrigger.Entry {eventID=EventTriggerType.Select};
            select.callback.AddListener(_=>screen.Focus(this));trigger.triggers.Add(select);
            var deselect=new EventTrigger.Entry {eventID=EventTriggerType.Deselect};
            deselect.callback.AddListener(_=>{if(!pointerInside)screen.Unfocus(this);});trigger.triggers.Add(deselect);
        }
        public void OnPointerEnter(PointerEventData e){pointerInside=true;screen.Focus(this);}
        public void OnPointerExit(PointerEventData e){pointerInside=false;screen.Unfocus(this);}
        void OnDisable(){pointerInside=false;if(screen!=null)screen.Unfocus(this);}
        void Update()
        {
            float target=screen!=null && screen.IsFocused(this)?1:0;
            float step=RetroMotionSettings.Reduced?1:1-Mathf.Exp(-18*Time.unscaledDeltaTime);
            emphasis=Mathf.Lerp(emphasis,target,step);Apply();
        }
        void Apply()
        {
            if(plate==null)return;
            var idle=RetroUi.Paper;idle.a=0;
            plate.color=Color.Lerp(idle,RetroUi.Red,emphasis);
            label.color=Color.Lerp(RetroUi.Muted,RetroUi.Paper,emphasis);
        }
    }
}

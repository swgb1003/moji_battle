using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>A quiet ink circle behind the VS mark, drawn at any display resolution.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DraftInkRing : MaskableGraphic
    {
        protected override void Awake(){base.Awake();raycastTarget=false;}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var rect=rectTransform.rect;
            var center=rect.center;float radius=Mathf.Min(rect.width,rect.height)*.44f;
            const int segments=192;
            for(int stroke=0;stroke<3;stroke++)
            {
                int start=vh.currentVertCount;
                for(int i=0;i<=segments;i++)
                {
                    float t=i/(float)segments*Mathf.PI*2;var dir=new Vector2(Mathf.Cos(t),Mathf.Sin(t));
                    float wobble=Mathf.Sin(t*5+stroke)*3+Mathf.Sin(t*17+stroke)*1.4f;
                    float r=radius+stroke*7+wobble;float width=3+Mathf.Sin(t*9+stroke)*1.2f;
                    var ink=new Color(RetroUi.Muted.r,RetroUi.Muted.g,RetroUi.Muted.b,.24f);
                    vh.AddVert(center+dir*(r-width),ink,Vector2.zero);vh.AddVert(center+dir*(r+width),ink,Vector2.zero);
                    if(i==0)continue;int v=start+(i-1)*2;vh.AddTriangle(v,v+1,v+3);vh.AddTriangle(v,v+3,v+2);
                }
            }
        }
    }
}

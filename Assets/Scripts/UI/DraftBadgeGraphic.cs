using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>Standalone letter-card medals: no atlas cropping, at every badge size.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DraftBadgeGraphic : MaskableGraphic
    {
        public int badgeId=57;
        protected override void Awake() { base.Awake(); raycastTarget=false; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect=GetPixelAdjustedRect();
            Vector2 P(Vector2 p) => new Vector2(rect.xMin+p.x*rect.width,rect.yMax-p.y*rect.height);
            void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
            {
                int n=vh.currentVertCount;
                foreach(var p in new[]{a,b,c,d}) vh.AddVert(P(p),tint,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
            }
            void Stroke(Vector2 a,Vector2 b,float width,Color tint)
            {
                var delta=(b-a).normalized; var normal=new Vector2(-delta.y,delta.x)*width*.5f;
                Quad(a+normal,b+normal,b-normal,a-normal,tint);
                // A subpixel fringe softens the diagonal card borders and brush strokes.
                var feather=normal.normalized*.006f; var clear=tint; clear.a=0;
                int n=vh.currentVertCount;
                foreach(var p in new[]{a+normal,b+normal,b+normal+feather,a+normal+feather})
                    vh.AddVert(P(p),vh.currentVertCount-n<2?tint:clear,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
                n=vh.currentVertCount;
                foreach(var p in new[]{a-normal,b-normal,b-normal-feather,a-normal-feather})
                    vh.AddVert(P(p),vh.currentVertCount-n<2?tint:clear,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
            void Card(float x,float y,float angle,float w,float h,Color ink)
            {
                var rot=Quaternion.Euler(0,0,angle);
                Vector2 At(float px,float py) => new Vector2(x,y)+(Vector2)(rot*new Vector2(px,py));
                var a=At(-w/2,-h/2);var b=At(w/2,-h/2);var c=At(w/2,h/2);var d=At(-w/2,h/2);
                Quad(a,b,c,d,RetroUi.Paper);
                Stroke(a,b,.019f,ink);Stroke(b,c,.019f,ink);Stroke(c,d,.019f,ink);Stroke(d,a,.019f,ink);
                Stroke(At(-w/2+.04f,-h/2+.04f),At(w/2-.04f,-h/2+.04f),.005f,ink);
                Stroke(At(-w/2+.04f,h/2-.04f),At(w/2-.04f,h/2-.04f),.005f,ink);
            }
            void Crown()
            {
                var a=new Vector2(.29f,.10f);var b=new Vector2(.39f,.17f);var c=new Vector2(.50f,.04f);
                var d=new Vector2(.61f,.17f);var e=new Vector2(.71f,.10f);
                Quad(a,b,new Vector2(.50f,.25f),new Vector2(.33f,.25f),RetroUi.Red);
                Quad(b,c,d,new Vector2(.50f,.25f),RetroUi.Red);
                Quad(d,e,new Vector2(.67f,.25f),new Vector2(.50f,.25f),RetroUi.Red);
                Stroke(new Vector2(.33f,.28f),new Vector2(.67f,.28f),.026f,RetroUi.Ink);
            }
            if(badgeId==59 || badgeId==61)
                Card(.33f,.52f,-14,.43f,.61f,badgeId==61?RetroUi.Blue:RetroUi.Red);
            if(badgeId>=62)
            {
                if(badgeId==63)
                {Card(.28f,.56f,-23,.35f,.56f,RetroUi.Blue);Card(.72f,.56f,23,.35f,.56f,RetroUi.Red);}
                Card(.35f,.54f,-13,.39f,.61f,RetroUi.Blue);Card(.65f,.54f,13,.39f,.61f,RetroUi.Red);
            }
            Card(.50f,.57f,0,.53f,.66f,RetroUi.Ink);
            if(badgeId==58 || badgeId==64) Crown();
            else if(badgeId==60)
            {
                Stroke(new Vector2(.28f,.23f),new Vector2(.46f,.10f),.026f,RetroUi.Red);
                Stroke(new Vector2(.46f,.10f),new Vector2(.70f,.08f),.026f,RetroUi.Red);
                Stroke(new Vector2(.70f,.08f),new Vector2(.61f,.04f),.026f,RetroUi.Red);
                Stroke(new Vector2(.70f,.08f),new Vector2(.64f,.16f),.026f,RetroUi.Red);
            }
            else if(badgeId==61)
            {
                Stroke(new Vector2(.30f,.11f),new Vector2(.65f,.11f),.018f,RetroUi.Blue);
                Stroke(new Vector2(.65f,.11f),new Vector2(.59f,.06f),.018f,RetroUi.Blue);
                Stroke(new Vector2(.65f,.11f),new Vector2(.59f,.16f),.018f,RetroUi.Blue);
            }
            int marks=badgeId==59?2:badgeId==63?3:1;
            for(int i=0;i<marks;i++)
            {
                float x=.5f+(i-(marks-1)*.5f)*.11f;
                Quad(new Vector2(x-.033f,.80f),new Vector2(x+.033f,.80f),new Vector2(x+.033f,.865f),new Vector2(x-.033f,.865f),RetroUi.Red);
            }
        }
    }
}

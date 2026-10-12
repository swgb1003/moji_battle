using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>Ink rings, laurel leaves and ribbon for the six challenge medals.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ChallengeBadgeGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r=GetPixelAdjustedRect(); float scale=Mathf.Min(r.width,r.height);
            Vector2 Point(Vector2 p) => new Vector2(r.center.x+p.x*scale,r.center.y+p.y*scale);
            void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
            {
                int index=vh.currentVertCount;
                foreach(var p in new[]{a,b,c,d}) vh.AddVert(Point(p),color,Vector2.zero);
                vh.AddTriangle(index,index+1,index+2); vh.AddTriangle(index,index+2,index+3);
            }
            void Ring(float radius,float width)
            {
                for(int i=0;i<96;i++)
                {
                    float a=i*Mathf.PI*2/96,b=(i+1)*Mathf.PI*2/96;
                    Vector2 pa=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),pb=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                    Quad(pa*radius,pb*radius,pb*(radius-width),pa*(radius-width));
                }
            }
            Ring(.36f,.014f); Ring(.333f,.006f);
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<8;i++)
                {
                    float a=(-70+i*20)*Mathf.Deg2Rad;
                    Vector2 center=new Vector2(side*.415f*Mathf.Cos(a),.415f*Mathf.Sin(a));
                    Vector2 along=new Vector2(side*Mathf.Cos(a+.55f),Mathf.Sin(a+.55f))*.056f;
                    Vector2 across=new Vector2(-along.y,along.x)*.33f;
                    int start=vh.currentVertCount;
                    for(int j=0;j<16;j++)
                    {
                        float t=j<8?j/7f:(15-j)/7f;
                        var p=center+along*(2*t-1)+across*Mathf.Sin(t*Mathf.PI)*(j<8?1:-1);
                        vh.AddVert(Point(p),color,Vector2.zero);
                    }
                    for(int j=1;j<15;j++)vh.AddTriangle(start,start+j,start+j+1);
                }
                for(int i=0;i<48;i++)
                {
                    float a=(-72+i*144f/48)*Mathf.Deg2Rad,b=(-72+(i+1)*144f/48)*Mathf.Deg2Rad;
                    Vector2 pa=new Vector2(side*Mathf.Cos(a),Mathf.Sin(a)),pb=new Vector2(side*Mathf.Cos(b),Mathf.Sin(b));
                    Quad(pa*.397f,pb*.397f,pb*.390f,pa*.390f);
                }
                Quad(new Vector2(side*.15f,-.30f),new Vector2(side*.30f,-.29f),
                    new Vector2(side*.31f,-.47f),new Vector2(side*.18f,-.42f));
            }
        }
        protected override void Awake() { base.Awake(); raycastTarget=false; }
    }
}

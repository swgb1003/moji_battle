using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public static class AchievementBadge
    {
        static Texture2D atlas;
        static Texture2D comeback;
        // Measured cell boundaries of the approved generated atlas (1774 x 887).
        // Cells have different widths: a uniform grid samples neighbouring medals.
        static readonly int[] Rows={0,190,363,537,710,887};
        static readonly int[][] Columns={
            new[]{0,174,333,497,662,823,991,1155,1321,1485,1640},
            new[]{0,174,337,498,662,825,990,1159,1341,1574,1774},
            new[]{0,177,343,510,680,844,1014,1201,1391,1571,1774},
            new[]{0,177,344,518,683,850,1029,1215,1390,1575,1774},
            new[]{0,172,338,507,677,847,1021,1200,1388,1571,1774}
        };
        public static Rect PixelRect(int id)
        {
            if (id < 1 || id > 50) throw new System.ArgumentOutOfRangeException(nameof(id));
            int index=id-1, row=index/10, col=index%10;
            return new Rect(Columns[row][col], Rows[row], Columns[row][col+1]-Columns[row][col], Rows[row+1]-Rows[row]);
        }
        public static RectTransform Create(Transform parent, int id, bool acquired, float x, float y, float size)
        {
            var root=RetroUi.Group(parent,acquired?"Badge_"+id:"LockedBadge",x,y,size,size);
            if (!acquired)
            {
                root.gameObject.AddComponent<AchievementSymbolGraphic>().color=RetroUi.Muted;
                RetroUi.Label(root,"Question","?",0,0,size,size,Mathf.RoundToInt(size*.45f),RetroUi.Muted,TextAnchor.MiddleCenter);
                return root;
            }
            if(id>=65 && id<=68)
            {
                root.gameObject.AddComponent<ChallengeBadgeGraphic>().color=RetroUi.Ink;
                RetroUi.Label(root,"RunRewardGlyph",new[]{"五","耐","育","極"}[id-65],size*.23f,size*.18f,size*.54f,size*.56f,Mathf.RoundToInt(size*.44f),RetroUi.Ink,TextAnchor.MiddleCenter);
                return root;
            }
            if (id>=51 && id<=56)
            {
                root.gameObject.AddComponent<ChallengeBadgeGraphic>().color=RetroUi.Ink;
                string glyph=new[]{"一","小","破","撃","声","挑"}[id-51];
                var text=RetroUi.Label(root,"RewardGlyph",glyph,size*.23f,size*.18f,size*.54f,size*.56f,
                    Mathf.RoundToInt(size*.44f),RetroUi.Ink,TextAnchor.MiddleCenter);
                text.font=GlyphFontLibrary.Default?.Get(FontStyleId.Brush)?.font ?? RetroUi.Font;
                return root;
            }
            if (id>=57 && id<=64)
            {
                var cards=root.gameObject.AddComponent<DraftBadgeGraphic>(); cards.badgeId=id;
                var glyph=RetroUi.Label(root,"DraftRewardGlyph",new[]{"縁","王","完","逆","後","即","変","極"}[id-57],
                    size*.27f,size*.28f,size*.46f,size*.48f,Mathf.RoundToInt(size*.40f),RetroUi.Ink,TextAnchor.MiddleCenter);
                glyph.font=GlyphFontLibrary.Default?.Get(FontStyleId.Brush)?.font ?? RetroUi.Font;
                return root;
            }
            // This original cell overlaps an extra decorative fist; use its repaired standalone artwork.
            if (id==10)
            {
                if (comeback==null) comeback=Resources.Load<Texture2D>("UI/Achievements/Badge10");
                if (comeback!=null)
                {
                    var image=root.gameObject.AddComponent<RawImage>();
                    image.texture=comeback; image.raycastTarget=false;
                    return root;
                }
            }
            if (atlas==null) atlas=Resources.Load<Texture2D>("UI/Achievements/Badges");
            if (atlas!=null)
            {
                var cell=PixelRect(id);
                var raw=root.gameObject.AddComponent<AchievementBadgeImage>();
                raw.badgeId=id;
                raw.texture=atlas; raw.raycastTarget=false;
                float width=cell.width,height=cell.height;
                raw.uvRect=new Rect(cell.x/atlas.width,1f-cell.yMax/atlas.height,width/atlas.width,height/atlas.height);
                float factor=size/Mathf.Max(width,height);
                root.sizeDelta=new Vector2(width*factor,height*factor);
                root.anchoredPosition+=new Vector2((size-root.sizeDelta.x)*.5f,-(size-root.sizeDelta.y)*.5f);
            }
            else
            {
                root.gameObject.AddComponent<AchievementSymbolGraphic>().color=RetroUi.Red;
                RetroUi.Label(root,"Glyph","文",0,0,size,size,Mathf.RoundToInt(size*.4f),RetroUi.Ink,TextAnchor.MiddleCenter);
            }
            return root;
        }
    }

    /// <summary>Clip neighbouring artwork where long emblems touch the next atlas cell.</summary>
    public sealed class AchievementBadgeImage : RawImage
    {
        public int badgeId;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if (badgeId!=28 && badgeId!=34) { base.OnPopulateMesh(vh); return; }
            vh.Clear();
            // The upper-left fragments belong to the previous badge's long diagonal brush.
            // Badge 28 also touches the left edge of badge 29 halfway down its right edge.
            float inset=badgeId==28 ? 5f/190f : 0f;
            var points=new[]{new Vector2(.35f,1),new Vector2(1,1),new Vector2(1,.72f),
                new Vector2(1-inset,.60f),new Vector2(1-inset,0),new Vector2(0,0),new Vector2(0,.65f)};
            var rect=GetPixelAdjustedRect();
            foreach(var p in points)
                vh.AddVert(new Vector3(rect.xMin+p.x*rect.width,rect.yMin+p.y*rect.height,0),color,
                    new Vector2(uvRect.xMin+p.x*uvRect.width,uvRect.yMin+p.y*uvRect.height));
            for(int i=1;i<points.Length-1;i++) vh.AddTriangle(0,i,i+1);
        }
    }

    /// <summary>Small vector marks for unknown medals and the menu's distinct friend connection icon.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AchievementSymbolGraphic : MaskableGraphic
    {
        public bool friends;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!friends) { Ring(vh,.5f,.46f,.39f,.027f,color); Ring(vh,.5f,.46f,.34f,.009f,color); return; }
            Circle(vh,.20f,.29f,.09f,RetroUi.Red); Circle(vh,.80f,.29f,.09f,RetroUi.Blue);
            Shoulder(vh,.20f,RetroUi.Red); Shoulder(vh,.80f,RetroUi.Blue);
            Ring(vh,.46f,.48f,.09f,.025f,RetroUi.Ink);
            Ring(vh,.54f,.44f,.09f,.025f,RetroUi.Ink);
        }
        Vector2 P(float x,float y) { var r=rectTransform.rect; return new Vector2(r.xMin+x*r.width,r.yMax-y*r.height); }
        void Triangle(VertexHelper vh, Vector2 a,Vector2 b,Vector2 c,Color tint)
        {
            int n=vh.currentVertCount; vh.AddVert(a,tint,Vector2.zero); vh.AddVert(b,tint,Vector2.zero); vh.AddVert(c,tint,Vector2.zero); vh.AddTriangle(n,n+1,n+2);
        }
        void Circle(VertexHelper vh,float x,float y,float radius,Color tint)
        {
            for(int i=0;i<48;i++)
            { float a=i*Mathf.PI/24f,b=(i+1)*Mathf.PI/24f;
                float vertical=radius*rectTransform.rect.width/Mathf.Max(1f,rectTransform.rect.height);
                Triangle(vh,P(x,y),P(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*vertical),P(x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*vertical),tint); }
        }
        void Ring(VertexHelper vh,float x,float y,float radius,float thickness,Color tint)
        {
            for(int i=0;i<64;i++)
            {
                float a=i*Mathf.PI/32f,b=(i+1)*Mathf.PI/32f;
                var p=P(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius); var q=P(x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius);
                var u=P(x+Mathf.Cos(a)*(radius-thickness),y+Mathf.Sin(a)*(radius-thickness)); var v=P(x+Mathf.Cos(b)*(radius-thickness),y+Mathf.Sin(b)*(radius-thickness));
                Triangle(vh,p,q,u,tint); Triangle(vh,q,v,u,tint);
            }
        }
        void Shoulder(VertexHelper vh,float x,Color tint)
        {
            for(int i=0;i<24;i++)
            {
                float a=Mathf.PI+i*Mathf.PI/24f,b=Mathf.PI+(i+1)*Mathf.PI/24f;
                Triangle(vh,P(x,.62f),P(x+Mathf.Cos(a)*.17f,.62f+Mathf.Sin(a)*.22f),P(x+Mathf.Cos(b)*.17f,.62f+Mathf.Sin(b)*.22f),tint);
            }
        }
    }
}

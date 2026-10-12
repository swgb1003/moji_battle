using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>Reusable rounded geometry. It never writes to a rigidbody, joint or collider.</summary>
    public sealed class FighterVolumeRig
    {
        static Material bodyMaterial,glyphMaterial,shadowMaterial;
        readonly VolumeMesh body,front,shadow;
        readonly SpriteRenderer[] weaponEdges=new SpriteRenderer[3];
        readonly SpriteRenderer weapon;
        SpriteRenderer offhand;
        SpriteRenderer[] offhandEdges;
        readonly Material originalWeaponMaterial;
        Color team;
        Vector3[] footPins=new Vector3[2];
        bool[] planted=new bool[2];
        bool visible;
        public Vector3 MainHand {get;private set;}
        public Vector3 SupportHand {get;private set;}
        public int VertexCount=>body.VertexCount+front.VertexCount+shadow.VertexCount;
        public MeshRenderer BodyRenderer=>body.Renderer;

        public FighterVolumeRig(Transform parent,SpriteRenderer weapon,Color team)
        {
            this.weapon=weapon;this.team=team;originalWeaponMaterial=weapon.sharedMaterial;
            if(bodyMaterial==null)bodyMaterial=new Material(Resources.Load<Shader>("UI/Combat/FighterVolume"));
            if(glyphMaterial==null)glyphMaterial=new Material(Resources.Load<Shader>("UI/Combat/GlyphSurface"));
            if(shadowMaterial==null){shadowMaterial=new Material(bodyMaterial);shadowMaterial.SetFloat("_ZWrite",0);}
            body=new VolumeMesh(parent,"VolumeBody",bodyMaterial,4);
            front=new VolumeMesh(parent,"VolumeHands",bodyMaterial,6);
            shadow=new VolumeMesh(parent,"ContactShadow",shadowMaterial,0);
            for(int i=0;i<weaponEdges.Length;i++)
            {
                weaponEdges[i]=UiKit.Rect(parent,"WeaponDepth"+i,team*.38f,4);
                weaponEdges[i].sprite=weapon.sprite;
                weaponEdges[i].color=new Color(team.r*.38f,team.g*.38f,team.b*.38f,1);
            }
        }
        public void SetVisible(bool visible)
        {
            if(this.visible==visible)return;
            this.visible=visible;
            body.Renderer.enabled=front.Renderer.enabled=shadow.Renderer.enabled=visible;
            foreach(var edge in weaponEdges)edge.enabled=visible;
            if(weapon!=null)weapon.sharedMaterial=visible?glyphMaterial:originalWeaponMaterial;
            if(offhand!=null){offhand.sharedMaterial=visible?glyphMaterial:originalWeaponMaterial;foreach(var edge in offhandEdges)edge.enabled=visible;}
        }
        /// <summary>二刀流の副武器にも同じ厚みを付ける。</summary>
        public void AddOffhand(Transform parent,SpriteRenderer sprite)
        {
            offhand=sprite;offhandEdges=new SpriteRenderer[weaponEdges.Length];
            for(int i=0;i<offhandEdges.Length;i++)
            {
                offhandEdges[i]=UiKit.Rect(parent,"OffhandDepth"+i,team*.38f,2);
                offhandEdges[i].sprite=sprite.sprite;
                offhandEdges[i].color=new Color(team.r*.38f,team.g*.38f,team.b*.38f,1);
            }
            if(visible){offhand.sharedMaterial=glyphMaterial;}
            else foreach(var edge in offhandEdges)edge.enabled=false;
        }
        public void SetTeam(Color color)
        {
            team = color;
            foreach (var edge in weaponEdges) edge.color = new Color(team.r * .38f, team.g * .38f, team.b * .38f, 1);
            if (offhandEdges != null) foreach (var edge in offhandEdges) edge.color = new Color(team.r * .38f, team.g * .38f, team.b * .38f, 1);
        }
        public void Begin(Vector3 position,Vector3 hip,Vector3 neck,Vector3 head,float turn,bool down)
        {
            body.Clear();front.Clear();shadow.Clear();
            Color ink=new Color(.105f,.12f,.135f,1),dark=new Color(.065f,.075f,.09f,1);
            body.Capsule(hip,Vector3.Lerp(hip,neck,.62f),.115f,ink,.035f);
            body.Capsule(Vector3.Lerp(hip,neck,.35f),neck,.13f*Mathf.Lerp(.65f,1,Mathf.Abs(turn)),ink,.02f);
            body.Capsule(neck,head,.058f,dark,.02f);
            body.Capsule(head,head,.203f,ink,-.01f);
            body.Capsule(hip,hip,.105f,dark,.04f);
            float height=Mathf.Max(0,position.y);
            shadow.Ellipse(new Vector3(position.x,.012f,0),down?.68f:.46f,.105f,new Color(.07f,.055f,.04f,.23f*Mathf.Clamp01(1-height*.2f)));
            for(int i=0;i<weaponEdges.Length;i++)
            {
                var edge=weaponEdges[i];edge.sprite=weapon.sprite;edge.flipX=weapon.flipX;
                edge.transform.SetPositionAndRotation(weapon.transform.position+new Vector3(.014f*(i+1),-.014f*(i+1),.03f),weapon.transform.rotation);
                edge.transform.localScale=weapon.transform.localScale;
                var scale=edge.transform.localScale;
                scale.x=Mathf.Sign(scale.x)*Mathf.Max(.025f,Mathf.Abs(scale.x));edge.transform.localScale=scale;
            }
            if(offhand!=null)
                for(int i=0;i<offhandEdges.Length;i++)
                {
                    var edge=offhandEdges[i];edge.flipX=offhand.flipX;
                    edge.transform.SetPositionAndRotation(offhand.transform.position+new Vector3(.014f*(i+1),-.014f*(i+1),.03f),offhand.transform.rotation);
                    var scale=offhand.transform.localScale;
                    scale.x=Mathf.Sign(scale.x)*Mathf.Max(.025f,Mathf.Abs(scale.x));edge.transform.localScale=scale;
                }
        }
        public static Vector3 Joint(Vector3 root,Vector3 end,float upper,float lower,Vector3 bend)
        {
            Vector3 delta=end-root;delta.z=0;float distance=delta.magnitude;
            if(distance<.0001f)return root+bend.normalized*upper;
            // Preserve the actual grip even during a physical pull. Both segments
            // expand equally only when a transient hinge stretch exceeds reach.
            float extra=Mathf.Max(0,distance-upper-lower+.001f)*.5f;upper+=extra;lower+=extra;
            float along=(upper*upper-lower*lower+distance*distance)/(2*distance);
            float height=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            Vector3 direction=delta/distance,perp=new Vector3(-direction.y,direction.x,0);
            if(Vector3.Dot(perp,bend)<0)perp=-perp;
            return root+direction*along+perp*height;
        }
        public void Arm(Vector3 shoulder,Vector3 grip,Vector3 bend,bool primary,bool holding,Vector3 weaponAxis)
        {
            var mesh=primary?front:body;
            float z=primary?-.1f:.07f;
            Color ink=primary?new Color(.12f,.135f,.15f,1):new Color(.06f,.07f,.085f,1);
            Vector3 elbow=Joint(shoulder,grip,.45f,.47f,bend);
            mesh.Capsule(shoulder,elbow,.049f,ink,z);
            mesh.Capsule(elbow,grip,.047f,ink,z);
            mesh.Capsule(elbow,elbow,.053f,ink,z);
            Vector3 axis=weaponAxis.sqrMagnitude>.0001f?weaponAxis.normalized:Vector3.up;
            Vector3 across=new Vector3(-axis.y,axis.x,0);
            // The palm and fingertips straddle the exact physical grip.
            var hand=front;
            hand.Capsule(grip-axis*.05f,grip+axis*.03f,.058f,ink,-.14f);
            if(holding)
            {
                for(int i=0;i<3;i++)
                {
                    var at=grip+axis*(.018f-i*.027f);
                    hand.Capsule(at-across*.035f,at+across*.035f,.017f,new Color(.16f,.17f,.18f,1),-.18f);
                }
                hand.Capsule(grip-axis*.064f-across*.01f,grip-axis*.015f-across*.046f,.023f,ink,-.19f);
            }
            var wrist=Vector3.Lerp(grip,elbow,.14f);
            hand.Capsule(wrist,wrist,.053f,new Color(team.r*.7f,team.g*.7f,team.b*.7f,1),z-.02f);
            if(primary)MainHand=grip;else SupportHand=grip;
        }
        public Vector3 PlantFoot(int side,Vector3 desired,Vector3 hip,bool stance,bool grounded)
        {
            if(!grounded){planted[side]=false;return desired;}
            if(!stance){planted[side]=false;return desired;}
            if(!planted[side] || Mathf.Abs(footPins[side].x-hip.x)>.54f || Mathf.Abs(footPins[side].y-desired.y)>.15f)
                footPins[side]=desired;
            planted[side]=true;return footPins[side];
        }
        public void Leg(Vector3 hip,Vector3 foot,Vector3 bend,bool primary,int facing,bool ground)
        {
            if(ground)foot.y+=.045f;
            var mesh=body;float z=primary?-.01f:.075f;
            Color ink=primary?new Color(.105f,.12f,.135f,1):new Color(.055f,.065f,.08f,1);
            Vector3 knee=Joint(hip,foot,.48f,.49f,bend-Vector3.Lerp(hip,foot,.5f));
            mesh.Capsule(hip,knee,.065f,ink,z);mesh.Capsule(knee,foot,.055f,ink,z);
            mesh.Capsule(knee,knee,.065f,ink,z);
            Vector3 toe=foot+new Vector3(.105f*facing,ground?.005f:-.045f,0);
            mesh.Capsule(foot-new Vector3(.035f*facing,0,0),toe,.049f,ink,z-.015f);
        }
        public void End(){body.Upload();front.Upload();shadow.Upload();}
        public void Dispose(){body.Dispose();front.Dispose();shadow.Dispose();}

        sealed class VolumeMesh
        {
            const int Radial=16,Rings=8;
            static readonly float[] latitude={-90,-60,-30,0,0,30,60,90};
            static readonly Vector3[] normals=MakeNormals();
            static readonly int[] templateTriangles=MakeTriangles();
            readonly List<Vector3> vertices=new List<Vector3>(3000),vertexNormals=new List<Vector3>(3000);
            readonly List<Color> colors=new List<Color>(3000);
            readonly List<int> triangles=new List<int>(12000);
            readonly Mesh mesh;
            Matrix4x4 toLocal;
            public MeshRenderer Renderer {get;}
            public int VertexCount=>vertices.Count;
            public VolumeMesh(Transform parent,string name,Material material,int order)
            {
                var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
                mesh=new Mesh{name=name};mesh.MarkDynamic();go.GetComponent<MeshFilter>().sharedMesh=mesh;
                Renderer=go.GetComponent<MeshRenderer>();Renderer.sharedMaterial=material;Renderer.sortingOrder=order;
                Renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;Renderer.receiveShadows=false;
            }
            public void Clear(){vertices.Clear();vertexNormals.Clear();colors.Clear();triangles.Clear();toLocal=Renderer.transform.worldToLocalMatrix;}
            public void Capsule(Vector3 a,Vector3 b,float radius,Color color,float z=0)
            {
                float depth=z+(Renderer.sortingOrder==4?.28f:0);a.z+=depth;b.z+=depth;
                Vector3 axis=b-a;axis.z=0;axis=axis.sqrMagnitude<.00001f?Vector3.up:axis.normalized;
                Vector3 across=new Vector3(axis.y,-axis.x,0);int offset=vertices.Count;
                for(int i=0;i<normals.Length;i++)
                {
                    Vector3 n=normals[i];Vector3 worldNormal=across*n.x+axis*n.y+Vector3.forward*n.z;
                    vertices.Add(toLocal.MultiplyPoint3x4((i/Radial<4?a:b)+worldNormal*radius));
                    vertexNormals.Add(toLocal.MultiplyVector(worldNormal));colors.Add(color);
                }
                foreach(int index in templateTriangles)triangles.Add(offset+index);
            }
            public void Ellipse(Vector3 center,float width,float height,Color color)
            {
                int offset=vertices.Count;vertices.Add(toLocal.MultiplyPoint3x4(center));vertexNormals.Add(Vector3.zero);colors.Add(color);
                for(int i=0;i<32;i++)
                {
                    float angle=i*Mathf.PI/16;
                    vertices.Add(toLocal.MultiplyPoint3x4(center+new Vector3(Mathf.Cos(angle)*width,Mathf.Sin(angle)*height,0)));
                    vertexNormals.Add(Vector3.zero);colors.Add(new Color(color.r,color.g,color.b,0));
                    triangles.Add(offset);triangles.Add(offset+1+i);triangles.Add(offset+1+(i+1)%32);
                }
            }
            public void Upload(){mesh.Clear();mesh.SetVertices(vertices);mesh.SetNormals(vertexNormals);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();}
            public void Dispose(){if(mesh!=null)Object.Destroy(mesh);}
            static Vector3[] MakeNormals()
            {
                var result=new Vector3[Radial*Rings];
                for(int ring=0;ring<Rings;ring++)for(int side=0;side<Radial;side++)
                {float lat=latitude[ring]*Mathf.Deg2Rad,angle=side*Mathf.PI*2/Radial;result[ring*Radial+side]=new Vector3(Mathf.Cos(lat)*Mathf.Cos(angle),Mathf.Sin(lat),Mathf.Cos(lat)*Mathf.Sin(angle));}
                return result;
            }
            static int[] MakeTriangles()
            {
                var result=new int[(Rings-1)*Radial*6];int at=0;
                for(int ring=0;ring<Rings-1;ring++)for(int side=0;side<Radial;side++)
                {int a=ring*Radial+side,b=ring*Radial+(side+1)%Radial,c=a+Radial,d=b+Radial;result[at++]=a;result[at++]=b;result[at++]=c;result[at++]=b;result[at++]=d;result[at++]=c;}
                return result;
            }
        }
    }
}

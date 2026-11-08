using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Diwali
{
    /// <summary>
    /// A small layered diya diorama drawn as UI geometry. Shallow parallax, orbiting rangoli petals,
    /// drifting embers and a breathing flame give depth without a full scene or post-processing.
    /// </summary>
    public sealed class FestivalSplashArt : MaskableGraphic
    {
        [Range(0, 1)] public float ignition = 1;
        public bool hovered;
        public Camera view;
        public float MotionTime { get; private set; }
        static readonly Color Gold = new Color(1, .74f, .38f);
        static readonly Color Pale = new Color(1, .94f, .73f);
        readonly List<Vector2> path = new List<Vector2>(100);
        float warmth;

        void Update()
        {
            MotionTime += Time.unscaledDeltaTime;
            warmth = Mathf.MoveTowards(warmth, hovered ? 1 : 0, Time.unscaledDeltaTime * 3);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float t = MotionTime;
            float size = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            Vector2 origin = rectTransform.rect.center;
            Vector2 parallax = Vector2.zero;
            if (view)
            {
                Vector3 eye = transform.InverseTransformPoint(view.transform.position);
                parallax = new Vector2(Mathf.Clamp(eye.x * .008f, -7, 7), Mathf.Clamp(eye.y * .008f, -5, 5));
            }
            // Broad, translucent layers supply depth without bloom or a full-screen flash.
            for (int i = 10; i > 0; i--)
                Ellipse(mesh, origin + parallax*.2f + new Vector2(0, 14), new Vector2(size*.078f*i,size*.073f*i), new Color(.95f,.39f,.15f,.014f*ignition));
            Ring(mesh, origin, new Vector2(size*.91f,size*.84f), new Color(.94f,.64f,.36f,.20f),1,96);
            Ring(mesh, origin, new Vector2(size*.82f,size*.75f), new Color(.94f,.64f,.36f,.10f),1,80);
            for (int i=0;i<16;i++)
            {
                float a = i*Mathf.PI/8 + t*.022f;
                Vector2 radial = new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                Vector2 tangent = new Vector2(-radial.y,radial.x);
                Vector2 p = origin + radial*size*.86f + parallax*.22f;
                Quad(mesh,p-radial*10,p+tangent*4,p+radial*10,p-tangent*4,new Color(.95f,.64f,.34f,.40f));
            }
            Vector2 front = origin + parallax;
            float bob = Mathf.Sin(t*.8f)*2;
            front.y += bob;
            Ellipse(mesh,front+new Vector2(0,-size*.64f),new Vector2(size*.57f,size*.07f),new Color(0,0,0,.24f));
            // Curved terracotta bowl, inset side, rim and reflective oil are separate depth layers.
            path.Clear();
            Curve(new Vector2(-.65f,-.20f),new Vector2(-.49f,-.82f),new Vector2(.49f,-.82f),new Vector2(.65f,-.20f));
            Polygon(mesh,path,front,size,new Color(.58f,.22f,.18f));
            path.Clear();
            Curve(new Vector2(-.55f,-.29f),new Vector2(-.37f,-.68f),new Vector2(.38f,-.68f),new Vector2(.55f,-.29f));
            Polygon(mesh,path,front,size,new Color(.78f,.35f,.22f));
            Ellipse(mesh,front+new Vector2(0,-size*.19f),new Vector2(size*.65f,size*.15f),Gold);
            Ellipse(mesh,front+new Vector2(0,-size*.17f),new Vector2(size*.57f,size*.105f),new Color(.27f,.11f,.17f));
            Ellipse(mesh,front+new Vector2(0,-size*.165f),new Vector2(size*.47f,size*.068f),new Color(.66f,.32f,.16f));
            Ellipse(mesh,front+new Vector2(size*.12f,-size*.15f),new Vector2(size*.30f,size*.022f),new Color(1,.73f,.35f,.45f));
            Line(mesh,front+new Vector2(size*.01f,-size*.15f),front+new Vector2(size*.06f,-size*.015f),size*.025f,Pale);
            float flameHeight = (1 + Mathf.Sin(t*3.1f)*.025f + Mathf.Sin(t*5.7f)*.014f + warmth*.09f + ignition*.25f) * ignition;
            float lean = Mathf.Sin(t*1.9f)*.022f;
            Vector2 flameOrigin = front+new Vector2(size*.05f,-size*.015f);
            path.Clear();
            Curve(new Vector2(0,0),new Vector2(-.36f,.15f),new Vector2(-.02f,.45f),new Vector2(.05f+lean,.77f*flameHeight));
            Curve(new Vector2(.05f+lean,.77f*flameHeight),new Vector2(-.02f,.39f),new Vector2(.39f,.15f),new Vector2(0,0));
            Polygon(mesh,path,flameOrigin,size,new Color(1,.62f,.20f,ignition));
            path.Clear();
            Curve(new Vector2(0,.025f),new Vector2(-.14f,.11f),new Vector2(.01f,.25f),new Vector2(.025f,.36f*flameHeight));
            Curve(new Vector2(.025f,.36f*flameHeight),new Vector2(.02f,.20f),new Vector2(.17f,.11f),new Vector2(0,.025f));
            Polygon(mesh,path,flameOrigin,size,new Color(Pale.r,Pale.g,Pale.b,ignition));
            for(int i=0;i<9;i++)
            {
                float phase = Mathf.Repeat(t*.075f+i*.113f,1);
                float a = i*2.399f;
                Vector2 p=front+new Vector2(Mathf.Sin(a+t*.13f)*size*(.4f+phase*.25f),size*(-.02f+phase*.96f));
                Ellipse(mesh,p,new Vector2(1.2f+i%3*.45f,1.8f+i%2),new Color(1,.82f,.5f,Mathf.Sin(phase*Mathf.PI)*.7f*ignition),8);
            }
        }

        void Curve(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {
            for(int i=0;i<=24;i++)
            {
                float t=i/24f,u=1-t;
                path.Add(u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d);
            }
        }

        static void Polygon(VertexHelper mesh,List<Vector2> points,Vector2 offset,float scale,Color colour)
        {
            int start=mesh.currentVertCount;
            Vector2 centre=Vector2.zero;
            foreach(var p in points) centre+=p;
            centre/=points.Count;
            mesh.AddVert(offset+centre*scale,colour,Vector2.zero);
            foreach(var p in points) mesh.AddVert(offset+p*scale,colour,Vector2.zero);
            for(int i=0;i<points.Count;i++)mesh.AddTriangle(start,start+i+1,start+(i+1)%points.Count+1);
        }

        static void Ellipse(VertexHelper mesh,Vector2 centre,Vector2 radius,Color colour,int count=48)
        {
            int start=mesh.currentVertCount;
            mesh.AddVert(centre,colour,Vector2.zero);
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count;
                mesh.AddVert(centre+new Vector2(Mathf.Cos(a)*radius.x,Mathf.Sin(a)*radius.y),colour,Vector2.zero);
            }
            for(int i=0;i<count;i++)mesh.AddTriangle(start,start+i+1,start+(i+1)%count+1);
        }

        static void Ring(VertexHelper mesh,Vector2 centre,Vector2 radius,Color colour,float width,int count)
        {
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;
                Line(mesh,centre+new Vector2(Mathf.Cos(a)*radius.x,Mathf.Sin(a)*radius.y),centre+new Vector2(Mathf.Cos(b)*radius.x,Mathf.Sin(b)*radius.y),width,colour);
            }
        }

        static void Line(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color colour)
        {
            Vector2 normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
            Quad(mesh,a-normal,a+normal,b+normal,b-normal,colour);
        }

        static void Quad(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color colour)
        {
            int start=mesh.currentVertCount;
            mesh.AddVert(a,colour,Vector2.zero);mesh.AddVert(b,colour,Vector2.zero);
            mesh.AddVert(c,colour,Vector2.zero);mesh.AddVert(d,colour,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
    }
}

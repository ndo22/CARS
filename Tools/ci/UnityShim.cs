using System; using System.Collections.Generic;
namespace UnityEngine.Rendering { public enum IndexFormat { UInt16, UInt32 } public enum ShadowCastingMode { Off, On } }
namespace UnityEngine {
public class HeaderAttribute : Attribute { public HeaderAttribute(string s){} }
public class TooltipAttribute : Attribute { public TooltipAttribute(string s){} }
public class RangeAttribute : Attribute { public RangeAttribute(float a,float b){} }
public class RequireComponent : Attribute { public RequireComponent(Type t){} }
public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public float sqrMagnitude=>x*x+y*y; public float magnitude=>(float)Math.Sqrt(x*x+y*y);
 public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y); public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
 public static Vector2 operator*(Vector2 a,float f)=>new Vector2(a.x*f,a.y*f);
 public static float Distance(Vector2 a,Vector2 b)=>(a-b).magnitude; public static Vector2 Min(Vector2 a,Vector2 b)=>new Vector2(Math.Min(a.x,b.x),Math.Min(a.y,b.y));
 public static Vector2 Max(Vector2 a,Vector2 b)=>new Vector2(Math.Max(a.x,b.x),Math.Max(a.y,b.y)); public static Vector2 zero=>new Vector2(0,0); public static Vector2 one=>new Vector2(1,1); }
public struct Vector3 {
 public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
 public static Vector3 zero=>new Vector3(0,0,0); public static Vector3 one=>new Vector3(1,1,1);
 public static Vector3 up=>new Vector3(0,1,0); public static Vector3 down=>new Vector3(0,-1,0);
 public static Vector3 left=>new Vector3(-1,0,0); public static Vector3 right=>new Vector3(1,0,0);
 public static Vector3 forward=>new Vector3(0,0,1); public static Vector3 back=>new Vector3(0,0,-1);
 public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
 public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
 public static Vector3 operator-(Vector3 a)=>new Vector3(-a.x,-a.y,-a.z);
 public static Vector3 operator*(Vector3 a,float f)=>new Vector3(a.x*f,a.y*f,a.z*f);
 public static Vector3 operator*(float f,Vector3 a)=>a*f;
 public static Vector3 operator/(Vector3 a,float f)=>new Vector3(a.x/f,a.y/f,a.z/f);
 public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
 public static Vector3 Cross(Vector3 a,Vector3 b)=>new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
 public float magnitude=>(float)Math.Sqrt(x*x+y*y+z*z); public float sqrMagnitude=>x*x+y*y+z*z;
 public Vector3 normalized{get{float m=magnitude;return m>1e-6f?this/m:zero;}} public void Normalize(){this=normalized;}
 public static float Distance(Vector3 a,Vector3 b)=>(a-b).magnitude;
 public static Vector3 Lerp(Vector3 a,Vector3 b,float t){t=Mathf.Clamp01(t);return a+(b-a)*t;}
 public static Vector3 Min(Vector3 a,Vector3 b)=>new Vector3(Math.Min(a.x,b.x),Math.Min(a.y,b.y),Math.Min(a.z,b.z));
 public static Vector3 Max(Vector3 a,Vector3 b)=>new Vector3(Math.Max(a.x,b.x),Math.Max(a.y,b.y),Math.Max(a.z,b.z));
 public static Vector3 ProjectOnPlane(Vector3 v,Vector3 n)=>v-n*Dot(v,n);
 public static Vector3 Scale(Vector3 a,Vector3 b)=>new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);
 public static float Angle(Vector3 a,Vector3 b){float d=(float)Math.Sqrt(a.sqrMagnitude*b.sqrMagnitude); if(d<1e-15f)return 0; return (float)(Math.Acos(Mathf.Clamp(Dot(a,b)/d,-1,1))*180/Math.PI);}
}
public struct Vector4 { public float x,y,z,w; public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;} }
public struct Quaternion { public float x,y,z,w; public Quaternion(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}
 public static Quaternion identity=>new Quaternion(0,0,0,1);
 public static Vector3 operator*(Quaternion q,Vector3 v){Vector3 u=new Vector3(q.x,q.y,q.z);float s=q.w;return 2f*Vector3.Dot(u,v)*u+(s*s-Vector3.Dot(u,u))*v+2f*s*Vector3.Cross(u,v);}
 static Quaternion Axis(Vector3 a,float deg){float r=deg*(float)Math.PI/360f;float s=(float)Math.Sin(r);return new Quaternion(a.x*s,a.y*s,a.z*s,(float)Math.Cos(r));}
 public static Quaternion Euler(float x,float y,float z)=>Axis(Vector3.up,y)*Axis(Vector3.right,x)*Axis(Vector3.forward,z);
 public static Quaternion operator*(Quaternion a,Quaternion b)=>new Quaternion(a.w*b.x+a.x*b.w+a.y*b.z-a.z*b.y,a.w*b.y-a.x*b.z+a.y*b.w+a.z*b.x,a.w*b.z+a.x*b.y-a.y*b.x+a.z*b.w,a.w*b.w-a.x*b.x-a.y*b.y-a.z*b.z);
 public static Quaternion Slerp(Quaternion a,Quaternion b,float t){ float d=a.x*b.x+a.y*b.y+a.z*b.z+a.w*b.w; if(d<0){b=new Quaternion(-b.x,-b.y,-b.z,-b.w);} var q=new Quaternion(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t,a.z+(b.z-a.z)*t,a.w+(b.w-a.w)*t); float m=(float)Math.Sqrt(q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w); return new Quaternion(q.x/m,q.y/m,q.z/m,q.w/m);}
 public static Quaternion LookRotation(Vector3 f,Vector3 up){ f=f.normalized; Vector3 r=Vector3.Cross(up,f).normalized; Vector3 u=Vector3.Cross(f,r);
  float m00=r.x,m01=u.x,m02=f.x,m10=r.y,m11=u.y,m12=f.y,m20=r.z,m21=u.z,m22=f.z; float tr=m00+m11+m22; Quaternion q;
  if(tr>0){float s=(float)Math.Sqrt(tr+1)*2; q=new Quaternion((m21-m12)/s,(m02-m20)/s,(m10-m01)/s,0.25f*s);} 
  else if(m00>m11&&m00>m22){float s=(float)Math.Sqrt(1+m00-m11-m22)*2; q=new Quaternion(0.25f*s,(m01+m10)/s,(m02+m20)/s,(m21-m12)/s);} 
  else if(m11>m22){float s=(float)Math.Sqrt(1+m11-m00-m22)*2; q=new Quaternion((m01+m10)/s,0.25f*s,(m12+m21)/s,(m02-m20)/s);} 
  else {float s=(float)Math.Sqrt(1+m22-m00-m11)*2; q=new Quaternion((m02+m20)/s,(m12+m21)/s,0.25f*s,(m10-m01)/s);} return q; }
}
public struct Color { public float r,g,b,a; public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;} public static Color magenta=>new Color(1,0,1); public static Color white=>new Color(1,1,1);
 public static Color Lerp(Color a,Color b,float t){t=Mathf.Clamp01(t);return new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);}
 public static Color operator*(Color a,float f)=>new Color(a.r*f,a.g*f,a.b*f,a.a*f); }
public enum ForceMode { Force, Acceleration }
public struct ContactPoint { public Vector3 point, normal; }
public enum Space { World, Self }
public static class Time { public static float deltaTime=0, fixedDeltaTime=0.02f, time=0, unscaledDeltaTime=0, unscaledTime=0; public static int frameCount=0; }
public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }
public struct RaycastHit { public Vector3 point, normal; }
public static class Physics { public const int DefaultRaycastLayers = -5; public static bool Raycast(Vector3 o, Vector3 d, out RaycastHit h, float m, int mask, QueryTriggerInteraction q){ h=new RaycastHit(); return false; } }
public class Camera : Behaviour { public static Camera main => null; }
public static class Mathf {
 public const float PI=(float)Math.PI; public const float Infinity=float.PositiveInfinity; public const float Rad2Deg=57.29578f;
 public static float Max(float a,float b)=>Math.Max(a,b); public static int Max(int a,int b)=>Math.Max(a,b);
 public static float Min(float a,float b)=>Math.Min(a,b); public static int Min(int a,int b)=>Math.Min(a,b);
 public static float Clamp(float v,float a,float b)=>v<a?a:v>b?b:v; public static int Clamp(int v,int a,int b)=>v<a?a:v>b?b:v; public static float Clamp01(float v)=>Clamp(v,0,1);
 public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
 public static float InverseLerp(float a,float b,float v)=>a!=b?Clamp01((v-a)/(b-a)):0;
 public static float Sqrt(float f)=>(float)Math.Sqrt(f); public static float Cos(float f)=>(float)Math.Cos(f); public static float Sin(float f)=>(float)Math.Sin(f);
 public static float Abs(float f)=>Math.Abs(f); public static int Abs(int f)=>Math.Abs(f); public static float Pow(float a,float b)=>(float)Math.Pow(a,b); public static float Exp(float a)=>(float)Math.Exp(a); public static float Floor(float a)=>(float)Math.Floor(a); public static float Sign(float a)=>a>=0?1:-1; public static float Atan2(float y,float x)=>(float)Math.Atan2(y,x);
 public static float MoveTowards(float c,float t,float d)=>Math.Abs(t-c)<=d?t:c+Math.Sign(t-c)*d; public static float Repeat(float t,float l)=>t-(float)Math.Floor(t/l)*l; public static bool Approximately(float a,float b)=>Math.Abs(a-b)<1e-5f;
 public static int RoundToInt(float f)=>(int)Math.Round(f); public static int CeilToInt(float f)=>(int)Math.Ceiling(f); public static int FloorToInt(float f)=>(int)Math.Floor(f);
 public static float SmoothStep(float a,float b,float t){t=Clamp01(t);t=-2f*t*t*t+3f*t*t;return b*t+a*(1f-t);}
 static float Fade(float t)=>t*t*t*(t*(t*6-15)+10);
 static float Grad(int h,float x,float y){h&=7;float u=h<4?x:y,v=h<4?y:x;return ((h&1)!=0?-u:u)+((h&2)!=0?-2f*v:2f*v);}
 static int[] p; static int P(int i){ if(p==null){p=new int[512];var r=new System.Random(0);var a=new int[256];for(int k=0;k<256;k++)a[k]=k;for(int k=255;k>0;k--){int j=r.Next(k+1);int t=a[k];a[k]=a[j];a[j]=t;}for(int k=0;k<512;k++)p[k]=a[k&255];} return p[i&511]; }
 public static float PerlinNoise(float x,float y){int X=(int)Math.Floor(x)&255,Y=(int)Math.Floor(y)&255;x-=(float)Math.Floor(x);y-=(float)Math.Floor(y);float u=Fade(x),v=Fade(y);int A=P(X)+Y,B=P(X+1)+Y;
  float r=Lerp2(Lerp2(Grad(P(A),x,y),Grad(P(B),x-1,y),u),Lerp2(Grad(P(A+1),x,y-1),Grad(P(B+1),x-1,y-1),u),v);return Clamp01(r*0.5f+0.5f);}
 static float Lerp2(float a,float b,float t)=>a+(b-a)*t;
}
public class Object { public string name; }
public class Material : Object { public Material(string n){name=n;} }
public class Mesh : Object { public Rendering.IndexFormat indexFormat; public List<Vector3> v; public List<int>[] t; public int subMeshCount{get=>t.Length;set=>t=new List<int>[value];}
 public Vector3[] vertices { get => v.ToArray(); set => v = new List<Vector3>(value); } public int[] GetTriangles(int i) => t[i].ToArray();
 public void SetVertices(List<Vector3> a){v=new List<Vector3>(a);} public void SetTriangles(List<int> a,int i){t[i]=new List<int>(a);} public void RecalculateNormals(){} public void RecalculateBounds(){} }
public class Component : Object { public GameObject gameObject; public Transform transform=>gameObject.transform; public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>(); public T[] GetComponentsInChildren<T>(bool all=false) where T:Component=>new T[0]; }
public class Behaviour : Component { public bool enabled=true; }
public class MonoBehaviour : Behaviour {}
public class Transform : Component { public Transform parent; public Vector3 position, localPosition; public Quaternion rotation=Quaternion.identity, localRotation=Quaternion.identity;
 public void SetParent(Transform p,bool w){parent=p;} public void SetPositionAndRotation(Vector3 p,Quaternion r){position=p;rotation=r;}
 public System.Collections.IEnumerator GetEnumerator(){ foreach(var g in GameObject.All) if(g.transform.parent==this) yield return g.transform; }
 public Vector3 up=>rotation*Vector3.up; public Vector3 forward=>rotation*Vector3.forward; public Vector3 InverseTransformPoint(Vector3 p)=>p; public void Rotate(Vector3 a,float d,Space s){} }
public class GameObject : Object { public static List<GameObject> All=new List<GameObject>(); public List<Component> comps=new List<Component>(); public Transform transform; public int layer; public bool isStatic; public bool active=true;
 public GameObject(string n,params Type[] t){name=n;transform=new Transform();transform.gameObject=this;comps.Add(transform);All.Add(this);}
 public T AddComponent<T>() where T:Component{var c=(T)Activator.CreateInstance(typeof(T));c.gameObject=this;comps.Add(c);return c;}
 public T GetComponent<T>() where T:Component{foreach(var c in comps)if(c is T)return (T)c;return null;}
 public void SetActive(bool a){active=a;} public static GameObject Find(string n){ foreach(var g in All) if(g.name==n) return g; return null; } }
public class MeshFilter : Component { public Mesh sharedMesh; }
public class MeshRenderer : Component { public Material sharedMaterial{get=>sharedMaterials[0];set=>sharedMaterials=new[]{value};} public Material[] sharedMaterials; public Rendering.ShadowCastingMode shadowCastingMode; }
public class Collider : Component {} public class MeshCollider : Collider { public Mesh sharedMesh; }
public class BoxCollider : Collider { public Vector3 center,size; public bool isTrigger; }
public enum CollisionDetectionMode { Discrete, Continuous } public enum RigidbodyInterpolation { None, Interpolate }
public class Rigidbody : Component { public float mass,drag,angularDrag; public CollisionDetectionMode collisionDetectionMode; public RigidbodyInterpolation interpolation; public Vector3 centerOfMass,velocity,angularVelocity,position; public Quaternion rotation;
 public void AddForce(Vector3 f){} public void AddForce(Vector3 f, ForceMode m){} public void AddForceAtPosition(Vector3 f,Vector3 p){} }
public struct JointSpring { public float spring,damper,targetPosition; }
public struct WheelFrictionCurve { public float extremumSlip,extremumValue,asymptoteSlip,asymptoteValue,stiffness; }
public struct WheelHit { public Vector3 point, normal; public float sidewaysSlip, forwardSlip; public Collider collider; }
public class WheelCollider : Collider { public float radius,mass,suspensionDistance,forceAppPointDistance,wheelDampingRate,motorTorque,brakeTorque,steerAngle; public bool isGrounded; public JointSpring suspensionSpring; public WheelFrictionCurve forwardFriction,sidewaysFriction;
 public bool GetGroundHit(out WheelHit h){h=new WheelHit();return false;} public void GetWorldPose(out Vector3 p,out Quaternion q){p=transform.position;q=transform.rotation;} }
public enum LightType { Spot, Directional, Point } public enum LightShadows { None, Hard, Soft }
public class Light : Behaviour { public LightType type; public float range, spotAngle, intensity; public Color color; public LightShadows shadows; }
public class Collision { public Vector3 relativeVelocity; public int contactCount; public ContactPoint GetContact(int i)=>new ContactPoint(); }
public static class LayerMask { public static int NameToLayer(string n)=>10; public static int GetMask(params string[] n)=>0; }
public static class Application { public static string persistentDataPath => System.IO.Path.GetTempPath(); public static bool isMobilePlatform => false; }
public static class Debug { public static void Log(object o){} public static void LogWarning(object o){ System.Console.WriteLine("WARN "+o); } public static void LogError(object o){ System.Console.WriteLine("ERR "+o); } }
public static class PlayerPrefs { static Dictionary<string,object> d=new Dictionary<string,object>();
 public static int GetInt(string k,int def=0)=>d.TryGetValue(k,out var v)?(int)v:def; public static void SetInt(string k,int v)=>d[k]=v;
 public static float GetFloat(string k,float def=0)=>d.TryGetValue(k,out var v)?(float)v:def; public static void SetFloat(string k,float v)=>d[k]=v;
 public static string GetString(string k,string def="")=>d.TryGetValue(k,out var v)?(string)v:def; public static void SetString(string k,string v)=>d[k]=v;
 public static bool HasKey(string k)=>d.ContainsKey(k); public static void DeleteKey(string k)=>d.Remove(k); public static void Save(){} }
public static class JsonUtility { public static string ToJson(object o)=>""; public static T FromJson<T>(string s)=>default(T); }
public class AudioClip : Object { public static AudioClip Create(string n,int len,int ch,int rate,bool stream)=>new AudioClip{name=n}; public bool SetData(float[] d,int off)=>true; }

}

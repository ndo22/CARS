// Low-poly voda: vrcholy se vlní, každá ploška má vlastní (plochou) normálu spočítanou v pixel shaderu.
Shader "LowPoly/Water"
{
    Properties
    {
        _Color ("Color", Color) = (0.18, 0.6, 0.78, 1)
        _DeepColor ("Deep Color", Color) = (0.1, 0.4, 0.6, 1)
        _WaveHeight ("Wave Height", Float) = 0.35
        _WaveSpeed ("Wave Speed", Float) = 1.2
        _Specular ("Specular", Range(0, 2)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            fixed4 _Color, _DeepColor;
            float _WaveHeight, _WaveSpeed, _Specular;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 w = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y * _WaveSpeed;
                w.y += (sin(w.x * 0.09 + t) + sin(w.z * 0.07 + t * 1.3) + sin((w.x + w.z) * 0.05 + t * 0.7)) * _WaveHeight * 0.4;
                o.world = w;
                o.pos = UnityWorldToClipPos(w);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Plochá normála plošky (low-poly vzhled)
                float3 n = normalize(cross(ddy(i.world), ddx(i.world)));
                if (n.y < 0) n = -n;
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 v = normalize(_WorldSpaceCameraPos - i.world);
                float diff = saturate(dot(n, l));
                float spec = pow(saturate(dot(reflect(-l, n), v)), 48.0) * _Specular;
                float facet = saturate(n.y * 1.5 - 0.5);
                fixed3 baseCol = lerp(_DeepColor.rgb, _Color.rgb, facet);
                fixed3 col = baseCol * (UNITY_LIGHTMODEL_AMBIENT.rgb + _LightColor0.rgb * diff * 0.8) + _LightColor0.rgb * spec;
                fixed4 c = fixed4(col, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}

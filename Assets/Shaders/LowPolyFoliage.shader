// Listí, které se mírně vlní ve větru (Standard osvětlení + posun vrcholů).
Shader "LowPoly/Foliage"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.6, 0.3, 1)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.05
        _Sway ("Sway", Float) = 0.12
        _SwaySpeed ("Sway Speed", Float) = 1.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows addshadow
        #pragma target 3.0

        fixed4 _Color;
        half _Glossiness;
        float _Sway, _SwaySpeed;

        struct Input { float3 worldPos; };

        void vert (inout appdata_full v)
        {
            float3 w = mul(unity_ObjectToWorld, v.vertex).xyz;
            float t = _Time.y * _SwaySpeed;
            float phase = w.x * 0.15 + w.z * 0.11;
            float3 offset = float3(sin(t + phase), 0, cos(t * 0.8 + phase)) * _Sway;
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, offset);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _Color.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    Fallback "Diffuse"
}

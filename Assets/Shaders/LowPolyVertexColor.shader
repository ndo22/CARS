// Low-poly materiál: barva materiálu (_Color) násobená barvou vrcholů.
// Barvy vrcholů nesou zapečené stínování (tmavší u země a v rozích), plynulé přechody terénu a tónování stromů.
// Když tenhle shader nejde použít, hra použije Standard - barvy vrcholů se pak jen ignorují.
Shader "LowPoly/VertexColor"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.1
        _Metallic ("Metallic", Range(0, 1)) = 0
        _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        fixed4 _EmissionColor;

        struct Input
        {
            float4 color : COLOR;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _Color.rgb * IN.color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}

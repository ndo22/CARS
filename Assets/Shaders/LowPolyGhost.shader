// Průhledný "hologram" pro ghost auto: barva + světlý okraj.
Shader "LowPoly/Ghost"
{
    Properties
    {
        _Color ("Color", Color) = (0.4, 0.85, 1, 0.35)
        _RimColor ("Rim", Color) = (0.8, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color, _RimColor;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float rim : TEXCOORD0; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                float3 view = normalize(WorldSpaceViewDir(v.vertex));
                o.rim = 1 - saturate(dot(n, view));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = _Color;
                c.rgb = lerp(c.rgb, _RimColor.rgb, i.rim * i.rim);
                c.a = saturate(_Color.a + i.rim * 0.5);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}

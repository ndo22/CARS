// Gradientová obloha pro low-poly tratě (skybox): nahoře / obzor / dole + slunce (měsíc).
Shader "LowPoly/Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.3, 0.5, 0.9, 1)
        _HorizonColor ("Horizon", Color) = (0.7, 0.85, 1, 1)
        _BottomColor ("Bottom", Color) = (0.5, 0.55, 0.6, 1)
        _SunColor ("Sun", Color) = (1, 0.95, 0.8, 1)
        _SunDir ("Sun Direction", Vector) = (0, 0.5, 1, 0)
        _SunSize ("Sun Size", Range(0.9, 0.9999)) = 0.9985
        _Exponent ("Gradient Exponent", Range(0.2, 4)) = 0.8
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor, _HorizonColor, _BottomColor, _SunColor;
            float4 _SunDir;
            float _SunSize, _Exponent;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = d.y;
                fixed3 col;
                if (up >= 0)
                    col = lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(up), _Exponent));
                else
                    col = lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(-up * 4.0));

                float sun = dot(d, normalize(_SunDir.xyz));
                float disc = smoothstep(_SunSize, _SunSize + 0.0008, sun);
                float glow = pow(saturate(sun), 64.0) * 0.35;
                col += _SunColor.rgb * (disc + glow);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}

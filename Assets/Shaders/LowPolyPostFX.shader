// Jednoduchý postprocessing: bloom (záře), barevné úpravy (sytost, kontrast, teplota) a vinětace.
Shader "Hidden/LowPoly/PostFX"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float _Threshold, _BloomIntensity, _Saturation, _Contrast, _Vignette, _Exposure;
    float4 _Tint;

    half3 Box(float2 uv, float delta)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-delta, delta).xxyy;
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb
                + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25;
    }

    // 0: výběr jasných míst + zmenšení
    half4 FragPrefilter (v2f_img i) : SV_Target
    {
        half3 c = Box(i.uv, 1);
        half brightness = max(c.r, max(c.g, c.b));
        half contribution = max(0, brightness - _Threshold) / max(brightness, 0.0001);
        return half4(c * contribution, 1);
    }

    // 1: zmenšení s rozmazáním
    half4 FragDown (v2f_img i) : SV_Target
    {
        return half4(Box(i.uv, 1), 1);
    }

    // 2: zvětšení (sčítá se blendem)
    half4 FragUp (v2f_img i) : SV_Target
    {
        return half4(Box(i.uv, 0.5), 1);
    }

    // 3: výsledné složení
    half4 FragFinal (v2f_img i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb * _Exposure;
        c += tex2D(_BloomTex, i.uv).rgb * _BloomIntensity;
        c *= _Tint.rgb;
        half luma = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = lerp(luma.xxx, c, _Saturation);
        c = (c - 0.5) * _Contrast + 0.5;
        float2 d = i.uv - 0.5;
        c *= 1 - dot(d, d) * _Vignette;
        return half4(saturate(c), 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragDown
            ENDCG }
        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragUp
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragFinal
            ENDCG }
    }
    Fallback Off
}

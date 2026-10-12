// Bloom + death tint for the built-in render pipeline, used by ProjectJuice.CameraFx on the Main Camera.
// Pass 0: bright-pass prefilter (threshold with soft knee) + first 2x downsample
// Pass 1: 2x downsample (4-tap box)
// Pass 2: 2x upsample (9-tap tent), added onto the next larger level
// Pass 3: composite: scene + bloom, then the red death tint and vignette
Shader "Hidden/ProjectJuice/CameraFx"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float4 _Threshold;      // x threshold, y threshold - knee, z 2 * knee, w 0.25 / knee
    float _Intensity;
    float4 _BloomTint;
    float _SampleScale;
    float4 _DeathColor;
    float _DeathAmount;     // 0..1
    float _DeathVignette;   // 0..1

    half3 Box4(float2 uv, float2 texel)
    {
        float4 d = texel.xyxy * float4(-1, -1, 1, 1);
        half3 s = tex2D(_MainTex, uv + d.xy).rgb + tex2D(_MainTex, uv + d.zy).rgb
                + tex2D(_MainTex, uv + d.xw).rgb + tex2D(_MainTex, uv + d.zw).rgb;
        return s * 0.25;
    }

    half3 Prefilter(half3 c)
    {
        half br = max(c.r, max(c.g, c.b));
        half rq = clamp(br - _Threshold.y, 0, _Threshold.z);
        rq = _Threshold.w * rq * rq;
        c *= max(rq, br - _Threshold.x) / max(br, 1e-4);
        return c;
    }

    half4 FragPrefilter(v2f_img i) : SV_Target
    {
        return half4(Prefilter(Box4(i.uv, _MainTex_TexelSize.xy)), 1);
    }

    half4 FragDown(v2f_img i) : SV_Target
    {
        return half4(Box4(i.uv, _MainTex_TexelSize.xy), 1);
    }

    half4 FragUp(v2f_img i) : SV_Target
    {
        float4 d = _MainTex_TexelSize.xyxy * float4(1, 1, -1, 0) * _SampleScale;
        half3 s;
        s  = tex2D(_MainTex, i.uv - d.xy).rgb;
        s += tex2D(_MainTex, i.uv - d.wy).rgb * 2;
        s += tex2D(_MainTex, i.uv - d.zy).rgb;
        s += tex2D(_MainTex, i.uv + d.zw).rgb * 2;
        s += tex2D(_MainTex, i.uv).rgb * 4;
        s += tex2D(_MainTex, i.uv + d.xw).rgb * 2;
        s += tex2D(_MainTex, i.uv + d.zy).rgb;
        s += tex2D(_MainTex, i.uv + d.wy).rgb * 2;
        s += tex2D(_MainTex, i.uv + d.xy).rgb;
        return half4(s * (1.0 / 16), 1);
    }

    half4 FragComposite(v2f_img i) : SV_Target
    {
        half4 c = tex2D(_MainTex, i.uv);
        c.rgb += tex2D(_BloomTex, i.uv).rgb * _BloomTint.rgb * _Intensity;

        // Death: the world loses its colour and turns red, darker and redder at the edges.
        half lum = dot(c.rgb, half3(0.299, 0.587, 0.114));
        half3 red = lum * _DeathColor.rgb * 1.35;
        float2 p = i.uv - 0.5;
        half edge = saturate(dot(p, p) * 2.6);
        half3 dead = lerp(c.rgb, red, 0.75);
        dead = lerp(dead, _DeathColor.rgb * 0.35, edge * _DeathVignette);
        c.rgb = lerp(c.rgb, dead, _DeathAmount);
        return c;
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
            #pragma fragment FragComposite
            ENDCG }
    }
    Fallback Off
}

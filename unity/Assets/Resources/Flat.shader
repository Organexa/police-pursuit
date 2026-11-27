// Every placeholder shape. It lives in Resources so a player build cannot leave it out: the default
// primitive material was missing on the phone build and drew everything as the error shader.
// Plain CGPROGRAM, so it renders with or without a URP asset.
Shader "PolicePursuit/Flat"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Windows ("Lit windows", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _Color; // above 1 glows with bloom
            float _Windows;
            half4 _NightFog;
            float4 _NightFogRange; // start, 1 / length

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed shade : TEXCOORD0;
                half fog : TEXCOORD1;
                float3 facade : TEXCOORD2; // metres along the wall, metres up, 1 on a wall
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // A fixed light from above and a little behind the camera, so box sides can be told apart.
                float3 n = UnityObjectToWorldNormal(v.normal);
                o.shade = 0.55 + 0.45 * saturate(dot(n, normalize(float3(0.3, 1, -0.5))));
                // Cube metres from the object's own size, so far-off tiles keep their precision.
                float3 size = float3(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21),
                    length(unity_ObjectToWorld._m02_m12_m22));
                float3 p = (v.vertex.xyz + 0.5) * size;
                bool alongZ = abs(v.normal.x) > 0.5;
                o.facade = float3((alongZ ? p.z : p.x) + (alongZ ? size.y : 0), p.y, abs(v.normal.y) < 0.5);
                float dist = distance(mul(unity_ObjectToWorld, v.vertex).xyz, _WorldSpaceCameraPos);
                o.fog = saturate((dist - _NightFogRange.x) * _NightFogRange.y);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half3 colour = _Color.rgb * i.shade;
                if (_Windows > 0 && i.facade.z > 0.5)
                {
                    float2 cell = floor(i.facade.xy / float2(1.6, 1.8));
                    float2 inside = frac(i.facade.xy / float2(1.6, 1.8));
                    float pane = step(0.2, inside.x) * step(inside.x, 0.75) * step(0.3, inside.y) * step(inside.y, 0.8);
                    float pick = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
                    // About a third of the windows are lit: mostly warm, some cool.
                    half3 light = pick > 0.9 ? half3(0.35, 0.8, 1.2) : half3(1.2, 0.8, 0.35);
                    colour = lerp(colour, light, pane * step(0.65, pick));
                }
                return half4(lerp(colour, _NightFog.rgb, i.fog), 1);
            }
            ENDCG
        }
    }
}

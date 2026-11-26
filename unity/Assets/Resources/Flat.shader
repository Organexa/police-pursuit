// Every placeholder shape. It lives in Resources so a player build cannot leave it out: the default
// primitive material was missing on the phone build and drew everything as the error shader.
// Plain CGPROGRAM, so it renders with or without a URP asset.
Shader "PolicePursuit/Flat"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
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

            fixed4 _Color;

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed shade : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // A fixed light from above and a little behind the camera, so box sides can be told apart.
                float3 n = UnityObjectToWorldNormal(v.normal);
                o.shade = 0.55 + 0.45 * saturate(dot(n, normalize(float3(0.3, 1, -0.5))));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(_Color.rgb * i.shade, 1);
            }
            ENDCG
        }
    }
}

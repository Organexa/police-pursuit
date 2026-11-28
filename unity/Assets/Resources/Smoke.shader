// Soft, see-through puffs for tyre and engine smoke (particles). Flat.shader is opaque, which drew every puff as a
// solid grey square (seen in the emulator autotest screenshots). Round fall-off from the quad's centre, alpha from
// the particle's colour (faded over its life), and the same night fog as Flat. In Resources so builds keep it.
Shader "Smoke"
{
    Properties
    {
        _Color ("Color", Color) = (0.6, 0.62, 0.66, 0.55)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _Color;
            half4 _NightFog;
            float4 _NightFogRange; // start, 1 / length

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; half fog : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2 - 1;
                o.color = v.color;
                float dist = distance(mul(unity_ObjectToWorld, v.vertex).xyz, _WorldSpaceCameraPos);
                o.fog = saturate((dist - _NightFogRange.x) * _NightFogRange.y);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half soft = saturate(1 - dot(i.uv, i.uv)); // 1 in the middle, 0 at the rim
                half3 colour = lerp(_Color.rgb * i.color.rgb, _NightFog.rgb, i.fog);
                return half4(colour, _Color.a * i.color.a * soft * soft);
            }
            ENDCG
        }
    }
}

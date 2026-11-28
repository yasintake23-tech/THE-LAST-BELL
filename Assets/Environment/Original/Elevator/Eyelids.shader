Shader "TheLastBell/Eyelids"
{
    Properties { _Openness ("Openness", Range(0, 1)) = 1 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragment
            #include "UnityCG.cginc"
            float _Openness;
            fixed4 fragment(v2f_img input) : SV_Target
            {
                float2 xy = input.uv * 2.0 - 1.0;
                float arch = abs(xy.y + 0.05 * (1.0 - _Openness)) + 0.26 * xy.x * xy.x;
                float edge = lerp(-0.14, 1.47, _Openness);
                return fixed4(0.006, 0.004, 0.004, smoothstep(edge - 0.06, edge + 0.08, arch));
            }
            ENDCG
        }
    }
}

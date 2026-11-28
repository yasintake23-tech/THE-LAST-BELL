Shader "TheLastBell/ElevatorGas"
{
    Properties { _MainTex ("Gas density", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vertex
            #pragma fragment fragment
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct Input { float4 position : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Varying { float4 position : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            Varying vertex(Input input)
            {
                Varying output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            fixed4 fragment(Varying input) : SV_Target
            {
                fixed4 density = tex2D(_MainTex, input.uv);
                return fixed4(density.rgb * input.color.rgb, density.a * input.color.a);
            }
            ENDCG
        }
    }
}

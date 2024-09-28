Shader "Custom/InstancedWhiteQuad"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            UNITY_INSTANCING_BUFFER_START(PerInstanceData)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(PerInstanceData)

            sampler2D _MainTex;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Sample the texture using the UV coordinates
                fixed4 texColor = tex2D(_MainTex, i.uv);
                // Multiply the texture color by the instance color
                fixed4 finalColor = texColor * UNITY_ACCESS_INSTANCED_PROP(PerInstanceData, _Color);
                return finalColor;
            }
            ENDCG
        }
    }
}
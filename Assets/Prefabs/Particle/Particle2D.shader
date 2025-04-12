Shader "Instanced/Particle2D"
{
    Properties
    {

    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "UnityCG.cginc"

            StructuredBuffer<float2> Positions2D;
            float scale;
            float4 particleColor;
            //Texture2D<float4> ColorMap;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                //float3 color : TEXCOORD1;
            };

            v2f vert (appdata_full v, uint instanceID : SV_INSTANCEID)
            {
                float3 centerWorld = float3(Positions2D[instanceID], 0);
                float3 worldVertPos = centerWorld + mul(unity_ObjectToWorld, v.vertex * scale);
                float3 objectVertPos = mul(unity_WorldToObject, float4(worldVertPos, 1));

                v2f o;
                o.uv = v.texcoord;
                o.pos = UnityObjectToClipPos(objectVertPos);
                //o.color = float3(1, 1, 1);

                return o;
            }

            float4 frag (v2f i) : SV_TARGET
            {
                return particleColor;
            }

            ENDCG
        }
        

    }
}

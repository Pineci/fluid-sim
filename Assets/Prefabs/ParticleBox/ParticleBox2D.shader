// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Instanced/ParticleBox2D"
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

            #define PI 3.14159265358979323846

            StructuredBuffer<float2> Positions2D;
            //StructuredBuffer<float2> Densities2D;
            int numParticles;
            float densityRadius;
            float restDensity;
            const float maxDensity = 100.0;
            //float scale;
            //Texture2D<float4> ColorMap;

            float4 restDensityColor;
            float4 lowDensityColor;
            float4 highDensityColor;
            float highDensityColorSaturation;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                //float3 color : TEXCOORD1;
            };

            v2f vert (appdata_full v, uint instanceID : SV_INSTANCEID)
            {
                v2f o;
                o.uv = v.texcoord;
                o.pos = UnityObjectToClipPos(v.vertex);
                //o.color = float3(1, 1, 1);

                return o;
            }

            #define NORM_CONST 4.0 /  3.14159265358979323846

            float SmoothingKernel(float sqrDistance, float sqrRadius, float eighthRadius){
                if (sqrRadius < sqrDistance){
                    return 0.0;
                }
                float v = sqrRadius - sqrDistance;
                v = v * v * v;
                return v * NORM_CONST / eighthRadius;
            }
        
            float CalculateDensity(float2 pos, float sqrRadius, float eighthRadius){
                float density = 0.0;
                for (int i = 0; i < numParticles; i++){
                    float2 dst = Positions2D[i] - pos;
                    float sqrDistance = dst.x * dst.x + dst.y * dst.y;
                    density += SmoothingKernel(sqrDistance, sqrRadius, eighthRadius);
                }
                return density;
            }

            float4 frag (v2f i) : SV_TARGET
            {
                //i.pos.x = saturate(i.pos.x);
                //i.pos.y = saturate(i.pos.y);
                float sqrRadius = densityRadius * densityRadius;
                float eighthRadius = sqrRadius * sqrRadius;
                eighthRadius *= eighthRadius;
                float density = CalculateDensity(i.uv, sqrRadius, eighthRadius);
                
                //float centeredDensity = density - restDensity;

                //float r = saturate(log10(centeredDensity / restDensity));
                //float b = saturate(centeredDensity / (maxDensity - restDensity));

                float4 color;
                if (density > restDensity){
                    float t = saturate((density - restDensity) / (maxDensity - restDensity));
                    t = saturate(log10(density - restDensity) / highDensityColorSaturation);
                    color = lerp(restDensityColor, highDensityColor, t);
                    //color = highDensityColor;
                } else {
                    float t = density / restDensity;
                    color = lerp(lowDensityColor, restDensityColor, t);
                }

                return color;
            }

            ENDCG
        }
        

    }
}

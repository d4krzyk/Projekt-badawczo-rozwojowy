Shader "Custom/PixelLit"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _Gloss ("Smoothness", Range(0,1)) = 0.2
        _LightSteps ("Light Steps", Range(1,8)) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf PixelToon fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        float _Gloss;
        float _LightSteps;

        struct Input { float2 uv_MainTex; };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            // smoothness/metal nie są używane przy SurfaceOutput, można dodać custom jeśli potrzeba
        }

        // custom lighting: posterize N·L -> pixel/toon look
        inline half4 LightingPixelToon (SurfaceOutput s, half3 lightDir, half atten)
        {
            half NdotL = saturate(dot(s.Normal, lightDir));
            float steps = max(1.0, _LightSteps);
            NdotL = floor(NdotL * steps) / max(0.0001, steps - 1.0);
            half3 diff = s.Albedo * _LightColor0.rgb * NdotL * atten;
            half3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * s.Albedo;
            return half4(diff + ambient, 1.0);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
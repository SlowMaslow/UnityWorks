Shader "ClimbUp/Character Toon"
{
    Properties
    {
        _Color ("Base Color", Color) = (1,0.70,0.40,1)
        _ShadowTint ("Shadow Tint", Color) = (0.66,0.43,0.31,1)
        _MidTint ("Midtone Tint", Color) = (0.88,0.76,0.62,1)
        _Threshold ("Light Threshold", Range(-0.5,0.8)) = 0.1
        _Softness ("Band Softness", Range(0.01,0.4)) = 0.15
        _FacetLight ("Continuous plane shading", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Toon fullforwardshadows addshadow noforwardadd noambient novertexlights
        #pragma target 3.0
        fixed4 _Color, _ShadowTint, _MidTint;
        half _Threshold, _Softness, _FacetLight;
        struct Input { float3 worldPos; };
        half4 LightingToon(SurfaceOutput s, half3 lightDir, half atten)
        {
            half n = dot(normalize(s.Normal), normalize(lightDir));
            half mid = smoothstep(_Threshold-_Softness, _Threshold+_Softness, n);
            half lit = smoothstep(0.58-_Softness,0.58+_Softness,n);
            half3 tone = lerp(_ShadowTint.rgb, _MidTint.rgb, mid);
            tone = lerp(tone,half3(1,1,1),lit);
            half3 planeTone = lerp(_ShadowTint.rgb,half3(1,1,1),saturate(n * 0.65 + 0.35));
            tone = lerp(tone,planeTone,_FacetLight);
            tone = lerp(_ShadowTint.rgb,tone,saturate(atten));
            // Keep the palette readable in the game's coloured lighting, with no specular highlights.
            half3 lightTint = lerp(half3(1,1,1),saturate(_LightColor0.rgb),0.15);
            return half4(s.Albedo*tone*lightTint,s.Alpha);
        }
        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo=_Color.rgb;
            o.Alpha=1;
            o.Specular=0;
            o.Gloss=0;
        }
        ENDCG
    }
    Fallback "Diffuse"
}

Shader "ClimbUp/PlatformBlock"
{
    // Лит-материал для 3D-блоков платформ (Built-in RP) под ОДНУ комбинированную текстуру грунта
    // (трава сверху + земля снизу в одном файле).
    // V (вертикаль) = по ОБЪЕКТНОЙ высоте куба → трава всегда у верхней грани, земля ниже,
    //   доля не зависит от масштаба блока. U (горизонталь) = по МИРОВЫМ координатам × _Tiling
    //   (триплонар: ±X грани берут world.z, остальные world.x) → бесшовно на любой ширине и на стыках.
    // Просто слотим материал на бокс — размер/ширина учитываются автоматически, без скриптов.
    Properties
    {
        _MainTex    ("Ground (grass top + dirt)", 2D) = "white" {}
        _Tiling     ("Horizontal world tiling", Float) = 1.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.05
        _Metallic   ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        float  _Tiling;
        half   _Glossiness;
        half   _Metallic;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            float  objY;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.objY = v.vertex.y; // объектная высота: стандартный куб -0.5..+0.5
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = normalize(IN.worldNormal);

            // Вертикаль текстуры = высота блока (верх = трава). Меш с верхним пивотом: objY ∈ [-1,0].
            float V = saturate(IN.objY + 1.0);

            // Горизонталь = мир, тайлинг; грани ±X берут world.z, прочие world.x.
            float u = (abs(n.x) > abs(n.z) ? IN.worldPos.z : IN.worldPos.x) * _Tiling;

            fixed4 c     = tex2D(_MainTex, float2(u, V));
            o.Albedo     = c.rgb;
            o.Metallic   = _Metallic;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

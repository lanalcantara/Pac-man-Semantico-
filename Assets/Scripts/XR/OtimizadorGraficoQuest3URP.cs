using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

/// <summary>
/// ============================================================================
/// TECH ART & OTIMIZAÇÃO VR PARA META QUEST 3 (Unity 6 - URP)
/// ============================================================================
/// Configura automaticamente o pipeline gráfico Universal Render Pipeline (URP),
/// XR eyeTextureResolutionScale, MSAA 4x, Anisotropic Filtering Forced On,
/// Post-Processing HDR com Bloom e Iluminação Noturna sem estouro de atlas de
/// sombras pontuais ("Reduced additional punctual light shadows resolution").
/// ============================================================================
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class OtimizadorGraficoQuest3URP : MonoBehaviour
{
    [Header("1. Nitidez Extrema e Anti-Aliasing (Meta Quest 3 Pancake Lenses)")]
    [Range(1.0f, 1.5f)]
    [Tooltip("Multiplicador de resolução de textura ocular XR e Render Scale do URP (recomendado Quest 3: 1.25).")]
    public float escalaResolucaoQuest3 = 1.25f;

    [Tooltip("Nível de Multisample Anti-Aliasing obrigatório para VR (4x).")]
    public int nivelMSAA = 4;

    [Tooltip("Força Anisotropic Filtering em todas as texturas de chão e paredes olhadas em ângulo.")]
    public bool forcarFiltroAnisotropico = true;

    [Header("2. Estética Cibernética (HDR Post-Processing & Bloom)")]
    [Tooltip("Limiar (Threshold = 1.5) alto do Bloom para que apenas as linhas finas da grade (Wireframe) brilhem suavemente.")]
    public float bloomThreshold = 1.5f;

    [Tooltip("Intensidade máxima (0.2) do Bloom para estética Dark Grid Retrowave limpa e nítida.")]
    [Range(0.05f, 1.0f)]
    public float bloomIntensity = 0.2f;

    [Tooltip("Espalhamento (Scatter) contido para preservar nitidez máxima no Quest 3.")]
    [Range(0.1f, 0.75f)]
    public float bloomScatter = 0.35f;

    [Header("3. Iluminação Diurna e Fix de Sombras URP")]
    [Tooltip("Desativa sombras de Point Lights adicionais para eliminar o erro 'Reduced additional punctual light shadows resolution'.")]
    public bool corrigirSombrasLuzesPontuais = true;

    [Tooltip("Intensidade solar diurna de tarde (Golden Hour).")]
    public float intensidadeLuzDirecionalNoturna = 1.65f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AplicarOtimizacaoAutomaticaNoBoot()
    {
        OtimizadorGraficoQuest3URP inst = FindAnyObjectByType<OtimizadorGraficoQuest3URP>();
        if (inst == null)
        {
            GameObject go = new GameObject("OtimizadorGrafico_Quest3_URP");
            inst = go.AddComponent<OtimizadorGraficoQuest3URP>();
        }
        inst.AplicarConfiguracoesQuest3();
    }

    private void Awake()
    {
        AplicarConfiguracoesQuest3();
    }

    private void OnEnable()
    {
        AplicarConfiguracoesQuest3();
    }

    [ContextMenu("Quest 3 URP/Aplicar Nitidez, Bloom HDR e Fix de Sombras")]
    public void AplicarConfiguracoesQuest3()
    {
        // 1. Quality Settings: Anisotropic Filtering Forced On & Anti-Aliasing 4x
        if (forcarFiltroAnisotropico)
        {
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        }
        QualitySettings.antiAliasing = nivelMSAA;
        QualitySettings.pixelLightCount = 1;

        // 2. XR Eye Texture Resolution Scale (1.25x para lentes Pancake do Quest 3)
        if (Application.isPlaying && XRSettings.enabled)
        {
            XRSettings.eyeTextureResolutionScale = escalaResolucaoQuest3;
        }

        // 3. Configuração do URP Asset Ativo (MSAA 4x, Render Scale 1.25, Color Grading HDR)
        UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null)
        {
            urpAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        }

        if (urpAsset != null)
        {
            urpAsset.msaaSampleCount = nivelMSAA;
            urpAsset.renderScale = escalaResolucaoQuest3;
            urpAsset.supportsHDR = true;
            urpAsset.colorGradingMode = ColorGradingMode.HighDynamicRange;
            urpAsset.colorGradingLutSize = 32;
        }

        // 4. Ativa Post-Processing, HDR e Fundo de Céu Diurno da Tarde em todas as Câmeras da cena
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.83f, 0.95f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.95f, 0.86f, 0.74f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.30f, 0.58f, 0.32f, 1f);
        RenderSettings.ambientIntensity = 1.35f;

        Color corCeuTarde = new Color(0.65f, 0.78f, 0.92f, 1f);
        Camera[] cameras = FindObjectsByType<Camera>();
        foreach (Camera cam in cameras)
        {
            if (cam == null) continue;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = corCeuTarde;

            UniversalAdditionalCameraData camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null)
            {
                camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            camData.renderPostProcessing = true;
            camData.renderShadows = true;
        }

        // 5. Garante Volume Global com Bloom HDR configurado
        ConfigurarVolumeGlobalBloom();

        // 6. Corrige "Reduced additional punctual light shadows resolution" e aplica iluminação noturna
        if (corrigirSombrasLuzesPontuais)
        {
            CorrigirLuzesESombrasNoturnas();
        }
    }

    public void ConfigurarVolumeGlobalBloom()
    {
        Volume volumeGlobal = FindAnyObjectByType<Volume>();
        if (volumeGlobal == null)
        {
            GameObject volObj = new GameObject("Global Volume");
            volumeGlobal = volObj.AddComponent<Volume>();
        }

        volumeGlobal.isGlobal = true;
        volumeGlobal.weight = 1.0f;
        volumeGlobal.priority = 10f;

        if (volumeGlobal.sharedProfile == null)
        {
            volumeGlobal.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            volumeGlobal.sharedProfile.name = "Quest3_CyberNeon_Profile";
        }

        VolumeProfile profile = volumeGlobal.sharedProfile;

        if (!profile.TryGet<Bloom>(out Bloom bloom))
        {
            bloom = profile.Add<Bloom>(true);
        }

        // Força valores estritos da Especificação Mestra (Seção 3: Intensity máx 0.2, Threshold 1.5)
        bloomThreshold = 1.5f;
        bloomIntensity = 0.2f;
        bloomScatter = 0.35f;

        bloom.active = true;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = bloomThreshold;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = bloomIntensity;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = bloomScatter;
        bloom.highQualityFiltering.overrideState = true;
        bloom.highQualityFiltering.value = true;

        if (!profile.TryGet<Tonemapping>(out Tonemapping tonemapping))
        {
            tonemapping = profile.Add<Tonemapping>(true);
        }
        tonemapping.active = true;
        tonemapping.mode.overrideState = true;
        tonemapping.mode.value = TonemappingMode.ACES;
    }

    public void CorrigirLuzesESombrasNoturnas()
    {
        // Remove o container antigo de 7 Point Lights com Soft Shadows que estouravam o Atlas de Sombras do URP
        GameObject containerLuzesAntigo = GameObject.Find("Iluminacao_Corredores_SciFi");
        if (containerLuzesAntigo != null)
        {
            if (Application.isPlaying) Destroy(containerLuzesAntigo);
            else DestroyImmediate(containerLuzesAntigo);
        }

        Light[] todasLuzes = FindObjectsByType<Light>();
        foreach (Light l in todasLuzes)
        {
            if (l == null) continue;

            if (l.type == LightType.Directional)
            {
                // Luz solar quente diurna de tarde (Golden Hour) com sombras suaves
                l.intensity = 1.65f;
                l.color = new Color(1.0f, 0.93f, 0.84f, 1.0f);
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.32f;
                l.transform.rotation = Quaternion.Euler(34f, 48f, 0f);
            }
            else if (l.name != "Luz_Portal")
            {
                // Qualquer luz pontual remanescente (ex: feedback visual temporário) nunca projeta sombra no atlas URP
                l.shadows = LightShadows.None;
            }
        }

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.83f, 0.95f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.95f, 0.86f, 0.74f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.30f, 0.58f, 0.32f, 1f);
        RenderSettings.ambientIntensity = 1.35f;
    }
}


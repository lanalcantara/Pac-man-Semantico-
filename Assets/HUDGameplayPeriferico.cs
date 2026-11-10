using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (CIn-UFPE) - HUD Holográfico de Gameplay Periférico
/// ============================================================================
/// Monitor de telemetria posicionado com precisão no canto superior esquerdo.
/// Exibe:
/// 1. Vidas com ícones de coração vermelhos (♥ ♥ ♥) estilizados e nítidos.
/// 2. Pontuação em alta definição (Zero-GC).
/// 3. Orbes semânticos capturados / total da missão.
/// 4. Barra de poder temporizada e alerta de proximidade integrados na moldura.
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class HUDGameplayPeriferico : MonoBehaviour
{
    public static HUDGameplayPeriferico Instancia { get; private set; }

    [Header("Configurações Visuais")]
    public bool exibirHUD = true;
    public Vector2 tamanhoPainel = new Vector2(320f, 165f);
    public Vector2 posicaoAncorada = new Vector2(30f, -30f);

    private Canvas m_CanvasHUD;
    private GameObject m_PainelRaiz;
    private TMP_Text m_TextoVidas;
    private TMP_Text m_TextoPontos;
    private TMP_Text m_TextoOrbes;

    // Barra de Power Pellet
    private GameObject m_ContainerBarraPoder;
    private Image m_PreenchimentoBarraPoder;
    private TMP_Text m_TextoTempoPoder;

    // Alerta de Proximidade
    private GameObject m_BadgePerigoProximo;
    private TMP_Text m_TextoPerigoProximo;

    // Cache Zero-GC
    private int m_UltimaPontuacao = -1;
    private int m_UltimasVidas = -1;
    private int m_UltimasOrbes = -1;
    private float m_TimerPiscaPerigo = 0f;

    private ControladorXRJogador m_ControladorJogador;
    private PacMan.Semantics.MotorRaciocinioSemantico m_MotorSemantico;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        Instancia = this;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void GarantirNaCena()
    {
        if (Instancia == null)
        {
            GameObject hudObj = new GameObject("HUD_Gameplay_Periferico");
            hudObj.AddComponent<HUDGameplayPeriferico>();
            DontDestroyOnLoad(hudObj);
        }
    }

    private void Start()
    {
        ConstruirInterfaceHUD();
    }

    private void Update()
    {
        if (!exibirHUD || m_PainelRaiz == null) return;

        // Oculta durante o Menu Hub aberto ou telas finais de Vitória/GameOver
        if (HubInicialAmbiente.Instancia != null &&
            (HubInicialAmbiente.Instancia.hubAberto || HubInicialAmbiente.Instancia.GameOverAberto || HubInicialAmbiente.Instancia.VitoriaAberta))
        {
            if (m_PainelRaiz.activeSelf) m_PainelRaiz.SetActive(false);
            return;
        }

        if (!m_PainelRaiz.activeSelf) m_PainelRaiz.SetActive(true);

        LocalizarReferencias();
        AtualizarPontosEVidas();
        AtualizarBarraPoder();
        AtualizarSensorProximidade();
    }

    private void LateUpdate()
    {
        AtualizarAcompanhamentoOlharVR();
    }

    /// <summary>
    /// Mantém o HUD periférico (Vidas, Pontos, Orbes) fixado no canto superior esquerdo da visão no VR.
    /// Acompanha o olhar do usuário suavemente, mantendo a telemetria sempre visível sem obstruir o jogo.
    /// </summary>
    private void AtualizarAcompanhamentoOlharVR()
    {
        if (m_CanvasHUD == null || !exibirHUD) return;
        if (m_PainelRaiz == null || !m_PainelRaiz.activeSelf) return;

        Camera cam = Camera.main;
        if (m_ControladorJogador != null && m_ControladorJogador.cameraPrincipalXR != null)
        {
            cam = m_ControladorJogador.cameraPrincipalXR;
        }
        if (cam == null) return;

        bool emVR = UnityEngine.XR.XRSettings.isDeviceActive || (UnityEngine.XR.XRSettings.enabled && !string.IsNullOrEmpty(UnityEngine.XR.XRSettings.loadedDeviceName)) ||
                    (UnityEngine.XR.Management.XRGeneralSettings.Instance != null && UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager != null && UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager.activeLoader != null) ||
                    Application.platform == RuntimePlatform.Android;

        if (emVR)
        {
            if (m_CanvasHUD.renderMode != RenderMode.WorldSpace)
            {
                m_CanvasHUD.renderMode = RenderMode.WorldSpace;
                m_CanvasHUD.transform.localScale = Vector3.one * 0.0011f;
            }

            Vector3 camPos = cam.transform.position;
            Vector3 camFwd = cam.transform.forward;
            Vector3 camUp = cam.transform.up;
            Vector3 camRight = cam.transform.right;

            // Flutua no canto superior esquerdo a 1.6 metros de distância da visão
            Vector3 posAlvo = camPos + camFwd * 1.6f + camUp * 0.44f - camRight * 0.62f;
            Quaternion rotAlvo = Quaternion.LookRotation(camFwd, camUp);

            m_CanvasHUD.transform.position = Vector3.Lerp(m_CanvasHUD.transform.position, posAlvo, Time.unscaledDeltaTime * 14f);
            m_CanvasHUD.transform.rotation = Quaternion.Slerp(m_CanvasHUD.transform.rotation, rotAlvo, Time.unscaledDeltaTime * 14f);
        }
    }

    private void LocalizarReferencias()
    {
        if (m_ControladorJogador == null)
        {
            m_ControladorJogador = FindAnyObjectByType<ControladorXRJogador>();
        }
        if (m_MotorSemantico == null)
        {
            m_MotorSemantico = FindAnyObjectByType<PacMan.Semantics.MotorRaciocinioSemantico>();
        }
    }

    private void AtualizarPontosEVidas()
    {
        // 1. Vidas com Corações
        int vidas = m_ControladorJogador != null ? m_ControladorJogador.vidasRestantes : 3;
        if (vidas != m_UltimasVidas && m_TextoVidas != null)
        {
            m_UltimasVidas = vidas;
            string coracoes = "";
            for (int i = 0; i < 3; i++)
            {
                if (i < vidas)
                {
                    coracoes += "<color=#FF2255>♥</color> ";
                }
                else
                {
                    coracoes += "<color=#3D485C>♥</color> ";
                }
            }
            m_TextoVidas.text = $"VIDAS:  <size=22>{coracoes.TrimEnd()}</size>";
        }

        // 2. Pontuação Digital
        int pontos = InstanciaSemanticaBase.pontuacaoLogica;
        if (pontos != m_UltimaPontuacao && m_TextoPontos != null)
        {
            m_UltimaPontuacao = pontos;
            m_TextoPontos.text = $"PONTOS:  <color=#00FF99><b>{pontos:D5}</b></color>";
        }

        // 3. Orbes Coletados
        int orbes = InstanciaSemanticaBase.totalColetadas;
        if (orbes != m_UltimasOrbes && m_TextoOrbes != null)
        {
            m_UltimasOrbes = orbes;
            m_TextoOrbes.text = $"ORBES:  <color=#00E5FF><b>{orbes} / 12</b></color>";
        }
    }

    private void AtualizarBarraPoder()
    {
        if (m_MotorSemantico == null || m_ContainerBarraPoder == null) return;

        bool isPowered = m_MotorSemantico.pacmanIsPowered;
        float tempoRestante = m_MotorSemantico.tempoPowerRestante;

        if (isPowered && tempoRestante > 0f)
        {
            if (!m_ContainerBarraPoder.activeSelf) m_ContainerBarraPoder.SetActive(true);

            float progresso = Mathf.Clamp01(tempoRestante / 8.0f);
            if (m_PreenchimentoBarraPoder != null)
            {
                m_PreenchimentoBarraPoder.fillAmount = progresso;
            }

            if (m_TextoTempoPoder != null)
            {
                m_TextoTempoPoder.text = $"PODER: <b>{tempoRestante:F1}s</b> (FANTASMAS AZUIS)";
            }
        }
        else
        {
            if (m_ContainerBarraPoder.activeSelf) m_ContainerBarraPoder.SetActive(false);
        }
    }

    private void AtualizarSensorProximidade()
    {
        if (m_ControladorJogador == null || m_BadgePerigoProximo == null) return;

        // Se estiver com poder, não há perigo de perseguição
        if (m_MotorSemantico != null && m_MotorSemantico.pacmanIsPowered)
        {
            if (m_BadgePerigoProximo.activeSelf) m_BadgePerigoProximo.SetActive(false);
            return;
        }

        Vector3 posJogador = m_ControladorJogador.transform.position;
        var fantasmas = FindObjectsByType<ObstaculoInvalidoSemantico>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float menorDistancia = 999f;

        for (int i = 0; i < fantasmas.Length; i++)
        {
            if (fantasmas[i] != null && fantasmas[i].gameObject.activeInHierarchy)
            {
                float d = Vector3.Distance(posJogador, fantasmas[i].transform.position);
                if (d < menorDistancia) menorDistancia = d;
            }
        }

        // Alerta quando o fantasma estiver a menos de 3.5 metros
        bool perigo = menorDistancia <= 3.5f;

        if (perigo)
        {
            m_TimerPiscaPerigo += Time.deltaTime * 6.0f;
            bool visivel = Mathf.Sin(m_TimerPiscaPerigo) > 0f;

            if (!m_BadgePerigoProximo.activeSelf) m_BadgePerigoProximo.SetActive(true);
            if (m_TextoPerigoProximo != null)
            {
                m_TextoPerigoProximo.color = visivel ? new Color(1.0f, 0.20f, 0.25f) : new Color(1.0f, 0.65f, 0.10f);
                m_TextoPerigoProximo.text = $"PERIGO PROXIMO: {menorDistancia:F1}m";
            }
        }
        else
        {
            if (m_BadgePerigoProximo.activeSelf) m_BadgePerigoProximo.SetActive(false);
        }
    }

    // =========================================================================
    // CONSTRUÇÃO DINÂMICA DO CANVAS E ELEMENTOS UI NO CANTO SUPERIOR ESQUERDO
    // =========================================================================

    private void ConstruirInterfaceHUD()
    {
        // 1. Canvas ScreenSpace
        GameObject canvasObj = new GameObject("HUD_Gameplay_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);
        m_CanvasHUD = canvasObj.GetComponent<Canvas>();

        Camera camTarget = Camera.main;
        var ctrl = FindAnyObjectByType<ControladorXRJogador>();
        if (ctrl != null && ctrl.cameraPrincipalXR != null) camTarget = ctrl.cameraPrincipalXR;

        bool emVR = UnityEngine.XR.XRSettings.isDeviceActive || (UnityEngine.XR.XRSettings.enabled && !string.IsNullOrEmpty(UnityEngine.XR.XRSettings.loadedDeviceName)) ||
                    (UnityEngine.XR.Management.XRGeneralSettings.Instance != null && UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager != null && UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager.activeLoader != null) ||
                    Application.platform == RuntimePlatform.Android;

        if (emVR)
        {
            m_CanvasHUD.renderMode = RenderMode.WorldSpace;
            m_CanvasHUD.transform.localScale = Vector3.one * 0.0011f;
        }
        else if (camTarget != null)
        {
            m_CanvasHUD.renderMode = RenderMode.ScreenSpaceCamera;
            m_CanvasHUD.worldCamera = camTarget;
            m_CanvasHUD.planeDistance = 1.0f;
        }
        else
        {
            m_CanvasHUD.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        m_CanvasHUD.sortingOrder = 30; // Visível sobre o jogo

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // 2. Painel Raiz (Canto Superior Esquerdo com Pivot 0,1)
        GameObject painel = new GameObject("Painel_Telemetria_SuperiorEsquerdo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        painel.transform.SetParent(canvasObj.transform, false);
        m_PainelRaiz = painel;

        RectTransform rt = painel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = posicaoAncorada; // (30f, -30f)
        rt.sizeDelta = tamanhoPainel;           // (320f, 165f)

        Image imgFundo = painel.GetComponent<Image>();
        imgFundo.color = new Color(0.02f, 0.05f, 0.12f, 0.90f);

        Outline outline = painel.GetComponent<Outline>();
        outline.effectColor = new Color(0.0f, 0.85f, 1.0f, 0.50f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // 3. Cabeçalho / Título do Monitor
        CriarTexto(painel.transform, "Txt_Titulo",
            "<b>PAC-MAN SEMÂNTICO</b>  <size=11><color=#88AAFF>| TELEMETRIA</color></size>",
            13, FontStyles.Bold,
            new Vector2(15f, -14f), new Vector2(290f, 22f),
            new Color(0.0f, 0.95f, 1.0f), TextAlignmentOptions.Left);

        // Divisor decorativo
        GameObject divisor = new GameObject("Divisor_HUD", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        divisor.transform.SetParent(painel.transform, false);
        RectTransform rtDiv = divisor.GetComponent<RectTransform>();
        rtDiv.anchorMin = new Vector2(0f, 1f);
        rtDiv.anchorMax = new Vector2(0f, 1f);
        rtDiv.pivot = new Vector2(0f, 1f);
        rtDiv.anchoredPosition = new Vector2(15f, -32f);
        rtDiv.sizeDelta = new Vector2(290f, 1.5f);
        divisor.GetComponent<Image>().color = new Color(0.0f, 0.85f, 1.0f, 0.40f);

        // 4. Linha de Vidas com Ícones de Corações
        m_TextoVidas = CriarTexto(painel.transform, "Txt_Vidas",
            "VIDAS:  <size=22><color=#FF2255>♥</color> <color=#FF2255>♥</color> <color=#FF2255>♥</color></size>",
            14, FontStyles.Bold,
            new Vector2(15f, -44f), new Vector2(290f, 26f),
            Color.white, TextAlignmentOptions.Left);

        // 5. Linha de Pontuação Digital
        m_TextoPontos = CriarTexto(painel.transform, "Txt_Pontos",
            "PONTOS:  <color=#00FF99><b>00000</b></color>",
            14, FontStyles.Bold,
            new Vector2(15f, -74f), new Vector2(290f, 24f),
            Color.white, TextAlignmentOptions.Left);

        // 6. Linha de Orbes
        m_TextoOrbes = CriarTexto(painel.transform, "Txt_Orbes",
            "ORBES:  <color=#00E5FF><b>0 / 12</b></color>",
            14, FontStyles.Bold,
            new Vector2(15f, -102f), new Vector2(290f, 24f),
            new Color(0.85f, 0.92f, 1.0f), TextAlignmentOptions.Left);

        // 7. Barra de Power Pellet (Contagem regressiva de Poder embutida)
        GameObject containerPoder = new GameObject("Container_Poder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        containerPoder.transform.SetParent(painel.transform, false);
        m_ContainerBarraPoder = containerPoder;

        RectTransform rtPoder = containerPoder.GetComponent<RectTransform>();
        rtPoder.anchorMin = new Vector2(0f, 1f);
        rtPoder.anchorMax = new Vector2(0f, 1f);
        rtPoder.pivot = new Vector2(0f, 1f);
        rtPoder.anchoredPosition = new Vector2(15f, -132f);
        rtPoder.sizeDelta = new Vector2(290f, 22f);

        Image imgFundoPoder = containerPoder.GetComponent<Image>();
        imgFundoPoder.color = new Color(0.18f, 0.14f, 0.02f, 0.95f);

        // Preenchimento animado
        GameObject barraFill = new GameObject("Barra_Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barraFill.transform.SetParent(containerPoder.transform, false);
        RectTransform rtFill = barraFill.GetComponent<RectTransform>();
        rtFill.anchorMin = Vector2.zero;
        rtFill.anchorMax = Vector2.one;
        rtFill.sizeDelta = Vector2.zero;

        m_PreenchimentoBarraPoder = barraFill.GetComponent<Image>();
        m_PreenchimentoBarraPoder.type = Image.Type.Filled;
        m_PreenchimentoBarraPoder.fillMethod = Image.FillMethod.Horizontal;
        m_PreenchimentoBarraPoder.fillOrigin = (int)Image.OriginHorizontal.Left;
        m_PreenchimentoBarraPoder.color = new Color(1.0f, 0.82f, 0.0f, 0.85f); // Dourado brilhante

        m_TextoTempoPoder = CriarTexto(containerPoder.transform, "Txt_TempoPoder",
            "PODER: 8.0s (FANTASMAS AZUIS)", 11, FontStyles.Bold,
            Vector2.zero, new Vector2(280f, 20f), Color.white, TextAlignmentOptions.Center);

        containerPoder.SetActive(false);

        // 8. Badge de Alerta de Proximidade (Abaixo do painel, integrado)
        GameObject badgePerigo = new GameObject("Badge_Perigo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        badgePerigo.transform.SetParent(painel.transform, false);
        m_BadgePerigoProximo = badgePerigo;

        RectTransform rtBadge = badgePerigo.GetComponent<RectTransform>();
        rtBadge.anchorMin = new Vector2(0f, 0f);
        rtBadge.anchorMax = new Vector2(1f, 0f);
        rtBadge.pivot = new Vector2(0.5f, 1f);
        rtBadge.anchoredPosition = new Vector2(0f, -4f);
        rtBadge.sizeDelta = new Vector2(0f, 24f);

        Image imgBadge = badgePerigo.GetComponent<Image>();
        imgBadge.color = new Color(0.25f, 0.02f, 0.05f, 0.95f);

        m_TextoPerigoProximo = CriarTexto(badgePerigo.transform, "Txt_Perigo", "PERIGO PROXIMO!", 11, FontStyles.Bold,
            Vector2.zero, new Vector2(290f, 22f), new Color(1.0f, 0.20f, 0.25f), TextAlignmentOptions.Center);

        badgePerigo.SetActive(false);
    }

    private static TMP_Text CriarTexto(Transform pai, string nome, string conteudo, float tamanhoFonte,
        FontStyles estilo, Vector2 posicao, Vector2 dimensao, Color cor,
        TextAlignmentOptions alinhamento = TextAlignmentOptions.Left)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(pai, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = posicao;
        rt.sizeDelta = dimensao;

        TMP_Text texto = go.GetComponent<TextMeshProUGUI>();
        texto.text = conteudo;
        texto.fontSize = tamanhoFonte;
        texto.fontStyle = estilo;
        texto.color = cor;
        texto.alignment = alinhamento;
        texto.raycastTarget = false;
        texto.overflowMode = TextOverflowModes.Ellipsis;

        return texto;
    }
}

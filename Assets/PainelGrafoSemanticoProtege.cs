using PacMan.XR;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (CIn-UFPE)
/// Painel de Grafo Semântico e Topologia Ontológica (Estilo Protégé / OntoGraf)
/// ============================================================================
/// 1. Durante a partida, mantém-se recolhido (exibindo apenas o banner inferior sutil ao coletar orbes).
/// 2. Ao final de toda partida (Vitória ou Derrota), apresenta o mapa ontológico centralizado,
///    nítido e perfeitamente ajustado dentro da moldura:
///    - Vitória: mapa COMPLETO com todos os nós e arestas iluminados em verde/neon (100% consistente).
///    - Derrota: mapa INCOMPLETO com apenas os nós coletados ativos e o restante apagado.
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class PainelGrafoSemanticoProtege : MonoBehaviour
{
    public static PainelGrafoSemanticoProtege Instancia { get; private set; }

    [Header("Configuração de Exibição")]
    public Vector2 tamanhoPainelFinal = new Vector2(940f, 620f);

    // Representação interna de um Nó do Grafo UI
    private class NoGrafoUI
    {
        public string id;
        public string rotulo;
        public Vector2 posicaoLocal;
        public Color corPadrao;
        public bool desatado;
        public Image imagemFundo;
        public Image imagemBorda;
        public TMP_Text textoRotulo;
        public GameObject objRaiz;
    }

    // Representação interna de uma Aresta UI
    private class ArestaGrafoUI
    {
        public string idOrigem;
        public string idDestino;
        public string relacaoOWL;
        public RectTransform rtLinha;
        public Image imagemLinha;
        public bool ativa;
    }

    private Canvas m_CanvasGrafo;
    private GameObject m_PainelRaiz;
    private Outline m_OutlinePainel;
    private TMP_Text m_TextoTituloPainel;
    private TMP_Text m_TextoMetricas;
    private TMP_Text m_TextoConceitoFoco;
    private Button m_BotaoVoltarMenu;

    // Banner de notificação inferior durante a partida
    private GameObject m_BannerOrbeColetada;
    private TMP_Text m_TextoBannerTitulo;
    private TMP_Text m_TextoBannerDetalhes;
    private float m_TimerBanner = 0f;

    // Dados da partida
    private int m_OrbesColetadas = 0;
    private int m_TotalOrbesMissao = 12;
    private bool m_ExibindoTelaFinal = false;

    private readonly Dictionary<string, NoGrafoUI> m_Nos = new Dictionary<string, NoGrafoUI>();
    private readonly List<ArestaGrafoUI> m_Arestas = new List<ArestaGrafoUI>();

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
    private static void GarantirPainelNaCena()
    {
        if (Instancia == null)
        {
            var painel = FindAnyObjectByType<PainelGrafoSemanticoProtege>();
            if (painel == null)
            {
                GameObject obj = new GameObject("PainelGrafoSemanticoProtege");
                obj.AddComponent<PainelGrafoSemanticoProtege>();
            }
        }
    }

    private void Start()
    {
        ConstruirInterfaceGrafo();
        InicializarTopologiaParasitologia();

        // Durante o gameplay, o mapa central fica oculto (só abre no final de todo jogo)
        if (m_PainelRaiz != null)
        {
            m_PainelRaiz.SetActive(false);
        }
    }

    private void Update()
    {
        // 1. Contador do banner inferior sutil de gema coletada
        if (m_TimerBanner > 0f)
        {
            m_TimerBanner -= Time.deltaTime;
            if (m_TimerBanner <= 0f && m_BannerOrbeColetada != null)
            {
                m_BannerOrbeColetada.SetActive(false);
            }
        }

        // 2. Interação de entrada na tela final do Grafo
        if (m_ExibindoTelaFinal && m_PainelRaiz != null && m_PainelRaiz.activeSelf)
        {
            bool confirmar = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) ||
                             Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton0) ||
                             Input.GetKeyDown(KeyCode.JoystickButton7);

            var provedorXR = FindAnyObjectByType<MonoBehaviour>() as IProvedorEntradaXR;
            if (provedorXR != null)
            {
                var dados = provedorXR.LerEntradas(0.1f);
                if (dados.Interagir || dados.AlternarMenuHub)
                {
                    confirmar = true;
                }
            }

            if (confirmar)
            {
                AcaoVoltarMenuPrincipal();
            }
        }
    }

    private void LateUpdate()
    {
        AtualizarAcompanhamentoOlharVR();
    }

    /// <summary>
    /// Mantém a janela final do grafo e o banner inferior sutil de orbes coletadas
    /// sempre alinhados diretamente na frente do olhar do jogador no Meta Quest 3.
    /// </summary>
    private void AtualizarAcompanhamentoOlharVR()
    {
        if (m_CanvasGrafo == null) return;
        bool visivel = (m_ExibindoTelaFinal && m_PainelRaiz != null && m_PainelRaiz.activeSelf) ||
                       (m_BannerOrbeColetada != null && m_BannerOrbeColetada.activeSelf);

        if (!visivel) return;

        Camera cam = Camera.main;
        var ctrl = FindAnyObjectByType<ControladorXRJogador>();
        if (ctrl != null && ctrl.cameraPrincipalXR != null) cam = ctrl.cameraPrincipalXR;
        if (cam == null) return;

        bool emVR = UnityEngine.XR.XRSettings.isDeviceActive || (UnityEngine.XR.XRSettings.enabled && !string.IsNullOrEmpty(UnityEngine.XR.XRSettings.loadedDeviceName)) ||
                    (UnityEngine.XR.Management.XRGeneralSettings.Instance != null && UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager != null && UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager.activeLoader != null) ||
                    Application.platform == RuntimePlatform.Android;

        if (emVR)
        {
            if (m_CanvasGrafo.renderMode != RenderMode.WorldSpace)
            {
                m_CanvasGrafo.renderMode = RenderMode.WorldSpace;
                m_CanvasGrafo.transform.localScale = Vector3.one * 0.0018f;
            }

            Vector3 camPos = cam.transform.position;
            Vector3 camFwd = cam.transform.forward;
            Vector3 camUp = cam.transform.up;

            Vector3 posAlvo = camPos + camFwd * 1.95f;
            Quaternion rotAlvo = Quaternion.LookRotation(camFwd, camUp);

            m_CanvasGrafo.transform.position = Vector3.Lerp(m_CanvasGrafo.transform.position, posAlvo, Time.unscaledDeltaTime * 14f);
            m_CanvasGrafo.transform.rotation = Quaternion.Slerp(m_CanvasGrafo.transform.rotation, rotAlvo, Time.unscaledDeltaTime * 14f);
        }
    }

    private void ConstruirInterfaceGrafo()
    {
        // 1. Canvas dedicado
        GameObject canvasObj = new GameObject("GrafoSemantico_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);

        m_CanvasGrafo = canvasObj.GetComponent<Canvas>();
        Camera camTarget = Camera.main;
        var ctrl = FindAnyObjectByType<ControladorXRJogador>();
        if (ctrl != null && ctrl.cameraPrincipalXR != null) camTarget = ctrl.cameraPrincipalXR;

        bool emVR = UnityEngine.XR.XRSettings.isDeviceActive || (UnityEngine.XR.XRSettings.enabled && !string.IsNullOrEmpty(UnityEngine.XR.XRSettings.loadedDeviceName)) ||
                    (UnityEngine.XR.Management.XRGeneralSettings.Instance != null && UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager != null && UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager.activeLoader != null) ||
                    Application.platform == RuntimePlatform.Android;

        if (emVR)
        {
            m_CanvasGrafo.renderMode = RenderMode.WorldSpace;
            m_CanvasGrafo.transform.localScale = Vector3.one * 0.0018f;
        }
        else if (camTarget != null)
        {
            m_CanvasGrafo.renderMode = RenderMode.ScreenSpaceCamera;
            m_CanvasGrafo.worldCamera = camTarget;
            m_CanvasGrafo.planeDistance = 1.0f;
        }
        else
        {
            m_CanvasGrafo.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        m_CanvasGrafo.sortingOrder = 95;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // 2. Painel Janela Centralizada do Grafo
        m_PainelRaiz = new GameObject("Janela_Grafo_Protege_Central", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        m_PainelRaiz.transform.SetParent(canvasObj.transform, false);

        RectTransform rtJanela = m_PainelRaiz.GetComponent<RectTransform>();
        rtJanela.anchorMin = new Vector2(0.5f, 0.5f);
        rtJanela.anchorMax = new Vector2(0.5f, 0.5f);
        rtJanela.pivot = new Vector2(0.5f, 0.5f);
        rtJanela.anchoredPosition = Vector2.zero;
        rtJanela.sizeDelta = tamanhoPainelFinal; // (940f, 620f)

        Image imgFundo = m_PainelRaiz.GetComponent<Image>();
        imgFundo.color = new Color(0.02f, 0.05f, 0.10f, 0.96f);

        m_OutlinePainel = m_PainelRaiz.GetComponent<Outline>();
        m_OutlinePainel.effectColor = new Color(0.0f, 0.90f, 1.0f, 0.85f);
        m_OutlinePainel.effectDistance = new Vector2(2f, -2f);

        // 3. Cabeçalho do Grafo
        m_TextoTituloPainel = CriarTexto(m_PainelRaiz.transform, "Titulo_Grafo",
            "★ TOPOLOGIA ONTOLÓGICA FORMAL (PROTÉGÉ OWL) ★",
            20, FontStyles.Bold,
            new Vector2(0f, 265f), new Vector2(900f, 34f),
            new Color(0.0f, 0.95f, 1.0f), TextAlignmentOptions.Center);

        m_TextoMetricas = CriarTexto(m_PainelRaiz.transform, "Texto_Metricas",
            "Síntese de Axiomas e Indivíduos • Raciocinador HermiT",
            14, FontStyles.Normal,
            new Vector2(0f, 230f), new Vector2(900f, 26f),
            new Color(0.75f, 0.85f, 0.95f), TextAlignmentOptions.Center);

        // Divisor Neon Superior
        GameObject divisor = new GameObject("Divisor_Sup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        divisor.transform.SetParent(m_PainelRaiz.transform, false);
        RectTransform rtDiv = divisor.GetComponent<RectTransform>();
        rtDiv.anchoredPosition = new Vector2(0f, 208f);
        rtDiv.sizeDelta = new Vector2(880f, 2f);
        divisor.GetComponent<Image>().color = new Color(0.0f, 0.85f, 1.0f, 0.50f);

        // 4. Área de Nós (Área central perfeitamente contida: 880x360)
        GameObject areaGrafo = new GameObject("Area_Desenho_Grafo", typeof(RectTransform));
        areaGrafo.transform.SetParent(m_PainelRaiz.transform, false);
        RectTransform rtArea = areaGrafo.GetComponent<RectTransform>();
        rtArea.anchoredPosition = new Vector2(0f, 20f);
        rtArea.sizeDelta = new Vector2(880f, 350f);

        // 5. Divisor Neon Inferior
        GameObject divisorInf = new GameObject("Divisor_Inf", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        divisorInf.transform.SetParent(m_PainelRaiz.transform, false);
        RectTransform rtDivInf = divisorInf.GetComponent<RectTransform>();
        rtDivInf.anchoredPosition = new Vector2(0f, -175f);
        rtDivInf.sizeDelta = new Vector2(880f, 1.5f);
        divisorInf.GetComponent<Image>().color = new Color(0.0f, 0.85f, 1.0f, 0.35f);

        // 6. Texto de Conclusão / Status
        m_TextoConceitoFoco = CriarTexto(m_PainelRaiz.transform, "Texto_Conceito_Foco",
            "Consistência Lógica dos Axiomas",
            14, FontStyles.Italic,
            new Vector2(0f, -200f), new Vector2(880f, 26f),
            new Color(0.85f, 0.95f, 1.0f), TextAlignmentOptions.Center);

        // 7. Botão Voltar ao Menu Principal
        GameObject btnGO = new GameObject("Botao_Voltar_Menu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
        btnGO.transform.SetParent(m_PainelRaiz.transform, false);
        RectTransform rtBtn = btnGO.GetComponent<RectTransform>();
        rtBtn.anchoredPosition = new Vector2(0f, -245f);
        rtBtn.sizeDelta = new Vector2(480f, 50f);

        Image imgBtn = btnGO.GetComponent<Image>();
        imgBtn.color = new Color(0.0f, 0.55f, 0.85f, 0.95f);
        btnGO.GetComponent<Outline>().effectColor = Color.white;

        m_BotaoVoltarMenu = btnGO.GetComponent<Button>();
        m_BotaoVoltarMenu.onClick.AddListener(AcaoVoltarMenuPrincipal);

        CriarTexto(btnGO.transform, "Txt_Btn", "<b>> VOLTAR AO MENU PRINCIPAL <</b>",
            18, FontStyles.Bold, Vector2.zero, new Vector2(460f, 44f), Color.white, TextAlignmentOptions.Center);

        // Atalho
        CriarTexto(m_PainelRaiz.transform, "Txt_Atalho",
            "[A / ENTER / START / SELECT] Voltar ao Menu",
            12, FontStyles.Italic,
            new Vector2(0f, -285f), new Vector2(880f, 20f),
            new Color(0.60f, 0.75f, 0.88f), TextAlignmentOptions.Center);

        // 8. Constrói Banner Inferior de Notificação Durante Gameplay
        ConstruirBannerInferior(canvasObj.transform);
    }

    private void ConstruirBannerInferior(Transform paiCanvas)
    {
        m_BannerOrbeColetada = new GameObject("Banner_Notificacao_Orbe", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        m_BannerOrbeColetada.transform.SetParent(paiCanvas, false);

        RectTransform rtBanner = m_BannerOrbeColetada.GetComponent<RectTransform>();
        rtBanner.anchorMin = new Vector2(0.5f, 0f);
        rtBanner.anchorMax = new Vector2(0.5f, 0f);
        rtBanner.pivot = new Vector2(0.5f, 0f);
        rtBanner.anchoredPosition = new Vector2(0f, 32f);
        rtBanner.sizeDelta = new Vector2(500f, 46f);

        Image imgBanner = m_BannerOrbeColetada.GetComponent<Image>();
        imgBanner.color = new Color(0.02f, 0.05f, 0.10f, 0.88f);

        Outline outline = m_BannerOrbeColetada.GetComponent<Outline>();
        outline.effectColor = new Color(0.0f, 0.95f, 0.65f, 0.70f);

        m_TextoBannerTitulo = CriarTexto(m_BannerOrbeColetada.transform, "Texto_Banner_Titulo",
            "<b>✦ NÓ ONTOLÓGICO SINTETIZADO ✦</b>", 11, FontStyles.Bold,
            new Vector2(0f, 10f), new Vector2(480f, 18f), new Color(0.0f, 1.0f, 0.70f), TextAlignmentOptions.Center);

        m_TextoBannerDetalhes = CriarTexto(m_BannerOrbeColetada.transform, "Texto_Banner_Detalhes",
            "Instância ontológica conectada ao reasoner...", 11, FontStyles.Normal,
            new Vector2(0f, -8f), new Vector2(480f, 18f), Color.white, TextAlignmentOptions.Center);

        m_BannerOrbeColetada.SetActive(false);
    }

    /// <summary>
    /// Constrói a topologia formal da ontologia com nós perfeitamente enquadrados no card.
    /// </summary>
    private void InicializarTopologiaParasitologia()
    {
        // Nós principais da TBox (Espaçamento amplo e confortável no card de 880x350)
        AdicionarNo("Parasite", "Parasite (Classe)", new Vector2(0f, 100f), new Color(0.88f, 0.15f, 0.95f));
        AdicionarNo("Treatment", "Tratamento", new Vector2(240f, 100f), new Color(1.0f, 0.65f, 0.10f));
        AdicionarNo("Sintomas", "Sintomas Clínicos", new Vector2(-240f, 100f), new Color(0.0f, 0.85f, 0.75f));

        AdicionarNo("Host", "Hospedeiro (Host)", new Vector2(-220f, 15f), new Color(0.0f, 0.85f, 1.0f));
        AdicionarNo("Nematoda", "Nematoda", new Vector2(80f, 10f), new Color(0.65f, 0.25f, 0.95f));
        AdicionarNo("Platyhelminthes", "Platyhelminthes", new Vector2(250f, 10f), new Color(0.50f, 0.15f, 0.85f));

        AdicionarNo("Human", "Human (Homem)", new Vector2(-290f, -85f), new Color(0.30f, 0.65f, 1.0f));
        AdicionarNo("Pig", "Pig (Suíno)", new Vector2(-150f, -85f), new Color(1.0f, 0.45f, 0.70f));
        AdicionarNo("Vetores", "Vetores Biológicos", new Vector2(160f, -85f), new Color(0.20f, 0.95f, 0.40f));

        // Arestas Formais OWL
        ConectarAresta("Host", "Parasite", "infects");
        ConectarAresta("Sintomas", "Host", "manifests_in");
        ConectarAresta("Human", "Host", "subClassOf");
        ConectarAresta("Pig", "Host", "subClassOf");
        ConectarAresta("Nematoda", "Parasite", "subClassOf");
        ConectarAresta("Platyhelminthes", "Parasite", "subClassOf");
        ConectarAresta("Treatment", "Parasite", "treats");
        ConectarAresta("Vetores", "Parasite", "transmits");
    }

    private void AdicionarNo(string id, string rotulo, Vector2 pos, Color corPadrao)
    {
        Transform areaGrafo = m_PainelRaiz.transform.Find("Area_Desenho_Grafo");
        if (areaGrafo == null) return;

        GameObject noObj = new GameObject("No_" + id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        noObj.transform.SetParent(areaGrafo, false);

        RectTransform rtNo = noObj.GetComponent<RectTransform>();
        rtNo.anchoredPosition = pos;
        rtNo.sizeDelta = new Vector2(115f, 34f);

        Image imgFundo = noObj.GetComponent<Image>();
        imgFundo.color = new Color(0.12f, 0.14f, 0.18f, 0.85f); // Inicialmente apagado

        GameObject bordaObj = new GameObject("Borda", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bordaObj.transform.SetParent(noObj.transform, false);
        RectTransform rtBorda = bordaObj.GetComponent<RectTransform>();
        rtBorda.anchorMin = Vector2.zero;
        rtBorda.anchorMax = Vector2.one;
        rtBorda.sizeDelta = Vector2.zero;
        Image imgBorda = bordaObj.GetComponent<Image>();
        imgBorda.color = new Color(0.35f, 0.40f, 0.50f, 0.50f);

        TMP_Text texto = CriarTexto(noObj.transform, "Rotulo", rotulo, 11, FontStyles.Bold, Vector2.zero, new Vector2(108f, 30f), new Color(0.65f, 0.70f, 0.80f), TextAlignmentOptions.Center);

        NoGrafoUI no = new NoGrafoUI
        {
            id = id,
            rotulo = rotulo,
            posicaoLocal = pos,
            corPadrao = corPadrao,
            desatado = false,
            imagemFundo = imgFundo,
            imagemBorda = imgBorda,
            textoRotulo = texto,
            objRaiz = noObj
        };

        m_Nos[id] = no;
    }

    private void ConectarAresta(string idOrigem, string idDestino, string relacao)
    {
        Transform areaGrafo = m_PainelRaiz.transform.Find("Area_Desenho_Grafo");
        if (areaGrafo == null) return;

        if (!m_Nos.TryGetValue(idOrigem, out NoGrafoUI noOrigem) ||
            !m_Nos.TryGetValue(idDestino, out NoGrafoUI noDestino))
        {
            return;
        }

        GameObject linhaObj = new GameObject($"Aresta_{idOrigem}_{idDestino}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        linhaObj.transform.SetParent(areaGrafo, false);
        linhaObj.transform.SetAsFirstSibling(); // Linhas atrás dos nós

        RectTransform rtLinha = linhaObj.GetComponent<RectTransform>();
        Vector2 p1 = noOrigem.posicaoLocal;
        Vector2 p2 = noDestino.posicaoLocal;
        Vector2 dir = p2 - p1;
        float distancia = dir.magnitude;
        float angulo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        rtLinha.anchorMin = new Vector2(0.5f, 0.5f);
        rtLinha.anchorMax = new Vector2(0.5f, 0.5f);
        rtLinha.pivot = new Vector2(0f, 0.5f);
        rtLinha.anchoredPosition = p1;
        rtLinha.sizeDelta = new Vector2(distancia, 1.8f);
        rtLinha.localRotation = Quaternion.Euler(0f, 0f, angulo);

        Image imgLinha = linhaObj.GetComponent<Image>();
        imgLinha.color = new Color(0.20f, 0.25f, 0.35f, 0.40f); // Inicialmente apagada

        ArestaGrafoUI aresta = new ArestaGrafoUI
        {
            idOrigem = idOrigem,
            idDestino = idDestino,
            relacaoOWL = relacao,
            rtLinha = rtLinha,
            imagemLinha = imgLinha,
            ativa = false
        };

        m_Arestas.Add(aresta);
    }

    /// <summary>
    /// Notifica o consumo de uma gema durante a partida.
    /// </summary>
    public void NotificarConsumoOntologico(string idInstancia, string nomeClasse)
    {
        string chaveBusca = nomeClasse.Trim();
        NoGrafoUI noAlvo = null;

        foreach (var kvp in m_Nos)
        {
            if (chaveBusca.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                kvp.Key.IndexOf(chaveBusca, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                noAlvo = kvp.Value;
                break;
            }
        }

        if (noAlvo == null)
        {
            if (chaveBusca.Contains("Ascaris") || chaveBusca.Contains("Enterobius"))
                m_Nos.TryGetValue("Nematoda", out noAlvo);
            else if (chaveBusca.Contains("Taenia"))
                m_Nos.TryGetValue("Platyhelminthes", out noAlvo);
            else if (chaveBusca.Contains("Febre") || chaveBusca.Contains("Sintoma"))
                m_Nos.TryGetValue("Sintomas", out noAlvo);
            else if (chaveBusca.Contains("Anopheles") || chaveBusca.Contains("Vetor"))
                m_Nos.TryGetValue("Vetores", out noAlvo);
            else
                m_Nos.TryGetValue("Parasite", out noAlvo);
        }

        if (noAlvo != null)
        {
            noAlvo.desatado = true;
        }
    }

    public void ExibirBannerOrbeColetada(string nomeClasse, string idInstancia, string expressaoDL, bool ehOrbeDePoder, int indiceOrbe = 0, int totalOrbes = 12)
    {
        if (m_BannerOrbeColetada == null) return;

        if (m_TextoBannerTitulo != null)
        {
            string tipo = ehOrbeDePoder ? "✦ ORBE DE PODER ONTOLÓGICO ✦" : "✦ NÓ ONTOLÓGICO SINTETIZADO ✦";
            string cor = ehOrbeDePoder ? "#FFD700" : "#00FF99";
            m_TextoBannerTitulo.text = $"<color={cor}><b>{tipo}</b></color>";
        }

        if (m_TextoBannerDetalhes != null)
        {
            m_TextoBannerDetalhes.text = $"<color=#00E5FF><b>{nomeClasse}</b></color>  |  <color=#CCCCCC>{expressaoDL}</color>";
        }

        m_BannerOrbeColetada.SetActive(true);
        m_TimerBanner = 2.5f;
    }

    /// <summary>
    /// =========================================================================
    /// APRESENTA O GRAFO ONTOLÓGICO NO FINAL DE TODO JOGO (VITÓRIA OU DERROTA)
    /// =========================================================================
    /// </summary>
    public void ExibirGrafoFinal(bool vitoria, int orbesColetadas, int totalOrbes, int pontuacao)
    {
        m_ExibindoTelaFinal = true;
        m_OrbesColetadas = orbesColetadas;
        m_TotalOrbesMissao = totalOrbes;

        if (m_PainelRaiz == null)
        {
            ConstruirInterfaceGrafo();
        }

        m_PainelRaiz.SetActive(true);

        if (vitoria)
        {
            // --- VITÓRIA: GRAFO COMPLETO E NÍTIDO ---
            if (m_OutlinePainel != null)
                m_OutlinePainel.effectColor = new Color(0.0f, 1.0f, 0.55f, 0.95f);

            if (m_TextoTituloPainel != null)
            {
                m_TextoTituloPainel.text = "<color=#FFD700>★ VITÓRIA ONTOLÓGICA - GRAFO COMPLETO SINTETIZADO ★</color>";
            }

            if (m_TextoMetricas != null)
            {
                m_TextoMetricas.text = $"Pontuação: <color=#00FF99><b>{pontuacao:D5} pts</b></color>   |   Orbes: <color=#00FF99><b>{totalOrbes} / {totalOrbes} (100%)</b></color>   |   Reasoner HermiT: <color=#00FF99><b>CONSISTENTE</b></color>";
            }

            if (m_TextoConceitoFoco != null)
            {
                m_TextoConceitoFoco.text = "<color=#00FF99>Todos os 12 nós ontológicos foram conectados e validados pelo Reasoner HermiT!</color>";
            }

            // Ilumina 100% dos nós
            Color corVerdeSucesso = new Color(0.0f, 1.0f, 0.55f, 1.0f);
            foreach (var kvp in m_Nos)
            {
                var no = kvp.Value;
                if (no != null)
                {
                    no.desatado = true;
                    no.imagemFundo.color = new Color(no.corPadrao.r * 0.45f, no.corPadrao.g * 0.45f, no.corPadrao.b * 0.45f, 0.95f);
                    no.imagemBorda.color = corVerdeSucesso;
                    no.textoRotulo.color = Color.white;
                }
            }

            // Ilumina todas as arestas em verde neon
            for (int i = 0; i < m_Arestas.Count; i++)
            {
                var aresta = m_Arestas[i];
                if (aresta != null)
                {
                    aresta.ativa = true;
                    aresta.imagemLinha.color = new Color(0.0f, 1.0f, 0.60f, 0.95f);
                    aresta.rtLinha.sizeDelta = new Vector2(aresta.rtLinha.sizeDelta.x, 3.0f);
                }
            }
        }
        else
        {
            // --- DERROTA: GRAFO INCOMPLETO E NÍTIDO ---
            if (m_OutlinePainel != null)
                m_OutlinePainel.effectColor = new Color(1.0f, 0.20f, 0.35f, 0.95f);

            if (m_TextoTituloPainel != null)
            {
                m_TextoTituloPainel.text = "<color=#FF4466>⚠ VIOLAÇÃO ONTOLÓGICA - GRAFO INCOMPLETO ⚠</color>";
            }

            if (m_TextoMetricas != null)
            {
                int faltam = Mathf.Max(0, totalOrbes - orbesColetadas);
                m_TextoMetricas.text = $"Pontuação: <color=#FFD700><b>{pontuacao:D5} pts</b></color>   |   Orbes: <color=#FFCC00><b>{orbesColetadas} / {totalOrbes}</b></color> (Faltaram: {faltam})   |   Reasoner HermiT: <color=#FF4466><b>VIOLAÇÃO AXIOMÁTICA</b></color>";
            }

            if (m_TextoConceitoFoco != null)
            {
                m_TextoConceitoFoco.text = "<color=#FF99AA>Nós apagados indicam conceitos não sintetizados. O reasoner detectou incompletude.</color>";
            }

            // Apenas os nós coletados ficam iluminados, o restante permanece apagado
            foreach (var kvp in m_Nos)
            {
                var no = kvp.Value;
                if (no != null)
                {
                    if (no.desatado)
                    {
                        no.imagemFundo.color = new Color(no.corPadrao.r * 0.40f, no.corPadrao.g * 0.40f, no.corPadrao.b * 0.40f, 0.95f);
                        no.imagemBorda.color = no.corPadrao;
                        no.textoRotulo.color = Color.white;
                    }
                    else
                    {
                        // Incompleto / Desconectado
                        no.imagemFundo.color = new Color(0.08f, 0.08f, 0.12f, 0.65f);
                        no.imagemBorda.color = new Color(0.35f, 0.25f, 0.30f, 0.45f);
                        no.textoRotulo.color = new Color(0.50f, 0.50f, 0.55f, 0.70f);
                    }
                }
            }

            // Arestas: apenas as conectadas entre nós ambos desatados acendem
            for (int i = 0; i < m_Arestas.Count; i++)
            {
                var aresta = m_Arestas[i];
                if (aresta != null)
                {
                    bool origemOk = m_Nos.TryGetValue(aresta.idOrigem, out NoGrafoUI nOrig) && nOrig.desatado;
                    bool destinoOk = m_Nos.TryGetValue(aresta.idDestino, out NoGrafoUI nDest) && nDest.desatado;

                    if (origemOk && destinoOk)
                    {
                        aresta.ativa = true;
                        aresta.imagemLinha.color = new Color(0.0f, 0.85f, 1.0f, 0.85f);
                        aresta.rtLinha.sizeDelta = new Vector2(aresta.rtLinha.sizeDelta.x, 2.5f);
                    }
                    else
                    {
                        aresta.ativa = false;
                        aresta.imagemLinha.color = new Color(0.20f, 0.15f, 0.22f, 0.35f);
                        aresta.rtLinha.sizeDelta = new Vector2(aresta.rtLinha.sizeDelta.x, 1.5f);
                    }
                }
            }
        }
    }

    public void AcaoVoltarMenuPrincipal()
    {
        m_ExibindoTelaFinal = false;
        if (m_PainelRaiz != null)
        {
            m_PainelRaiz.SetActive(false);
        }

        if (HubInicialAmbiente.Instancia != null)
        {
            HubInicialAmbiente.Instancia.AcaoIrParaMenuPrincipal();
        }
    }

    
    public void ExibirFluxogramaCompletoVitoria()
    {
        ExibirGrafoFinal(true, m_TotalOrbesMissao, m_TotalOrbesMissao, InstanciaSemanticaBase.pontuacaoLogica);
    }

    public void DefinirAlvoOrbe(Transform transformOrbe, int indice, int total, bool ehOrbeDePoder, string nomeOrbe)
    {
        m_OrbesColetadas = Mathf.Max(0, indice - 1);
        m_TotalOrbesMissao = total;
    }

    public void ReiniciarMetricas()
    {
        m_ExibindoTelaFinal = false;
        m_OrbesColetadas = 0;

        if (m_PainelRaiz != null)
        {
            m_PainelRaiz.SetActive(false);
        }

        foreach (var kvp in m_Nos)
        {
            var no = kvp.Value;
            if (no != null)
            {
                no.desatado = false;
                no.imagemFundo.color = new Color(0.12f, 0.14f, 0.18f, 0.85f);
                no.imagemBorda.color = new Color(0.35f, 0.40f, 0.50f, 0.50f);
                no.textoRotulo.color = new Color(0.65f, 0.70f, 0.80f);
            }
        }

        for (int i = 0; i < m_Arestas.Count; i++)
        {
            var aresta = m_Arestas[i];
            if (aresta != null)
            {
                aresta.ativa = false;
                aresta.imagemLinha.color = new Color(0.20f, 0.25f, 0.35f, 0.40f);
                aresta.rtLinha.sizeDelta = new Vector2(aresta.rtLinha.sizeDelta.x, 1.8f);
            }
        }
    }

    private static TMP_Text CriarTexto(Transform pai, string nome, string conteudo, float tamanhoFonte,
        FontStyles estilo, Vector2 posicao, Vector2 dimensao, Color cor,
        TextAlignmentOptions alinhamento = TextAlignmentOptions.Center)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(pai, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
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

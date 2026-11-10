using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PacMan.Network;

/// <summary>
/// ============================================================================
/// INTERFACE ESPACIAL BIDIRECIONAL PARA ENGENHARIA DE ONTOLOGIAS OWL (Unity 6)
/// ============================================================================
/// Gerenciador Científico de HUD (SemanticHUDManager) com Tipografia Grande e Explicativa:
/// 1. Barra Superior Grande (24pt):
///    - "Rede Ontológica (ABox): [X] Elos"
///    - "Último Comido: [Tipo | Nome da Classe]"
///    - "Status do Reasoner & Fantasmas"
/// 2. Painel Explicativo em Tempo Real (22pt-25pt):
///    - Explica em letras grandes O QUE o Pac-Man acabou de comer (Classe, Subclasse ou Instância).
///    - Explica POR QUE os fantasmas mudaram de cor (Azuis = Calmos após comer item certo;
///      Vermelhos = Caçando para quebrar um pedaço da sua linha da Rede Neural!).
/// 3. Terminal da Rede Neural Ontológica (Inferior Esquerdo ampliado, 19pt):
///    - Mostra em letras grandes cada conexão criada ou quebrada pelo fantasma.
/// ============================================================================
/// </summary>
public class SemanticHUDManager : MonoBehaviour
{
    private static SemanticHUDManager s_Instancia;
    public static SemanticHUDManager Instancia => s_Instancia;

    [Header("Visibilidade e Minimalismo")]
    public static bool exibirHUDGeral = false;
    public bool exibirTerminalLog = false;
    public bool exibirPainelExplicativo = false;
    public bool exibirPainelSuperior = false;

    [Header("Componentes Principais (Canvas)")]
    public Canvas canvasPrincipal;
    public CanvasScaler canvasScaler;
    public GraphicRaycaster graphicRaycaster;

    [Header("Modal de Início (Sessão de Engenharia OWL)")]
    public GameObject modalPlayCanvas;
    public Button botaoIniciarPlay;
    public TMP_Text textoTituloPlayTMP;
    public TMP_Text textoSubtituloPlayTMP;
    public TMP_Text textoDescricaoPlayTMP;

    [Header("Painel Superior do HUD (TextMeshPro Grande)")]
    public GameObject painelHUD;
    public TMP_Text textoAxiomasABoxTMP;
    public TMP_Text textoStatusReasonerTMP;
    public TMP_Text textoFocoAtualTMP;

    [Header("Painel Explicativo de Jogo (O que comeu + Por que o Fantasma mudou de cor)")]
    public GameObject painelExplicativoJogo;
    public TMP_Text textoExplicacaoCapturaTMP;
    public TMP_Text textoExplicacaoFantasmaTMP;

    [Header("Terminal / Log da Rede Neural Ontológica (ScrollRect Ampliado)")]
    public GameObject painelTerminalLog;
    public ScrollRect scrollRectTerminal;
    public RectTransform conteudoScrollTerminal;
    public TMP_Text textoLogTerminalTMP;

    [Header("Compatibilidade Legacy UI (Opcional)")]
    public Text textoPontuacao;
    public Text textoVidas;
    public Text textoEntidadeOntologica;

    [Header("Estado Semântico e do Reasoner (HermiT)")]
    public int axiomasInseridos = 0;
    public bool reasonerConsistente = true;
    public string detalheErroReasoner = "";
    public string focoAtualClasse = "Siga as Placas de Trânsito!";
    public bool painelAberto = true;

    public int pontuacaoAtual
    {
        get => axiomasInseridos;
        set { axiomasInseridos = value; AtualizarDisplay(); }
    }

    public int vidasRestantes
    {
        get => reasonerConsistente ? 3 : 0;
        set { }
    }

    public string entidadeAtual
    {
        get => focoAtualClasse;
        set { focoAtualClasse = value; AtualizarDisplay(); }
    }

    private readonly List<string> m_LinhasLog = new List<string>();
    private const int MAX_LINHAS_LOG = 40;
    private float m_TimerPiscaInconsistente = 0f;
    private bool m_EstadoVisivelPisca = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void GarantirHUDAtivoNaCena()
    {
        RemoverCanvasArcadeLegado();

        SemanticHUDManager hud = FindFirstObjectByType<SemanticHUDManager>();
        if (hud != null && hud.GetComponent<RectTransform>() == null)
        {
            DestroyImmediate(hud.gameObject);
            hud = null;
        }

        if (hud == null)
        {
            GameObject canvasObj = new GameObject("Semantic_HUD_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            hud = canvasObj.AddComponent<SemanticHUDManager>();
        }
        else if (hud.gameObject.name.IndexOf("Arcade", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            hud.gameObject.name = "Semantic_HUD_Canvas";
        }

        hud.DefinirPainelVisivel(exibirHUDGeral);
    }

    public static void RemoverCanvasArcadeLegado()
    {
        string[] nomesLegados = new string[]
        {
            "HUD_Canvas_Arcade",
            "Painel_Arcade_HUD",
            "Modal_Play_Canvas",
            "Canvas_Arcade"
        };

        for (int i = 0; i < nomesLegados.Length; i++)
        {
            GameObject objLegado = GameObject.Find(nomesLegados[i]);
            if (objLegado != null && objLegado.GetComponent<SemanticHUDManager>() == null)
            {
                DestroyImmediate(objLegado);
            }
        }

        TMP_Text[] todosTextos = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < todosTextos.Length; i++)
        {
            TMP_Text t = todosTextos[i];
            if (t != null && t is TextMeshProUGUI && t.GetComponent<RectTransform>() == null)
            {
                DestroyImmediate(t.gameObject);
            }
        }
    }

    private void Awake()
    {
        s_Instancia = this;
        if (gameObject.name.IndexOf("Arcade", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            gameObject.name = "Semantic_HUD_Canvas";
        }
        RemoverCanvasArcadeLegado();
        GarantirEventSystem();
        GarantirCanvasEComponentes();
        if (!exibirHUDGeral && canvasPrincipal != null)
        {
            canvasPrincipal.gameObject.SetActive(false);
        }
        Time.timeScale = 1.0f;
    }

    private void OnEnable()
    {
        InstanciaSemanticaBase.OnInstanciaInteragida += AoInteragirInstancia;
        InstanciaSemanticaBase.OnInstanciaFocada += AoFocarInstancia;
        InstanciaSemanticaBase.OnInstanciaDesfocada += AoDesfocarInstancia;

        if (ProtegeWebSocketBridge.Instance != null)
        {
            InscreverEventosWebSocket(ProtegeWebSocketBridge.Instance);
        }
    }

    private void OnDisable()
    {
        InstanciaSemanticaBase.OnInstanciaInteragida -= AoInteragirInstancia;
        InstanciaSemanticaBase.OnInstanciaFocada -= AoFocarInstancia;
        InstanciaSemanticaBase.OnInstanciaDesfocada -= AoDesfocarInstancia;

        if (ProtegeWebSocketBridge.Instance != null)
        {
            DesinscreverEventosWebSocket(ProtegeWebSocketBridge.Instance);
        }
    }

    private void Start()
    {
        Time.timeScale = 1.0f;
        RemoverCanvasArcadeLegado();

        var ws = ProtegeWebSocketBridge.Instance ?? FindFirstObjectByType<ProtegeWebSocketBridge>();
        if (ws != null)
        {
            DesinscreverEventosWebSocket(ws);
            InscreverEventosWebSocket(ws);
        }

        if (m_LinhasLog.Count == 0)
        {
            AdicionarLinhaLog("<color=#FFD700><b>[COMO JOGAR]</b></color> Siga as <b>Placas de Trânsito</b> nos corredores para ver quais Classes e Subclasses pegar!");
            AdicionarLinhaLog("<color=#00FF88><b>[REDE NEURAL]</b></color> Tudo que você come forma uma linha conectada atrás do Pac-Man.");
            AdicionarLinhaLog("<color=#FF4455><b>[FANTASMAS]</b></color> Fantasmas <b>Vermelhos</b> perseguem e quebram sua linha! Coma itens certos para deixá-los <b>Azuis (Calmos)</b>!");
        }

        DefinirPainelVisivel(exibirHUDGeral);
        AtualizarDisplay();
    }

    private void Update()
    {
        if (!reasonerConsistente && textoStatusReasonerTMP != null)
        {
            m_TimerPiscaInconsistente += Time.unscaledDeltaTime * 5.0f;
            bool visivel = Mathf.Sin(m_TimerPiscaInconsistente * Mathf.PI) >= 0f;
            if (visivel != m_EstadoVisivelPisca)
            {
                m_EstadoVisivelPisca = visivel;
                textoStatusReasonerTMP.text = m_EstadoVisivelPisca
                    ? "Reasoner: <color=#FF2233><b>⚠ INCONSISTENTE (Fantasmas Caçando!)</b></color>"
                    : "Reasoner: <color=#880011><b>⚠ INCONSISTENTE (Fantasmas Caçando!)</b></color>";
            }
        }

        AtualizarExplicacaoDinamicaFantasmas();
    }

    private void AtualizarExplicacaoDinamicaFantasmas()
    {
        if (textoExplicacaoFantasmaTMP == null) return;

        var motor = FindFirstObjectByType<PacMan.Semantics.MotorRaciocinioSemantico>();
        if (motor != null && motor.pacmanIsPowered && motor.tempoPowerRestante > 0f)
        {
            textoExplicacaoFantasmaTMP.text =
                $"<color=#38BDF8><b>🟦 POR QUE OS FANTASMAS MUDARAM PARA AZUL ({Mathf.CeilToInt(motor.tempoPowerRestante)}s)?</b></color> " +
                $"Você comeu uma Classe/Subclasse CERTA! O Reasoner validou sua rede e eles ficam <b>CALMOS (não vão atrás de você)</b>!";
        }
        else if (!reasonerConsistente)
        {
            textoExplicacaoFantasmaTMP.text =
                "<color=#FF3344><b>🟥 POR QUE OS FANTASMAS ESTÃO VERMELHOS?</b></color> " +
                "Inconsistência detectada! Eles estão <b>PERSEGUINDO VOCÊ</b> para quebrar a linha da sua Rede Ontológica!";
        }
        else
        {
            textoExplicacaoFantasmaTMP.text =
                "<color=#F59E0B><b>🟧/🟥 COMPORTAMENTO DOS FANTASMAS:</b></color> " +
                "Perto de você eles ficam <b>VERMELHOS e perseguem</b> para quebrar sua linha! Coma um item CERTO para deixá-los <b>AZUIS (Calmos)</b>!";
        }
    }

    private void InscreverEventosWebSocket(ProtegeWebSocketBridge ws)
    {
        ws.OnActionLogged += AdicionarLinhaLog;
        ws.OnReasonerStatusChanged += DefinirStatusReasoner;
        ws.OnAxiomCountChanged += DefinirAxiomasInseridos;
    }

    private void DesinscreverEventosWebSocket(ProtegeWebSocketBridge ws)
    {
        ws.OnActionLogged -= AdicionarLinhaLog;
        ws.OnReasonerStatusChanged -= DefinirStatusReasoner;
        ws.OnAxiomCountChanged -= DefinirAxiomasInseridos;
    }

    public static void GarantirEventSystem()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();

            Type inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null)
            {
                esObj.AddComponent(inputSystemModule);
            }
            else
            {
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }
    }

    private void AoFocarInstancia(IInstanciaSemantica inst)
    {
        if (inst == null) return;
        string nome = InstanciaSemanticaBase.FormatarNomeClasse(inst.NomeClasse);
        string tipo = RelationBuilder.ClassificarTipoElementoOntologico(inst.NomeClasse, inst.ExpressaoDL);
        focoAtualClasse = $"[{tipo}] {nome}";
        AtualizarDisplay();
    }

    private void AoDesfocarInstancia(IInstanciaSemantica inst)
    {
        AtualizarDisplay();
    }

    private void AoInteragirInstancia(IInstanciaSemantica inst, bool sucesso)
    {
        if (inst != null)
        {
            string nomeClasse = InstanciaSemanticaBase.FormatarNomeClasse(inst.NomeClasse);
            string tipo = RelationBuilder.ClassificarTipoElementoOntologico(inst.NomeClasse, inst.ExpressaoDL);
            focoAtualClasse = $"[{tipo}] {nomeClasse}";

            if (!sucesso || !inst.EhValida)
            {
                DefinirStatusReasoner(false, $"Violação em {nomeClasse}");
            }
        }
        AtualizarDisplay();
    }

    /// <summary>
    /// Exibe em letras grandes e legíveis exatamente o que o Pac-Man acabou de comer
    /// e como isso foi conectado na Rede Neural Ontológica.
    /// </summary>
    public void RegistrarCapturaExplicativa(string nomeItem, string tipoHierarquico, string axiomaDL, int totalElosRede, string relacaoCriada)
    {
        focoAtualClasse = $"[{tipoHierarquico}] {nomeItem}";
        reasonerConsistente = true;

        if (textoExplicacaoCapturaTMP != null)
        {
            textoExplicacaoCapturaTMP.text =
                $"<color=#00FF88><b>🟢 VOCÊ COMEU ({tipoHierarquico.ToUpperInvariant()}):</b></color> <color=#FFFFFF><size=112%><b>{nomeItem}</b></size></color>   " +
                $"<color=#38BDF8><b>[Conectado via '{relacaoCriada}' — Elo #{totalElosRede} da Rede Neural]</b></color>";
        }

        AdicionarLinhaLog(
            $"<color=#00FF88><b>[✔ COMEU #{totalElosRede}]</b></color> <b>{tipoHierarquico}: {nomeItem}</b> conectado na Rede Neural via <i>{relacaoCriada}</i>! (Fantasmas Azuis por 6s)");

        AtualizarDisplay();
    }

    /// <summary>
    /// Exibe um alerta grande e claro quando o Fantasma bate no Pac-Man e quebra um pedaço da linha!
    /// </summary>
    public void ExibirAlertaQuebraDeLinha(string nomeFantasma, string itemQuebrado, string explicacao)
    {
        focoAtualClasse = $"🔄 Reconectar: {itemQuebrado}";

        if (textoExplicacaoCapturaTMP != null)
        {
            textoExplicacaoCapturaTMP.text =
                $"<color=#FF3344><b>💥 LINHA QUEBRADA ({itemQuebrado})!</b></color> <color=#FFD700><b>{explicacao}</b></color>";
        }

        AtualizarDisplay();
    }

    public void DefinirAxiomasInseridos(int quantidade)
    {
        axiomasInseridos = Mathf.Max(0, quantidade);
        AtualizarDisplay();
    }

    public void DefinirStatusReasoner(bool consistente, string detalhe = "")
    {
        reasonerConsistente = consistente;
        detalheErroReasoner = detalhe;
        m_TimerPiscaInconsistente = 0f;
        m_EstadoVisivelPisca = true;
        AtualizarDisplay();
    }

    public void DefinirFocoAtual(string nomeClasse)
    {
        if (!string.IsNullOrEmpty(nomeClasse))
        {
            focoAtualClasse = nomeClasse;
            AtualizarDisplay();
        }
    }

    public void AdicionarLinhaLog(string mensagem)
    {
        if (string.IsNullOrEmpty(mensagem)) return;

        m_LinhasLog.Add(mensagem);
        if (m_LinhasLog.Count > MAX_LINHAS_LOG)
        {
            m_LinhasLog.RemoveAt(0);
        }

        if (textoLogTerminalTMP != null)
        {
            textoLogTerminalTMP.text = string.Join("\n", m_LinhasLog);
        }

        if (conteudoScrollTerminal != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(conteudoScrollTerminal);
        }

        if (scrollRectTerminal != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRectTerminal.verticalNormalizedPosition = 0f;
        }
    }

    public void DefinirPontuacao(int pontos) => DefinirAxiomasInseridos(pontos);
    public void DefinirVidas(int vidas) => DefinirStatusReasoner(vidas > 0);
    public void DefinirEntidade(string nomeEntidade) => DefinirFocoAtual(nomeEntidade);

    public void AlternarVisibilidadePainel()
    {
        painelAberto = !painelAberto;
        if (painelHUD != null) painelHUD.SetActive(painelAberto);
        if (painelExplicativoJogo != null) painelExplicativoJogo.SetActive(painelAberto);
        if (painelTerminalLog != null) painelTerminalLog.SetActive(painelAberto);
    }

    public void DefinirModalPlayVisivel(bool visivel)
    {
        if (modalPlayCanvas != null) modalPlayCanvas.SetActive(visivel);
    }

    public void DefinirPainelVisivel(bool visivel)
    {
        painelAberto = visivel && exibirHUDGeral;
        if (canvasPrincipal != null) canvasPrincipal.gameObject.SetActive(painelAberto);
        if (painelHUD != null) painelHUD.SetActive(visivel && exibirHUDGeral && exibirPainelSuperior);
        if (painelExplicativoJogo != null) painelExplicativoJogo.SetActive(visivel && exibirHUDGeral && exibirPainelExplicativo);
        if (painelTerminalLog != null) painelTerminalLog.SetActive(visivel && exibirHUDGeral && exibirTerminalLog);
    }

    public bool TemModalPlayAtivo()
    {
        return modalPlayCanvas != null && modalPlayCanvas.activeSelf;
    }

    public void AoClicarIniciarPlay()
    {
        DefinirModalPlayVisivel(false);
        Time.timeScale = 1.0f;
        DefinirPainelVisivel(exibirHUDGeral);

        var ctrl = FindFirstObjectByType<PacMan.Web.ControladorDemoWeb>();
        if (ctrl != null)
        {
            ctrl.IniciarJogoPlay();
        }
        AdicionarLinhaLog("[+] Sessão de Engenharia Espacial OWL iniciada.");
    }

    public void AtualizarDisplay()
    {
        string txtAxiomas = $"Rede Neural (ABox): <color=#00FF88><b>{axiomasInseridos} Elos</b></color>";
        string txtReasoner = reasonerConsistente
            ? "Reasoner: <color=#00FF88><b>CONSISTENTE</b></color>"
            : "Reasoner: <color=#FF2233><b>⚠ INCONSISTENTE</b></color>";
        string txtFoco = $"Item / Alvo: <color=#FFD700><b>{focoAtualClasse}</b></color>";

        if (textoAxiomasABoxTMP != null) textoAxiomasABoxTMP.text = txtAxiomas;
        if (textoStatusReasonerTMP != null) textoStatusReasonerTMP.text = txtReasoner;
        if (textoFocoAtualTMP != null) textoFocoAtualTMP.text = txtFoco;

        if (textoPontuacao != null) textoPontuacao.text = txtAxiomas;
        if (textoVidas != null) textoVidas.text = txtReasoner;
        if (textoEntidadeOntologica != null) textoEntidadeOntologica.text = txtFoco;
    }

    private static RectTransform ObterOuCriarFilhoUI(Transform pai, string nome)
    {
        Transform existente = pai.Find(nome);
        if (existente != null)
        {
            RectTransform rtExistente = existente.GetComponent<RectTransform>();
            if (rtExistente != null)
            {
                return rtExistente;
            }
            DestroyImmediate(existente.gameObject);
        }

        GameObject novoObj = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer));
        novoObj.transform.SetParent(pai, false);
        return novoObj.GetComponent<RectTransform>();
    }

    private static void AplicarNitidezTextoTMP(TextMeshProUGUI tmp, Color corBase)
    {
        if (tmp == null) return;
        tmp.color = corBase;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        tmp.outlineWidth = 0.20f;
    }

    private void GarantirCanvasEComponentes()
    {
        if (canvasPrincipal == null) canvasPrincipal = GetComponent<Canvas>();
        if (canvasPrincipal == null)
        {
            canvasPrincipal = gameObject.AddComponent<Canvas>();
        }
        canvasPrincipal.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasPrincipal.pixelPerfect = true;

        if (GetComponent<RectTransform>() == null)
        {
            gameObject.AddComponent<RectTransform>();
        }

        canvasScaler = GetComponent<CanvasScaler>();
        if (canvasScaler == null) canvasScaler = gameObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        canvasScaler.matchWidthOrHeight = 0.5f;

        graphicRaycaster = GetComponent<GraphicRaycaster>();
        if (graphicRaycaster == null) graphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();
        graphicRaycaster.enabled = true;

        // Reconstrói os painéis com fontes grandes e legíveis
        string[] paineisParaAtualizar = { "Modal_Play_Canvas", "Painel_Arcade_HUD", "Painel_Terminal_Log", "Painel_Explicativo_Jogo" };
        for (int i = 0; i < paineisParaAtualizar.Length; i++)
        {
            Transform trAntigo = transform.Find(paineisParaAtualizar[i]);
            if (trAntigo != null)
            {
                if (Application.isPlaying) Destroy(trAntigo.gameObject);
                else DestroyImmediate(trAntigo.gameObject);
            }
        }
        modalPlayCanvas = null;

        // =====================================================================
        // 1. BARRA SUPERIOR AMPLIADA (FONTE 24PT BOLD)
        // =====================================================================
        RectTransform rtPainelHUD = ObterOuCriarFilhoUI(transform, "Painel_OWL_HUD");
        painelHUD = rtPainelHUD.gameObject;
        rtPainelHUD.anchorMin = new Vector2(0f, 1f);
        rtPainelHUD.anchorMax = new Vector2(1f, 1f);
        rtPainelHUD.pivot = new Vector2(0.5f, 1f);
        rtPainelHUD.anchoredPosition = Vector2.zero;
        rtPainelHUD.sizeDelta = new Vector2(0f, 68f);

        Image imgPainel = painelHUD.GetComponent<Image>();
        if (imgPainel == null) imgPainel = painelHUD.AddComponent<Image>();
        imgPainel.color = new Color(0.02f, 0.05f, 0.12f, 0.94f);

        // 1.1 Esquerda: Rede Neural (ABox)
        {
            RectTransform rt = ObterOuCriarFilhoUI(rtPainelHUD, "Texto_AxiomasABox_TMP");
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(28f, 0f);
            rt.sizeDelta = new Vector2(540f, 54f);

            TextMeshProUGUI tmp = rt.gameObject.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.richText = true;
            AplicarNitidezTextoTMP(tmp, Color.white);
            textoAxiomasABoxTMP = tmp;
        }

        // 1.2 Centro: Último Comido / Alvo em Foco
        {
            RectTransform rt = ObterOuCriarFilhoUI(rtPainelHUD, "Texto_FocoAtual_TMP");
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(760f, 54f);

            TextMeshProUGUI tmp = rt.gameObject.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.richText = true;
            AplicarNitidezTextoTMP(tmp, Color.white);
            textoFocoAtualTMP = tmp;
        }

        // 1.3 Direita: Status do Reasoner
        {
            RectTransform rt = ObterOuCriarFilhoUI(rtPainelHUD, "Texto_StatusReasoner_TMP");
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-65f, 0f);
            rt.sizeDelta = new Vector2(520f, 54f);

            TextMeshProUGUI tmp = rt.gameObject.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 23;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.MidlineRight;
            tmp.richText = true;
            AplicarNitidezTextoTMP(tmp, Color.white);
            textoStatusReasonerTMP = tmp;
        }

        // =====================================================================
        // 2. PAINEL EXPLICATIVO EM TEMPO REAL (O QUE COMEU + POR QUE O FANTASMA MUDOU DE COR)
        // =====================================================================
        {
            RectTransform rtExp = ObterOuCriarFilhoUI(transform, "Painel_Explicativo_Jogo");
            painelExplicativoJogo = rtExp.gameObject;
            rtExp.anchorMin = new Vector2(0f, 1f);
            rtExp.anchorMax = new Vector2(1f, 1f);
            rtExp.pivot = new Vector2(0.5f, 1f);
            rtExp.anchoredPosition = new Vector2(0f, -70f);
            rtExp.sizeDelta = new Vector2(-48f, 92f);

            Image imgExp = painelExplicativoJogo.GetComponent<Image>();
            if (imgExp == null) imgExp = painelExplicativoJogo.AddComponent<Image>();
            imgExp.color = new Color(0.01f, 0.04f, 0.10f, 0.88f);

            // Linha 1: O que o Pac-Man comeu e qual elo da rede neural foi formado/quebrado
            RectTransform rtCap = ObterOuCriarFilhoUI(rtExp, "Texto_ExplicacaoCaptura_TMP");
            rtCap.anchorMin = new Vector2(0f, 0.5f);
            rtCap.anchorMax = new Vector2(1f, 1f);
            rtCap.offsetMin = new Vector2(20f, 2f);
            rtCap.offsetMax = new Vector2(-20f, -6f);

            TextMeshProUGUI tmpCap = rtCap.gameObject.GetComponent<TextMeshProUGUI>();
            if (tmpCap == null) tmpCap = rtCap.gameObject.AddComponent<TextMeshProUGUI>();
            tmpCap.fontSize = 22;
            tmpCap.fontStyle = FontStyles.Bold;
            tmpCap.alignment = TextAlignmentOptions.MidlineLeft;
            tmpCap.richText = true;
            tmpCap.text = "<color=#00FF88><b>🟢 OBJETIVO:</b></color> Siga as <b>Placas de Trânsito</b> nos corredores e coma as <b>Classes, Subclasses e Instâncias certas</b> para tecer sua Rede Neural Ontológica!";
            AplicarNitidezTextoTMP(tmpCap, Color.white);
            textoExplicacaoCapturaTMP = tmpCap;

            // Linha 2: Por que os fantasmas mudaram de cor (Azul vs Vermelho)
            RectTransform rtFan = ObterOuCriarFilhoUI(rtExp, "Texto_ExplicacaoFantasma_TMP");
            rtFan.anchorMin = new Vector2(0f, 0f);
            rtFan.anchorMax = new Vector2(1f, 0.5f);
            rtFan.offsetMin = new Vector2(20f, 6f);
            rtFan.offsetMax = new Vector2(-20f, -2f);

            TextMeshProUGUI tmpFan = rtFan.gameObject.GetComponent<TextMeshProUGUI>();
            if (tmpFan == null) tmpFan = rtFan.gameObject.AddComponent<TextMeshProUGUI>();
            tmpFan.fontSize = 20;
            tmpFan.fontStyle = FontStyles.Bold;
            tmpFan.alignment = TextAlignmentOptions.MidlineLeft;
            tmpFan.richText = true;
            AplicarNitidezTextoTMP(tmpFan, Color.white);
            textoExplicacaoFantasmaTMP = tmpFan;
        }

        // =====================================================================
        // 3. PAINEL INFERIOR ESQUERDO AMPLIADO (FONTE GRANDE 19PT PARA LEITURA FÁCIL)
        // =====================================================================
        {
            RectTransform rtTerm = ObterOuCriarFilhoUI(transform, "Painel_Terminal_Log");
            painelTerminalLog = rtTerm.gameObject;
            rtTerm.anchorMin = new Vector2(0f, 0f);
            rtTerm.anchorMax = new Vector2(0f, 0f);
            rtTerm.pivot = new Vector2(0f, 0f);
            rtTerm.anchoredPosition = new Vector2(24f, 20f);
            rtTerm.sizeDelta = new Vector2(920f, 230f);

            Image imgTerm = painelTerminalLog.GetComponent<Image>();
            if (imgTerm == null) imgTerm = painelTerminalLog.AddComponent<Image>();
            imgTerm.color = new Color(0.01f, 0.03f, 0.09f, 0.90f);

            RectTransform rtHeader = ObterOuCriarFilhoUI(rtTerm, "Header_Terminal_TMP");
            rtHeader.anchorMin = new Vector2(0f, 1f);
            rtHeader.anchorMax = new Vector2(1f, 1f);
            rtHeader.pivot = new Vector2(0.5f, 1f);
            rtHeader.anchoredPosition = new Vector2(0f, -6f);
            rtHeader.sizeDelta = new Vector2(-24f, 30f);

            TextMeshProUGUI txtHeader = rtHeader.gameObject.GetComponent<TextMeshProUGUI>();
            if (txtHeader == null) txtHeader = rtHeader.gameObject.AddComponent<TextMeshProUGUI>();
            txtHeader.fontSize = 18;
            txtHeader.fontStyle = FontStyles.Bold;
            txtHeader.text = "📋 HISTÓRICO DO QUE O PAC-MAN COMEU & REDE NEURAL ONTOLÓGICA";
            txtHeader.color = new Color(0.0f, 0.92f, 1.0f);
            txtHeader.alignment = TextAlignmentOptions.MidlineLeft;

            RectTransform rtVp = ObterOuCriarFilhoUI(rtTerm, "Viewport");
            GameObject viewportObj = rtVp.gameObject;
            rtVp.anchorMin = new Vector2(0f, 0f);
            rtVp.anchorMax = new Vector2(1f, 1f);
            rtVp.offsetMin = new Vector2(14f, 10f);
            rtVp.offsetMax = new Vector2(-14f, -38f);

            Image imgVp = viewportObj.GetComponent<Image>();
            if (imgVp == null) imgVp = viewportObj.AddComponent<Image>();
            imgVp.color = new Color(0f, 0f, 0f, 0.01f);
            if (viewportObj.GetComponent<RectMask2D>() == null) viewportObj.AddComponent<RectMask2D>();

            conteudoScrollTerminal = ObterOuCriarFilhoUI(rtVp, "Content");
            GameObject contentObj = conteudoScrollTerminal.gameObject;
            conteudoScrollTerminal.anchorMin = new Vector2(0f, 1f);
            conteudoScrollTerminal.anchorMax = new Vector2(1f, 1f);
            conteudoScrollTerminal.pivot = new Vector2(0.5f, 1f);
            conteudoScrollTerminal.anchoredPosition = Vector2.zero;
            conteudoScrollTerminal.sizeDelta = new Vector2(0f, 180f);

            ContentSizeFitter csf = contentObj.GetComponent<ContentSizeFitter>();
            if (csf == null) csf = contentObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI tmpLog = contentObj.GetComponent<TextMeshProUGUI>();
            if (tmpLog == null) tmpLog = contentObj.AddComponent<TextMeshProUGUI>();
            tmpLog.fontSize = 19;
            tmpLog.fontStyle = FontStyles.Bold;
            tmpLog.lineSpacing = 4f;
            tmpLog.alignment = TextAlignmentOptions.BottomLeft;
            tmpLog.richText = true;
            tmpLog.textWrappingMode = TextWrappingModes.Normal;
            AplicarNitidezTextoTMP(tmpLog, Color.white);
            textoLogTerminalTMP = tmpLog;

            scrollRectTerminal = painelTerminalLog.GetComponent<ScrollRect>();
            if (scrollRectTerminal == null) scrollRectTerminal = painelTerminalLog.AddComponent<ScrollRect>();
            scrollRectTerminal.viewport = rtVp;
            scrollRectTerminal.content = conteudoScrollTerminal;
            scrollRectTerminal.horizontal = false;
            scrollRectTerminal.vertical = true;
            scrollRectTerminal.movementType = ScrollRect.MovementType.Clamped;
            scrollRectTerminal.scrollSensitivity = 25f;
        }

        if (!exibirHUDGeral)
        {
            if (canvasPrincipal != null) canvasPrincipal.gameObject.SetActive(false);
            if (painelHUD != null) painelHUD.SetActive(false);
            if (painelExplicativoJogo != null) painelExplicativoJogo.SetActive(false);
            if (painelTerminalLog != null) painelTerminalLog.SetActive(false);
        }
        else
        {
            if (painelHUD != null) painelHUD.SetActive(exibirPainelSuperior);
            if (painelExplicativoJogo != null) painelExplicativoJogo.SetActive(exibirPainelExplicativo);
            if (painelTerminalLog != null) painelTerminalLog.SetActive(exibirTerminalLog);
        }
    }
}

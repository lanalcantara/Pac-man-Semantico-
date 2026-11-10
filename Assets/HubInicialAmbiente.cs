using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using TMPro;
using PacMan.Web;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Pac-Man Semântico - CIn-UFPE) - Menu Principal & Hub de Configurações
/// ============================================================================
/// Gerencia o fluxo completo de telas pré-jogo, controles nativos do Meta Quest,
/// carrosséis visuais interativos e encerramento limpo da partida com imobilização
/// imediata de fantasmas e exibição do mapa taxonômico ontológico em alta resolução.
///
/// FLUXO DE TELAS:
/// 1. Sincronização com Protégé (Plugin Java) & Seleção de Arquivo Ontológico
/// 2. Tela de Boas-Vindas (> JOGAR < e Configurações)
/// 3. Tela de Seleção de Mapa (Carrossel com artes dos 3 mapas)
/// 4. Tela de Seleção de Câmera (Carrossel com perspectivas 1ª Pessoa, 3ª Pessoa e Aérea)
/// 5. Contagem Regressiva de 5 segundos estilo arcade (Ready Pause com áudio retrô)
/// 6. Início da partida limpa (reset integral de orbes e entidades)
/// 7. Fim de jogo (Game Over / Vitória) com congelamento instantâneo e fluxo ontológico completo
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class HubInicialAmbiente : MonoBehaviour
{
    public static HubInicialAmbiente Instancia { get; private set; }

    public enum EstadoTelaHub
    {
        SelecaoOntologia = 0, // Tela 1: Sincronização Protégé (Plugin Java) e escolha do arquivo OWL/RDF
        BoasVindas = 1,       // Tela 2: Boas-Vindas com botão JOGAR em destaque
        EscolhaMapa = 2,      // Tela 3: Carrossel de Mapas com prévia gráfica
        EscolhaCamera = 3,    // Tela 4: Carrossel de Câmeras com perspectivas
        Configuracoes = 4     // Tela 5: Áudio e Pareamento Bluetooth
    }

    [Header("Estado do Hub e Partida")]
    [Tooltip("Indica se o Hub/Menu está atualmente visível e ativo.")]
    public bool hubAberto = true;

    [Tooltip("Indica se há uma partida de gameplay ativamente em andamento.")]
    public bool partidaEmAndamento = false;

    [Header("Referências")]
    public ControladorXRJogador controladorJogador;
    public ControladorDemoWeb controladorDemoWeb;

    // Estado da tela atual
    private EstadoTelaHub m_EstadoTela = EstadoTelaHub.SelecaoOntologia;

    // Componentes de UI gerados dinamicamente
    private Canvas m_CanvasHub;
    private GameObject m_PainelFundoEscuro;
    private GameObject m_PainelJanelaHub;
    private GameObject m_PainelJanelaGameOver;
    private GameObject m_PainelJanelaVitoria;

    // Sub-painéis de fluxo
    private GameObject m_PainelSelecaoOntologia;
    private GameObject m_PainelBoasVindas;
    private GameObject m_PainelEscolhaMapa;
    private GameObject m_PainelEscolhaCamera;
    private GameObject m_PainelConfiguracoes;

    // Painel de Contagem Regressiva (Ready Pause)
    private GameObject m_PainelContagemRegressiva;
    private TMP_Text m_TextoContagemRegressiva;

    // Elementos da Seleção de Ontologia (Mock Protégé Plugin)
    private int m_IndiceOntologiaSelecionada = 0;
    private readonly string[] m_NomesOntologias = new string[]
    {
        "Ontologia_Parasitologia_CIn_v2.4.owl",
        "SmartCampus_UFPE_2026.rdf",
        "Ciberseguranca_Critica_Infra.owl",
        "Logistica_Hospitalar_Triagem.owl"
    };
    private readonly string[] m_DescricoesOntologias = new string[]
    {
        "18 Classes OWL • 12 Instâncias ABox • 6 Regras SWRL • 4 Propriedades de Objeto (Taxonomia Médica CIn-UFPE)",
        "24 Classes OWL • 16 Instâncias ABox • 8 Restrições DL • Modelo Espacial e Sensores Smart Campus",
        "30 Classes OWL • 20 Instâncias ABox • 10 Axiomas de Inconsistência • Detecção de Ameaças Cibernéticas",
        "16 Classes OWL • 10 Instâncias ABox • 5 Restrições de Cardinalidade • Triagem Clínica Automatizada"
    };
    private readonly string[] m_StatusOntologias = new string[]
    {
        "✓ RECEBIDO RECENTEMENTE DO PLUGIN PROTÉGÉ (Porta 8085)",
        "✓ SINCRONIZADO VIA PROTEGEBRIDGE WEBSOCKET",
        "✓ BASE DE CONHECIMENTO CIn-UFPE CARREGADA",
        "✓ MODELO CLÍNICO OWL 2.0 PRONTO PARA INSTANCIAÇÃO"
    };
    private GameObject[] m_CardsOntologia = new GameObject[4];
    private Outline[] m_OutlinesOntologia = new Outline[4];

    // Elementos do Carrossel de Mapas
    private int m_IndiceMapaAtual = 1; // 0: Neon, 1: Clássico Dia (CIn-UFPE), 2: Especial Natal
    private RawImage m_ImgPreviewMapa;
    private TMP_Text m_TextoTituloMapa;
    private TMP_Text m_TextoDescricaoMapa;
    private TMP_Text m_TextoIndicadorMapa;

    // Elementos do Carrossel de Câmeras
    private int m_IndiceCameraAtual = 0; // 0: 1ª Pessoa VR 360°, 1: 3ª Pessoa, 2: Aérea Tática
    private RawImage m_ImgPreviewCamera;
    private TMP_Text m_TextoTituloCamera;
    private TMP_Text m_TextoDescricaoCamera;
    private TMP_Text m_TextoIndicadorCamera;

    // Textos informativos dinâmicos
    private TMP_Text m_TextoInfoGameOver;
    private TMP_Text m_TextoInfoVitoria;
    private TMP_Text m_TextoStatusAudio;
    private TMP_Text m_TextoSensibilidade;
    private float m_FatorSensibilidade = 1.0f;

    // Cooldown para navegação por analógico
    private float m_TimerCooldownStick = 0f;
    private bool m_PrevTouchBtnA = false;
    private bool m_PrevTouchBtnB = false;
    private bool m_PrevTouchBtnX = false;
    private bool m_PrevTouchBtnY = false;
    private bool m_PrevTouchTrigger = false;
    private bool m_PrevTouchMenu = false;

    // Estado dos Modais
    public bool GameOverAberto => m_PainelJanelaGameOver != null && m_PainelJanelaGameOver.activeSelf;
    public bool VitoriaAberta => m_PainelJanelaVitoria != null && m_PainelJanelaVitoria.activeSelf;

    // Texturas em cache
    private Texture2D m_TexMapaNeon;
    private Texture2D m_TexMapaDia;
    private Texture2D m_TexMapaNatal;
    private Texture2D m_TexCam1P;
    private Texture2D m_TexCam3P;
    private Texture2D m_TexCamAerea;

    // Áudio procedural para beeps
    private AudioSource m_AudioSourceHub;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        Instancia = this;
        CarregarTexturasEmCache();

        m_AudioSourceHub = gameObject.GetComponent<AudioSource>();
        if (m_AudioSourceHub == null)
        {
            m_AudioSourceHub = gameObject.AddComponent<AudioSource>();
            m_AudioSourceHub.playOnAwake = false;
            m_AudioSourceHub.spatialBlend = 0f;
            m_AudioSourceHub.volume = 0.5f;
        }
    }

    private void Start()
    {
        LocalizarControladores();
        ConstruirInterfaceHub();

        // Inicia na Tela 1: Sincronização com Protégé (Plugin Java)
        partidaEmAndamento = false;
        DefinirEstadoHub(true);
        DefinirEstadoTela(EstadoTelaHub.SelecaoOntologia);
    }

    private void Update()
    {
        if (m_TimerCooldownStick > 0f) m_TimerCooldownStick -= Time.unscaledDeltaTime;

        // Leitura de Entrada dos Controles Touch do Meta Quest, Gamepad e Teclado
        LerEntradasMenuControles();
    }

    private void LateUpdate()
    {
        AtualizarAcompanhamentoOlharVR(false);
    }

    /// <summary>
    /// Mantém o menu e todos os modais (Vitória, Game Over, Contagem Regressiva)
    /// estritamente na frente do olhar do jogador no Meta Quest 3.
    /// Se o usuário virar para os lados, o canvas acompanha a visão naturalmente sem se perder no mapa.
    /// </summary>
    public void AtualizarAcompanhamentoOlharVR(bool forcarSnap)
    {
        if (m_CanvasHub == null) return;

        bool menuVisivel = hubAberto ||
                           (m_PainelJanelaGameOver != null && m_PainelJanelaGameOver.activeSelf) ||
                           (m_PainelJanelaVitoria != null && m_PainelJanelaVitoria.activeSelf) ||
                           (m_PainelContagemRegressiva != null && m_PainelContagemRegressiva.activeSelf);

        if (!menuVisivel) return;

        Camera cam = Camera.main;
        if (controladorJogador != null && controladorJogador.cameraPrincipalXR != null)
        {
            cam = controladorJogador.cameraPrincipalXR;
        }
        if (cam == null) return;

        bool emVR = XRSettings.isDeviceActive || (XRSettings.enabled && !string.IsNullOrEmpty(XRSettings.loadedDeviceName)) ||
                    (UnityEngine.XR.Management.XRGeneralSettings.Instance != null && UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager != null && UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager.activeLoader != null) ||
                    Application.platform == RuntimePlatform.Android;

        if (emVR)
        {
            if (m_CanvasHub.renderMode != RenderMode.WorldSpace)
            {
                m_CanvasHub.renderMode = RenderMode.WorldSpace;
                m_CanvasHub.transform.localScale = Vector3.one * 0.0018f;
            }

            Vector3 camPos = cam.transform.position;
            Vector3 camFwd = cam.transform.forward;
            Vector3 camUp = cam.transform.up;

            // Posição exatamente na frente do usuário a 1.9m de distância
            Vector3 targetPos = camPos + camFwd * 1.90f;
            Quaternion targetRot = Quaternion.LookRotation(camFwd, camUp);

            if (forcarSnap || Vector3.Distance(m_CanvasHub.transform.position, targetPos) > 4.0f)
            {
                m_CanvasHub.transform.position = targetPos;
                m_CanvasHub.transform.rotation = targetRot;
            }
            else
            {
                m_CanvasHub.transform.position = Vector3.Lerp(m_CanvasHub.transform.position, targetPos, Time.unscaledDeltaTime * 14f);
                m_CanvasHub.transform.rotation = Quaternion.Slerp(m_CanvasHub.transform.rotation, targetRot, Time.unscaledDeltaTime * 14f);
            }
        }
    }

    private void CarregarTexturasEmCache()
    {
        m_TexMapaNeon = Resources.Load<Texture2D>("Textures/Preview_Mapa_Neon");
        m_TexMapaDia = Resources.Load<Texture2D>("Textures/Preview_Mapa_Dia");
        m_TexMapaNatal = Resources.Load<Texture2D>("Textures/Preview_Mapa_Natal");

        m_TexCam1P = Resources.Load<Texture2D>("Textures/Preview_Camera_1Pessoa");
        m_TexCam3P = Resources.Load<Texture2D>("Textures/Preview_Camera_3Pessoa");
        m_TexCamAerea = Resources.Load<Texture2D>("Textures/Preview_Camera_Aerea");
    }

    /// <summary>
    /// Processa navegação por analógico, botões nativos do Quest Touch (A, B, Menu, Gatilho), Gamepad e Teclado.
    /// Suporte total para controles nativos do Quest 3 sem exigir joystick externo conectado.
    /// </summary>
    private void LerEntradasMenuControles()
    {
        // 1. Leitura de Controles Touch Nativos do Meta Quest via OpenXR
        bool touchBtnA = false;
        bool touchBtnB = false;
        bool touchBtnX = false;
        bool touchBtnY = false;
        bool touchTrigger = false;
        bool touchMenu = false;
        Vector2 touchStickEsq = Vector2.zero;
        Vector2 touchStickDir = Vector2.zero;

        var devDir = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!devDir.isValid)
        {
            var rightList = new System.Collections.Generic.List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, rightList);
            if (rightList.Count > 0) devDir = rightList[0];
        }

        if (devDir.isValid)
        {
            devDir.TryGetFeatureValue(CommonUsages.primaryButton, out touchBtnA);
            devDir.TryGetFeatureValue(CommonUsages.secondaryButton, out touchBtnB);
            devDir.TryGetFeatureValue(CommonUsages.triggerButton, out touchTrigger);
            devDir.TryGetFeatureValue(CommonUsages.primary2DAxis, out touchStickDir);
        }

        var devEsq = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!devEsq.isValid)
        {
            var leftList = new System.Collections.Generic.List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, leftList);
            if (leftList.Count > 0) devEsq = leftList[0];
        }

        if (devEsq.isValid)
        {
            devEsq.TryGetFeatureValue(CommonUsages.primaryButton, out touchBtnX);
            devEsq.TryGetFeatureValue(CommonUsages.secondaryButton, out touchBtnY);
            devEsq.TryGetFeatureValue(CommonUsages.menuButton, out touchMenu);
            devEsq.TryGetFeatureValue(CommonUsages.primary2DAxis, out touchStickEsq);
        }

        // Detecção de borda de subida (debounce) para evitar que um único toque passe várias telas direto
        bool touchBtnADown = touchBtnA && !m_PrevTouchBtnA;
        bool touchBtnBDown = touchBtnB && !m_PrevTouchBtnB;
        bool touchBtnXDown = touchBtnX && !m_PrevTouchBtnX;
        bool touchBtnYDown = touchBtnY && !m_PrevTouchBtnY;
        bool touchTriggerDown = touchTrigger && !m_PrevTouchTrigger;
        bool touchMenuDown = touchMenu && !m_PrevTouchMenu;

        m_PrevTouchBtnA = touchBtnA;
        m_PrevTouchBtnB = touchBtnB;
        m_PrevTouchBtnX = touchBtnX;
        m_PrevTouchBtnY = touchBtnY;
        m_PrevTouchTrigger = touchTrigger;
        m_PrevTouchMenu = touchMenu;

        Vector2 stickConsolidado = touchStickEsq.sqrMagnitude > touchStickDir.sqrMagnitude ? touchStickEsq : touchStickDir;

#if ENABLE_INPUT_SYSTEM
        // 2. Leitura de Todos os Gamepads e Joysticks do New Input System
        for (int i = 0; i < UnityEngine.InputSystem.Gamepad.all.Count; i++)
        {
            var gp = UnityEngine.InputSystem.Gamepad.all[i];
            if (gp == null) continue;

            Vector2 gpStick = gp.leftStick.ReadValue();
            if (gpStick.sqrMagnitude > stickConsolidado.sqrMagnitude) stickConsolidado = gpStick;

            Vector2 dpad = gp.dpad.ReadValue();
            if (dpad.sqrMagnitude > stickConsolidado.sqrMagnitude) stickConsolidado = dpad;
        }

        for (int i = 0; i < UnityEngine.InputSystem.Joystick.all.Count; i++)
        {
            var joy = UnityEngine.InputSystem.Joystick.all[i];
            if (joy == null) continue;

            Vector2 joyStickVal = joy.stick.ReadValue();
            if (joyStickVal.sqrMagnitude > stickConsolidado.sqrMagnitude) stickConsolidado = joyStickVal;
        }
#endif

        // 3. Fallback Clássico do Legacy Input Manager (Teclado, Joystick e D-Pad)
        float legacyX = Input.GetAxisRaw("Horizontal");
        float legacyY = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(legacyX) > Mathf.Abs(stickConsolidado.x)) stickConsolidado.x = legacyX;
        if (Mathf.Abs(legacyY) > Mathf.Abs(stickConsolidado.y)) stickConsolidado.y = legacyY;

        try
        {
            float dpadX = Input.GetAxisRaw("DPad_X");
            float dpadY = Input.GetAxisRaw("DPad_Y");
            if (Mathf.Abs(dpadX) > Mathf.Abs(stickConsolidado.x)) stickConsolidado.x = dpadX;
            if (Mathf.Abs(dpadY) > Mathf.Abs(stickConsolidado.y)) stickConsolidado.y = dpadY;
        }
        catch { }

        // Botões de Confirmação (A, Gatilho, Enter, Espaço, JoystickButton0)
        bool botaoConfirmar = touchBtnADown || touchTriggerDown || touchBtnXDown ||
                              Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                              Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);

        // Botões de Cancelar / Voltar (B, Y, ESC, JoystickButton1)
        bool botaoCancelar = touchBtnBDown || touchBtnYDown ||
                             Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);

        // Botão SELECT (JoystickButton6), START (JoystickButton7) ou Menu do Quest
        bool botaoSelectMenu = touchMenuDown ||
                               Input.GetKeyDown(KeyCode.JoystickButton6) ||
                               Input.GetKeyDown(KeyCode.JoystickButton7) ||
                               Input.GetKeyDown(KeyCode.Escape);

#if ENABLE_INPUT_SYSTEM
        for (int i = 0; i < UnityEngine.InputSystem.Gamepad.all.Count; i++)
        {
            var gp = UnityEngine.InputSystem.Gamepad.all[i];
            if (gp == null) continue;

            if (gp.buttonSouth.wasPressedThisFrame) botaoConfirmar = true;
            if (gp.buttonEast.wasPressedThisFrame) botaoCancelar = true;
            if (gp.selectButton.wasPressedThisFrame || gp.startButton.wasPressedThisFrame) botaoSelectMenu = true;
        }

        for (int i = 0; i < UnityEngine.InputSystem.Joystick.all.Count; i++)
        {
            var joy = UnityEngine.InputSystem.Joystick.all[i];
            if (joy == null) continue;

            if (joy.trigger.wasPressedThisFrame) botaoConfirmar = true;
        }
#endif

        // 2. Quando Game Over ou Vitória estiverem ativos: qualquer confirmação ou Select volta ao Menu Principal
        if (GameOverAberto || VitoriaAberta)
        {
            if (botaoConfirmar || botaoCancelar || botaoSelectMenu)
            {
                AcaoIrParaMenuPrincipal();
                return;
            }
        }

        // 3. Atalho do Botão SELECT / BACK / MENU durante gameplay ativo -> Volta ao Menu Principal
        if (partidaEmAndamento && !hubAberto)
        {
            if (botaoSelectMenu)
            {
                AcaoIrParaMenuPrincipal();
                return;
            }
        }

        // 4. Navegação dentro do Hub Inicial
        if (hubAberto && !GameOverAberto && !VitoriaAberta)
        {
            // Navegação vertical ou horizontal por analógico/setas com cooldown
            if (m_TimerCooldownStick <= 0f)
            {
                // Navegação vertical (para lista de ontologias)
                if (m_EstadoTela == EstadoTelaHub.SelecaoOntologia)
                {
                    if (stickConsolidado.y < -0.45f || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                    {
                        m_TimerCooldownStick = 0.22f;
                        m_IndiceOntologiaSelecionada = (m_IndiceOntologiaSelecionada + 1) % m_NomesOntologias.Length;
                        AtualizarVisualSelecaoOntologia();
                        TocarSomBeepProcedural(440f);
                    }
                    else if (stickConsolidado.y > 0.45f || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                    {
                        m_TimerCooldownStick = 0.22f;
                        m_IndiceOntologiaSelecionada = (m_IndiceOntologiaSelecionada - 1 + m_NomesOntologias.Length) % m_NomesOntologias.Length;
                        AtualizarVisualSelecaoOntologia();
                        TocarSomBeepProcedural(440f);
                    }
                }
                // Navegação horizontal (para carrossel de mapas e câmeras)
                else if (m_EstadoTela == EstadoTelaHub.EscolhaMapa || m_EstadoTela == EstadoTelaHub.EscolhaCamera)
                {
                    if (stickConsolidado.x > 0.45f || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                    {
                        m_TimerCooldownStick = 0.25f;
                        if (m_EstadoTela == EstadoTelaHub.EscolhaMapa) AvancarMapaCarrossel();
                        else if (m_EstadoTela == EstadoTelaHub.EscolhaCamera) AvancarCameraCarrossel();
                        TocarSomBeepProcedural(520f);
                    }
                    else if (stickConsolidado.x < -0.45f || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                    {
                        m_TimerCooldownStick = 0.25f;
                        if (m_EstadoTela == EstadoTelaHub.EscolhaMapa) VoltarMapaCarrossel();
                        else if (m_EstadoTela == EstadoTelaHub.EscolhaCamera) VoltarCameraCarrossel();
                        TocarSomBeepProcedural(520f);
                    }
                }
            }

            // Confirmação
            if (botaoConfirmar)
            {
                m_TimerCooldownStick = 0.25f;
                switch (m_EstadoTela)
                {
                    case EstadoTelaHub.SelecaoOntologia:
                        AcaoConfirmarOntologiaESeguir();
                        break;
                    case EstadoTelaHub.BoasVindas:
                        AcaoAvancarParaEscolhaMapa();
                        break;
                    case EstadoTelaHub.EscolhaMapa:
                        AcaoConfirmarMapaEAvancar();
                        break;
                    case EstadoTelaHub.EscolhaCamera:
                        AcaoPlayIniciarPartida();
                        break;
                }
                return;
            }

            // Cancelamento / Voltar tela
            if (botaoCancelar)
            {
                m_TimerCooldownStick = 0.25f;
                switch (m_EstadoTela)
                {
                    case EstadoTelaHub.EscolhaCamera:
                        DefinirEstadoTela(EstadoTelaHub.EscolhaMapa);
                        break;
                    case EstadoTelaHub.EscolhaMapa:
                        DefinirEstadoTela(EstadoTelaHub.BoasVindas);
                        break;
                    case EstadoTelaHub.BoasVindas:
                    case EstadoTelaHub.Configuracoes:
                        DefinirEstadoTela(EstadoTelaHub.SelecaoOntologia);
                        break;
                }
                return;
            }
        }
    }

    private void LocalizarControladores()
    {
        if (controladorJogador == null)
        {
            controladorJogador = FindFirstObjectByType<ControladorXRJogador>();
        }
        if (controladorDemoWeb == null)
        {
            controladorDemoWeb = FindFirstObjectByType<ControladorDemoWeb>();
        }
    }

    // =========================================================================
    // GERENCIAMENTO DE ESTADO E TELAS DO HUB
    // =========================================================================

    public void DefinirEstadoTela(EstadoTelaHub estado)
    {
        m_EstadoTela = estado;

        if (m_PainelSelecaoOntologia != null) m_PainelSelecaoOntologia.SetActive(estado == EstadoTelaHub.SelecaoOntologia);
        if (m_PainelBoasVindas != null) m_PainelBoasVindas.SetActive(estado == EstadoTelaHub.BoasVindas);
        if (m_PainelEscolhaMapa != null) m_PainelEscolhaMapa.SetActive(estado == EstadoTelaHub.EscolhaMapa);
        if (m_PainelEscolhaCamera != null) m_PainelEscolhaCamera.SetActive(estado == EstadoTelaHub.EscolhaCamera);
        if (m_PainelConfiguracoes != null) m_PainelConfiguracoes.SetActive(estado == EstadoTelaHub.Configuracoes);

        if (estado == EstadoTelaHub.SelecaoOntologia)
        {
            AtualizarVisualSelecaoOntologia();
        }
        else if (estado == EstadoTelaHub.EscolhaMapa)
        {
            AtualizarVisualCarrosselMapa();
        }
        else if (estado == EstadoTelaHub.EscolhaCamera)
        {
            AtualizarVisualCarrosselCamera();
        }
    }

    public void AlternarHub()
    {
        DefinirEstadoHub(!hubAberto);
    }

    public void DefinirEstadoHub(bool aberto)
    {
        hubAberto = aberto;

        if (m_CanvasHub == null)
        {
            ConstruirInterfaceHub();
        }

        PosicionarCanvasParaVRSeNecessario();

        if (m_PainelFundoEscuro != null) m_PainelFundoEscuro.SetActive(aberto);
        if (m_PainelJanelaHub != null) m_PainelJanelaHub.SetActive(aberto);

        LocalizarControladores();
        if (controladorJogador != null)
        {
            if (aberto)
            {
                controladorJogador.PararMovimentoTotal();
                controladorJogador.controlesHabilitados = false;
                controladorJogador.travarCursorAoClicar = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                controladorJogador.travarCursorAoClicar = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        if (aberto)
        {
            ImobilizarTodosFantasmas();
        }
    }

    public void ImobilizarTodosFantasmas()
    {
        var fantasmas = FindObjectsByType<ObstaculoInvalidoSemantico>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < fantasmas.Length; i++)
        {
            if (fantasmas[i] != null)
            {
                fantasmas[i].PararMovimentoTotalFantasma();
            }
        }
    }

    public void AcaoIrParaMenuPrincipal()
    {
        partidaEmAndamento = false;

        if (m_PainelJanelaGameOver != null) m_PainelJanelaGameOver.SetActive(false);
        if (m_PainelJanelaVitoria != null) m_PainelJanelaVitoria.SetActive(false);
        if (m_PainelContagemRegressiva != null) m_PainelContagemRegressiva.SetActive(false);

        // Zera e limpa qualquer estado residual do labirinto e orbes
        if (GerenciadorCenarioSemantico.Instancia != null)
        {
            GerenciadorCenarioSemantico.Instancia.ReiniciarLabirintoEInstancias();
        }

        LocalizarControladores();
        if (controladorJogador != null)
        {
            controladorJogador.PararMovimentoTotal();
            controladorJogador.controlesHabilitados = false;
            controladorJogador.transform.position = new Vector3(0f, 0.0f, -5.0f);
            controladorJogador.AlinharBaseAoChao();
        }
        ImobilizarTodosFantasmas();

        DefinirEstadoHub(true);
        DefinirEstadoTela(EstadoTelaHub.BoasVindas);
    }

    // =========================================================================
    // SELEÇÃO DE ONTOLOGIA (PLUGIN JAVA PROTÉGÉ)
    // =========================================================================

    public void AcaoConfirmarOntologiaESeguir()
    {
        string nomeOnto = m_NomesOntologias[m_IndiceOntologiaSelecionada];
        Debug.Log($"<color=#00FF99><b>[PAC-MAN SEMÂNTICO]</b></color> Documento ontológico selecionado: '{nomeOnto}'. Sincronizando classes com o labirinto...");

        TocarSomBeepProcedural(660f);
        DefinirEstadoTela(EstadoTelaHub.BoasVindas);
    }

    private void AtualizarVisualSelecaoOntologia()
    {
        for (int i = 0; i < m_OutlinesOntologia.Length; i++)
        {
            if (m_OutlinesOntologia[i] != null)
            {
                bool selecionado = (i == m_IndiceOntologiaSelecionada);
                m_OutlinesOntologia[i].effectColor = selecionado ? new Color(0.0f, 1.0f, 0.60f, 0.95f) : new Color(0.15f, 0.35f, 0.55f, 0.40f);
                m_OutlinesOntologia[i].effectDistance = selecionado ? new Vector2(3f, -3f) : new Vector2(1f, -1f);
            }
            if (m_CardsOntologia[i] != null)
            {
                Image img = m_CardsOntologia[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = (i == m_IndiceOntologiaSelecionada) ? new Color(0.04f, 0.12f, 0.22f, 0.98f) : new Color(0.02f, 0.05f, 0.10f, 0.88f);
                }
            }
        }
    }

    // =========================================================================
    // AÇÕES DE AVANÇO DE FLUXO
    // =========================================================================

    public void AcaoAvancarParaEscolhaMapa()
    {
        DefinirEstadoTela(EstadoTelaHub.EscolhaMapa);
        TocarSomBeepProcedural(520f);
    }

    public void AcaoConfirmarMapaEAvancar()
    {
        DefinirEstadoTela(EstadoTelaHub.EscolhaCamera);
        TocarSomBeepProcedural(580f);
    }

    public void AcaoAbrirConfiguracoes()
    {
        DefinirEstadoTela(EstadoTelaHub.Configuracoes);
        TocarSomBeepProcedural(480f);
    }

    /// <summary>
    /// Ação final: Inicia a partida limpa com contagem regressiva de 5 segundos estilo arcade.
    /// </summary>
    public void AcaoPlayIniciarPartida()
    {
        StartCoroutine(RotinaContagemRegressivaEInicio());
    }

    private IEnumerator RotinaContagemRegressivaEInicio()
    {
        // 1. Fecha o Hub e garante congelamento total
        DefinirEstadoHub(false);
        partidaEmAndamento = true;

        ControladorXRJogador.ModoCameraJogador modoCam = (ControladorXRJogador.ModoCameraJogador)m_IndiceCameraAtual;
        LocalizarControladores();
        if (controladorJogador != null)
        {
            controladorJogador.DefinirModoCamera(modoCam);
            controladorJogador.controlesHabilitados = false;
            controladorJogador.PararMovimentoTotal();
        }

        GerenciadorCenarioSemantico.TipoTemaLabirinto tema = (GerenciadorCenarioSemantico.TipoTemaLabirinto)m_IndiceMapaAtual;
        if (GerenciadorCenarioSemantico.Instancia != null)
        {
            GerenciadorCenarioSemantico.Instancia.AplicarTemaLabirinto(tema);
            // RESET INTEGRAL: orbes, pontuação, fantasmas e posições zeradas para nova rodada limpa
            GerenciadorCenarioSemantico.Instancia.ReiniciarLabirintoEInstancias();
        }

        ImobilizarTodosFantasmas();

        // 2. Constrói / exibe o painel de contagem regressiva
        if (m_PainelContagemRegressiva == null)
        {
            ConstruirPainelContagemRegressiva();
        }
        m_PainelContagemRegressiva.SetActive(true);

        // 3. Contagem regressiva de 5 segundos
        for (int i = 5; i >= 1; i--)
        {
            if (m_TextoContagemRegressiva != null)
            {
                m_TextoContagemRegressiva.text = $"<color=#00E5FF><b>PREPARAR...</b></color>\n<size=110><color=#FFD700><b>{i}</b></color></size>";
            }
            TocarSomBeepProcedural(480f + (5 - i) * 60f);
            yield return new WaitForSeconds(1.0f);
        }

        if (m_TextoContagemRegressiva != null)
        {
            m_TextoContagemRegressiva.text = "<size=110><color=#00FF99><b>VAI!</b></color></size>";
        }
        TocarSomBeepProcedural(880f);
        yield return new WaitForSeconds(0.6f);

        m_PainelContagemRegressiva.SetActive(false);

        // 4. Libera locomoção e inicia gameplay ativo
        if (controladorJogador != null)
        {
            controladorJogador.controlesHabilitados = true;
            controladorJogador.AtualizarPosicaoCamera();
        }

        if (controladorDemoWeb != null)
        {
            controladorDemoWeb.IniciarJogoPlay();
        }

        Debug.Log($"<color=#00FF99><b>[PARTIDA INICIADA APÓS CONTAGEM DE 5s]</b></color> Mapa: {tema} | Câmera: {modoCam}");
    }

    // =========================================================================
    // CARROSSEL DE MAPAS (INFINITO)
    // =========================================================================

    public void AvancarMapaCarrossel()
    {
        m_IndiceMapaAtual = (m_IndiceMapaAtual + 1) % 3;
        AtualizarVisualCarrosselMapa();
    }

    public void VoltarMapaCarrossel()
    {
        m_IndiceMapaAtual = (m_IndiceMapaAtual - 1 + 3) % 3;
        AtualizarVisualCarrosselMapa();
    }

    private void AtualizarVisualCarrosselMapa()
    {
        switch (m_IndiceMapaAtual)
        {
            case 0: // Dark Grid Neon
                if (m_ImgPreviewMapa != null && m_TexMapaNeon != null) m_ImgPreviewMapa.texture = m_TexMapaNeon;
                if (m_TextoTituloMapa != null) m_TextoTituloMapa.text = "MAPA 1: DARK NEON GRID";
                if (m_TextoDescricaoMapa != null) m_TextoDescricaoMapa.text = "Grade cibernética escura com linhas neon ciano e magenta. Visual futurista ideal para alto contraste no Quest 3.";
                if (m_TextoIndicadorMapa != null) m_TextoIndicadorMapa.text = "● ○ ○  (1 de 3)";
                break;

            case 1: // Clássico Dia (CIn-UFPE)
                if (m_ImgPreviewMapa != null && m_TexMapaDia != null) m_ImgPreviewMapa.texture = m_TexMapaDia;
                if (m_TextoTituloMapa != null) m_TextoTituloMapa.text = "MAPA 2: CLÁSSICO DIA (CIn-UFPE)";
                if (m_TextoDescricaoMapa != null) m_TextoDescricaoMapa.text = "Piso verde esmeralda, paredes cinzas com quadros e janelas, e floresta perimetral infinita 360° com mais de 120 árvores.";
                if (m_TextoIndicadorMapa != null) m_TextoIndicadorMapa.text = "○ ● ○  (2 de 3)";
                break;

            case 2: // Especial Natal
                if (m_ImgPreviewMapa != null && m_TexMapaNatal != null) m_ImgPreviewMapa.texture = m_TexMapaNatal;
                if (m_TextoTituloMapa != null) m_TextoTituloMapa.text = "MAPA 3: ESPECIAL DE NATAL";
                if (m_TextoDescricaoMapa != null) m_TextoDescricaoMapa.text = "Chão nevado, pinheiros decorados com estrelas, presentes natalinos, bengalas doces e neve suave caindo no labirinto.";
                if (m_TextoIndicadorMapa != null) m_TextoIndicadorMapa.text = "○ ○ ●  (3 de 3)";
                break;
        }
    }

    // =========================================================================
    // CARROSSEL DE CÂMERAS (INFINITO)
    // =========================================================================

    public void AvancarCameraCarrossel()
    {
        m_IndiceCameraAtual = (m_IndiceCameraAtual + 1) % 3;
        AtualizarVisualCarrosselCamera();
    }

    public void VoltarCameraCarrossel()
    {
        m_IndiceCameraAtual = (m_IndiceCameraAtual - 1 + 3) % 3;
        AtualizarVisualCarrosselCamera();
    }

    private void AtualizarVisualCarrosselCamera()
    {
        switch (m_IndiceCameraAtual)
        {
            case 0: // 1ª Pessoa VR 360°
                if (m_ImgPreviewCamera != null && m_TexCam1P != null) m_ImgPreviewCamera.texture = m_TexCam1P;
                if (m_TextoTituloCamera != null) m_TextoTituloCamera.text = "1ª PESSOA: OLHOS DO PAC-MAN (VR 360°)";
                if (m_TextoDescricaoCamera != null) m_TextoDescricaoCamera.text = "Imersão total em primeira pessoa através dos olhos do Pac-Man com rastreamento 6DoF pelo labirinto.";
                if (m_TextoIndicadorCamera != null) m_TextoIndicadorCamera.text = "● ○ ○  (1 de 3)";
                break;

            case 1: // 3ª Pessoa Seguidora
                if (m_ImgPreviewCamera != null && m_TexCam3P != null) m_ImgPreviewCamera.texture = m_TexCam3P;
                if (m_TextoTituloCamera != null) m_TextoTituloCamera.text = "3ª PESSOA: SEGUIDORA PRÓXIMA";
                if (m_TextoDescricaoCamera != null) m_TextoDescricaoCamera.text = "Câmera suavemente posicionada logo atrás do Pac-Man (1,35m), mantendo o personagem à frente sem clipping de paredes.";
                if (m_TextoIndicadorCamera != null) m_TextoIndicadorCamera.text = "○ ● ○  (2 de 3)";
                break;

            case 2: // Aérea Tática Pinball
                if (m_ImgPreviewCamera != null && m_TexCamAerea != null) m_ImgPreviewCamera.texture = m_TexCamAerea;
                if (m_TextoTituloCamera != null) m_TextoTituloCamera.text = "AÉREA: VISÃO SUPERIOR DO LABIRINTO";
                if (m_TextoDescricaoCamera != null) m_TextoDescricaoCamera.text = "Visão panorâmica superior a 6,5m de altura, oferecendo controle tático integral do mapa e grafo ontológico.";
                if (m_TextoIndicadorCamera != null) m_TextoIndicadorCamera.text = "○ ○ ●  (3 de 3)";
                break;
        }
    }

    // =========================================================================
    // CONFIGURAÇÕES: ÁUDIO, BLUETOOTH E SENSIBILIDADE
    // =========================================================================

    public void AcaoDefinirVolume(float volume)
    {
        AudioListener.volume = Mathf.Clamp01(volume);
        AtualizarTextosStatus();
    }

    public void AcaoAumentarSensibilidade()
    {
        m_FatorSensibilidade = Mathf.Clamp(m_FatorSensibilidade + 0.25f, 0.5f, 3.0f);
        AplicarSensibilidade();
    }

    public void AcaoDiminuirSensibilidade()
    {
        m_FatorSensibilidade = Mathf.Clamp(m_FatorSensibilidade - 0.25f, 0.5f, 3.0f);
        AplicarSensibilidade();
    }

    private void AplicarSensibilidade()
    {
        LocalizarControladores();
        if (controladorJogador != null)
        {
            controladorJogador.sensibilidadeOlharYaw = 85.0f * m_FatorSensibilidade;
            controladorJogador.sensibilidadeOlharPitch = 65.0f * m_FatorSensibilidade;
        }
        AtualizarTextosStatus();
    }

    public void AbrirConfiguracoesBluetooth()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    using (var intent = new AndroidJavaObject("android.content.Intent", "android.settings.BLUETOOTH_SETTINGS"))
                    {
                        intent.Call<AndroidJavaObject>("addFlags", 0x10000000); // FLAG_ACTIVITY_NEW_TASK
                        currentActivity.Call("startActivity", intent);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HUB] Erro ao abrir configurações de Bluetooth: {ex.Message}");
        }
#else
        Debug.Log("[HUB] Abrir configurações Bluetooth (Desktop Simulado).");
#endif
    }

    private void AtualizarTextosStatus()
    {
        if (m_TextoStatusAudio != null)
        {
            m_TextoStatusAudio.text = $"Volume: {Mathf.RoundToInt(AudioListener.volume * 100)}%";
        }
        if (m_TextoSensibilidade != null)
        {
            m_TextoSensibilidade.text = $"Sensibilidade: {m_FatorSensibilidade:F2}x";
        }
    }

    // =========================================================================
    // POSICIONAMENTO PARA REALIDADE VIRTUAL (360° VR FLUTUANTE)
    // =========================================================================

    public void PosicionarCanvasParaVRSeNecessario()
    {
        AtualizarAcompanhamentoOlharVR(true);
    }

    // =========================================================================
    // MODAIS DE VITÓRIA E GAME OVER
    // =========================================================================

    public void ExibirGameOver(int pontuacaoFinal, int gemasComidas)
    {
        partidaEmAndamento = false;
        hubAberto = false;
        if (m_PainelJanelaHub != null) m_PainelJanelaHub.SetActive(false);
        if (m_PainelJanelaVitoria != null) m_PainelJanelaVitoria.SetActive(false);

        // IMOBILIZAÇÃO TOTAL IMEDIATA
        LocalizarControladores();
        if (controladorJogador != null)
        {
            controladorJogador.PararMovimentoTotal();
            controladorJogador.controlesHabilitados = false;
            controladorJogador.travarCursorAoClicar = false;
        }
        ImobilizarTodosFantasmas();

        PosicionarCanvasParaVRSeNecessario();
        if (m_PainelFundoEscuro != null) m_PainelFundoEscuro.SetActive(true);

        if (PainelGrafoSemanticoProtege.Instancia != null)
        {
            PainelGrafoSemanticoProtege.Instancia.ExibirGrafoFinal(false, gemasComidas, 12, pontuacaoFinal);
        }
        else
        {
            if (m_PainelJanelaGameOver == null) ConstruirInterfaceGameOver();
            if (m_TextoInfoGameOver != null)
            {
                m_TextoInfoGameOver.text = $"Pontuação Final: <color=#00FF99><b>{pontuacaoFinal} pts</b></color>\n" +
                                           $"Nós Ontológicos Coletados: <b>{gemasComidas}</b>\n\n" +
                                           $"<color=#FF99AA>Seus elos de consistência foram partidos pelos fantasmas.</color>\n" +
                                           $"<color=#00E5FF>Pressione [A], [START] ou [SELECT] para voltar ao Menu Inicial.</color>";
            }
            if (m_PainelJanelaGameOver != null) m_PainelJanelaGameOver.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ExibirVitoria(int pontuacaoFinal, int totalOrbes)
    {
        partidaEmAndamento = false;
        hubAberto = false;
        if (m_PainelJanelaHub != null) m_PainelJanelaHub.SetActive(false);
        if (m_PainelJanelaGameOver != null) m_PainelJanelaGameOver.SetActive(false);

        // IMOBILIZAÇÃO TOTAL IMEDIATA DE TUDO NO MAPA
        LocalizarControladores();
        if (controladorJogador != null)
        {
            controladorJogador.PararMovimentoTotal();
            controladorJogador.controlesHabilitados = false;
            controladorJogador.travarCursorAoClicar = false;
        }
        ImobilizarTodosFantasmas();

        PosicionarCanvasParaVRSeNecessario();
        if (m_PainelFundoEscuro != null) m_PainelFundoEscuro.SetActive(true);

        if (PainelGrafoSemanticoProtege.Instancia != null)
        {
            PainelGrafoSemanticoProtege.Instancia.ExibirGrafoFinal(true, totalOrbes, totalOrbes, pontuacaoFinal);
        }
        else
        {
            if (m_PainelJanelaVitoria == null) ConstruirInterfaceVitoria();
            if (m_TextoInfoVitoria != null)
            {
                m_TextoInfoVitoria.text = $"PONTUAÇÃO FINAL: <color=#FFD700><b>{pontuacaoFinal} pts</b></color>   |   ORBES ONTOLÓGICAS: <color=#00FF99><b>{totalOrbes} / {totalOrbes} (100%)</b></color>   |   REASONER: <color=#00E5FF><b>100% CONSISTENTE</b></color>";
            }
            if (m_PainelJanelaVitoria != null) m_PainelJanelaVitoria.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // =========================================================================
    // CONSTRUÇÃO DINÂMICA DA INTERFACE DO HUB
    // =========================================================================

    private void ConstruirInterfaceHub()
    {
        // 1. Canvas do Hub
        GameObject canvasObj = new GameObject("Hub_Inicial_Canvas");
        canvasObj.transform.SetParent(transform, false);
        m_CanvasHub = canvasObj.AddComponent<Canvas>();
        m_CanvasHub.renderMode = RenderMode.ScreenSpaceOverlay;
        m_CanvasHub.sortingOrder = 50;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var raycaster = canvasObj.AddComponent<GraphicRaycaster>();
        raycaster.enabled = true;

        // 2. Fundo Translúcido Escurecido
        GameObject painelFundo = new GameObject("Painel_Fundo_Escuro", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        painelFundo.transform.SetParent(canvasObj.transform, false);
        m_PainelFundoEscuro = painelFundo;
        RectTransform rtFundo = painelFundo.GetComponent<RectTransform>();
        rtFundo.anchorMin = Vector2.zero;
        rtFundo.anchorMax = Vector2.one;
        rtFundo.sizeDelta = Vector2.zero;
        Image imgFundo = painelFundo.GetComponent<Image>();
        imgFundo.color = new Color(0f, 0f, 0f, 0.76f);

        // 3. Janela Central do Hub
        GameObject janela = new GameObject("Janela_Central_Hub", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        janela.transform.SetParent(painelFundo.transform, false);
        m_PainelJanelaHub = janela;
        RectTransform rtJanela = janela.GetComponent<RectTransform>();
        rtJanela.anchorMin = new Vector2(0.5f, 0.5f);
        rtJanela.anchorMax = new Vector2(0.5f, 0.5f);
        rtJanela.pivot = new Vector2(0.5f, 0.5f);
        rtJanela.sizeDelta = new Vector2(920f, 650f);
        Image imgJanela = janela.GetComponent<Image>();
        imgJanela.color = new Color(0.04f, 0.08f, 0.16f, 0.98f);

        var outlineJanela = janela.AddComponent<Outline>();
        outlineJanela.effectColor = new Color(0.0f, 0.85f, 1.0f, 0.85f);
        outlineJanela.effectDistance = new Vector2(2f, -2f);

        // Construção das 5 Telas do Hub
        ConstruirTelaSelecaoOntologia(janela.transform);
        ConstruirTelaBoasVindas(janela.transform);
        ConstruirTelaEscolhaMapa(janela.transform);
        ConstruirTelaEscolhaCamera(janela.transform);
        ConstruirTelaConfiguracoes(janela.transform);
    }

    // -------------------------------------------------------------------------
    // TELA 1: SINCRONIZAÇÃO PROTÉGÉ & SELEÇÃO DE ONTOLOGIA (PLUGIN JAVA)
    // -------------------------------------------------------------------------
    private void ConstruirTelaSelecaoOntologia(Transform pai)
    {
        GameObject tela = new GameObject("Tela_SelecaoOntologia", typeof(RectTransform));
        tela.transform.SetParent(pai, false);
        m_PainelSelecaoOntologia = tela;
        RectTransform rt = tela.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        // Cabeçalho
        CriarTexto(tela.transform, "Titulo_SO", "PAC-MAN SEMÂNTICO - SINCRONIZAÇÃO DE ONTOLOGIAS", 26, FontStyles.Bold,
            new Vector2(0f, 255f), new Vector2(860f, 40f), new Color(1.0f, 0.85f, 0.10f));

        CriarTexto(tela.transform, "Status_Servidor", "● SERVIDOR HTTP/WEBSOCKET ATIVO (Porta 8085 / 8080) • PLUGIN PROTÉGÉ CONECTADO", 13, FontStyles.Bold,
            new Vector2(0f, 222f), new Vector2(860f, 26f), new Color(0.0f, 1.0f, 0.60f));

        CriarTexto(tela.transform, "SubTitulo_SO", "Selecione o documento ontológico recebido para mapear as classes e instâncias no Labirinto:", 14, FontStyles.Normal,
            new Vector2(0f, 192f), new Vector2(860f, 26f), new Color(0.80f, 0.90f, 1.0f));

        // 4 Cards de Arquivos Ontológicos
        float yInicial = 120f;
        float espacamentoY = 82f;

        for (int i = 0; i < m_NomesOntologias.Length; i++)
        {
            int idx = i;
            GameObject card = new GameObject($"Card_Onto_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            card.transform.SetParent(tela.transform, false);
            m_CardsOntologia[i] = card;

            RectTransform rtCard = card.GetComponent<RectTransform>();
            rtCard.anchoredPosition = new Vector2(0f, yInicial - i * espacamentoY);
            rtCard.sizeDelta = new Vector2(820f, 72f);

            Image imgCard = card.GetComponent<Image>();
            imgCard.color = (i == 0) ? new Color(0.04f, 0.12f, 0.22f, 0.98f) : new Color(0.02f, 0.05f, 0.10f, 0.88f);

            var outline = card.AddComponent<Outline>();
            m_OutlinesOntologia[i] = outline;
            outline.effectColor = (i == 0) ? new Color(0.0f, 1.0f, 0.60f, 0.95f) : new Color(0.15f, 0.35f, 0.55f, 0.40f);
            outline.effectDistance = (i == 0) ? new Vector2(2f, -2f) : new Vector2(1f, -1f);

            Button btnCard = card.GetComponent<Button>();
            btnCard.onClick.AddListener(() =>
            {
                m_IndiceOntologiaSelecionada = idx;
                AtualizarVisualSelecaoOntologia();
                AcaoConfirmarOntologiaESeguir();
            });

            CriarTexto(card.transform, $"Txt_Nome_{i}", $"📁  <b>{m_NomesOntologias[i]}</b>", 16, FontStyles.Bold,
                new Vector2(-30f, 14f), new Vector2(740f, 26f), new Color(0.0f, 0.95f, 1.0f));

            CriarTexto(card.transform, $"Txt_Desc_{i}", m_DescricoesOntologias[i], 11, FontStyles.Normal,
                new Vector2(-30f, -12f), new Vector2(740f, 22f), new Color(0.80f, 0.88f, 0.95f));

            CriarTexto(card.transform, $"Txt_Status_{i}", m_StatusOntologias[i], 9, FontStyles.Italic,
                new Vector2(250f, 14f), new Vector2(300f, 20f), new Color(0.0f, 1.0f, 0.50f));
        }

        // Botão de Confirmação Inferior
        CriarBotao(tela.transform, "Btn_ConfirmarOntologia", "> SINCRONIZAR ONTOLOGIA & JOGAR >", 18, FontStyles.Bold,
            new Vector2(0f, -225f), new Vector2(560f, 54f),
            new Color(0.0f, 0.65f, 0.35f), Color.white, AcaoConfirmarOntologiaESeguir);

        CriarTexto(tela.transform, "Atalhos_SO",
            "[▲ / ▼] Selecionar Arquivo   •   [A / ENTER / GATILHO] Sincronizar",
            13, FontStyles.Italic, new Vector2(0f, -275f), new Vector2(800f, 25f), new Color(0.65f, 0.75f, 0.90f));
    }

    // -------------------------------------------------------------------------
    // TELA 2: BOAS-VINDAS / JOGAR
    // -------------------------------------------------------------------------
    private void ConstruirTelaBoasVindas(Transform pai)
    {
        GameObject tela = new GameObject("Tela_BoasVindas", typeof(RectTransform));
        tela.transform.SetParent(pai, false);
        m_PainelBoasVindas = tela;
        RectTransform rt = tela.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        // Cabeçalho
        CriarTexto(tela.transform, "Titulo_BV", "PAC-MAN SEMÂNTICO VR", 32, FontStyles.Bold,
            new Vector2(0f, 230f), new Vector2(820f, 48f), new Color(1.0f, 0.85f, 0.10f));

        CriarTexto(tela.transform, "SubTitulo_BV", "Pac-Man Semântico • Centro de Informática (CIn - UFPE)", 17, FontStyles.Normal,
            new Vector2(0f, 185f), new Vector2(820f, 32f), new Color(0.0f, 0.90f, 1.0f));

        // Caixa de Explicação
        CriarTexto(tela.transform, "Desc_BV",
            "Navegue pelo labirinto tridimensional em VR 360°, colete os nós ontológicos consistentes " +
            "(Sintomas, Parasitas e Vetores) e evite os fantasmas de violação semântica.\n" +
            "Demonstre a consistência axiomática com o Reasoner HermiT em tempo real!",
            15, FontStyles.Normal, new Vector2(0f, 85f), new Vector2(760f, 100f), new Color(0.85f, 0.92f, 1.0f));

        // Botão Principal: JOGAR
        CriarBotao(tela.transform, "Btn_Jogar", "> JOGAR PARTIDA <", 24, FontStyles.Bold,
            new Vector2(0f, -30f), new Vector2(520f, 66f),
            new Color(0.0f, 0.72f, 0.35f), Color.white, AcaoAvancarParaEscolhaMapa);

        // Botão Secundário: CONFIGURAÇÕES
        CriarBotao(tela.transform, "Btn_Configuracoes", "⚙️ CONFIGURAÇÕES (ÁUDIO & CONTROLES)", 17, FontStyles.Normal,
            new Vector2(0f, -115f), new Vector2(520f, 48f),
            new Color(0.12f, 0.30f, 0.50f), Color.white, AcaoAbrirConfiguracoes);

        // Rodapé com atalhos
        CriarTexto(tela.transform, "Atalhos_BV",
            "[A / GATILHO / ENTER] Jogar   •   [B / ESC] Voltar Ontologias",
            14, FontStyles.Italic, new Vector2(0f, -240f), new Vector2(800f, 30f), new Color(0.65f, 0.75f, 0.90f));
    }

    // -------------------------------------------------------------------------
    // TELA 3: ESCOLHA DE MAPA (CARROSSEL)
    // -------------------------------------------------------------------------
    private void ConstruirTelaEscolhaMapa(Transform pai)
    {
        GameObject tela = new GameObject("Tela_EscolhaMapa", typeof(RectTransform));
        tela.transform.SetParent(pai, false);
        m_PainelEscolhaMapa = tela;
        RectTransform rt = tela.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        CriarTexto(tela.transform, "Titulo_EM", "PASSO 1 DE 2: SELEÇÃO DE MAPA", 26, FontStyles.Bold,
            new Vector2(0f, 245f), new Vector2(820f, 40f), new Color(1.0f, 0.85f, 0.10f));

        CriarTexto(tela.transform, "Sub_EM", "Escolha o cenário visual e ambiente temático para jogar:", 15, FontStyles.Normal,
            new Vector2(0f, 208f), new Vector2(820f, 28f), new Color(0.0f, 0.90f, 1.0f));

        // Área Central do Carrossel (Card com imagem e descrição)
        GameObject card = new GameObject("Card_Carrossel_Mapa", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        card.transform.SetParent(tela.transform, false);
        RectTransform rtCard = card.GetComponent<RectTransform>();
        rtCard.anchoredPosition = new Vector2(0f, 25f);
        rtCard.sizeDelta = new Vector2(540f, 280f);
        card.GetComponent<Image>().color = new Color(0.02f, 0.05f, 0.10f, 0.95f);
        card.AddComponent<Outline>().effectColor = new Color(0.0f, 0.85f, 1.0f, 0.50f);

        // Imagem Preview
        GameObject imgGO = new GameObject("Preview_Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imgGO.transform.SetParent(card.transform, false);
        RectTransform rtImg = imgGO.GetComponent<RectTransform>();
        rtImg.anchoredPosition = new Vector2(0f, 50f);
        rtImg.sizeDelta = new Vector2(480f, 150f);
        m_ImgPreviewMapa = imgGO.GetComponent<RawImage>();

        m_TextoTituloMapa = CriarTexto(card.transform, "Titulo_Mapa_Card", "MAPA", 18, FontStyles.Bold,
            new Vector2(0f, -48f), new Vector2(500f, 30f), Color.white);

        m_TextoDescricaoMapa = CriarTexto(card.transform, "Desc_Mapa_Card", "Descrição do mapa...", 12, FontStyles.Normal,
            new Vector2(0f, -85f), new Vector2(500f, 45f), new Color(0.80f, 0.88f, 0.95f));

        m_TextoIndicadorMapa = CriarTexto(card.transform, "Indicador_Mapa", "● ○ ○  (1 de 3)", 13, FontStyles.Bold,
            new Vector2(0f, -120f), new Vector2(500f, 22f), new Color(0.0f, 0.90f, 1.0f));

        // Botões de navegação lateral do carrossel
        CriarBotao(tela.transform, "Btn_Ant_Mapa", "◄", 28, FontStyles.Bold,
            new Vector2(-330f, 25f), new Vector2(80f, 160f),
            new Color(0.10f, 0.35f, 0.55f), Color.white, VoltarMapaCarrossel);

        CriarBotao(tela.transform, "Btn_Prox_Mapa", "►", 28, FontStyles.Bold,
            new Vector2(330f, 25f), new Vector2(80f, 160f),
            new Color(0.10f, 0.35f, 0.55f), Color.white, AvancarMapaCarrossel);

        // Botões Inferiores
        CriarBotao(tela.transform, "Btn_Voltar_EM", "◄ VOLTAR", 16, FontStyles.Normal,
            new Vector2(-170f, -180f), new Vector2(260f, 50f),
            new Color(0.35f, 0.15f, 0.20f), Color.white, () => DefinirEstadoTela(EstadoTelaHub.BoasVindas));

        CriarBotao(tela.transform, "Btn_Confirmar_EM", "> ESCOLHER CÂMERA >", 18, FontStyles.Bold,
            new Vector2(170f, -180f), new Vector2(340f, 50f),
            new Color(0.0f, 0.60f, 0.85f), Color.white, AcaoConfirmarMapaEAvancar);

        CriarTexto(tela.transform, "Atalhos_EM",
            "[◄ / ►] Mudar Mapa   •   [A / ENTER] Confirmar   •   [B / ESC] Voltar",
            13, FontStyles.Italic, new Vector2(0f, -245f), new Vector2(800f, 25f), new Color(0.65f, 0.75f, 0.90f));
    }

    // -------------------------------------------------------------------------
    // TELA 4: ESCOLHA DE CÂMERA (CARROSSEL)
    // -------------------------------------------------------------------------
    private void ConstruirTelaEscolhaCamera(Transform pai)
    {
        GameObject tela = new GameObject("Tela_EscolhaCamera", typeof(RectTransform));
        tela.transform.SetParent(pai, false);
        m_PainelEscolhaCamera = tela;
        RectTransform rt = tela.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        CriarTexto(tela.transform, "Titulo_EC", "PASSO 2 DE 2: PERSPECTIVA DE CÂMERA", 26, FontStyles.Bold,
            new Vector2(0f, 245f), new Vector2(820f, 40f), new Color(1.0f, 0.85f, 0.10f));

        CriarTexto(tela.transform, "Sub_EC", "Selecione como deseja visualizar o jogo no Meta Quest:", 15, FontStyles.Normal,
            new Vector2(0f, 208f), new Vector2(820f, 28f), new Color(0.0f, 0.90f, 1.0f));

        // Card Central do Carrossel de Câmeras
        GameObject card = new GameObject("Card_Carrossel_Camera", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        card.transform.SetParent(tela.transform, false);
        RectTransform rtCard = card.GetComponent<RectTransform>();
        rtCard.anchoredPosition = new Vector2(0f, 25f);
        rtCard.sizeDelta = new Vector2(540f, 280f);
        card.GetComponent<Image>().color = new Color(0.02f, 0.05f, 0.10f, 0.95f);
        card.AddComponent<Outline>().effectColor = new Color(0.0f, 0.85f, 1.0f, 0.50f);

        // Imagem Preview
        GameObject imgGO = new GameObject("Preview_Camera_Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imgGO.transform.SetParent(card.transform, false);
        RectTransform rtImg = imgGO.GetComponent<RectTransform>();
        rtImg.anchoredPosition = new Vector2(0f, 50f);
        rtImg.sizeDelta = new Vector2(480f, 150f);
        m_ImgPreviewCamera = imgGO.GetComponent<RawImage>();

        m_TextoTituloCamera = CriarTexto(card.transform, "Titulo_Camera_Card", "CÂMERA", 18, FontStyles.Bold,
            new Vector2(0f, -48f), new Vector2(500f, 30f), Color.white);

        m_TextoDescricaoCamera = CriarTexto(card.transform, "Desc_Camera_Card", "Descrição da perspectiva...", 12, FontStyles.Normal,
            new Vector2(0f, -85f), new Vector2(500f, 45f), new Color(0.80f, 0.88f, 0.95f));

        m_TextoIndicadorCamera = CriarTexto(card.transform, "Indicador_Camera", "● ○ ○  (1 de 3)", 13, FontStyles.Bold,
            new Vector2(0f, -120f), new Vector2(500f, 22f), new Color(0.0f, 0.90f, 1.0f));

        // Botões de navegação lateral do carrossel
        CriarBotao(tela.transform, "Btn_Ant_Cam", "◄", 28, FontStyles.Bold,
            new Vector2(-330f, 25f), new Vector2(80f, 160f),
            new Color(0.10f, 0.35f, 0.55f), Color.white, VoltarCameraCarrossel);

        CriarBotao(tela.transform, "Btn_Prox_Cam", "►", 28, FontStyles.Bold,
            new Vector2(330f, 25f), new Vector2(80f, 160f),
            new Color(0.10f, 0.35f, 0.55f), Color.white, AvancarCameraCarrossel);

        // Botões Inferiores
        CriarBotao(tela.transform, "Btn_Voltar_EC", "◄ VOLTAR", 16, FontStyles.Normal,
            new Vector2(-170f, -180f), new Vector2(260f, 50f),
            new Color(0.35f, 0.15f, 0.20f), Color.white, () => DefinirEstadoTela(EstadoTelaHub.EscolhaMapa));

        CriarBotao(tela.transform, "Btn_Iniciar_Partida", "🎮 INICIAR PARTIDA AGORA", 19, FontStyles.Bold,
            new Vector2(170f, -180f), new Vector2(360f, 50f),
            new Color(0.0f, 0.70f, 0.35f), Color.white, AcaoPlayIniciarPartida);

        CriarTexto(tela.transform, "Atalhos_EC",
            "[◄ / ►] Mudar Câmera   •   [A / ENTER] Iniciar Partida   •   [B / ESC] Voltar",
            13, FontStyles.Italic, new Vector2(0f, -245f), new Vector2(800f, 25f), new Color(0.65f, 0.75f, 0.90f));
    }

    // -------------------------------------------------------------------------
    // TELA 5: CONFIGURAÇÕES (ÁUDIO / BLUETOOTH / SENSIBILIDADE)
    // -------------------------------------------------------------------------
    private void ConstruirTelaConfiguracoes(Transform pai)
    {
        GameObject tela = new GameObject("Tela_Configuracoes", typeof(RectTransform));
        tela.transform.SetParent(pai, false);
        m_PainelConfiguracoes = tela;
        RectTransform rt = tela.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        CriarTexto(tela.transform, "Titulo_Cfg", "CONFIGURAÇÕES DO JOGO & CONTROLES", 26, FontStyles.Bold,
            new Vector2(0f, 245f), new Vector2(820f, 40f), new Color(0.0f, 0.90f, 1.0f));

        // Seção Áudio
        m_TextoStatusAudio = CriarTexto(tela.transform, "Txt_Vol", "Volume: 100%", 17, FontStyles.Bold,
            new Vector2(0f, 175f), new Vector2(500f, 30f), Color.white);

        CriarBotao(tela.transform, "Btn_Vol_Menos", "– 20%", 16, FontStyles.Bold,
            new Vector2(-150f, 130f), new Vector2(140f, 42f),
            new Color(0.20f, 0.35f, 0.50f), Color.white, () => AcaoDefinirVolume(AudioListener.volume - 0.2f));

        CriarBotao(tela.transform, "Btn_Vol_Mudo", "MUDO", 15, FontStyles.Normal,
            new Vector2(0f, 130f), new Vector2(120f, 42f),
            new Color(0.40f, 0.15f, 0.15f), Color.white, () => AcaoDefinirVolume(0f));

        CriarBotao(tela.transform, "Btn_Vol_Mais", "+ 20%", 16, FontStyles.Bold,
            new Vector2(150f, 130f), new Vector2(140f, 42f),
            new Color(0.20f, 0.35f, 0.50f), Color.white, () => AcaoDefinirVolume(AudioListener.volume + 0.2f));

        // Seção Sensibilidade do Analógico
        m_TextoSensibilidade = CriarTexto(tela.transform, "Txt_Sens", "Sensibilidade: 1.00x", 17, FontStyles.Bold,
            new Vector2(0f, 65f), new Vector2(500f, 30f), Color.white);

        CriarBotao(tela.transform, "Btn_Sens_Menos", "– 0.25x", 16, FontStyles.Bold,
            new Vector2(-120f, 20f), new Vector2(150f, 42f),
            new Color(0.20f, 0.35f, 0.50f), Color.white, AcaoDiminuirSensibilidade);

        CriarBotao(tela.transform, "Btn_Sens_Mais", "+ 0.25x", 16, FontStyles.Bold,
            new Vector2(120f, 20f), new Vector2(150f, 42f),
            new Color(0.20f, 0.35f, 0.50f), Color.white, AcaoAumentarSensibilidade);

        // Seção Bluetooth (Pareamento de Joysticks)
        CriarTexto(tela.transform, "Txt_Bt_Info", "Pareamento de Controles Touch / Joysticks Bluetooth:", 15, FontStyles.Normal,
            new Vector2(0f, -45f), new Vector2(600f, 26f), new Color(0.85f, 0.90f, 0.95f));

        CriarBotao(tela.transform, "Btn_Abrir_Bluetooth", "📶 ABRIR BLUETOOTH DO META QUEST", 16, FontStyles.Bold,
            new Vector2(0f, -90f), new Vector2(480f, 50f),
            new Color(0.0f, 0.55f, 0.85f), Color.white, AbrirConfiguracoesBluetooth);

        // Botão Voltar
        CriarBotao(tela.transform, "Btn_Voltar_Cfg", "◄ VOLTAR AO MENU", 18, FontStyles.Bold,
            new Vector2(0f, -180f), new Vector2(380f, 50f),
            new Color(0.35f, 0.15f, 0.20f), Color.white, () => DefinirEstadoTela(EstadoTelaHub.BoasVindas));

        AtualizarTextosStatus();
    }

    // -------------------------------------------------------------------------
    // PAINEL DE CONTAGEM REGRESSIVA (READY PAUSE DE 5 SEGUNDOS)
    // -------------------------------------------------------------------------
    private void ConstruirPainelContagemRegressiva()
    {
        if (m_CanvasHub == null) return;

        GameObject painel = new GameObject("Painel_Contagem_Regressiva", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        painel.transform.SetParent(m_CanvasHub.transform, false);
        m_PainelContagemRegressiva = painel;

        RectTransform rt = painel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(560f, 320f);

        Image img = painel.GetComponent<Image>();
        img.color = new Color(0.02f, 0.05f, 0.12f, 0.92f);
        painel.AddComponent<Outline>().effectColor = new Color(0.0f, 0.85f, 1.0f, 0.90f);

        m_TextoContagemRegressiva = CriarTexto(painel.transform, "Texto_Contagem",
            "<color=#00E5FF><b>PREPARAR...</b></color>\n<size=110><color=#FFD700><b>5</b></color></size>",
            32, FontStyles.Bold, Vector2.zero, new Vector2(520f, 260f), Color.white);
    }

    // -------------------------------------------------------------------------
    // MODAL DE GAME OVER
    // -------------------------------------------------------------------------
    private void ConstruirInterfaceGameOver()
    {
        if (m_CanvasHub == null) return;

        GameObject janelaGO = new GameObject("Janela_Central_GameOver", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        janelaGO.transform.SetParent(m_CanvasHub.transform, false);
        m_PainelJanelaGameOver = janelaGO;

        RectTransform rt = janelaGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(760f, 520f);

        Image img = janelaGO.GetComponent<Image>();
        img.color = new Color(0.12f, 0.02f, 0.04f, 0.98f);
        janelaGO.AddComponent<Outline>().effectColor = new Color(1.0f, 0.15f, 0.25f, 0.85f);

        CriarTexto(janelaGO.transform, "Texto_GameOver_Titulo", "VIOLAÇÃO ONTOLÓGICA - GAME OVER", 28, FontStyles.Bold,
            new Vector2(0f, 210f), new Vector2(700f, 45f), new Color(1.0f, 0.20f, 0.30f));

        m_TextoInfoGameOver = CriarTexto(janelaGO.transform, "Texto_GameOver_Info",
            "Pontuação Final: 0 pts", 16, FontStyles.Normal,
            new Vector2(0f, 75f), new Vector2(680f, 160f), Color.white);

        CriarBotao(janelaGO.transform, "Botao_VoltarMenuGameOver", "> VOLTAR AO MENU PRINCIPAL <", 19, FontStyles.Bold,
            new Vector2(0f, -90f), new Vector2(520f, 52f),
            new Color(0.0f, 0.55f, 0.85f), Color.white, AcaoIrParaMenuPrincipal);

        CriarTexto(janelaGO.transform, "Texto_AtalhoGameOver",
            "[A / ENTER / START / SELECT / ESC] Voltar ao Menu Principal",
            14, FontStyles.Italic, new Vector2(0f, -195f), new Vector2(680f, 25f), new Color(0.75f, 0.80f, 0.90f));
    }

    // -------------------------------------------------------------------------
    // MODAL DE VITÓRIA (MAPA TAXONÔMICO ONTOLÓGICO COMPLETO EM ALTA RESOLUÇÃO)
    // -------------------------------------------------------------------------
    private void ConstruirInterfaceVitoria()
    {
        if (m_CanvasHub == null) return;

        GameObject janelaGO = new GameObject("Janela_Central_Vitoria", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        janelaGO.transform.SetParent(m_CanvasHub.transform, false);
        m_PainelJanelaVitoria = janelaGO;

        RectTransform rt = janelaGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1180f, 730f);

        Image img = janelaGO.GetComponent<Image>();
        img.color = new Color(0.02f, 0.08f, 0.05f, 0.98f);
        janelaGO.AddComponent<Outline>().effectColor = new Color(0.0f, 1.0f, 0.55f, 0.85f);

        CriarTexto(janelaGO.transform, "Texto_Vitoria_Titulo", "★ VITÓRIA ONTOLÓGICA CONCLUÍDA! ★", 28, FontStyles.Bold,
            new Vector2(0f, 320f), new Vector2(1100f, 45f), new Color(1.0f, 0.85f, 0.10f));

        m_TextoInfoVitoria = CriarTexto(janelaGO.transform, "Texto_Vitoria_Info", "PONTUAÇÃO: 0 pts", 15, FontStyles.Bold,
            new Vector2(0f, 280f), new Vector2(1100f, 30f), new Color(0.0f, 1.0f, 0.60f));

        CriarTexto(janelaGO.transform, "Texto_Taxonomia_Titulo",
            "FLUXOGRAMA COMPLETO - INSTÂNCIAS MAPEADAS PELO RACIOCINADOR HERMIT",
            16, FontStyles.Bold, new Vector2(0f, 240f), new Vector2(1100f, 26f), new Color(0.0f, 0.90f, 1.0f));

        // 3 Colunas Ontológicas Destacadas
        ConstruirColunaTaxonomia(janelaGO.transform, "COLUNA 1: SINTOMAS",
            new Color(0.0f, 0.85f, 0.95f), new Vector2(-370f, 45f),
            new string[] { "FebreAlta_Axioma1", "CefaleiaIntensa_Axioma2", "Calafrios_Axioma3", "SudoreseNoturna_Axioma4", "MialgiaGeneralizada_Axioma5", "Artralgia_Axioma6", "FadigaSevera_Axioma7", "DorRetroorbital_Axioma8" });

        ConstruirColunaTaxonomia(janelaGO.transform, "COLUNA 2: PARASITAS",
            new Color(0.95f, 0.20f, 0.40f), new Vector2(0f, 45f),
            new string[] { "Plasmodium_vivax_Esquizonte", "Plasmodium_falciparum_Trofozoito", "Plasmodium_malariae_Merozoito", "Plasmodium_ovale_Hipnozoito", "P_falciparum_GametaMasc", "P_vivax_GametaFem", "Plasmodium_knowlesi_Zoonose", "Complexo_Plasmodial_Invasao" });

        ConstruirColunaTaxonomia(janelaGO.transform, "COLUNA 3: VETORES",
            new Color(0.20f, 0.95f, 0.40f), new Vector2(370f, 45f),
            new string[] { "Anopheles_darlingi_Femea", "Anopheles_aquasalis_Litoraneo", "Anopheles_albitarsis_Complexo", "Anopheles_nuneztovari_Interior", "Anopheles_triannulatus_Silvestre", "Anopheles_cruzii_MataAtlantica", "Anopheles_bellator_Bromelias", "Larva_Culicidae_Aquatica" });

        CriarBotao(janelaGO.transform, "Botao_VoltarMenuVitoria", "> MENU PRINCIPAL (HUB) <", 20, FontStyles.Bold,
            new Vector2(0f, -225f), new Vector2(540f, 55f),
            new Color(0.0f, 0.55f, 0.85f), Color.white, AcaoIrParaMenuPrincipal);

        CriarTexto(janelaGO.transform, "Texto_AtalhoVitoria",
            "[A / ENTER / START / SELECT / ESC] Voltar ao Menu Principal",
            14, FontStyles.Italic, new Vector2(0f, -280f), new Vector2(1000f, 25f), new Color(0.70f, 0.80f, 0.90f));
    }

    private void ConstruirColunaTaxonomia(Transform pai, string titulo, Color cor, Vector2 pos, string[] itens)
    {
        GameObject colGO = new GameObject("Col_" + titulo, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        colGO.transform.SetParent(pai, false);
        RectTransform rtCol = colGO.GetComponent<RectTransform>();
        rtCol.anchoredPosition = pos;
        rtCol.sizeDelta = new Vector2(340f, 320f);
        colGO.GetComponent<Image>().color = new Color(0.02f, 0.05f, 0.08f, 0.90f);
        colGO.AddComponent<Outline>().effectColor = cor * 0.7f;

        CriarTexto(colGO.transform, "TitCol", titulo, 15, FontStyles.Bold,
            new Vector2(0f, 135f), new Vector2(320f, 28f), cor);

        string corpo = "";
        for (int i = 0; i < itens.Length; i++)
        {
            corpo += $"✓ <color=#FFFFFF>{itens[i]}</color>\n";
        }

        CriarTexto(colGO.transform, "CorpoCol", corpo, 12, FontStyles.Normal,
            new Vector2(0f, -15f), new Vector2(310f, 250f), new Color(0.85f, 0.90f, 0.95f));
    }

    // =========================================================================
    // SOM BEEP PROCEDURAL (ZERO ALOCAÇÃO EXTERNA)
    // =========================================================================

    private void TocarSomBeepProcedural(float frequencia)
    {
        if (m_AudioSourceHub == null) return;
        int sampleRate = 44100;
        float duracao = 0.08f;
        int numSamples = (int)(sampleRate * duracao);
        float[] samples = new float[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (t / duracao);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequencia * t) * envelope * 0.35f;
        }

        AudioClip clip = AudioClip.Create("Beep_Menu", numSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        m_AudioSourceHub.PlayOneShot(clip);
    }

    // =========================================================================
    // UTILITÁRIOS ZERO-GC PARA CRIAÇÃO DE UI
    // =========================================================================

    private TMP_Text CriarTexto(Transform pai, string nome, string conteudo, float tamanho, FontStyles estilo, Vector2 pos, Vector2 dim, Color cor)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = dim;

        TMP_Text txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = conteudo;
        txt.fontSize = tamanho;
        txt.fontStyle = estilo;
        txt.color = cor;
        txt.alignment = TextAlignmentOptions.Center;
        txt.textWrappingMode = TextWrappingModes.Normal;
        return txt;
    }

    private Button CriarBotao(Transform pai, string nome, string rotulo, float tamanhoTexto, FontStyles estiloTexto, Vector2 pos, Vector2 dim, Color corFundo, Color corTexto, UnityEngine.Events.UnityAction acao)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(pai, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = dim;

        Image img = go.GetComponent<Image>();
        img.color = corFundo;

        Button btn = go.GetComponent<Button>();
        var cb = btn.colors;
        cb.normalColor = corFundo;
        cb.highlightedColor = corFundo * 1.25f;
        cb.pressedColor = corFundo * 0.75f;
        cb.selectedColor = corFundo * 1.15f;
        btn.colors = cb;
        btn.onClick.AddListener(acao);

        CriarTexto(go.transform, "Texto_" + nome, rotulo, tamanhoTexto, estiloTexto, Vector2.zero, dim, corTexto);
        return btn;
    }
}

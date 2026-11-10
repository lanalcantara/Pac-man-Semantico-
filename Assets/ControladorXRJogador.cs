using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using PacMan.Semantics;
using PacMan.XR;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Controlador em Primeira Pessoa (1ª Pessoa)
/// ============================================================================
/// Gerencia a locomoção contínua, colisão física, orientação 360° e perspectiva
/// em primeira pessoa com escala 1:1 humana para garantir máxima verossimilhança
/// com a arquitetura do labirinto (paredes de 2.60m de altura e corredores de 2.20m).
///
/// Responsabilidades:
/// 1. Perspectiva em Primeira Pessoa (olhos a 1.68m do chão - escala humana realista).
/// 2. Orientação livre com Mouse Look / Thumbstick VR (Yaw 360° e Pitch vertical).
/// 3. Locomoção física suave com inércia e deslizamento em paredes (Wall Sliding).
/// 4. Colisor físico de cápsula humana (1.75m de altura, 0.76m de largura de ombros).
/// 5. Suporte transparente a VR (Meta Quest 3/3S com 6DoF nativo) e Desktop FPS.
/// 6. Disciplina rigorosa de Zero-GC (0 bytes alocados no heap durante o gameplay).
/// ============================================================================
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class ControladorXRJogador : MonoBehaviour
{
    private Transform m_VisualPacManTransform;
    public Transform PosicaoPacManReal => transform;

    private Rigidbody m_Rigidbody;
    private CapsuleCollider m_CapsuleCollider;
    private Vector3 m_VetorMovimentoDesejado = Vector3.zero;
    private Vector3 m_VelocidadeLinearAtual = Vector3.zero;

    // Buffers estáticos para alocação Zero-GC em tempo de execução
    private readonly Collider[] m_BufferColisoresGemas = new Collider[16];
    private readonly Collider[] m_BufferColisoresFantasmas = new Collider[8];
    private readonly RaycastHit[] m_BufferHitsParedes = new RaycastHit[4];

    // Sistema desacoplado de entrada unificada (Meta Quest Touch Plus, Gamepad Xbox, Teclado/Mouse)
    private readonly GerenciadorEntradaUnificada m_GerenciadorEntrada = new GerenciadorEntradaUnificada();

    [Header("Identidade do Sistema")]
    [Tooltip("Nome do módulo de controle.")]
    public string moduloControle = "Pac-Man Semântico XR Rig (1ª Pessoa)";

    [Header("Componentes Físicos")]
    [Tooltip("Referência ao CharacterController do jogador (mantido para compatibilidade).")]
    public CharacterController characterController;

    [Header("Hierarquia XR Origin")]
    [Tooltip("Transform do Offset de Câmera (ajuste de altura/chão no XR Origin).")]
    public Transform cameraOffset;

    [Tooltip("Câmera principal dos olhos do jogador (Headset / HMD / FPS Camera).")]
    public Camera cameraPrincipalXR;

    [Tooltip("Objeto que representa a Mão/Controlador Esquerdo.")]
    public Transform controladorEsquerdo;

    [Tooltip("Objeto que representa a Mão/Controlador Direito.")]
    public Transform controladorDireito;

    [Header("Perspectiva em Primeira Pessoa (Escala 1:1 Humana)")]
    [Tooltip("Altura exata dos olhos em relação ao chão (1.68m = altura média humana para verossimilhança com paredes de 2.60m).")]
    public float alturaOlhosHumano = 1.68f;

    [Tooltip("Sensibilidade horizontal do mouse/analógico para olhar ao redor (Yaw).")]
    public float sensibilidadeOlharYaw = 160f;

    [Tooltip("Sensibilidade vertical do mouse/analógico para olhar para cima/baixo (Pitch).")]
    public float sensibilidadeOlharPitch = 110f;

    [Tooltip("Limite inferior de inclinação da cabeça em graus (olhar para o chão).")]
    public float pitchMinimo = -80f;

    [Tooltip("Limite superior de inclinação da cabeça em graus (olhar para o topo das paredes/céu).")]
    public float pitchMaximo = 80f;

    [Tooltip("Campo de visão da câmera em 1ª pessoa no Desktop (75° proporciona profundidade e proporção verossímil sem distorção).")]
    public float campoDeVisaoFPS = 75f;

    [Tooltip("Travar o cursor do mouse na janela do jogo para controle de câmera FPS (ESC libera).")]
    public bool travarCursorAoClicar = true;

    public enum ModoCameraJogador
    {
        PrimeiraPessoa = 0,
        TerceiraPessoa = 1,
        AereaTatica = 2
    }

    [Header("Modo de Câmera")]
    [Tooltip("Modo de visualização da câmera ativa.")]
    public ModoCameraJogador modoCameraAtual = ModoCameraJogador.PrimeiraPessoa;

    [Header("Permissões de Gameplay")]
    [Tooltip("Se falso, desabilita a movimentação física (ex: durante o Hub Inicial ou menus).")]
    public bool controlesHabilitados = true;

    [Header("Locomoção Contínua Física")]
    [Tooltip("Velocidade linear de caminhada no labirinto em metros por segundo.")]
    public float velocidadeMovimento = 4.2f;

    [Tooltip("Velocidade angular (graus/segundo) com que a frente/boca do Pac-Man vira para a direção de movimento.")]
    public float velocidadeGiroPacMan = 1080f;

    private float m_YawVisualPacMan = 0f;

    [Tooltip("Aceleração da gravidade aplicada.")]
    public float gravidade = 9.81f;

    [Tooltip("Zona morta (Deadzone) para o analógico de locomoção.")]
    public float deadzoneMovimento = 0.12f;

    [Header("Configuração de Giro VR (Joystick Direito)")]
    [Tooltip("Se ativado, utiliza Snap Turn em headset VR.")]
    public bool usarSnapTurn = false;

    [Tooltip("Ângulo em graus para cada passo do Snap Turn (padrão: 45°).")]
    public float anguloSnapTurn = 45f;

    [Tooltip("Tempo de intervalo/cooldown entre giros Snap Turn.")]
    public float intervaloSnapTurn = 0.28f;

    [Header("Sistema de Vidas e Dano")]
    [Tooltip("Vidas restantes do jogador.")]
    public int vidasRestantes = 3;

    [Tooltip("Distância limite para detecção limpa de proximidade com Fantasmas hostis.")]
    public float distanciaDanoFantasma = 0.8f;

    [Tooltip("Ponto de Spawn seguro para onde o jogador é reposicionado ao sofrer dano.")]
    public Vector3 pontoSpawn = new Vector3(0f, 0.0f, -5.0f);

    [Tooltip("Tempo de invulnerabilidade após sofrer dano em segundos.")]
    public float tempoInvulnerabilidade = 2.0f;

    // Propriedades mantidas para retrocompatibilidade
    public bool habilitarSimuladorDesktop = true;
    public float velocidadeGiroTeclado = 110f;
    public float sensibilidadeMouse { get => sensibilidadeOlharYaw / 75f; set => sensibilidadeOlharYaw = value * 75f; }
    public float sensibilidadeAnalogico { get => sensibilidadeOlharYaw; set => sensibilidadeOlharYaw = value; }
    public float distanciaGTA { get => 0f; set { } }
    public float alturaGTA { get => alturaOlhosHumano; set => alturaOlhosHumano = value; }
    public float zoomAlvo = 0.0f;
    public float zoomAtual = 0.0f;
    public float distanciaCamera { get => 0f; set { } }
    public float alturaCamera { get => alturaOlhosHumano; set => alturaOlhosHumano = value; }

    private float m_TimerInvulnerabilidade = 0f;
    private float m_TimerSnapTurn = 0f;
    private float m_TimerHapticProximidade = 0f;
    private float m_CameraYaw = 90f;
    private float m_CameraPitch = 0f;
    public float CameraYawAtual => m_CameraYaw;
    public float CameraPitchAtual => m_CameraPitch;

    public void DefinirOrientacaoOlhar(float yaw, float pitch)
    {
        m_CameraYaw = yaw;
        m_CameraPitch = Mathf.Clamp(pitch, pitchMinimo, pitchMaximo);
    }

    private void Awake()
    {
        GameObject visualInit = GameObject.Find("Visual_PacMan");
        if (visualInit != null) m_VisualPacManTransform = visualInit.transform;

        GarantirInicializacaoControlador();
        LocalizarComponentesHierarquia();
        GarantirColisoresFisicosXR();
    }

    private void OnEnable()
    {
        GarantirInicializacaoControlador();
    }

    private void Start()
    {
        GarantirInicializacaoControlador();
        LocalizarComponentesHierarquia();
        GarantirColisoresFisicosXR();

        // 1. Posicionamento inicial do jogador no corredor aberto do labirinto
        pontoSpawn = new Vector3(0f, 0.0f, -5.0f);
        transform.position = pontoSpawn;
        m_CameraYaw = 0f;
        m_CameraPitch = 0f;

        // 2. Localização e configuração do avatar visual
        GameObject visualPac = GameObject.Find("Visual_PacMan");
        if (visualPac != null)
        {
            m_VisualPacManTransform = visualPac.transform;
            visualPac.SetActive(true);
            visualPac.transform.localPosition = new Vector3(0f, 0.425f, 0f);
            visualPac.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);

            var avatarFisico = visualPac.GetComponent<AvatarPacManFisico>();
            if (avatarFisico != null)
            {
                if (Application.isPlaying) Destroy(avatarFisico);
                else DestroyImmediate(avatarFisico);
            }
        }

        // 3. Aplica câmera e visibilidade conforme o modo ativo
        AtualizarPosicaoCamera();
    }

    public void AlinharBaseAoChao()
    {
        transform.position = new Vector3(transform.position.x, 0.0f, transform.position.z);
    }

    private void Update()
    {
        if (m_TimerSnapTurn > 0f)
        {
            m_TimerSnapTurn -= Time.deltaTime;
        }

        if (m_TimerInvulnerabilidade > 0f)
        {
            m_TimerInvulnerabilidade -= Time.deltaTime;

            // Piscar visual (flicker) do avatar do Pac-Man durante invulnerabilidade
            bool piscarVisivel = (Mathf.FloorToInt(m_TimerInvulnerabilidade * 8f) % 2) == 0;
            DefinirVisibilidadeVisualPacMan(piscarVisivel);

            if (m_TimerInvulnerabilidade <= 0f)
            {
                DefinirVisibilidadeVisualPacMan(modoCameraAtual != ModoCameraJogador.PrimeiraPessoa);
            }
        }

        ProcessarTravaCursor();

        // 1. Lê entradas consolidadas Zero-GC (Meta Quest Touch Plus, Xbox Gamepad, Teclado/Mouse)
        DadosEntradaJogador entrada = m_GerenciadorEntrada.ObterEntradaConsolidada(deadzoneMovimento);

        // 2. Intercepta Game Over ou Vitória abertos: Qualquer tecla de confirmação/menu sai para o Menu Principal
        if (HubInicialAmbiente.Instancia != null && (HubInicialAmbiente.Instancia.GameOverAberto || HubInicialAmbiente.Instancia.VitoriaAberta))
        {
            if (entrada.AlternarMenuHub || entrada.Interagir ||
                Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) ||
                Input.GetKeyDown(KeyCode.JoystickButton6))
            {
                HubInicialAmbiente.Instancia.AcaoIrParaMenuPrincipal();
                return;
            }
        }

        // 2.1 Atalho do Botão SELECT (JoystickButton6) ou Botão MENU do Quest durante gameplay -> Volta ao Menu
        if (entrada.AlternarMenuHub || Input.GetKeyDown(KeyCode.JoystickButton6) || Input.GetKeyDown(KeyCode.Escape))
        {
            if (HubInicialAmbiente.Instancia != null)
            {
                HubInicialAmbiente.Instancia.AcaoIrParaMenuPrincipal();
                return;
            }
        }

        // 3. Processamento contextual (Hub Aberto vs Gameplay Ativo)
        bool hubAberto = HubInicialAmbiente.Instancia != null && (HubInicialAmbiente.Instancia.hubAberto || !HubInicialAmbiente.Instancia.partidaEmAndamento);

        if (hubAberto)
        {
            m_VetorMovimentoDesejado = Vector3.zero;
            return;
        }
        else
        {
            // --- AÇÕES EM GAMEPLAY ---
            // *** MUDAR CÂMERA: BOTÃO B (Xbox) OU BOLA (PlayStation) OU B (Meta Quest) OU TECLA C ***
            if (entrada.AlternarModoCamera)
            {
                AlternarModoCamera();
            }
            // Reset de Checkpoint: Botão X (Xbox/Quest) ou Tecla X
            if (entrada.ResetCheckpoint)
            {
                ResetarParaCheckpoint();
            }
            // Interação ontológica / Háptica: Botão A (Xbox/Quest) ou Espaço
            if (entrada.Interagir)
            {
                DispararHapticsDualController(0.4f, 0.1f);
            }

            // Processa locomoção e rotação física da visão
            ProcessarEntradasMovimento(entrada);
            RotacionarCorpoJogador();
            VerificarProximidadeFantasmas();
            VerificarConsumoGemasProximas();
        }
    }

    private void LateUpdate()
    {
        AtualizarPosicaoCamera();
    }

    private void FixedUpdate()
    {
        ProcessarMovimentoFisico();
    }

    /// <summary>
    /// Localiza câmeras, offsets e controladores na hierarquia do XR Origin.
    /// </summary>
    private void LocalizarComponentesHierarquia()
    {
        if (cameraOffset == null)
        {
            Transform offset = transform.Find("Camera Offset") ?? transform.Find("CameraOffset");
            if (offset != null) cameraOffset = offset;
        }

        if (cameraPrincipalXR == null)
        {
            cameraPrincipalXR = GetComponentInChildren<Camera>() ?? Camera.main;
        }

        if (cameraOffset != null)
        {
            cameraOffset.localPosition = Vector3.zero;
            cameraOffset.localRotation = Quaternion.identity;
        }

        if (controladorEsquerdo == null)
        {
            Transform esq = transform.Find("Camera Offset/Left Hand Controller") ?? transform.Find("Left Hand Controller") ?? transform.Find("LeftController");
            controladorEsquerdo = esq;
        }

        if (controladorDireito == null)
        {
            Transform dir = transform.Find("Camera Offset/Right Hand Controller") ?? transform.Find("Right Hand Controller") ?? transform.Find("RightController");
            controladorDireito = dir;
        }
    }

    /// <summary>
    /// Configura o Rigidbody contínuo dinâmico e o colisor de cápsula com proporções humanas
    /// (altura 1.75m, raio 0.38m) para presença física realista no labirinto.
    /// </summary>
    private void GarantirInicializacaoControlador()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>() ?? GetComponentInChildren<CharacterController>();
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // Rigidbody com travas rigorosas no eixo Y e em todas as rotações físicas
        m_Rigidbody = GetComponent<Rigidbody>();
        if (m_Rigidbody == null)
        {
            m_Rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        m_Rigidbody.isKinematic = false;
        m_Rigidbody.useGravity = false;
        m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        m_Rigidbody.constraints = RigidbodyConstraints.FreezePositionY |
                                  RigidbodyConstraints.FreezeRotationX |
                                  RigidbodyConstraints.FreezeRotationY |
                                  RigidbodyConstraints.FreezeRotationZ;

        // Remove colisor esférico antigo caso exista
        SphereCollider oldSc = GetComponent<SphereCollider>();
        if (oldSc != null)
        {
            if (Application.isPlaying) Destroy(oldSc);
            else DestroyImmediate(oldSc);
        }

        // Garante Colisor de Cápsula Humana (1.75m de altura, 0.76m de largura de ombros)
        m_CapsuleCollider = GetComponent<CapsuleCollider>();
        if (m_CapsuleCollider == null)
        {
            m_CapsuleCollider = gameObject.AddComponent<CapsuleCollider>();
        }
        m_CapsuleCollider.isTrigger = false;
        m_CapsuleCollider.radius = 0.38f;
        m_CapsuleCollider.height = 1.75f;
        m_CapsuleCollider.center = new Vector3(0f, 0.88f, 0f);

        // Material físico sem atrito: permite deslizar livremente ao longo de paredes sem travar ou grudar
        PhysicsMaterial matZeroAtrito = new PhysicsMaterial("PacMan_ZeroFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        m_CapsuleCollider.sharedMaterial = matZeroAtrito;
    }

    private void GarantirColisoresFisicosXR()
    {
        if (cameraPrincipalXR != null)
        {
            SphereCollider scCam = cameraPrincipalXR.GetComponent<SphereCollider>();
            if (scCam != null)
            {
                scCam.enabled = false;
            }
        }
    }

    /// <summary>
    /// Trava o cursor do mouse na janela ao clicar, liberando com ESC.
    /// </summary>
    private void ProcessarTravaCursor()
    {
        if (travarCursorAoClicar)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    /// <summary>
    /// Processa entradas de locomoção e orientação de forma desacoplada via IProvedorEntradaXR:
    /// - Meta Quest Touch Plus (VR)
    /// - Gamepad Xbox (New Input System / Legacy)
    /// - Teclado & Mouse (Desktop FPS)
    /// Zero alocações de Heap por frame (0 B GC Alloc).
    /// </summary>
    private void ProcessarEntradasMovimento(DadosEntradaJogador entrada)
    {
        if (!controlesHabilitados)
        {
            m_VetorMovimentoDesejado = Vector3.zero;
            return;
        }

        Vector2 dir2D = entrada.Movimento2D;
        if (dir2D.sqrMagnitude > 1f) dir2D.Normalize();

        // Rotação horizontal livre da visão (Yaw) via Stick Direito, Mouse X ou Q/E
        if (Mathf.Abs(entrada.GiroCameraYaw) > 0.005f)
        {
            m_CameraYaw += entrada.GiroCameraYaw * sensibilidadeOlharYaw * Time.deltaTime;
        }

        // Inclinação vertical da visão (Pitch - olhar para cima/baixo) via Mouse Y ou Stick Direito Y
        if (Mathf.Abs(entrada.OlharCameraPitch) > 0.005f)
        {
            m_CameraPitch = Mathf.Clamp(m_CameraPitch + entrada.OlharCameraPitch * sensibilidadeOlharPitch * Time.deltaTime, pitchMinimo, pitchMaximo);
        }

        if (m_CameraYaw > 360f) m_CameraYaw -= 360f;
        else if (m_CameraYaw < 0f) m_CameraYaw += 360f;

        Vector3 camFwd;
        Vector3 camRgt;

        bool emVR = XRSettings.isDeviceActive || (XRSettings.enabled && !string.IsNullOrEmpty(XRSettings.loadedDeviceName));
        if (emVR && modoCameraAtual == ModoCameraJogador.PrimeiraPessoa && cameraPrincipalXR != null)
        {
            camFwd = cameraPrincipalXR.transform.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude > 0.001f) camFwd.Normalize();
            else camFwd = Vector3.forward;

            camRgt = cameraPrincipalXR.transform.right;
            camRgt.y = 0f;
            if (camRgt.sqrMagnitude > 0.001f) camRgt.Normalize();
            else camRgt = Vector3.right;
        }
        else
        {
            Quaternion rotYawCam = Quaternion.Euler(0f, m_CameraYaw, 0f);
            camFwd = rotYawCam * Vector3.forward;
            camRgt = rotYawCam * Vector3.right;
        }

        m_VetorMovimentoDesejado = (camFwd * dir2D.y + camRgt * dir2D.x);

        // COMBO CONTROLE DIREITO META QUEST: Gatilho Indicador (Index Trigger) Acelera para a Frente
        // O analógico direito controla a rotação/câmera e o gatilho acelera na direção apontada!
        if (entrada.AceleradorGatilho > 0.02f)
        {
            m_VetorMovimentoDesejado += camFwd * entrada.AceleradorGatilho;
        }

        if (m_VetorMovimentoDesejado.sqrMagnitude > 1f) m_VetorMovimentoDesejado.Normalize();
    }

    /// <summary>
    /// Aplica a velocidade de movimento diretamente ao Rigidbody nos eixos X e Z.
    /// Com o PhysicMaterial sem atrito no CapsuleCollider, o jogador desliza naturalmente
    /// em qualquer parede sem travar ou grudar nas esquinas.
    /// </summary>
    private void ProcessarMovimentoFisico()
    {
        if (m_Rigidbody == null) m_Rigidbody = GetComponent<Rigidbody>();
        if (m_Rigidbody == null) return;

        m_Rigidbody.constraints = RigidbodyConstraints.FreezePositionY |
                                  RigidbodyConstraints.FreezeRotationX |
                                  RigidbodyConstraints.FreezeRotationY |
                                  RigidbodyConstraints.FreezeRotationZ;

        if (!controlesHabilitados)
        {
            m_Rigidbody.linearVelocity = Vector3.zero;
#pragma warning disable CS0618
            m_Rigidbody.velocity = Vector3.zero;
#pragma warning restore CS0618
            return;
        }

        Vector3 vel = m_VetorMovimentoDesejado * velocidadeMovimento;

        m_Rigidbody.linearVelocity = new Vector3(vel.x, 0f, vel.z);
#pragma warning disable CS0618
        m_Rigidbody.velocity = new Vector3(vel.x, 0f, vel.z);
#pragma warning restore CS0618

        // Trava absoluta de segurança: y = 0.0f rente ao chão
        Vector3 posAtual = transform.position;
        if (Mathf.Abs(posAtual.y - 0.0f) > 0.001f)
        {
            transform.position = new Vector3(posAtual.x, 0.0f, posAtual.z);
        }
    }

    /// <summary>
    /// Controla a orientação do corpo e do avatar visual do Pac-Man:
    /// - Em Primeira Pessoa: o rig XR Origin gira junto com a visão horizontal da câmera.
    /// - Em Terceira Pessoa / Visão Aérea: a frente (boca) do Pac-Man vira suave e imediatamente
    ///   para a direção do vetor de movimento, garantindo que ele sempre ande de frente (mouth-first).
    ///   Ao girar a câmera em 360°, o jogador inspeciona o personagem e os caminhos livremente.
    /// </summary>
    private void RotacionarCorpoJogador()
    {
        if (!controlesHabilitados) return;

        if (modoCameraAtual == ModoCameraJogador.PrimeiraPessoa)
        {
            transform.rotation = Quaternion.Euler(0f, m_CameraYaw, 0f);
        }
        else
        {
            transform.rotation = Quaternion.identity;

            if (m_VisualPacManTransform == null)
            {
                GameObject v = GameObject.Find("Visual_PacMan");
                if (v != null) m_VisualPacManTransform = v.transform;
            }

            Vector3 dirMov = m_VetorMovimentoDesejado;
            dirMov.y = 0f;

            if (dirMov.sqrMagnitude > 0.001f)
            {
                m_YawVisualPacMan = Mathf.Atan2(dirMov.x, dirMov.z) * Mathf.Rad2Deg;
            }
            else
            {
                // Se parado, mantém a orientação sincronizada com a direção da câmera para disparar com o gatilho
                m_YawVisualPacMan = m_CameraYaw;
            }

            if (m_VisualPacManTransform != null)
            {
                Quaternion rotAlvo = Quaternion.Euler(0f, m_YawVisualPacMan, 0f);
                m_VisualPacManTransform.rotation = Quaternion.RotateTowards(
                    m_VisualPacManTransform.rotation,
                    rotAlvo,
                    velocidadeGiroPacMan * Time.deltaTime
                );
            }
        }
    }

    /// <summary>
    /// Gerenciador de Câmera Unificado com suporte a:
    /// 1. Primeira Pessoa 1:1 (FPS / VR 360° com olhos a 1.68m)
    /// 2. Terceira Pessoa Dinâmica (Atrás do Pac-Man)
    /// 3. Visão Tática Aérea (Panorâmica do Labirinto)
    /// </summary>
    public void AtualizarPosicaoCamera()
    {
        if (cameraPrincipalXR == null)
        {
            cameraPrincipalXR = GetComponentInChildren<Camera>() ?? Camera.main;
        }
        if (cameraPrincipalXR == null) return;

        bool emVR = XRSettings.isDeviceActive || (XRSettings.enabled && !string.IsNullOrEmpty(XRSettings.loadedDeviceName)) ||
                    (UnityEngine.XR.Management.XRGeneralSettings.Instance != null && UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager != null && UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager.activeLoader != null);

        switch (modoCameraAtual)
        {
            case ModoCameraJogador.PrimeiraPessoa:
                if (cameraPrincipalXR.nearClipPlane > 0.02f) cameraPrincipalXR.nearClipPlane = 0.015f;
                cameraPrincipalXR.fieldOfView = campoDeVisaoFPS;

                if (emVR && cameraOffset != null)
                {
                    // Ancoragem nos olhos do Pac-Man (0.50m do chão) com rastreamento 6DoF
                    cameraOffset.position = transform.position + new Vector3(0f, 0.45f, 0f);
                    cameraOffset.rotation = Quaternion.Euler(0f, m_CameraYaw, 0f);
                }
                else
                {
                    if (cameraOffset != null)
                    {
                        cameraOffset.localPosition = Vector3.zero;
                        cameraOffset.localRotation = Quaternion.identity;
                    }
                    cameraPrincipalXR.transform.position = transform.position + Vector3.up * 0.55f;
                    cameraPrincipalXR.transform.rotation = Quaternion.Euler(m_CameraPitch, m_CameraYaw, 0f);
                }
                DefinirVisibilidadeVisualPacMan(false);
                break;

            case ModoCameraJogador.TerceiraPessoa:
                cameraPrincipalXR.nearClipPlane = 0.03f;
                cameraPrincipalXR.fieldOfView = 68f;

                // 3ª Pessoa Próxima (Logo atrás do Pac-Man sem vazar paredes em corredores estreitos)
                Quaternion rotTerceira = Quaternion.Euler(0f, m_CameraYaw, 0f);
                Vector3 offsetAtras = rotTerceira * new Vector3(0f, 0.85f, -1.35f);

                if (emVR && cameraOffset != null)
                {
                    cameraOffset.position = transform.position + offsetAtras;
                    cameraOffset.rotation = Quaternion.Euler(0f, m_CameraYaw, 0f);
                }
                else
                {
                    if (cameraOffset != null)
                    {
                        cameraOffset.localPosition = Vector3.zero;
                        cameraOffset.localRotation = Quaternion.identity;
                    }
                    cameraPrincipalXR.transform.position = transform.position + offsetAtras;
                    cameraPrincipalXR.transform.rotation = Quaternion.Euler(m_CameraPitch + 10f, m_CameraYaw, 0f);
                }

                DefinirVisibilidadeVisualPacMan(true);
                break;

            case ModoCameraJogador.AereaTatica:
                cameraPrincipalXR.nearClipPlane = 0.1f;
                cameraPrincipalXR.fieldOfView = 65f;

                // Câmera aérea orbital 360° em torno do Pac-Man com ângulo tático descendente de 62°
                Quaternion rotAerea = Quaternion.Euler(0f, m_CameraYaw, 0f);
                Vector3 offsetAerea = rotAerea * new Vector3(0f, 10.0f, -5.5f);

                if (emVR && cameraOffset != null)
                {
                    cameraOffset.position = transform.position + offsetAerea;
                    cameraOffset.rotation = Quaternion.Euler(62f, m_CameraYaw, 0f);
                }
                else
                {
                    if (cameraOffset != null)
                    {
                        cameraOffset.localPosition = Vector3.zero;
                        cameraOffset.localRotation = Quaternion.identity;
                    }
                    cameraPrincipalXR.transform.position = transform.position + offsetAerea;
                    cameraPrincipalXR.transform.rotation = Quaternion.Euler(62f, m_CameraYaw, 0f);
                }

                DefinirVisibilidadeVisualPacMan(true);
                break;
        }
    }

    public void DefinirVisibilidadeVisualPacMan(bool visivel)
    {
        if (m_VisualPacManTransform == null)
        {
            GameObject v = GameObject.Find("Visual_PacMan");
            if (v != null) m_VisualPacManTransform = v.transform;
        }

        if (m_VisualPacManTransform != null)
        {
            MeshRenderer[] renderers = m_VisualPacManTransform.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = visivel;
            }
        }
    }

    public void DefinirModoCamera(ModoCameraJogador novoModo)
    {
        modoCameraAtual = novoModo;
        AtualizarPosicaoCamera();
        Debug.Log($"<color=#00E5FF><b>[Câmera]</b></color> Modo alterado para: {modoCameraAtual}");
    }

    public void AlternarModoCamera()
    {
        int proximo = ((int)modoCameraAtual + 1) % 3;
        DefinirModoCamera((ModoCameraJogador)proximo);
    }

    public void ResetarParaCheckpoint()
    {
        transform.position = pontoSpawn;
        m_CameraYaw = 0f;
        m_CameraPitch = 0f;
        m_YawVisualPacMan = 0f;
        if (m_VisualPacManTransform != null) m_VisualPacManTransform.rotation = Quaternion.identity;
        m_VetorMovimentoDesejado = Vector3.zero;

        if (m_Rigidbody != null)
        {
            m_Rigidbody.linearVelocity = Vector3.zero;
#pragma warning disable CS0618
            m_Rigidbody.velocity = Vector3.zero;
#pragma warning restore CS0618
            m_Rigidbody.angularVelocity = Vector3.zero;
        }

        AtualizarPosicaoCamera();
        Debug.Log($"<color=#00FF99><b>[Checkpoint]</b></color> Jogador restaurado no ponto seguro: {pontoSpawn}");
    }

    public void AtualizarPosicaoCameraPrimeiraPessoa()
    {
        AtualizarPosicaoCamera();
    }

    public void AtualizarPosicaoCameraGTA()
    {
        AtualizarPosicaoCamera();
    }

    public void ProcessarCameraGTA()
    {
        // Compatibilidade de API
    }

    /// <summary>
    /// Verificação Zero-GC de proximidade com fantasmas utilizando OverlapSphereNonAlloc.
    /// Evita qualquer varredura da hierarquia da cena via FindObjectsByType em tempo de execução.
    /// </summary>
    private void VerificarProximidadeFantasmas()
    {
        if (vidasRestantes <= 0 || m_TimerInvulnerabilidade > 0f) return;

        Vector3 posCentroCorpo = transform.position + Vector3.up * 0.88f;

        // Feedback Háptico de Proximidade (Sprint 4: Batimento cardíaco tátil suave quando um fantasma hostil está nos corredores a < 3.2m)
        m_TimerHapticProximidade += Time.deltaTime;
        if (m_TimerHapticProximidade >= 0.85f)
        {
            m_TimerHapticProximidade = 0f;
            int numAlerta = Physics.OverlapSphereNonAlloc(posCentroCorpo, 3.2f, m_BufferColisoresFantasmas, ~0, QueryTriggerInteraction.Collide);
            for (int a = 0; a < numAlerta; a++)
            {
                Collider cAlerta = m_BufferColisoresFantasmas[a];
                if (cAlerta != null && !cAlerta.transform.IsChildOf(transform))
                {
                    ObstaculoInvalidoSemantico obsAlerta = cAlerta.GetComponent<ObstaculoInvalidoSemantico>() ?? cAlerta.GetComponentInParent<ObstaculoInvalidoSemantico>();
                    if (obsAlerta != null && !obsAlerta.EhVulneravel)
                    {
                        DispararHapticsDualController(0.18f, 0.06f);
                        break;
                    }
                }
            }
        }

        int numHits = Physics.OverlapSphereNonAlloc(posCentroCorpo, 1.05f, m_BufferColisoresFantasmas, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < numHits; i++)
        {
            Collider col = m_BufferColisoresFantasmas[i];
            if (col == null || col.gameObject == gameObject || col.transform.IsChildOf(transform)) continue;

            IInstanciaSemantica inst = col.GetComponent<IInstanciaSemantica>() ?? col.GetComponentInParent<IInstanciaSemantica>();
            if (inst != null && !inst.EhValida)
            {
                ProcessarColisaoEntidade(col);
                return;
            }

            if (col.CompareTag("ObstaculoInvalido") || col.gameObject.name.Contains("Fantasma") || col.gameObject.name.Contains("Obstaculo") || col.gameObject.name.Contains("Blinky"))
            {
                ProcessarColisaoEntidade(col);
                return;
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null) return;
        ProcessarColisaoEntidade(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        ProcessarColisaoEntidade(other);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit == null || hit.collider == null) return;
        ProcessarColisaoEntidade(hit.collider);
    }

    /// <summary>
    /// Processa a colisão tanto com gemas amarelas (consumo imediato) quanto com fantasmas.
    /// </summary>
    public void ProcessarColisaoEntidade(Collider col)
    {
        if (col == null) return;

        IInstanciaSemantica inst = col.GetComponent<IInstanciaSemantica>() ?? col.GetComponentInParent<IInstanciaSemantica>();

        bool ehTagGema = false;
        bool ehTagObstaculo = false;
        try { ehTagGema = col.CompareTag("GemaValida"); } catch { }
        try { ehTagObstaculo = col.CompareTag("ObstaculoInvalido"); } catch { }

        // 1. Gema amarela / Válida: consome e registra na ontologia
        if (ehTagGema || col.gameObject.name.Contains("Gema") || col.gameObject.name.Contains("PacDot") || (inst != null && inst.EhValida))
        {
            TratarConsumoGema(col, inst);
        }
        // 2. Obstáculo / Fantasma: dano limpo sem empurrão físico
        else if (ehTagObstaculo || col.gameObject.name.Contains("Obstaculo") || col.gameObject.name.Contains("Fantasma") || col.gameObject.name.Contains("Blinky") || (inst != null && !inst.EhValida))
        {
            TratarEncontroFantasma(col, inst);
        }
    }

    /// <summary>
    /// Consome a gema, registra a instância e classe exata na ontologia (atualizando HUDs),
    /// soma pontos e desativa/destrói a gema imediatamente para remover qualquer empurrão físico.
    /// </summary>
    private void TratarConsumoGema(Collider col, IInstanciaSemantica inst)
    {
        GameObject gemaObj = (inst is Component comp && comp != null) ? comp.gameObject : (col != null ? col.gameObject : null);
        if (gemaObj == null) return;

        // Exige contato físico real (distância horizontal <= 0.65m)
        Vector2 posGema2D = new Vector2(gemaObj.transform.position.x, gemaObj.transform.position.z);
        Vector2 posPac2D = new Vector2(transform.position.x, transform.position.z);
        if (Vector2.Distance(posGema2D, posPac2D) > 0.65f) return;

        Collider[] cols = gemaObj.GetComponentsInChildren<Collider>();
        if (cols != null)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].enabled = false;
            }
        }

        bool ehPowerPellet = inst is PowerPelletIndividualSemantico ||
                             gemaObj.GetComponent<PowerPelletIndividualSemantico>() != null ||
                             gemaObj.name.ToLower().Contains("power");
        if (ehPowerPellet)
        {
            var motor = FindFirstObjectByType<MotorRaciocinioSemantico>();
            if (motor != null)
            {
                motor.AtivarPowerPellet(8.0f);
            }
        }

        if (inst != null)
        {
            inst.Interagir(gameObject, false);

            if (RelationBuilder.Instance != null)
            {
                RelationBuilder.Instance.AdicionarItemComidoNaRedeNeural(inst, gemaObj);
            }
        }

        // Feedback Háptico Refinado (Sprint 4): Pulso potente se for Power Pellet, pulso ágil suave se for orbe normal
        DispararHapticsDualController(ehPowerPellet ? 0.75f : 0.30f, ehPowerPellet ? 0.25f : 0.08f);

        if (GerenciadorEfeitosJuice.Instancia != null)
        {
            Color corGema = inst != null ? inst.CorDestaque : new Color(0.1f, 1.0f, 0.55f);
            GerenciadorEfeitosJuice.Instancia.CriarExplosaoBrilhoColeta(gemaObj.transform.position, corGema);
            GerenciadorEfeitosJuice.Instancia.TocarSomColeta(ehPowerPellet, gemaObj.transform.position);
        }
    }

    /// <summary>
    /// Processa o encontro com o fantasma conforme regras SWRL.
    /// Em modo Power Pellet (Vulnerable), o Pac-Man devora o fantasma, multiplicando pontos e enviando os olhos à gaiola.
    /// Em modo normal ou agressivo, o Pac-Man sofre dano e perde 1 vida com respawn seguro.
    /// </summary>
    private void TratarEncontroFantasma(Collider col, IInstanciaSemantica inst)
    {
        var motor = FindFirstObjectByType<MotorRaciocinioSemantico>();
        string idFantasma = inst != null && !string.IsNullOrEmpty(inst.Id) ? inst.Id : "ghost_blinky";
        bool pacmanPowered = motor != null && motor.pacmanIsPowered;
        GameEntityState estado = motor != null ? motor.ObterEstadoEntidade(idFantasma) : GameEntityState.Patrol;

        // Dispara o efeito de explosão de brilho/faíscas ao encostar no fantasma
        Vector3 posContato = col != null ? col.transform.position : transform.position;
        if (GerenciadorEfeitosJuice.Instancia != null)
        {
            GerenciadorEfeitosJuice.Instancia.CriarEfeitoBrilhoContatoFantasma(posContato);
        }

        if (pacmanPowered && estado == GameEntityState.Vulnerable)
        {
            // Feedback Háptico Crocante ao devorar fantasma
            DispararHapticsDualController(0.85f, 0.30f);

            var obs = col != null ? (col.GetComponent<ObstaculoInvalidoSemantico>() ?? col.GetComponentInParent<ObstaculoInvalidoSemantico>()) : null;
            if (obs != null)
            {
                obs.DevorarFantasmaPorPowerPellet();
            }
            else
            {
                int pts = motor != null ? motor.RegistrarFantasmaDevorado() : 200;
                InstanciaSemanticaBase.pontuacaoLogica += pts;
                InstanciaSemanticaBase.RegistrarConsumoOntologico(idFantasma, "VulnerableGhost");
                if (col != null)
                {
                    AnimacaoFantasma anim = col.GetComponent<AnimacaoFantasma>() ?? col.GetComponentInParent<AnimacaoFantasma>();
                    if (anim != null)
                    {
                        anim.ExecutarDerrotaERespawn(col.transform.position);
                    }
                }
            }
        }
        else
        {
            ExecutarDanoPacMan(col != null ? col.gameObject : null);
        }
    }

    /// <summary>
    /// Executa o dano direto ao jogador no XR Origin, emitindo impulso háptico potente
    /// e reposicionando com segurança absoluta, sem empurrar o jogador fisicamente pelo mapa.
    /// </summary>
    public void ExecutarDanoPacMan(GameObject fantasma)
    {
        var motor = FindFirstObjectByType<MotorRaciocinioSemantico>();
        if (motor != null && motor.pacmanIsPowered)
        {
            // Se Pac-Man estiver com Power Pellet ativo, devora o fantasma ao invés de receber dano
            if (fantasma != null)
            {
                var obs = fantasma.GetComponent<ObstaculoInvalidoSemantico>() ?? fantasma.GetComponentInParent<ObstaculoInvalidoSemantico>();
                if (obs != null)
                {
                    obs.DevorarFantasmaPorPowerPellet();
                    return;
                }
            }
            return;
        }

        if (vidasRestantes <= 0 || m_TimerInvulnerabilidade > 0f) return;

        m_TimerInvulnerabilidade = tempoInvulnerabilidade;

        if (fantasma != null)
        {
            Vector3 dirAfastamento = (fantasma.transform.position - transform.position).normalized;
            dirAfastamento.y = 0f;
            if (dirAfastamento.sqrMagnitude < 0.01f) dirAfastamento = transform.forward;
            
            // Afasta o fantasma sem empurrão físico bruto
            var navFantasma = fantasma.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Vector3 posRecuo = fantasma.transform.position + dirAfastamento * 6.0f;
            if (navFantasma != null && navFantasma.isOnNavMesh)
            {
                navFantasma.isStopped = true;
                navFantasma.velocity = Vector3.zero;
                if (UnityEngine.AI.NavMesh.SamplePosition(posRecuo, out var hit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    navFantasma.Warp(hit.position);
                }
            }
            else
            {
                fantasma.transform.position = posRecuo;
            }
        }
        vidasRestantes = Mathf.Max(0, vidasRestantes - 1);

        Debug.LogWarning($"<color=#FF0044><b>[DANO 1ª PESSOA]</b></color> Encontro com {fantasma?.name ?? "Fantasma Hostil"}! " +
                         $"Vidas restantes: <b>{vidasRestantes}</b>.");

        if (GerenciadorEfeitosJuice.Instancia != null)
        {
            GerenciadorEfeitosJuice.Instancia.CriarEfeitoStunImpacto(transform.position);
        }

        DispararHapticsDualController(0.85f, 0.45f);

        string nomeFantasma = fantasma != null ? fantasma.name : "Fantasma Caçador";
        if (RelationBuilder.Instance != null)
        {
            RelationBuilder.Instance.QuebrarUltimoPedacoDaRedePorFantasma(nomeFantasma);
        }

        var demoWeb = FindFirstObjectByType<PacMan.Web.ControladorDemoWeb>();
        if (demoWeb != null)
        {
            demoWeb.vidasRestantes = vidasRestantes;
            demoWeb.ExibirAvisoColisao($"💥 FANTASMA QUEBROU SUA LINHA! Vidas restantes: {vidasRestantes}");
        }

        var hudVR = FindFirstObjectByType<HUDHolograficoXR>();
        if (hudVR != null)
        {
            hudVR.AtualizarTextosHUD();
        }

        if (vidasRestantes > 0)
        {
            // Reposicionamento seguro no spawn com salvaguarda física
            ReposicionarNoSpawn();
        }
        else
        {
            // GAME OVER COMPLETO: Imobilização total e definitiva!
            PararMovimentoTotal();
            if (HubInicialAmbiente.Instancia != null)
            {
                HubInicialAmbiente.Instancia.ExibirGameOver(InstanciaSemanticaBase.pontuacaoLogica, InstanciaSemanticaBase.totalColetadas);
            }
            if (demoWeb != null)
            {
                demoWeb.FinalizarJogoGameOver();
            }
        }
    }

    /// <summary>
    /// Paralisa imediatamente o Pac-Man (Game Over ou Vitória):
    /// Zera velocidade física, cancela acelerações residuais e cessa animações de locomoção e mastigação.
    /// </summary>
    public void PararMovimentoTotal()
    {
        controlesHabilitados = false;
        m_VetorMovimentoDesejado = Vector3.zero;
        m_VelocidadeLinearAtual = Vector3.zero;

        if (m_Rigidbody != null)
        {
            m_Rigidbody.linearVelocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
#pragma warning disable CS0618
            m_Rigidbody.velocity = Vector3.zero;
#pragma warning restore CS0618
        }

        var animBoca = GetComponentInChildren<AnimacaoBocaPacMan>();
        if (animBoca != null) animBoca.enabled = false;

        var animacoes = GetComponentsInChildren<Animation>();
        for (int i = 0; i < animacoes.Length; i++)
        {
            if (animacoes[i] != null) animacoes[i].Stop();
        }

        var animadores = GetComponentsInChildren<Animator>();
        for (int i = 0; i < animadores.Length; i++)
        {
            if (animadores[i] != null) animadores[i].speed = 0f;
        }
    }

    /// <summary>
    /// Emite vibração háptica nos dois comandos de RV e nos motores de Gamepad simultaneamente.
    /// Zero-GC e compatível com OpenXR e XInput.
    /// </summary>
    public void DispararHapticsDualController(float amplitude, float duracao)
    {
        m_GerenciadorEntrada.DispararHaptics(amplitude, duracao);
    }

    /// <summary>
    /// Reposiciona o jogador de volta ao ponto inicial com integridade física garantida.
    /// </summary>
    public void ReposicionarNoSpawn()
    {
        transform.position = pontoSpawn;

        if (m_Rigidbody != null)
        {
            m_Rigidbody.linearVelocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
        }

        m_CameraYaw = 90f;
        m_CameraPitch = 0f;
        AtualizarPosicaoCamera();
    }

    /// <summary>
    /// Varredura contínua horizontal em 360° para coleta de gemas próximas ao corpo utilizando OverlapSphereNonAlloc.
    /// Gera rigorosamente ZERO bytes de alocação de memória no managed heap por frame.
    /// </summary>
    private void VerificarConsumoGemasProximas()
    {
        if (!controlesHabilitados) return;

        Vector3 centroCorpo = transform.position + Vector3.up * 0.45f;
        int numHits = Physics.OverlapSphereNonAlloc(centroCorpo, 0.45f, m_BufferColisoresGemas, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < numHits; i++)
        {
            Collider col = m_BufferColisoresGemas[i];
            if (col == null || col.gameObject == gameObject || col.transform.IsChildOf(transform)) continue;

            IInstanciaSemantica inst = col.GetComponent<IInstanciaSemantica>() ?? col.GetComponentInParent<IInstanciaSemantica>();
            if (inst != null && inst.EhValida)
            {
                TratarConsumoGema(col, inst);
                return;
            }
        }
    }
}

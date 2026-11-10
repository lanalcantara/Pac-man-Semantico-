using UnityEngine;
using PacMan.Semantics;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Modelagem Orientada a Objetos (UML/C++)
/// ============================================================================
/// Classe concreta que representa um Obstáculo Inválido (Violação Ontológica / Fantasma).
/// Representa indivíduos ou axiomas que violam restrições de Description Logic.
/// Penaliza a pontuação do Pac-Man e navega de forma autônoma pelo NavMesh.
///
/// Ciclo de Vida:
/// 1. Nascimento/Materialização com animação procedural na Gaiola Central (escala 0 -> 1).
/// 2. Liberação escalonada/sequencial (Blinky 0s, Pinky 4s, Inky 8s, Clyde 12s) sem aglomeração.
/// 3. Navegação autônoma por FSM (Patrol consistente vs Chase inconsistente).
/// 4. Contato limpo: dano sem empurrão físico bruto ou consumo pelo Pac-Man sob efeito de Power Pellet.
/// ============================================================================
/// </summary>
public class ObstaculoInvalidoSemantico : InstanciaSemanticaBase
{
    public enum EstadoCicloVidaFantasma
    {
        Nascendo,          // Materializando proceduralmente dentro da gaiola (escala e brilho)
        AguardandoGaiola,  // Flutuando no seu slot designado aguardando o tempo de liberação
        SaindoGaiola,      // Movendo suavemente pelo portal de saída da gaiola central para o corredor
        NavegandoAtivo,    // No labirinto navegando com NavMeshAgent (Patrol ou Chase)
        RetornandoGaiola   // Consumido em modo Power Pellet, voltando para renascer
    }

    [Header("Configurações do Obstáculo")]
    [Tooltip("Tempo de intervalo antes de disparar nova penalidade caso o jogador permaneça em contato.")]
    public float tempoCooldownPenalidade = 1.0f;

    [Header("Inteligência Artificial (NavMesh)")]
    [Tooltip("Habilita a perseguição dinâmica ao Pac-Man via malha NavMesh.")]
    public bool habilitarIA = true;

    [Header("Estado do Reasoner Agent (FSM NavMesh)")]
    [Tooltip("Quando o Reasoner está CONSISTENTE (Patrol), o agente vaga proceduralmente e ignora o jogador. Quando INCONSISTENTE (Chase), persegue o jogador.")]
    public bool emModoChaseInconsistente = false;

    [Header("Ciclo de Vida na Gaiola Central")]
    [Tooltip("Estado atual no ciclo de vida do fantasma.")]
    public EstadoCicloVidaFantasma estadoCicloVida = EstadoCicloVidaFantasma.Nascendo;

    [Tooltip("Tempo restante de espera na gaiola central antes da saída escalonada.")]
    public float tempoAtrasoSaidaGaiola = 0f;

    [Tooltip("Coordenada designada de nascimento dentro da gaiola central.")]
    public Vector3 posicaoSlotGaiola = Vector3.zero;

    [Tooltip("Ponto de saída / portal da gaiola central para o corredor.")]
    public Vector3 posicaoSaidaGaiola = new Vector3(0f, 0f, 1.8f);

    private float m_TimerCooldown = 0f;
    private UnityEngine.AI.NavMeshAgent m_NavAgent;
    private Transform m_AlvoPacMan;
    private float m_TimerAtualizacaoIA = 0f;
    private Vector3 m_AlvoPatrulhaProcedural = Vector3.zero;
    private AnimacaoFantasma m_AnimFantasma;

    // Áudio Espacial 3D (Sprint 4)
    private AudioSource m_AudioSourceEspacial;
    private static AudioClip s_ClipZumbidoFantasma;

    private static void GarantirClipZumbido()
    {
        if (s_ClipZumbidoFantasma != null) return;
        int sampleRate = 44100;
        float duracao = 1.0f;
        int totalSamples = (int)(sampleRate * duracao);
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float f1 = 110f; // Tom espectral grave
            float f2 = 220f; // Harmônico suave
            float mod = 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 3f * t); // Modulação 3Hz
            float wave1 = Mathf.Sin(2f * Mathf.PI * f1 * t);
            float wave2 = Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.35f;
            samples[i] = (wave1 + wave2) * mod * 0.25f;
        }

        s_ClipZumbidoFantasma = AudioClip.Create("Ghost_Spectral_Drone_Loop", totalSamples, 1, sampleRate, false);
        s_ClipZumbidoFantasma.SetData(samples, 0);
    }

    protected override void Awake()
    {
        base.Awake();
        m_EhValida = false;
        m_ValorPontuacao = -5;
        if (m_CorDestaque == Color.green || m_CorDestaque == Color.clear)
        {
            m_CorDestaque = new Color(1f, 0.15f, 0.2f);
        }

        // Garante que TODOS os colisores sejam Triggers (isTrigger = true) para eliminar empurrão físico bruto
        Collider[] cols = GetComponentsInChildren<Collider>();
        if (cols != null)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].isTrigger = true;
            }
        }

        // Rigidbody cinemático sem forças físicas passivas
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
        }

        m_NavAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        m_AnimFantasma = GetComponent<AnimacaoFantasma>() ?? gameObject.AddComponent<AnimacaoFantasma>();

        // Áudio Espacial 3D (Sprint 4)
        GarantirClipZumbido();
        m_AudioSourceEspacial = gameObject.AddComponent<AudioSource>();
        m_AudioSourceEspacial.spatialBlend = 1.0f;
        m_AudioSourceEspacial.rolloffMode = AudioRolloffMode.Logarithmic;
        m_AudioSourceEspacial.minDistance = 1.5f;
        m_AudioSourceEspacial.maxDistance = 12.0f;
        m_AudioSourceEspacial.loop = true;
        m_AudioSourceEspacial.volume = 0.28f;
        m_AudioSourceEspacial.clip = s_ClipZumbidoFantasma;
        m_AudioSourceEspacial.playOnAwake = false;
        if (gameObject.activeInHierarchy) m_AudioSourceEspacial.Play();
    }

    protected override void Start()
    {
        base.Start();
        if (posicaoSlotGaiola == Vector3.zero)
        {
            posicaoSlotGaiola = transform.position;
        }

        // Se ainda estiver no estado inicial de nascimento, dispara animação procedural
        if (estadoCicloVida == EstadoCicloVidaFantasma.Nascendo)
        {
            IniciarAnimacaoNascimentoProcedural();
        }
    }

    /// <summary>
    /// Configura o nascimento ordenado na Gaiola Central com saída escalonada.
    /// </summary>
    public void ConfigurarSpawnGaiola(Vector3 slot, float atrasoSaida, Vector3 saida)
    {
        posicaoSlotGaiola = slot;
        tempoAtrasoSaidaGaiola = atrasoSaida;
        posicaoSaidaGaiola = saida;
        transform.position = slot;
        estadoCicloVida = EstadoCicloVidaFantasma.Nascendo;

        if (m_NavAgent != null)
        {
            m_NavAgent.enabled = false;
        }

        IniciarAnimacaoNascimentoProcedural();
    }

    private void IniciarAnimacaoNascimentoProcedural()
    {
        if (m_AnimFantasma == null)
        {
            m_AnimFantasma = GetComponent<AnimacaoFantasma>() ?? gameObject.AddComponent<AnimacaoFantasma>();
        }

        m_AnimFantasma.ExecutarAnimacaoNascimento(1.2f, () =>
        {
            // Ao concluir a materialização, passa para espera escalonada na gaiola
            estadoCicloVida = EstadoCicloVidaFantasma.AguardandoGaiola;
        });
    }

    public bool EhVulneravel
    {
        get
        {
            var motor = FindFirstObjectByType<MotorRaciocinioSemantico>();
            return (motor != null && motor.pacmanIsPowered) || (motor != null && motor.ObterEstadoEntidade(m_Id) == GameEntityState.Vulnerable);
        }
    }

    public bool JogoEmPausaOuMenu()
    {
        if (HubInicialAmbiente.Instancia != null)
        {
            if (HubInicialAmbiente.Instancia.hubAberto || 
                HubInicialAmbiente.Instancia.GameOverAberto || 
                HubInicialAmbiente.Instancia.VitoriaAberta ||
                !HubInicialAmbiente.Instancia.partidaEmAndamento)
            {
                return true;
            }
        }
        return false;
    }

    public void PararMovimentoTotalFantasma()
    {
        if (m_NavAgent == null) m_NavAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (m_NavAgent != null && m_NavAgent.isActiveAndEnabled && m_NavAgent.isOnNavMesh)
        {
            m_NavAgent.isStopped = true;
            m_NavAgent.velocity = Vector3.zero;
        }
        if (m_AudioSourceEspacial != null && m_AudioSourceEspacial.isPlaying)
        {
            m_AudioSourceEspacial.Pause();
        }
    }

    protected override void Update()
    {
        base.Update();
        if (m_TimerCooldown > 0f)
        {
            m_TimerCooldown -= Time.deltaTime;
        }

        // Se o jogo estiver pausado ou no menu, imobiliza o fantasma e silencia o áudio
        if (JogoEmPausaOuMenu())
        {
            PararMovimentoTotalFantasma();
            return;
        }
        else
        {
            if (m_NavAgent != null && m_NavAgent.isActiveAndEnabled && m_NavAgent.isOnNavMesh && m_NavAgent.isStopped)
            {
                m_NavAgent.isStopped = false;
            }
        }

        // Modulação do Áudio Espacial 3D conforme o estado e Menu
        if (m_AudioSourceEspacial != null)
        {
            if (HubInicialAmbiente.Instancia != null && HubInicialAmbiente.Instancia.hubAberto)
            {
                if (m_AudioSourceEspacial.isPlaying) m_AudioSourceEspacial.Pause();
            }
            else
            {
                if (!m_AudioSourceEspacial.isPlaying) m_AudioSourceEspacial.Play();

                if (EhVulneravel)
                {
                    m_AudioSourceEspacial.pitch = 0.72f;
                    m_AudioSourceEspacial.volume = 0.35f;
                }
                else if (estadoCicloVida == EstadoCicloVidaFantasma.RetornandoGaiola)
                {
                    m_AudioSourceEspacial.pitch = 1.65f;
                    m_AudioSourceEspacial.volume = 0.40f;
                }
                else
                {
                    m_AudioSourceEspacial.pitch = 1.0f;
                    m_AudioSourceEspacial.volume = 0.28f;
                }
            }
        }

        // Máquina de estados Zero-GC do ciclo de vida
        switch (estadoCicloVida)
        {
            case EstadoCicloVidaFantasma.Nascendo:
                // Animação de nascimento gerenciada pelo AnimacaoFantasma
                break;

            case EstadoCicloVidaFantasma.AguardandoGaiola:
                ProcessarEsperaGaiola();
                break;

            case EstadoCicloVidaFantasma.SaindoGaiola:
                ProcessarSaidaGaiola();
                break;

            case EstadoCicloVidaFantasma.NavegandoAtivo:
                if (habilitarIA)
                {
                    ProcessarNavegacaoIA();
                }
                break;

            case EstadoCicloVidaFantasma.RetornandoGaiola:
                // Retorno gerenciado pela corrotina de respawn
                break;
        }
    }

    private void ProcessarEsperaGaiola()
    {
        var hub = FindFirstObjectByType<HubInicialAmbiente>();
        if (hub != null && hub.hubAberto) return; // Permanece aguardando na gaiola enquanto o Hub inicial estiver aberto

        tempoAtrasoSaidaGaiola -= Time.deltaTime;
        if (tempoAtrasoSaidaGaiola <= 0f)
        {
            estadoCicloVida = EstadoCicloVidaFantasma.SaindoGaiola;
        }
    }

    private void ProcessarSaidaGaiola()
    {
        var hub = FindFirstObjectByType<HubInicialAmbiente>();
        if (hub != null && hub.hubAberto) return;

        // Move suavemente do slot interno em direção ao portal de saída da gaiola
        transform.position = Vector3.MoveTowards(transform.position, posicaoSaidaGaiola, 2.2f * Time.deltaTime);

        // Ao ultrapassar o limiar da porta, ativa o NavMeshAgent para assumir a navegação
        float distSaida = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z),
                                           new Vector3(posicaoSaidaGaiola.x, 0f, posicaoSaidaGaiola.z));
        if (distSaida <= 0.35f)
        {
            estadoCicloVida = EstadoCicloVidaFantasma.NavegandoAtivo;
            if (m_NavAgent == null) m_NavAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (m_NavAgent != null)
            {
                if (UnityEngine.AI.NavMesh.SamplePosition(posicaoSaidaGaiola, out UnityEngine.AI.NavMeshHit hit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    m_NavAgent.Warp(hit.position);
                    m_NavAgent.enabled = true;
                }
                else
                {
                    m_NavAgent.Warp(posicaoSaidaGaiola);
                    m_NavAgent.enabled = true;
                }
            }
        }
    }

    private void ProcessarNavegacaoIA()
    {
        if (m_NavAgent == null)
        {
            m_NavAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        }

        if (m_NavAgent == null || !m_NavAgent.isOnNavMesh) return;

        // Sincroniza estado com o MotorRaciocinioSemantico e ProtegeWebSocketBridge
        var ws = PacMan.Network.ProtegeWebSocketBridge.Instance;
        var motor = FindFirstObjectByType<PacMan.Semantics.MotorRaciocinioSemantico>();
        bool consistente = (ws != null) ? ws.ReasonerConsistent : (motor == null || motor.reasonerConsistente);
        emModoChaseInconsistente = !consistente;

        // Se o MotorRaciocinioSemantico estiver gerenciando a navegação centralmente, delega a ele
        if (motor != null && motor.moverFantasmasDinamicamente) return;

        m_TimerAtualizacaoIA -= Time.deltaTime;
        if (m_TimerAtualizacaoIA <= 0f)
        {
            m_TimerAtualizacaoIA = 0.45f;

            if (emModoChaseInconsistente)
            {
                // CHASE (INCONSISTENTE): Persegue o jogador
                if (m_AlvoPacMan == null)
                {
                    GameObject pac = GameObject.Find("Visual_PacMan") ?? GameObject.FindWithTag("Player");
                    if (pac != null) m_AlvoPacMan = pac.transform;
                }

                if (m_AlvoPacMan != null)
                {
                    m_NavAgent.speed = 2.6f;
                    m_NavAgent.SetDestination(m_AlvoPacMan.position);
                }
            }
            else
            {
                // PATROL (CONSISTENTE): Ignora o jogador e vaga proceduralmente pelo NavMesh
                m_NavAgent.speed = 1.9f;
                if (m_AlvoPatrulhaProcedural == Vector3.zero || Vector3.Distance(transform.position, m_AlvoPatrulhaProcedural) < 1.8f)
                {
                    Vector2 rand = Random.insideUnitCircle * 9.5f;
                    Vector3 candidato = new Vector3(rand.x, transform.position.y, rand.y);
                    if (UnityEngine.AI.NavMesh.SamplePosition(candidato, out UnityEngine.AI.NavMeshHit hit, 4.0f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        m_AlvoPatrulhaProcedural = hit.position;
                    }
                }
                if (m_AlvoPatrulhaProcedural != Vector3.zero)
                {
                    m_NavAgent.SetDestination(m_AlvoPatrulhaProcedural);
                }
            }
        }
    }

    /// <summary>
    /// Executa o fluxo clássico de arcade quando o Pac-Man devora este fantasma durante Power Pellet:
    /// 1. Concede pontuação multiplicada (200 -> 400 -> 800 -> 1600 pts).
    /// 2. Dispara feedback juice (popup 3D de pontuação, partículas e áudio).
    /// 3. Oculta o corpo, desativa colisores e faz os olhos voarem de volta para a gaiola central.
    /// 4. Dispara a animação de renascimento procedural ao assentar no slot.
    /// </summary>
    public void DevorarFantasmaPorPowerPellet()
    {
        if (estadoCicloVida == EstadoCicloVidaFantasma.RetornandoGaiola || estadoCicloVida == EstadoCicloVidaFantasma.Nascendo)
        {
            return;
        }

        var motor = FindFirstObjectByType<PacMan.Semantics.MotorRaciocinioSemantico>();
        int pts = motor != null ? motor.RegistrarFantasmaDevorado() : 200;
        InstanciaSemanticaBase.pontuacaoLogica += pts;

        RegistrarConsumoOntologico(m_Id, "VulnerableGhost", true, $"PacMan(x) ^ Ghost({m_Id}) -> Eaten (+{pts} pts)");
        Debug.Log($"<color=#00FF99><b>[FANTASMA DEVORADO!]</b></color> '{name}' consumido pelo Pac-Man! +{pts} pts. Total: <b>{pontuacaoLogica}</b>.");

        if (GerenciadorEfeitosJuice.Instancia != null)
        {
            GerenciadorEfeitosJuice.Instancia.CriarEfeitoFantasmaDevorado(transform.position, pts);
        }

        var ctrlXR = FindFirstObjectByType<ControladorXRJogador>();
        if (ctrlXR != null)
        {
            ctrlXR.DispararHapticsDualController(0.65f, 0.20f);
        }

        estadoCicloVida = EstadoCicloVidaFantasma.RetornandoGaiola;

        if (m_NavAgent != null)
        {
            m_NavAgent.enabled = false;
        }

        if (m_AnimFantasma != null)
        {
            m_AnimFantasma.ExecutarDerrotaERespawn(posicaoSlotGaiola, () =>
            {
                tempoAtrasoSaidaGaiola = 3.5f;
                estadoCicloVida = EstadoCicloVidaFantasma.AguardandoGaiola;
            });
        }
    }

    protected override void AoExecutarColeta(GameObject interator, bool ehRaycast)
    {
        // Obstáculos inválidos não são coletados como gemas
    }

    protected override void AoDetectarViolacao(GameObject interator, bool ehRaycast)
    {
        if (JogoEmPausaOuMenu()) return;
        if (m_TimerCooldown > 0f) return;

        // REJEIÇÃO ABSOLUTA DA CÂMARA/TELA/HMD: NUNCA pontua nem retira vida ao bater na tela!
        if (!ehRaycast && interator != null)
        {
            string nomeInter = interator.name.ToLower();
            if (interator.CompareTag("MainCamera") || nomeInter.Contains("camera") || nomeInter.Contains("screen") || 
                nomeInter.Contains("offset") || nomeInter.Contains("hmd") || interator.GetComponentInParent<Camera>() != null)
            {
                return; // Bloqueio total na câmera!
            }

            // O contato DEVE ser estritamente no corpo físico do Pac-Man no piso
            bool ehPacMan = interator.GetComponent<AvatarPacManFisico>() != null || interator.GetComponentInParent<AvatarPacManFisico>() != null || nomeInter.Contains("pacman");
            if (!ehPacMan)
            {
                GameObject vis = GameObject.Find("Visual_PacMan");
                if (vis != null && (interator == vis || interator.transform.IsChildOf(vis.transform)))
                {
                    ehPacMan = true;
                }
            }
            if (!ehPacMan) return;
        }

        m_TimerCooldown = tempoCooldownPenalidade;

        // VERIFICA SE O PAC-MAN ESTÁ PODEROSO (POWER PELLET ATIVO)
        var motor = FindFirstObjectByType<PacMan.Semantics.MotorRaciocinioSemantico>();
        bool pacmanPoderoso = motor != null && motor.pacmanIsPowered;
        bool fantasmaVulneravel = pacmanPoderoso || (motor != null && motor.ObterEstadoEntidade(m_Id) == PacMan.Semantics.GameEntityState.Vulnerable);

        if (fantasmaVulneravel)
        {
            DevorarFantasmaPorPowerPellet();
            return;
        }

        // CASO B: FANTASMA HOSTIL COLIDE COM O PAC-MAN (DANO E PERDA DE VIDA)
        RegistrarConsumoOntologico(m_Id, m_NomeClasse, false, m_ExpressaoDL);

        if (motor != null)
        {
            motor.reasonerConsistente = false;
        }

        string nomeClasseFormatado = FormatarNomeClasse(m_NomeClasse);
        Debug.LogWarning($"<color=#FF3344><b>[Pac-Man Semântico - OBSTÁCULO INVÁLIDO]</b></color> '{nomeClasseFormatado}' ({m_Id}) colidiu com o Pac-Man! " +
                         $"Violação DL: [{m_ExpressaoDL}]. Penalidade: {m_ValorPontuacao} pts. Total: <b>{pontuacaoLogica}</b>.");

        EmitirFeedbackHaptico(interator, 0.85f, 0.35f);
        NotificarInteracao(sucesso: false);
        CriarEfeitoVisual(sucesso: false);

        // Aplica dano limpo no jogador (sistema de 3 vidas, invulnerabilidade e respawn)
        var ctrlJogador = FindFirstObjectByType<ControladorXRJogador>();
        if (ctrlJogador != null)
        {
            ctrlJogador.ExecutarDanoPacMan(gameObject);
        }
        else
        {
            var web = FindFirstObjectByType<PacMan.Web.ControladorDemoWeb>();
            if (web != null) web.ExecutarResetColisao(0.5f);
        }

        // Repele o fantasma 6.0 metros para trás do Pac-Man com salvaguarda no NavMesh
        GameObject visPac = GameObject.Find("Visual_PacMan");
        Vector3 posPac = (visPac != null) ? visPac.transform.position : transform.position;
        Vector3 dirRecuo = (transform.position - posPac).normalized;
        dirRecuo.y = 0f;
        if (dirRecuo.sqrMagnitude < 0.01f) dirRecuo = -transform.forward;

        Vector3 novaPos = transform.position + dirRecuo * 6.0f;
        if (m_NavAgent != null && m_NavAgent.isOnNavMesh)
        {
            m_NavAgent.isStopped = true;
            m_NavAgent.velocity = Vector3.zero;

            if (UnityEngine.AI.NavMesh.SamplePosition(novaPos, out UnityEngine.AI.NavMeshHit hit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                m_NavAgent.Warp(hit.position);
            }
            else
            {
                m_NavAgent.Warp(transform.position);
            }
        }
        else
        {
            transform.position = novaPos;
        }
    }
}

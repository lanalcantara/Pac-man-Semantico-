using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Modelagem Orientada a Objetos (UML/C++)
/// ============================================================================
/// Classe concreta que representa uma Gema Válida (Indivíduo Ontológico Conforme).
/// Satisfaz as regras e restrições de Description Logic (DL) na TBox/ABox.
/// Concede pontos positivos ao Pac-Man ao ser coletada e é destruída do labirinto imediatamente.
///
/// Inclui comportamento de fuga dinâmica (Evasão Tática) quando o jogador se aproxima (< 3m),
/// com velocidade estritamente ajustada para metade da velocidade do jogador (50%),
/// permitindo que o jogador a encurrale estrategicamente nos corredores.
/// ============================================================================
/// </summary>
public class GemaValidaSemantica : InstanciaSemanticaBase
{
    [Header("Configurações da Gema")]
    [Tooltip("Intensidade do brilho emissivo em RV/RA.")]
    public float intensidadeBrilho = 1.2f;

    [Tooltip("Raio do colisor Trigger para captura estrita por toque físico.")]
    public float raioCaptura = 0.35f;

    [Header("Missão Sequencial e Poder")]
    [Tooltip("Se verdadeiro, esta orbe concede o poder de devorar fantasmas (estritamente 1 a cada 3 orbes).")]
    public bool ehOrbeDePoder = false;

    [Tooltip("Índice sequencial desta orbe na missão.")]
    public int indiceOrbe = 1;

    [Tooltip("Total de orbes da missão ontológica.")]
    public int totalOrbes = 12;

    protected override void Awake()
    {
        base.Awake();
        m_EhValida = true;
        m_ValorPontuacao = 10;
        habilitarFuga = false;
        raioDeteccaoFuga = 2.8f;
        velocidadeFuga = 1.6f;

        // Remoção imediata de NavMeshAgent ou script de fuga para garantir Pac-Dots 100% estáticos
        UnityEngine.AI.NavMeshAgent nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav != null) Destroy(nav);
        GemaFugitiva fuga = GetComponent<GemaFugitiva>();
        if (fuga != null) Destroy(fuga);
        if (m_CorDestaque == Color.green || m_CorDestaque == Color.clear)
        {
            m_CorDestaque = new Color(0.0f, 0.90f, 1.0f, 0.58f);
        }

        // Garante que o MeshFilter seja um Cubo/Prisma (Nodo de Dados OWL) e material translúcido
        GarantirGeometriaPrismaTranslucido();

        // Garante que TODOS os colisores sejam Triggers (isTrigger = true) para eliminar empurrão físico
        Collider[] todosColisores = GetComponentsInChildren<Collider>();
        if (todosColisores != null)
        {
            for (int i = 0; i < todosColisores.Length; i++)
            {
                if (todosColisores[i] != null) todosColisores[i].isTrigger = true;
            }
        }

        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc == null)
        {
            sc = gameObject.AddComponent<SphereCollider>();
        }
        sc.isTrigger = true;
        sc.radius = raioCaptura;

        // Garante que a física seja cinemática (sem forças ou empurrões físicos)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
        }
    }

    protected override void Update()
    {
        base.Update();

        // Coleta automática por proximidade absoluta vinculada ao corpo do Pac-Man / ControladorXRJogador
        if (!m_Coletada)
        {
            var ctrl = FindAnyObjectByType<ControladorXRJogador>();
            if (ctrl == null || !ctrl.controlesHabilitados) return;

            GameObject pacmanCorpo = GameObject.Find("Visual_PacMan");
            if (pacmanCorpo == null) pacmanCorpo = ctrl.gameObject;

            Vector2 posGema2D = new Vector2(transform.position.x, transform.position.z);
            Vector2 posPac2D = new Vector2(pacmanCorpo.transform.position.x, pacmanCorpo.transform.position.z);
            float distHorizontal = Vector2.Distance(posGema2D, posPac2D);
            float diffVertical = Mathf.Abs(transform.position.y - pacmanCorpo.transform.position.y);
            // Captura justa e proporcional: raio de 0.85m (compatível com a boca e corpo do Pac-Man)
            if (distHorizontal <= 0.85f && diffVertical < 1.10f)
            {
                AoExecutarColeta(pacmanCorpo, ehRaycast: false);
            }
        }
    }

    protected override void OnTriggerEnter(Collider other)
    {
        if (m_Coletada) return;
        if (other == null) return;

        var ctrl = other.GetComponentInParent<ControladorXRJogador>();
        if (ctrl != null && ctrl.controlesHabilitados)
        {
            // Validação estrita por proximidade proporcional (<= 0.85m)
            Vector2 posGema2D = new Vector2(transform.position.x, transform.position.z);
            Vector2 posPac2D = new Vector2(ctrl.transform.position.x, ctrl.transform.position.z);
            float distHorizontal = Vector2.Distance(posGema2D, posPac2D);
            if (distHorizontal <= 0.85f)
            {
                AoExecutarColeta(ctrl.gameObject, ehRaycast: false);
            }
        }
    }

    protected override void AoExecutarColeta(GameObject interator, bool ehRaycast)
    {
        // Se controles estiverem desabilitados (Hub aberto ou Game Over), ignora coleta
        var ctrlJogador = FindAnyObjectByType<ControladorXRJogador>();
        if (ctrlJogador != null && !ctrlJogador.controlesHabilitados) return;

        // REJEIÇÃO ABSOLUTA: Câmera ou tela NÃO coletam gemas
        if (!ehRaycast && interator != null)
        {
            string nomeI = interator.name.ToLower();
            if (interator.CompareTag("MainCamera") || nomeI.Contains("camera") || nomeI.Contains("screen") || 
                nomeI.Contains("offset") || nomeI.Contains("hmd") || interator.GetComponentInParent<Camera>() != null)
            {
                return;
            }
        }
        if (m_Coletada) return;
        m_Coletada = true;
        totalColetadas++;
        pontuacaoLogica += m_ValorPontuacao;
        instanciasValidasRestantes = Mathf.Max(0, instanciasValidasRestantes - 1);

        string nomeClasseFormatado = FormatarNomeClasse(m_NomeClasse);

        Debug.Log($"<color=#00E5FF><b>[Interface OWL - NODO DE DADOS]</b></color> '{nomeClasseFormatado}' ({m_Id}) instanciado na ABox! " +
                  $"Axioma DL: [{m_ExpressaoDL}].");

        // 1. Envia a mensagem de instanciação via WebSocket (porta 8085) e registra no Grafo
        RegistrarConsumoOntologico(m_Id, m_NomeClasse, m_EhValida, m_ExpressaoDL);

        // 2. Atualiza o HUD em tempo real com a classe em foco
        AtualizarHUDs(m_Id, nomeClasseFormatado, m_ValorPontuacao, m_ExpressaoDL, m_DescricaoSemantica);

        // 3. REGRA DE PODER: estritamente 1 a cada 3 orbes concede o poder de devorar fantasmas!
        if (ehOrbeDePoder)
        {
            var motor = FindFirstObjectByType<PacMan.Semantics.MotorRaciocinioSemantico>();
            if (motor != null)
            {
                motor.AtivarPowerPellet(8.0f);
            }
            Debug.Log($"<color=#FFD700><b>[PODER ESPECIAL (1 A CADA 3)]</b></color> Orbe #{indiceOrbe} concedeu modo Power Pellet por 8s!");
        }

        // 4. Notifica o Gerenciador de Cenário para avançar a sequência e spawnar a próxima orbe
        if (GerenciadorCenarioSemantico.Instancia != null)
        {
            GerenciadorCenarioSemantico.Instancia.NotificarOrbeColetada(this);
        }

        // 5. Exibe Banner na lateral inferior da tela com o nome ontológico do elemento
        if (PainelGrafoSemanticoProtege.Instancia != null)
        {
            PainelGrafoSemanticoProtege.Instancia.ExibirBannerOrbeColetada(nomeClasseFormatado, m_Id, m_ExpressaoDL, ehOrbeDePoder, indiceOrbe, totalOrbes);
        }

        EmitirFeedbackHaptico(interator, 0.35f, 0.12f);
        NotificarInteracao(sucesso: true);
        if (RelationBuilder.Instance != null)
        {
            RelationBuilder.Instance.AdicionarItemComidoNaRedeNeural(this, gameObject);
        }
        CriarEfeitoVisual(sucesso: true);

        // 6. Desativa completamente visual e colisão: orbe capturada some na hora (sem deixar asset vazio)
        Renderer[] rends = GetComponentsInChildren<Renderer>(true);
        if (rends != null)
        {
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] != null) rends[i].enabled = false;
            }
        }
        Light[] luzes = GetComponentsInChildren<Light>(true);
        if (luzes != null)
        {
            for (int i = 0; i < luzes.Length; i++)
            {
                if (luzes[i] != null) luzes[i].enabled = false;
            }
        }
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        if (cols != null)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].enabled = false;
            }
        }

        // Destrói o objeto da orbe capturada definitivamente da cena (sem poluir memória ou cache)
        Destroy(gameObject, 0.15f);
    }

    /// <summary>
    /// Restaura completamente a gema para uma nova rodada limpa.
    /// </summary>
    public void ReiniciarGemaParaNovaPartida()
    {
        m_Coletada = false;
        Renderer[] rends = GetComponentsInChildren<Renderer>(true);
        if (rends != null)
        {
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] != null) rends[i].enabled = true;
            }
        }
        Light[] luzes = GetComponentsInChildren<Light>(true);
        if (luzes != null)
        {
            for (int i = 0; i < luzes.Length; i++)
            {
                if (luzes[i] != null) luzes[i].enabled = true;
            }
        }
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        if (cols != null)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].enabled = true;
            }
        }
    }

    private void GarantirGeometriaPrismaTranslucido()
    {
        MeshFilter[] mfs = GetComponentsInChildren<MeshFilter>(true);
        if (mfs != null && mfs.Length > 0)
        {
            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh cubeMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(tempCube);
            else DestroyImmediate(tempCube);

            foreach (var mf in mfs)
            {
                if (mf != null)
                {
                    mf.sharedMesh = cubeMesh;
                    mf.transform.localRotation = Quaternion.Euler(35f, 45f, 0f);
                }
            }
        }
    }

    protected override void AoDetectarViolacao(GameObject interator, bool ehRaycast)
    {
        // Gemas válidas não disparam violação
    }
}

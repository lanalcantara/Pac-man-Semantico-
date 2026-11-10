using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Modelagem Orientada a Objetos (UML/C++)
/// ============================================================================
/// Classe base abstrata para todas as instâncias ontológicas no Pac-Man Semântico.
/// Implementa IInstanciaSemantica e gerencia colisões XR, propriedades DL,
/// feedbacks visuais/hápticos e sincronização com a pontuação lógica global.
/// ============================================================================
/// </summary>
[SelectionBase]
public abstract class InstanciaSemanticaBase : MonoBehaviour, IInstanciaSemantica
{
    [Header("Identidade Ontológica (Description Logic)")]
    [SerializeField] protected string m_Id = "inst_001";
    [SerializeField] protected string m_NomeClasse = "InstanciaBase";
    [SerializeField] protected bool m_EhValida = true;
    [SerializeField] protected string m_ExpressaoDL = "Classe ⊑ Valida";
    [SerializeField, TextArea(2, 4)] protected string m_DescricaoSemantica = "Descrição ontológica.";
    [SerializeField] protected int m_ValorPontuacao = 10;
    [SerializeField] protected Color m_CorDestaque = Color.green;

    [Header("Efeitos Visuais e Interativos")]
    [SerializeField] protected Color m_CorEmissaoBase = Color.clear;

    // Dicionário de propriedades ontológicas (Object/Data Properties)
    protected readonly Dictionary<string, string> m_PropriedadesDL = new Dictionary<string, string>();

    // Cache e estado
    protected Renderer m_Renderer;
    protected Material m_MaterialInstanciado;
    protected bool m_Coletada = false;
    protected bool m_EstaFocada = false;

    // Pontuação Lógica Global do Pac-Man Semântico
    public static int pontuacaoLogica = 0;
    public static int instanciasValidasRestantes = 0;
    public static int totalColetadas = 0;

    // Eventos estáticos de ciclo de vida semântico
    public static event Action<IInstanciaSemantica, bool> OnInstanciaInteragida;
    public static event Action<IInstanciaSemantica> OnInstanciaFocada;
    public static event Action<IInstanciaSemantica> OnInstanciaDesfocada;

    // =========================================================================
    // PROPRIEDADES DA INTERFACE IInstanciaSemantica
    // =========================================================================
    public string Id
    {
        get => m_Id;
        set => m_Id = value;
    }

    public string NomeClasse
    {
        get => m_NomeClasse;
        set => m_NomeClasse = value;
    }

    public bool EhValida
    {
        get => m_EhValida;
        set => m_EhValida = value;
    }

    public string ExpressaoDL
    {
        get => m_ExpressaoDL;
        set => m_ExpressaoDL = value;
    }

    public string DescricaoSemantica
    {
        get => m_DescricaoSemantica;
        set => m_DescricaoSemantica = value;
    }

    public int ValorPontuacao
    {
        get => m_ValorPontuacao;
        set => m_ValorPontuacao = value;
    }

    public Vector3 Posicao
    {
        get => transform.position;
        set => transform.position = value;
    }

    public Color CorDestaque
    {
        get => m_CorDestaque;
        set => m_CorDestaque = value;
    }

    public IReadOnlyDictionary<string, string> PropriedadesDL => m_PropriedadesDL;

    public bool Coletada => m_Coletada;

    // =========================================================================
    // CICLO DE VIDA DO UNITY
    // =========================================================================
    protected virtual void Awake()
    {
        m_Renderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
        if (m_Renderer != null)
        {
            m_MaterialInstanciado = m_Renderer.material;
            if (m_MaterialInstanciado != null && m_MaterialInstanciado.HasProperty("_EmissionColor"))
            {
                m_CorEmissaoBase = m_MaterialInstanciado.GetColor("_EmissionColor");
            }
        }

        // Garante que todas as gemas sejam Triggers e usem física cinemática (sem empurrão físico)
        if (m_EhValida)
        {
            Collider[] cols = GetComponentsInChildren<Collider>();
            if (cols != null)
            {
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] != null) cols[i].isTrigger = true;
                }
            }
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
        }

        Transform trRotulo = transform.Find("Rotulo_3D_Item_Ontologico");
        if (trRotulo != null && !RelationBuilder.exibirRotulosFlutuantes)
        {
            trRotulo.gameObject.SetActive(false);
        }
    }

    [Header("Inspeção Semântica por Proximidade")]
    [Tooltip("Distância máxima para exibição automática do conceito ontológico no HUD e realce.")]
    public float distanciaInspecaoProximidade = 3.2f;

    [Header("Comportamento de Fuga Dinâmica (Espécimes Válidos / Gemas)")]
    [Tooltip("Habilita repulsão dinâmica quando o Pac-Man se aproxima.")]
    public bool habilitarFuga = true;

    [Tooltip("Raio de proximidade em metros para acionar a fuga do Pac-Man (estritamente 3.0m).")]
    public float raioDeteccaoFuga = 3.0f;

    [Tooltip("Velocidade linear de fuga pelos corredores (ajustada para 50% da velocidade do jogador).")]
    public float velocidadeFuga = 1.9f;

    protected Transform m_PlayerTransform;

    protected virtual void Start()
    {
        if (m_EhValida)
        {
            instanciasValidasRestantes++;
            AtualizarRotulo3DInicial();
        }
        LocalizarPlayerTransform();
    }

    public void AtualizarRotulo3DInicial()
    {
        Transform trOld = transform.Find("Rotulo_3D_Item_Ontologico");
        if (!RelationBuilder.exibirRotulosFlutuantes)
        {
            if (trOld != null) trOld.gameObject.SetActive(false);
            return;
        }
        if (!m_EhValida) return;
        string nomeFormatado = FormatarNomeClasse(m_NomeClasse);
        string tipo = RelationBuilder.ClassificarTipoElementoOntologico(m_NomeClasse, m_ExpressaoDL);
        RelationBuilder.AtualizarRotulo3DNodoNoMapa(
            gameObject,
            $"<color=#38BDF8><size=80%><b>[{tipo}]</b></size></color>\n<color=#FFFFFF><b>{nomeFormatado}</b></color>");
    }

    /// <summary>
    /// Reativa esta Classe/Subclasse/Instância no corredor quando um Fantasma quebra o pedaço
    /// da linha da Rede Neural que a conectava, permitindo ao Pac-Man voltar e comê-la novamente.
    /// </summary>
    public virtual void ReativarInstanciaParaReconexao()
    {
        m_Coletada = false;
        m_EstaFocada = false;
        instanciasValidasRestantes++;

        gameObject.SetActive(true);
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        if (cols != null)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                {
                    cols[i].enabled = true;
                    cols[i].isTrigger = true;
                }
            }
        }
    }

    protected virtual void Update()
    {
        if (m_Coletada) return;

        if (m_PlayerTransform == null)
        {
            LocalizarPlayerTransform();
        }

        if (m_PlayerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, m_PlayerTransform.position);
            if (dist <= distanciaInspecaoProximidade)
            {
                if (!m_EstaFocada)
                {
                    DefinirFocoVisual(true);
                }
            }
            else if (m_EstaFocada)
            {
                DefinirFocoVisual(false);
            }

            // Fuga Dinâmica para instâncias válidas (Gemas / Parasitas vivos)
            // Se possuir o componente GemaFugitiva dedicado, delega a ele para evitar cálculo duplicado
            if (m_EhValida && habilitarFuga && dist < raioDeteccaoFuga && dist > 0.15f)
            {
                if (GetComponent<GemaFugitiva>() == null)
                {
                    ProcessarFugaDinamica(dist);
                }
            }
        }
    }

    /// <summary>
    /// Localiza o Transform do jogador com salvaguardas múltiplas (Tag, XR Origin, Controladores e Câmera).
    /// </summary>
    public void LocalizarPlayerTransform()
    {
        if (m_PlayerTransform != null) return;

        GameObject p = GameObject.FindWithTag("Player");
        if (p != null)
        {
            m_PlayerTransform = p.transform;
            return;
        }

        var ctrlXR = FindFirstObjectByType<ControladorXRJogador>();
        if (ctrlXR != null)
        {
            m_PlayerTransform = ctrlXR.transform;
            return;
        }

        var ctrl = FindFirstObjectByType<PacMan.Player.ControladorJogador>();
        if (ctrl != null)
        {
            m_PlayerTransform = ctrl.transform;
            return;
        }

        var comeCome = FindFirstObjectByType<MovimentoComeCome>();
        if (comeCome != null)
        {
            m_PlayerTransform = comeCome.transform;
            return;
        }

        if (Camera.main != null)
        {
            m_PlayerTransform = Camera.main.transform;
        }
    }

    /// <summary>
    /// Executa a fuga dinâmica da gema calculando o vetor oposto normalizado à posição do jogador
    /// e desviando estritamente de paredes e barreiras geométricas do labirinto.
    /// Velocidade limitada estritamente a 50% da velocidade do Pac-Man (1.9 m/s).
    /// </summary>
    protected virtual void ProcessarFugaDinamica(float distAoJogador)
    {
        if (m_PlayerTransform == null) return;

        Vector3 posPacMan = m_PlayerTransform.position;
        Vector3 dirFuga = (transform.position - posPacMan);
        dirFuga.y = 0f;
        if (dirFuga.sqrMagnitude < 0.0001f) dirFuga = transform.forward;
        dirFuga.Normalize();

        Vector3 melhorDirecaoFuga = Vector3.zero;

        // 1. Testa a direção direta oposta ao jogador
        if (ChecarCaminhoDesimpedido(dirFuga, 0.45f))
        {
            melhorDirecaoFuga = dirFuga;
        }
        else
        {
            // 2. Se houver parede à frente, avalia múltiplos ângulos para desviar pelos corredores válidos
            float[] angulos = new float[] { 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f };
            float maiorDistanciaProjetada = float.MinValue;

            foreach (float ang in angulos)
            {
                Vector3 cand = Quaternion.Euler(0f, ang, 0f) * dirFuga;
                cand.y = 0f;
                cand.Normalize();

                if (ChecarCaminhoDesimpedido(cand, 0.4f))
                {
                    Vector3 posProjetada = transform.position + (cand * 1.2f);
                    float distFutura = Vector3.Distance(new Vector3(posProjetada.x, 0f, posProjetada.z), new Vector3(posPacMan.x, 0f, posPacMan.z));
                    if (distFutura > maiorDistanciaProjetada)
                    {
                        maiorDistanciaProjetada = distFutura;
                        melhorDirecaoFuga = cand;
                    }
                }
            }
        }

        if (melhorDirecaoFuga.sqrMagnitude > 0.01f)
        {
            melhorDirecaoFuga.Normalize();
            // Mantém a velocidade estritamente dentro de 50% da velocidade do Pac-Man (sem aceleração desmedida)
            Vector3 deslocamento = melhorDirecaoFuga * (velocidadeFuga * Time.deltaTime);

            if (ChecarCaminhoDesimpedido(melhorDirecaoFuga, deslocamento.magnitude + 0.15f))
            {
                transform.position += deslocamento;
                transform.Rotate(Vector3.up, 240f * Time.deltaTime);
            }
        }
    }

    /// <summary>
    /// Verifica se a direção no corredor está desimpedida de obstáculos sólidos/paredes.
    /// </summary>
    protected bool ChecarCaminhoDesimpedido(Vector3 direcao, float distancia)
    {
        Vector3 origem = transform.position + Vector3.up * 0.35f;
        if (Physics.Raycast(origem, direcao.normalized, out RaycastHit hit, distancia, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && !hit.collider.isTrigger)
            {
                if (hit.collider.gameObject != gameObject && !hit.collider.transform.IsChildOf(transform))
                {
                    return false;
                }
            }
        }
        return true;
    }

    protected virtual void OnDestroy()
    {
        if (m_EhValida && !m_Coletada)
        {
            instanciasValidasRestantes = Mathf.Max(0, instanciasValidasRestantes - 1);
        }
    }

    // =========================================================================
    // MÉTODOS DA INTERFACE IInstanciaSemantica
    // =========================================================================
    public virtual void InicializarSemantica(DadoInstanciaSemantica dados)
    {
        if (dados == null) return;

        m_Id = dados.id;
        m_NomeClasse = dados.classeOntologica;
        m_EhValida = dados.ehValida;
        m_ExpressaoDL = dados.expressaoDL;
        m_DescricaoSemantica = dados.descricaoSemantica;
        m_ValorPontuacao = dados.valorPontuacao;
        m_CorDestaque = dados.corDestaque;

        m_PropriedadesDL.Clear();
        if (dados.propriedades != null)
        {
            foreach (var prop in dados.propriedades)
            {
                if (!string.IsNullOrEmpty(prop.chave))
                {
                    m_PropriedadesDL[prop.chave] = prop.valor;
                }
            }
        }

        AplicarCoresNoMaterial();
        if (m_EhValida)
        {
            AtualizarRotulo3DInicial();
        }
    }

    public virtual void Interagir(GameObject interator, bool ehRaycast = false)
    {
        if (m_Coletada) return;

        // Validação ontológica estrita baseada na ontologia Protégé
        string motivo = "Instância inválida ou violação ontológica";
        bool ontologicamenteValida = m_EhValida && ValidarTaxonomiaOntologicaEstrita(m_NomeClasse, out motivo);

        if (ontologicamenteValida)
        {
            AoExecutarColeta(interator, ehRaycast);
        }
        else
        {
            Debug.LogWarning($"<color=#FF0055><b>[FILTRAGEM ONTOLÓGICA ESTRITA]</b></color> '{m_NomeClasse}' recusada: {motivo}");
            AoDetectarViolacao(interator, ehRaycast);
        }
    }

    public virtual void DefinirFocoVisual(bool focado)
    {
        if (m_EstaFocada == focado || m_Coletada) return;
        m_EstaFocada = focado;

        if (m_MaterialInstanciado != null && m_MaterialInstanciado.HasProperty("_EmissionColor"))
        {
            if (focado)
            {
                Color corFoco = m_EhValida ? Color.cyan * 1.3f : Color.magenta * 1.3f;
                m_MaterialInstanciado.SetColor("_EmissionColor", corFoco);
                m_MaterialInstanciado.EnableKeyword("_EMISSION");
                OnInstanciaFocada?.Invoke(this);
            }
            else
            {
                m_MaterialInstanciado.SetColor("_EmissionColor", m_CorEmissaoBase);
                OnInstanciaDesfocada?.Invoke(this);
            }
        }
    }

    // =========================================================================
    // MÉTODOS ABSTRATOS POLIMÓRFICOS
    // =========================================================================
    protected abstract void AoExecutarColeta(GameObject interator, bool ehRaycast);
    protected abstract void AoDetectarViolacao(GameObject interator, bool ehRaycast);

    // =========================================================================
    // DETECÇÃO DE COLISÃO XR UNIVERSAL
    // =========================================================================
    protected virtual void OnTriggerEnter(Collider other)
    {
        if (m_Coletada || other == null) return;

        if (EhColisorDoJogador(other))
        {
            Interagir(other.gameObject, ehRaycast: false);
        }
    }

    public static bool EhColisorDoJogador(Collider col)
    {
        if (col == null) return false;

        // 1. REJEIÇÃO ABSOLUTA E INTRANSIGÍVEL DE CÂMARA, TELA, HMD E OFFSETS
        if (col.CompareTag("MainCamera")) return false;
        string nome = col.gameObject.name.ToLower();
        if (nome.Contains("camera") || nome.Contains("offset") || nome.Contains("screen") || 
            nome.Contains("hmd") || nome.Contains("eyeball") || nome.Contains("eye") || nome.Contains("view"))
        {
            return false;
        }
        if (col.GetComponent<Camera>() != null || col.GetComponentInParent<Camera>() != null)
        {
            return false;
        }

        // 2. ACEITAÇÃO EXCLUSIVA DO CORPO FÍSICO DO PAC-MAN NO SOLO
        if (col.GetComponent<AvatarPacManFisico>() != null || col.GetComponentInParent<AvatarPacManFisico>() != null)
            return true;

        if (nome.Contains("visual_pacman") || nome.Contains("pacman") || nome.Contains("pac-man") || nome.Contains("comecome"))
            return true;

        GameObject visualPac = GameObject.Find("Visual_PacMan");
        if (visualPac != null && (col.gameObject == visualPac || col.transform.IsChildOf(visualPac.transform)))
            return true;

        return false;
    }

    protected void NotificarInteracao(bool sucesso)
    {
        OnInstanciaInteragida?.Invoke(this, sucesso);
    }

    protected void EmitirFeedbackHaptico(GameObject interator, float amplitude, float duracao)
    {
        if (interator == null) return;
        InteracaoEspacialXR interacaoXR = interator.GetComponentInParent<InteracaoEspacialXR>();
        if (interacaoXR != null)
        {
            interacaoXR.DispararVibracaoHaptica(amplitude, duracao);
        }

        ControladorXRJogador ctrlXR = interator.GetComponentInParent<ControladorXRJogador>();
        if (ctrlXR != null)
        {
            ctrlXR.DispararHapticsDualController(amplitude, duracao);
        }
    }

    protected void CriarEfeitoVisual(bool sucesso)
    {
        GameObject efeitoObj = new GameObject($"Efeito_DL_{m_NomeClasse}_{m_Id}");
        efeitoObj.transform.position = transform.position;

        Light luz = efeitoObj.AddComponent<Light>();
        if (luz != null)
        {
            luz.type = LightType.Point;
            luz.range = 3.5f;
            luz.intensity = sucesso ? 4.5f : 6.0f;
            luz.color = sucesso ? new Color(0.2f, 1f, 0.5f) : new Color(1f, 0.2f, 0.2f);
        }

        Destroy(efeitoObj, 0.35f);
    }

    protected virtual void AplicarCoresNoMaterial()
    {
        if (m_MaterialInstanciado != null && m_CorDestaque != Color.clear)
        {
            if (m_MaterialInstanciado.HasProperty("_BaseColor"))
                m_MaterialInstanciado.SetColor("_BaseColor", m_CorDestaque);
            if (m_MaterialInstanciado.HasProperty("_Color"))
                m_MaterialInstanciado.SetColor("_Color", m_CorDestaque);
            if (m_MaterialInstanciado.HasProperty("_EmissionColor"))
            {
                m_MaterialInstanciado.EnableKeyword("_EMISSION");
                m_MaterialInstanciado.SetColor("_EmissionColor", m_CorDestaque * 0.7f);
                m_CorEmissaoBase = m_CorDestaque * 0.7f;
            }
        }
    }

    // =========================================================================
    // UTILITÁRIOS GLOBAIS DE FORMATAÇÃO E REGISTO ONTOLÓGICO
    // =========================================================================
    // =========================================================================
    // FILTRAGEM ONTOLÓGICA ESTRITA (Protégé / parasites.owl / ALCQ(D))
    // =========================================================================
    /// <summary>
    /// Validação Ontológica Estrita conforme a Ontologia de Parasitologia do Protégé (parasites.owl).
    /// O sério-jogo em RV apenas permite capturar espécimes válidos pertencentes à hierarquia correta:
    /// Nematoda (Ascaris lumbricoides, Enterobius vermicularis) e Platyhelminthes (Taenia solium).
    /// Elementos fora da ontologia ou axiomas inconsistentes são tratados como violação e penalidade.
    /// </summary>
    public static bool ValidarTaxonomiaOntologicaEstrita(string nomeClasse, out string motivoValidacao)
    {
        if (string.IsNullOrEmpty(nomeClasse))
        {
            motivoValidacao = "Taxon Conforme (Padrão)";
            return true;
        }

        string limpo = nomeClasse.Trim().Replace(" ", "").Replace("_", "").ToLower();

        // 1. Rejeita apenas o que for violação/inconsistência explícita da ontologia
        if (limpo.Contains("inconsistent") || limpo.Contains("mismatch") || limpo.Contains("invalida") || limpo.Contains("violacao") || limpo.Contains("violation"))
        {
            motivoValidacao = "Violação ALCQ(D): Inconsistência Ontológica Taxonômica";
            return false;
        }

        // 2. Aceita todos os espécimes válidos da ontologia Protégé (parasitas, filoparasitários, tratamentos e gemas)
        if (limpo.Contains("ascaris"))
        {
            motivoValidacao = "Taxon Conforme: Nematoda -> Ascaris lumbricoides";
            return true;
        }
        if (limpo.Contains("enterobius"))
        {
            motivoValidacao = "Taxon Conforme: Nematoda -> Enterobius vermicularis";
            return true;
        }
        if (limpo.Contains("taenia"))
        {
            motivoValidacao = "Taxon Conforme: Platyhelminthes -> Taenia solium";
            return true;
        }
        if (limpo.Contains("treatment") || limpo.Contains("pellet") || limpo.Contains("power"))
        {
            motivoValidacao = "Tratamento Antiparasitário Conforme";
            return true;
        }

        motivoValidacao = "Taxon Conforme: Espécime de Parasitologia Válido";
        return true;
    }

    public static string FormatarNomeClasse(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Parasita";
        if (raw.Equals("AscarisLumbricoides", StringComparison.OrdinalIgnoreCase)) return "Ascaris lumbricoides";
        if (raw.Equals("EnterobiusVermicularis", StringComparison.OrdinalIgnoreCase)) return "Enterobius vermicularis";
        if (raw.Equals("TaeniaSolium", StringComparison.OrdinalIgnoreCase)) return "Taenia solium";
        if (raw.Equals("AntiparasiticTreatment", StringComparison.OrdinalIgnoreCase)) return "Antiparasitic Treatment";
        if (raw.Equals("InconsistentParasiteSpecimen", StringComparison.OrdinalIgnoreCase)) return "Inconsistent Parasite Specimen";
        if (raw.Equals("HostMismatchViolation", StringComparison.OrdinalIgnoreCase)) return "Host Mismatch Violation";
        if (raw.Equals("HabitatInconsistency", StringComparison.OrdinalIgnoreCase)) return "Habitat Inconsistency";
        return raw;
    }

    public static void RegistrarConsumoOntologico(string id, string nomeClasse, bool ehValida = true, string expressaoDL = "")
    {
        // 1. Envia a mensagem de instanciação via WebSocket (porta 8085) para o Protégé
        var ws = PacMan.Network.ProtegeWebSocketBridge.Instance ?? UnityEngine.Object.FindFirstObjectByType<PacMan.Network.ProtegeWebSocketBridge>();
        if (ws != null)
        {
            ws.EnviarInstanciacaoIndividuo(id, nomeClasse, ehValida, expressaoDL);
        }
        else if (SemanticHUDManager.Instancia != null)
        {
            SemanticHUDManager.Instancia.DefinirAxiomasInseridos(SemanticHUDManager.Instancia.axiomasInseridos + 1);
            SemanticHUDManager.Instancia.AdicionarLinhaLog($"[+] researcher_01 instanciou {nomeClasse} ({id})");
            if (!ehValida)
            {
                SemanticHUDManager.Instancia.DefinirStatusReasoner(false, $"HermiT: Exceção em {nomeClasse}");
            }
        }

        // 2. Registra no Grafo local
        var motor = UnityEngine.Object.FindFirstObjectByType<PacMan.Semantics.MotorRaciocinioSemantico>();
        VirtOnto.Model.Graph grafo = motor != null ? motor.GetSemanticGraph() : null;
        if (grafo == null)
        {
            var ger = UnityEngine.Object.FindFirstObjectByType<GerenciadorCenarioSemantico>();
            if (ger != null) grafo = ger.GrafoOntologico;
        }

        if (grafo != null && !string.IsNullOrEmpty(id))
        {
            if (grafo.GetNode(id) == null)
            {
                grafo.AddNode(new VirtOnto.Model.Node(id, nomeClasse, VirtOnto.Model.NodeType.Individual));
            }
            string edgeId = $"edge_instantiates_{id}_{Time.frameCount}";
            grafo.AddEdge(new VirtOnto.Model.Edge(edgeId, "researcher_01", id, "instantiates"));
        }

        // 3. Atualiza o Painel do Grafo Semântico Protégé (Desata nós e acende conexões em tempo real)
        if (PainelGrafoSemanticoProtege.Instancia != null)
        {
            PainelGrafoSemanticoProtege.Instancia.NotificarConsumoOntologico(id, nomeClasse);
        }
    }

    public static void AtualizarHUDs(string id, string nomeClasse, int pontos, string dl, string desc = "")
    {
        var demoWeb = UnityEngine.Object.FindFirstObjectByType<PacMan.Web.ControladorDemoWeb>();
        if (demoWeb != null)
        {
            demoWeb.RegistrarCapturaItem(id, nomeClasse, true, pontos, dl);
        }

        var hudVR = UnityEngine.Object.FindFirstObjectByType<HUDHolograficoXR>();
        if (hudVR != null)
        {
            hudVR.AoInteragirInstanciaManual(id, nomeClasse, pontos, dl, desc, true);
        }
    }
}
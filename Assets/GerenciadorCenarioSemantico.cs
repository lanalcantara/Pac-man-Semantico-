using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using VirtOnto.Model;
using PacMan.Semantics;
using PacMan.Network;
using PacMan.XR;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Modelagem Orientada a Objetos (CIn-UFPE)
/// ============================================================================
/// Gerenciador Central de Cenário Semântico do Pac-Man Semântico.
/// 
/// Responsabilidades Principais:
/// 1. Posicionamento Procedural Preciso: Ajusta o eixo Y com base no cálculo dinâmico
///    de bounds e pivô de cada modelo 3D, garantindo assentamento exato sobre a
///    superfície do Plane (eliminando flutuação indesejada ou corte no chão).
/// 2. Salvaguarda e Validação Robusta de Prefabs: Recupera automaticamente referências
///    perdidas/missing (Gemas Válidas, Obstáculos Inválidos, Agentes Pac-Man e Fantasmas),
///    com fallback procedural inteligente para que nenhuma categoria falhe.
/// 3. Espelhamento de Grafo Ontológico (VirtOnto em C#): Indexa nós e arestas da
///    ontologia OWL/SWRL (TBox, ABox, ObjectProperties e regras de inferência DL),
///    permitindo consultas estruturadas de alto desempenho em tempo real.
/// ============================================================================
/// </summary>
[ExecuteAlways]
public class GerenciadorCenarioSemantico : MonoBehaviour
{
    public enum OrigemDadosOntologia
    {
        ProceduralInterno,
        ProtegeWebSocket8085,
        ArquivoJson,
        ArquivoOwl,
        GrafoOntologicoPacMan,
        InjecaoExternaDinamica,
        BackendJavaSpringBoot
    }

    public enum TipoTemaLabirinto
    {
        DarkGridNeon = 0,
        ClassicoVerdeCinza = 1,
        EspecialNatal = 2
    }

    [Header("Tema Visual do Labirinto")]
    [Tooltip("Tema estético ativo do labirinto (Dark Grid Neon, Clássico Verde/Cinza, ou Especial de Natal).")]
    public TipoTemaLabirinto temaAtual = TipoTemaLabirinto.DarkGridNeon;

    [Header("Identidade do Projeto")]
    [Tooltip("Nome oficial do sistema.")]
    public string nomeProjeto = "Pac-Man Semântico (Semantic Pac-Man) - CIn-UFPE";

    [Header("Origem dos Dados Ontológicos")]
    [Tooltip("Define a fonte das classes e instâncias da ontologia.")]
    public OrigemDadosOntologia origemDados = OrigemDadosOntologia.ProceduralInterno;

    [Tooltip("Arquivo JSON de ontologia exportada (utilizado quando origem for ArquivoJson).")]
    public TextAsset arquivoOntologiaJson;

    [Tooltip("Arquivo OWL (RDF/XML) exportado diretamente do Protégé (utilizado quando origem for ArquivoOwl).")]
    public TextAsset arquivoOntologiaOwl;

    [Header("Superfície e Assentamento no Chão (Plane)")]
    [Tooltip("Referência explícita ao GameObject do Plane/Piso. Se nulo, localiza automaticamente na cena.")]
    public Transform planoChao;

    [Tooltip("Garante que a base visual/física do modelo assente perfeitamente sobre a superfície do Plane, eliminando flutuação indesejada.")]
    public bool assentarPerfeitamenteNoChao = true;

    [Tooltip("Altura adicional das Gemas Válidas acima da superfície (0 = base perfeitamente tangenciando o chão).")]
    public float alturaGemas = 0.0f;

    [Tooltip("Altura adicional dos Obstáculos acima da superfície (0 = base perfeitamente apoiada no chão).")]
    public float alturaObstaculos = 0.0f;

    [Tooltip("Distância mínima entre instâncias para evitar sobreposição.")]
    public float distanciaMinimaEntreInstancias = 1.8f;

    [Header("Geração Procedural")]
    [Tooltip("Quantidade de instâncias válidas (Gemas que satisfazem regras de Description Logic).")]
    [Range(1, 100)]
    public int quantidadeValidos = 12;

    [Tooltip("Quantidade de instâncias inválidas (Obstáculos / Violações Semânticas).")]
    [Range(0, 50)]
    public int quantidadeInvalidos = 6;

    [Tooltip("Raio de distribuição dos elementos a partir do centro da arena.")]
    public float raioEspalhamento = 14f;

    [Header("Prefabs Semânticos (Carregamento e Validação Robusta)")]
    [Tooltip("Prefab do modelo de gema válida (ex: GemaValida / PacDot).")]
    public GameObject prefabGemaValida;

    [Tooltip("Prefab do modelo de obstáculo inválido (ex: ObstaculoInvalido / Fantasma Blinky).")]
    public GameObject prefabObstaculoInvalido;

    [Tooltip("Prefab opcional do agente jogador (Pac-Man).")]
    public GameObject prefabAgentePacMan;

    [Tooltip("Prefab opcional para agentes fantasmas específicos.")]
    public GameObject prefabAgenteFantasma;

    [Tooltip("Prefab opcional para gemas especiais (Power Pellet).")]
    public GameObject prefabGemaEspecial;

    [Header("Estética Visual")]
    [Tooltip("Se verdadeiro, aplica a textura wireframe de grade emissiva Dark Grid. Se falso, mantém paredes e chão limpos e sólidos.")]
    public bool usarEsteticaDarkGrid = true;

    [Header("Wayfinding Semântico (Placas de Sinalização TBox)")]
    [Tooltip("Ativa a detecção de bifurcações/entradas de corredor e instanciação de Placas de Sinalização Semântica.")]
    public bool instanciarPlacasSinalizacao = true;

    [Tooltip("Prefab da Placa de Sinalização Semântica (Poste / Painel Holográfico com TextMeshPro).")]
    public GameObject prefabPlacaSinalizacao;

    [System.Serializable]
    public struct MapeamentoClassePrefab
    {
        public string nomeClasseOntologica;
        public GameObject prefabCorrespondente;

        public MapeamentoClassePrefab(string nome, GameObject prefab)
        {
            nomeClasseOntologica = nome;
            prefabCorrespondente = prefab;
        }
    }

    [Tooltip("Mapeamentos personalizados entre classes da ontologia OWL e modelos 3D específicos.")]
    public List<MapeamentoClassePrefab> mapeamentoClassesCustomizadas = new List<MapeamentoClassePrefab>();

    [Header("Raciocínio Ontológico (SWRL Engine)")]
    [Tooltip("Motor de Raciocínio Semântico que avalia regras SWRL e gere comportamentos dos agentes.")]
    public MotorRaciocinioSemantico motorRaciocinio;

    [Header("Conexão com Backend Java / Protégé (WebSocket 8085 & REST)")]
    [Tooltip("Cliente WebSocket nativo para o plugin Java/OSGi no Protégé (porta 8085).")]
    public ProtegeWebSocketBridge protegeWebSocket;

    [Tooltip("Cliente de rede REST opcional.")]
    public ApiClient apiClient;

    [Header("Labirinto 3D (Paredes Físicas e Delimitação)")]
    [Tooltip("Gera as paredes físicas perimetrais e internas do labirinto.")]
    public bool instanciarLabirinto3D = true;

    [Tooltip("Altura das paredes 3D do labirinto (mais alta para imersão total nos corredores).")]
    public float alturaParedes = 2.60f;

    [Header("Visibilidade de Entidades (Fase de Validação de Navegação)")]
    [Tooltip("Se falso, oculta/não instancia gemas, power pellets e cerejas temporariamente.")]
    public bool instanciarGemas = true;

    [Tooltip("Se falso, oculta/não instancia fantasmas e obstáculos temporariamente.")]
    public bool instanciarFantasmas = true;
    // Spawns dos 4 Fantasmas estritamente dentro da Gaiola Central do Labirinto (Y = 0.0f Snap no Chao)
    private int m_ContadorSpawnFantasmas = 0;
    private int m_ContadorFantasmasConfigurados = 0;
    public static readonly Vector3[] s_SpawnsGaiolaCentral = new Vector3[]
    {
        new Vector3(0.0f, 0.0f, 0.6f),    // Blinky (Vermelho) - Slot Norte (Porta)
        new Vector3(-1.2f, 0.0f, -0.4f),  // Pinky (Rosa) - Slot Oeste
        new Vector3(0.0f, 0.0f, -0.8f),   // Inky (Ciano) - Slot Sul Central
        new Vector3(1.2f, 0.0f, -0.4f)    // Clyde (Laranja) - Slot Leste
    };

    public static readonly float[] s_AtrasosSaidaGaiola = new float[]
    {
        0.0f,  // Blinky - sai imediatamente ao começar
        4.0f,  // Pinky - sai após 4s
        8.0f,  // Inky - sai após 8s
        12.0f  // Clyde - sai após 12s
    };

    public static readonly Vector3 s_PosicaoSaidaGaiola = new Vector3(0f, 0f, 1.8f);

    // Cores canônicas vibrantes dos 4 Fantasmas clássicos (Vermelho, Rosa, Azul, Laranja)
    public static readonly Color[] CoresClassicasFantasmas = new Color[]
    {
        new Color(1.0f, 0.10f, 0.12f, 1f), // Blinky - Vermelho
        new Color(1.0f, 0.45f, 0.75f, 1f), // Pinky - Rosa
        new Color(0.0f, 0.85f, 1.0f, 1f),  // Inky - Ciano
        new Color(1.0f, 0.55f, 0.05f, 1f)  // Clyde - Laranja
    };

    public static readonly string[] NomesFantasmasClassicos = new string[]
    {
        "Blinky_Vermelho",
        "Pinky_Rosa",
        "Inky_Azul",
        "Clyde_Laranja"
    };


    [Tooltip("Espessura das paredes.")]
    public float espessuraParedes = 1.2f;

    [Tooltip("Dimensão total do labirinto em metros (X e Z).")]
    public float tamanhoLabirinto = 28f;

    [Tooltip("Material customizado para as paredes (se nulo, aplica Topologia Semântica por Superclasse).")]
    public Material materialParedes;

    [System.Serializable]
    public struct MapeamentoCorSuperclasse
    {
        public string nomeSuperclasse;
        public Color corMaterialRegiao;

        public MapeamentoCorSuperclasse(string superclasse, Color cor)
        {
            nomeSuperclasse = superclasse;
            corMaterialRegiao = cor;
        }
    }

    [Header("Topologia Semântica (Cores do Labirinto por Superclasse OWL)")]
    [Tooltip("Associa cores de materiais às paredes/regiões da NavMesh Surface conforme a Superclasse ontológica.")]
    public bool usarTopologiaSemanticaPorSuperclasse = true;

    [Tooltip("Cor da região da Superclasse 'Parasitas' (Nematoda / Platyhelminthes / Protozoa) -> Roxo.")]
    public Color corSuperclasseParasitas = new Color(0.55f, 0.16f, 0.90f, 1f);

    [Tooltip("Cor da região da Superclasse 'Hospedeiros' (Host / Human / Vertebrate) -> Azul.")]
    public Color corSuperclasseHospedeiros = new Color(0.10f, 0.45f, 0.95f, 1f);

    [Tooltip("Tabela extensível de mapeamento Superclasse OWL -> Cor de Material da Região.")]
    public List<MapeamentoCorSuperclasse> mapeamentoCoresSuperclasse = new List<MapeamentoCorSuperclasse>()
    {
        new MapeamentoCorSuperclasse("Parasitas", new Color(0.55f, 0.16f, 0.90f, 1f)),
        new MapeamentoCorSuperclasse("Hospedeiros", new Color(0.10f, 0.45f, 0.95f, 1f))
    };

    private GameObject m_ContainerParedes;
        // Contêineres de decorações perimétricas 360° por tema
    private GameObject m_TotemCInPortao;
    private GameObject m_ContainerDecoracoesGrid;
    private GameObject m_PrefabPacmanGhost;
    private GameObject m_PrefabArcade;
    private GameObject m_ContainerDecoracoesClassico;
    private GameObject m_ContainerDecoracoesNatal;

    [Header("Status do Grafo Ontológico (VirtOnto C#)")]
    [SerializeField] private string m_NomeOntologiaAtiva = "Pac-Man Semantic Ontology";
    [SerializeField] private string m_VersaoAxiomas = "ALCQ(D)";
    [SerializeField] private int m_TotalInstanciasInstanciadas = 0;
    [SerializeField] private int m_TotalNosGrafo = 0;
    [SerializeField] private int m_TotalArestasGrafo = 0;

    public static GerenciadorCenarioSemantico Instancia { get; private set; }

    // Missão sequencial de orbes ontológicas (1 de cada vez, spawn aleatório nos corredores)
    private readonly List<Node> m_FilaNodosOrbes = new List<Node>();
    private int m_IndiceOrbeAtual = 0;
    private int m_TotalOrbesSequencia = 12;
    private GameObject m_OrbeAtivaAtual = null;

    // Coordenadas verificadas de corredores navegáveis do labirinto clássico
    public static readonly Vector2[] s_CorredoresLabirinto = new Vector2[]
    {
        new Vector2(0f, 3.5f),
        new Vector2(0f, 7.0f),
        new Vector2(0f, -7.0f),
        new Vector2(-5.5f, 0f),
        new Vector2(5.5f, 0f),
        new Vector2(-5.5f, 3.5f),
        new Vector2(5.5f, 3.5f),
        new Vector2(-5.5f, -4.0f),
        new Vector2(5.5f, -4.0f),
        new Vector2(-8.5f, 3.5f),
        new Vector2(8.5f, 3.5f),
        new Vector2(-8.5f, -3.5f),
        new Vector2(8.5f, -3.5f),
        new Vector2(-3.5f, 8.5f),
        new Vector2(3.5f, 8.5f),
        new Vector2(-3.5f, -8.5f),
        new Vector2(3.5f, -8.5f)
    };

    // Coleções ativas do labirinto
    private readonly List<IInstanciaSemantica> m_InstanciasSemanticas = new List<IInstanciaSemantica>();
    private readonly List<GameObject> m_ObjetosInstanciados = new List<GameObject>();
    private readonly List<Vector3> m_PosicoesOcupadas = new List<Vector3>();
    private readonly List<PlacaSinalizacaoSemantica> m_PlacasWayfinding = new List<PlacaSinalizacaoSemantica>();

    // Taxonomia dinâmica agnóstica de domínio (alimentada pelo Grafo Ontológico ou WebSocket 8085)
    private string[] m_SuperclassesAtivas = new string[] { "Parasitas", "Hospedeiros" };
    private string[] m_SubclassesAtivas = new string[] { "Nematoda", "Platyhelminthes", "Human", "SmallIntestine", "FecalOral", "AntiparasiticTreatment" };

    // Grafo Ontológico VirtOnto em C#
    public Graph GrafoOntologico { get; private set; } = new Graph();

    // Cache interno de materiais procedurais de contingência
    private Material m_MaterialGemaProcedural;
    private Material m_MaterialObstaculoProcedural;

    /// <summary>
    /// Lista pública somente-leitura de todas as instâncias ontológicas ativas no labirinto.
    /// </summary>
    public IReadOnlyList<IInstanciaSemantica> InstanciasAtivas => m_InstanciasSemanticas;

    public NavMeshSurface GarantirNavMeshSurface()
    {
        NavMeshSurface surface = GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            surface = gameObject.AddComponent<NavMeshSurface>();
        }
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        return surface;
    }

    private void Awake()
    {
        Instancia = this;
        alturaParedes = 2.60f;
        usarEsteticaDarkGrid = true;
        instanciarPlacasSinalizacao = true;
        instanciarGemas = true;
        instanciarFantasmas = true;

        CarregarPrefabsAutomaticamente();
        LocalizarPlanoChao();
        GarantirNavMeshSurface();
        InicializarMotorRaciocinio();
        InicializarApiClient();
        GarantirPainelGrafo();
    }

    private void GarantirPainelGrafo()
    {
        if (PainelGrafoSemanticoProtege.Instancia == null)
        {
            var p = FindAnyObjectByType<PainelGrafoSemanticoProtege>();
            if (p == null)
            {
                GameObject obj = new GameObject("PainelGrafoSemanticoProtege");
                obj.AddComponent<PainelGrafoSemanticoProtege>();
            }
        }
    }

    private void OnEnable()
    {
        ProtegeWebSocketBridge.OnTaxonomyReceived += AtualizarPlacasWayfindingTaxonomia;
        ConfigurarIluminacaoSciFi();
        LocalizarPlanoChao();
    }

    private void OnDisable()
    {
        ProtegeWebSocketBridge.OnTaxonomyReceived -= AtualizarPlacasWayfindingTaxonomia;
    }

    public void InicializarMotorRaciocinio()
    {
        if (motorRaciocinio == null)
        {
            motorRaciocinio = GetComponent<MotorRaciocinioSemantico>() ?? FindAnyObjectByType<MotorRaciocinioSemantico>();
            if (motorRaciocinio == null)
            {
                motorRaciocinio = gameObject.AddComponent<MotorRaciocinioSemantico>();
            }
        }

        if (motorRaciocinio != null)
        {
            motorRaciocinio.gerenciadorCenario = this;
        }
    }

    public void InicializarApiClient()
    {
        if (apiClient == null)
        {
            apiClient = GetComponent<ApiClient>() ?? FindAnyObjectByType<ApiClient>();
            if (apiClient == null)
            {
                apiClient = gameObject.AddComponent<ApiClient>();
            }
        }

        if (apiClient != null)
        {
            apiClient.gerenciadorCenario = this;
        }
    }

    private void Start()
    {
        alturaParedes = 2.60f;
        usarEsteticaDarkGrid = true;
        instanciarPlacasSinalizacao = true;
        instanciarGemas = true;
        instanciarFantasmas = true;

        ConfigurarIluminacaoSciFi();
        GerarInstanciasOntologicas();
        AplicarTemaLabirinto(temaAtual);
    }

    /// <summary>
    /// Oculta temporariamente gemas, power pellets, cerejas, fantasmas e placas
    /// para foco exclusivo na validação de mapa e movimentação.
    /// </summary>
    public void OcultarEntidadesTemporarias()
    {
        if (!instanciarGemas)
        {
            var gemas = FindObjectsByType<GemaValidaSemantica>(FindObjectsInactive.Include);
            for (int i = 0; i < gemas.Length; i++)
            {
                if (gemas[i] != null) gemas[i].gameObject.SetActive(false);
            }
            var pellets = FindObjectsByType<PowerPelletIndividualSemantico>(FindObjectsInactive.Include);
            for (int i = 0; i < pellets.Length; i++)
            {
                if (pellets[i] != null) pellets[i].gameObject.SetActive(false);
            }
            var cerejas = FindObjectsByType<CerejaObjetoSemantico>(FindObjectsInactive.Include);
            for (int i = 0; i < cerejas.Length; i++)
            {
                if (cerejas[i] != null) cerejas[i].gameObject.SetActive(false);
            }
            var fugitivas = FindObjectsByType<GemaFugitiva>(FindObjectsInactive.Include);
            for (int i = 0; i < fugitivas.Length; i++)
            {
                if (fugitivas[i] != null) fugitivas[i].gameObject.SetActive(false);
            }
        }

        if (!instanciarFantasmas)
        {
            var obstaculos = FindObjectsByType<ObstaculoInvalidoSemantico>(FindObjectsInactive.Include);
            for (int i = 0; i < obstaculos.Length; i++)
            {
                if (obstaculos[i] != null) obstaculos[i].gameObject.SetActive(false);
            }
            var animFantasmas = FindObjectsByType<AnimacaoFantasma>(FindObjectsInactive.Include);
            for (int i = 0; i < animFantasmas.Length; i++)
            {
                if (animFantasmas[i] != null) animFantasmas[i].gameObject.SetActive(false);
            }
        }

        if (!instanciarPlacasSinalizacao)
        {
            var placas = FindObjectsByType<PlacaSinalizacaoSemantica>(FindObjectsInactive.Include);
            for (int i = 0; i < placas.Length; i++)
            {
                if (placas[i] != null) placas[i].gameObject.SetActive(false);
            }
        }
    }

#if UNITY_EDITOR



    private void OnValidate()
    {
        CarregarPrefabsAutomaticamente();
        if (planoChao == null)
        {
            LocalizarPlanoChao();
        }
    }
#endif

    public void LocalizarPlanoChao()
    {
        if (planoChao == null)
        {
            GameObject planeObj = GameObject.Find("Plane") ?? GameObject.Find("Floor") ?? GameObject.Find("Chao") ?? GameObject.Find("Piso");
            if (planeObj != null)
            {
                planoChao = planeObj.transform;
            }
        }

        if (planoChao != null)
        {
            AplicarMaterialChaoLaboratorio();
        }
    }

    /// <summary>
    /// Aplica o material texturizado de laboratório limpo e reflexivo de alta fidelidade ao chão.
    /// </summary>
    public void AplicarMaterialChaoLaboratorio()
    {
        if (planoChao == null) return;

        Renderer rendChao = planoChao.GetComponent<Renderer>();
        if (rendChao != null)
        {
            Material matChao = CriarMaterialChaoLaboratorio();
            rendChao.material = matChao;
            rendChao.receiveShadows = true;
        }
    }

    // =========================================================================
    // 1. GERAÇÃO E CONSUMO DO CENÁRIO ONTOLÓGICO
    // =========================================================================

    /// <summary>
    /// Ponto de entrada principal: Gera o labirinto semântico conforme a origem de dados configurada.
    /// </summary>
    [ContextMenu("Pac-Man Semântico/Gerar Cenário Ontológico")]
    public void GerarInstanciasOntologicas()
    {
        LimparCenarioExistente();
        CarregarPrefabsAutomaticamente();
        LocalizarPlanoChao();
        GarantirNavMeshSurface();

        // 0. GARANTIA ABSOLUTA DE INSTANCIAÇÃO DAS PAREDES DO LABIRINTO 3D
        if (instanciarLabirinto3D)
        {
            ConstruirLabirinto3D();
        }

        CenarioSemanticoDTO cenario = null;

        switch (origemDados)
        {
            case OrigemDadosOntologia.GrafoOntologicoPacMan:
                Debug.Log("<color=#00DDFF><b>[Pac-Man Semântico C#]</b></color> Construindo e consumindo Grafo Ontológico VirtOnto...");
                GrafoOntologico = ConstruirGrafoOntologicoPadrao();
                cenario = GrafoOntologico.ToCenarioDTO();
                break;

            case OrigemDadosOntologia.ProtegeWebSocket8085:
                Debug.Log("<color=#00DDFF><b>[Interface Espacial OWL - Protégé WS:8085]</b></color> Conectando ao servidor WebSocket Java/OSGi no Protégé...");
                if (protegeWebSocket == null)
                {
                    protegeWebSocket = GetComponent<ProtegeWebSocketBridge>() ?? FindFirstObjectByType<ProtegeWebSocketBridge>();
                    if (protegeWebSocket == null)
                    {
                        protegeWebSocket = gameObject.AddComponent<ProtegeWebSocketBridge>();
                    }
                }
                cenario = BackendOntologiaBridge.GerarCenarioOntologiaPadrao(quantidadeValidos, quantidadeInvalidos, raioEspalhamento, alturaGemas);
                break;

            case OrigemDadosOntologia.ArquivoJson:
                if (arquivoOntologiaJson != null)
                {
                    Debug.Log($"<color=#00DDFF><b>[Pac-Man Semântico - JSON]</b></color> Carregando ontologia do arquivo '{arquivoOntologiaJson.name}'...");
                    cenario = BackendOntologiaBridge.CarregarDeJson(arquivoOntologiaJson.text);
                }
                else
                {
                    Debug.LogWarning("[Pac-Man Semântico] Nenhum arquivo JSON atribuído. Ativando gerador procedural de contingência.");
                    cenario = BackendOntologiaBridge.GerarCenarioOntologiaPadrao(quantidadeValidos, quantidadeInvalidos, raioEspalhamento, alturaGemas);
                }
                break;

            case OrigemDadosOntologia.ArquivoOwl:
                if (arquivoOntologiaOwl != null)
                {
                    Debug.Log($"<color=#00DDFF><b>[Pac-Man Semântico - OWL]</b></color> Carregando ontologia Protégé do arquivo '{arquivoOntologiaOwl.name}'...");
                    cenario = BackendOntologiaBridge.CarregarDeOwl(arquivoOntologiaOwl.text);
                }
                else
                {
                    TextAsset resOwl = Resources.Load<TextAsset>("parasites.owl") ?? Resources.Load<TextAsset>("parasites");
                    if (resOwl != null)
                    {
                        Debug.Log($"<color=#00DDFF><b>[Pac-Man Semântico - OWL]</b></color> Carregando ontologia Protégé de Resources/parasites.owl...");
                        cenario = BackendOntologiaBridge.CarregarDeOwl(resOwl.text);
                    }
                    else
                    {
                        cenario = BackendOntologiaBridge.GerarCenarioOntologiaPadrao(quantidadeValidos, quantidadeInvalidos, raioEspalhamento, alturaGemas);
                    }
                }
                break;

            case OrigemDadosOntologia.InjecaoExternaDinamica:
                Debug.Log("[Pac-Man Semântico] Modo de Injeção Dinâmica Ativo. Aguardando chamada de CarregarCenarioDeDados().");
                return;

            case OrigemDadosOntologia.BackendJavaSpringBoot:
                Debug.Log("<color=#00DDFF><b>[Pac-Man Semântico - Backend Java Spring Boot]</b></color> Consultando microsserviço REST em " + (apiClient != null ? apiClient.baseUrl : "http://localhost:8080/api/ontology") + "...");
                InicializarApiClient();
                if (apiClient != null)
                {
                    apiClient.SincronizarCenarioComBackend();
                }
                return;

            case OrigemDadosOntologia.ProceduralInterno:
            default:
                Debug.Log("<color=#00FF99><b>[Pac-Man Semântico - Procedural]</b></color> Gerando instâncias ontológicas Description Logic...");
                cenario = BackendOntologiaBridge.GerarCenarioOntologiaPadrao(quantidadeValidos, quantidadeInvalidos, raioEspalhamento, alturaGemas);
                break;
        }

        if (cenario != null)
        {
            CarregarCenarioDeDados(cenario);
        }
    }

    /// <summary>
    /// API Modular orientada a objetos: Constrói o Grafo Ontológico e instancia o cenário físico no Unity.
    /// </summary>
    public void CarregarCenarioDeDados(CenarioSemanticoDTO cenario)
    {
        if (cenario == null)
        {
            Debug.LogError("[Pac-Man Semântico] Erro: Tentativa de carregar cenário ontológico nulo.");
            return;
        }

        LimparCenarioExistente();

        m_NomeOntologiaAtiva = string.IsNullOrEmpty(cenario.nomeOntologia) ? "Pac-Man Ontology" : cenario.nomeOntologia;
        m_VersaoAxiomas = string.IsNullOrEmpty(cenario.versaoAxiomas) ? "v1.0" : cenario.versaoAxiomas;

        PrepararMateriaisProcedurais();
        LocalizarPlanoChao();
        ConfigurarIluminacaoSciFi();

        // 0. Instanciação física do Labirinto 3D (Paredes delimitadoras)
        if (instanciarLabirinto3D)
        {
            ConstruirLabirinto3D();
        }

        // 0.1 BAKE DINÂMICO DO NAVMESH EM TEMPO DE EXECUÇÃO
        // Chamado exatamente após todas as paredes e chão serem instanciados, mas antes de instanciar ou habilitar os NavMeshAgents (Fantasmas).
        NavMeshSurface navSurface = GetComponent<NavMeshSurface>();
        if (navSurface == null)
        {
            navSurface = gameObject.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.All;
        navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        }
        navSurface.BuildNavMesh();
        Debug.Log("<color=#00FF99><b>[NavMesh Dinâmico]</b></color> Malha de navegação gerada em tempo de execução com BuildNavMesh() após instanciação de paredes e chão.");

        // 1. Espelhamento formal: Constrói e indexa o Grafo Ontológico a partir do DTO
        GrafoOntologico.BuildFromCenarioDTO(cenario);
        m_TotalNosGrafo = GrafoOntologico.GetNodeCount();
        m_TotalArestasGrafo = GrafoOntologico.GetEdgeCount();

        // 1.1 Extrai a taxonomia TBox ativa (agnóstica de domínio) e atualiza as Placas de Sinalização Semântica
        ExtrairTaxonomiaDoGrafoAtivo();

        int contadorValidos = 0;
        int contadorInvalidos = 0;

        // 2. Separação de Nodos: Fantasmas são instanciados na gaiola; Orbes são enfileiradas para spawn sequencial (1 a 1)
        m_FilaNodosOrbes.Clear();
        var nosIndividuos = GrafoOntologico.GetNodesByType(NodeType.INDIVIDUAL);
        foreach (var node in nosIndividuos)
        {
            if (node == null) continue;
            if (node.IsValid)
            {
                if (instanciarGemas)
                {
                    m_FilaNodosOrbes.Add(node);
                }
            }
            else if (instanciarFantasmas)
            {
                DadoInstanciaSemantica dado = new DadoInstanciaSemantica
                {
                    id = node.Id,
                    classeOntologica = string.IsNullOrEmpty(node.ConceptClass) ? node.Label : node.ConceptClass,
                    expressaoDL = node.DLExpression,
                    ehValida = false,
                    posicao = node.Position,
                    valorPontuacao = node.ScoreValue,
                    descricaoSemantica = node.SemanticDescription,
                    corDestaque = node.HighlightColor
                };

                GameObject objInstanciado = InstanciarObjetoSemantico(dado);
                if (objInstanciado != null)
                {
                    m_ObjetosInstanciados.Add(objInstanciado);
                    node.GameObjectInstance = objInstanciado;
                    node.Position = objInstanciado.transform.position;

                    IInstanciaSemantica instSemantica = objInstanciado.GetComponent<IInstanciaSemantica>();
                    if (instSemantica != null)
                    {
                        m_InstanciasSemanticas.Add(instSemantica);
                    }
                    contadorInvalidos++;
                }
            }
        }

        m_TotalOrbesSequencia = m_FilaNodosOrbes.Count;
        InstanciaSemanticaBase.instanciasValidasRestantes = m_TotalOrbesSequencia;
        m_IndiceOrbeAtual = 0;

        if (instanciarGemas && m_FilaNodosOrbes.Count > 0)
        {
            SpawnarOrbeAtual();
            contadorValidos = 1;
        }

        InstanciarCerejasEPowerPellets();
        m_TotalInstanciasInstanciadas = m_ObjetosInstanciados.Count;

        // 2.1 Garante ponte WebSocket ativa com o plugin Java/OSGi do Protégé (porta 8085)
        if (protegeWebSocket == null)
        {
            protegeWebSocket = GetComponent<ProtegeWebSocketBridge>() ?? FindFirstObjectByType<ProtegeWebSocketBridge>();
            if (protegeWebSocket == null && Application.isPlaying)
            {
                protegeWebSocket = gameObject.AddComponent<ProtegeWebSocketBridge>();
            }
        }

        // 3. Avaliação inicial de regras SWRL sobre o grafo instanciado
        GrafoOntologico.AvaliarRegrasSWRL(msg => Debug.Log($"<color=#9B59B6><b>[Pac-Man Semântico SWRL]</b></color> {msg}"));

        // 4. Conecta e sincroniza o Grafo Ontológico com o Motor de Raciocínio Semântico
        if (motorRaciocinio != null)
        {
            motorRaciocinio.SetSemanticGraph(GrafoOntologico);
        }

        ConfigurarIluminacaoSciFi();
        AplicarMaterialChaoLaboratorio();

        Debug.Log($"<color=#00FF99><b>[Pac-Man Semântico CIn-UFPE]</b></color> Cenário '<b>{m_NomeOntologiaAtiva}</b>' ({m_VersaoAxiomas}) instanciado com sucesso!\n" +
                  $"• Instâncias Físicas: {m_TotalInstanciasInstanciadas} ({contadorValidos} Gemas Válidas / {contadorInvalidos} Obstáculos/Fantasmas)\n" +
                  $"• Grafo Ontológico: {m_TotalNosGrafo} Nós (Classes TBox + Indivíduos ABox) / {m_TotalArestasGrafo} Arestas (OWL/SWRL).");
    }

    /// <summary>
    /// Spawna a orbe ontológica da etapa atual da missão sequencial.
    /// Spawna 1 orbe por vez em um corredor aleatório do labirinto afastado do Pac-Man.
    /// Aplica a regra: estritamente 1 a cada 3 orbes concede o poder de devorar fantasmas!
    /// </summary>
    public void SpawnarOrbeAtual()
    {
        if (m_IndiceOrbeAtual >= m_FilaNodosOrbes.Count)
        {
            Debug.Log("<color=#00FF99><b>[VITÓRIA COMPLETA]</b></color> Todas as 12 orbes foram coletadas com sucesso!");
            if (HubInicialAmbiente.Instancia != null)
            {
                HubInicialAmbiente.Instancia.ExibirVitoria(InstanciaSemanticaBase.pontuacaoLogica, m_TotalOrbesSequencia);
            }
            if (PainelGrafoSemanticoProtege.Instancia != null)
            {
                PainelGrafoSemanticoProtege.Instancia.ExibirFluxogramaCompletoVitoria();
            }
            return;
        }

        Node node = m_FilaNodosOrbes[m_IndiceOrbeAtual];
        bool ehOrbeDePoder = ((m_IndiceOrbeAtual + 1) % 3 == 0);

        // Seleciona um corredor seguro aleatório a pelo menos 5 metros do Pac-Man
        Vector3 posPac = new Vector3(0f, 0f, -5f);
        var ctrl = FindFirstObjectByType<ControladorXRJogador>();
        if (ctrl != null) posPac = ctrl.transform.position;

        List<Vector2> corredoresCandidatos = new List<Vector2>();
        for (int i = 0; i < s_CorredoresLabirinto.Length; i++)
        {
            Vector2 c = s_CorredoresLabirinto[i];
            float dist = Vector2.Distance(new Vector2(posPac.x, posPac.z), c);
            if (dist >= 4.8f)
            {
                corredoresCandidatos.Add(c);
            }
        }
        if (corredoresCandidatos.Count == 0)
        {
            corredoresCandidatos.AddRange(s_CorredoresLabirinto);
        }

        Vector2 corredorEscolhido = corredoresCandidatos[UnityEngine.Random.Range(0, corredoresCandidatos.Count)];
        Vector3 posSpawn = new Vector3(corredorEscolhido.x, 0.42f, corredorEscolhido.y);

        DadoInstanciaSemantica dado = new DadoInstanciaSemantica
        {
            id = node.Id,
            classeOntologica = string.IsNullOrEmpty(node.ConceptClass) ? node.Label : node.ConceptClass,
            expressaoDL = node.DLExpression,
            ehValida = true,
            posicao = posSpawn,
            valorPontuacao = ehOrbeDePoder ? 50 : 20,
            descricaoSemantica = node.SemanticDescription,
            corDestaque = ehOrbeDePoder ? new Color(1.0f, 0.85f, 0.0f, 1.0f) : new Color(0.0f, 0.90f, 1.0f, 0.8f)
        };

        GameObject objInstanciado = InstanciarObjetoSemantico(dado);
        if (objInstanciado != null)
        {
            objInstanciado.transform.position = posSpawn;
            m_OrbeAtivaAtual = objInstanciado;
            m_ObjetosInstanciados.Add(objInstanciado);
            node.GameObjectInstance = objInstanciado;
            node.Position = posSpawn;

            var gema = objInstanciado.GetComponent<GemaValidaSemantica>();
            if (gema != null)
            {
                gema.ehOrbeDePoder = ehOrbeDePoder;
                gema.indiceOrbe = m_IndiceOrbeAtual + 1;
                gema.totalOrbes = m_TotalOrbesSequencia;
                gema.raioCaptura = 0.35f;

                if (ehOrbeDePoder)
                {
                    // Visual radiante dourado / aura especial de poder
                    var rend = objInstanciado.GetComponentInChildren<Renderer>();
                    if (rend != null)
                    {
                        rend.material = CriarMaterialTranslúcidoNodo(new Color(1.0f, 0.82f, 0.0f, 1.0f), 0.92f);
                    }
                    var luz = objInstanciado.GetComponentInChildren<Light>();
                    if (luz != null)
                    {
                        luz.color = new Color(1.0f, 0.85f, 0.0f);
                        luz.intensity = 4.0f;
                        luz.range = 4.5f;
                    }
                }
            }

            IInstanciaSemantica instSemantica = objInstanciado.GetComponent<IInstanciaSemantica>();
            if (instSemantica != null)
            {
                m_InstanciasSemanticas.Add(instSemantica);
            }

            if (PainelGrafoSemanticoProtege.Instancia != null)
            {
                string label = string.IsNullOrEmpty(node.Label) ? node.Id : node.Label;
                PainelGrafoSemanticoProtege.Instancia.DefinirAlvoOrbe(objInstanciado.transform, m_IndiceOrbeAtual + 1, m_TotalOrbesSequencia, ehOrbeDePoder, label);
            }

            Debug.Log($"<color=#00E5FF><b>[Missão Sequencial]</b></color> Orbe #{m_IndiceOrbeAtual + 1} / {m_TotalOrbesSequencia} spawnada em {posSpawn} (Orbe de Poder: {ehOrbeDePoder})");
        }
    }

    /// <summary>
    /// Notificação recebida ao capturar uma orbe ontológica válida: avança para a próxima da lista!
    /// </summary>
    public void NotificarOrbeColetada(GemaValidaSemantica gema)
    {
        m_IndiceOrbeAtual++;
        SpawnarOrbeAtual();
    }

    // =========================================================================
    // 2. CÁLCULO DE SPAWN E ASSENTAMENTO PERFEITO SOBRE O CHÃO (PLANE)
    // =========================================================================

    private GameObject InstanciarObjetoSemantico(DadoInstanciaSemantica dado)
    {
        // 1. Obtém o prefab validado ou determina o tipo para fallback
        GameObject prefabEscolhido = ObterPrefabParaClasse(dado.classeOntologica, dado.ehValida);

        // 2. Determina a coordenada horizontal (X, Z) garantindo que esteja estritamente livre de paredes
        bool ehFantasma = !dado.ehValida ||
                          dado.classeOntologica.IndexOf("fantasma", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          dado.classeOntologica.IndexOf("ghost", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          dado.classeOntologica.IndexOf("invalido", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          dado.classeOntologica.IndexOf("violacao", StringComparison.OrdinalIgnoreCase) >= 0;

        Vector3 posicaoPlana;
        if (ehFantasma)
        {
            // FANTASMAS (Obstáculos Inválidos): Nascem ESTRITAMENTE no interior da gaiola central do labirinto
            posicaoPlana = ObterPosicaoValidaNoChao(false);
            posicaoPlana.x = Mathf.Clamp(posicaoPlana.x, -2.2f, 2.2f);
            posicaoPlana.z = Mathf.Clamp(posicaoPlana.z, -2.0f, 1.5f);
        }
        else if (dado.posicao != Vector3.zero)
        {
            posicaoPlana = dado.posicao;
        }
        else
        {
            posicaoPlana = ObterPosicaoValidaNoChao(true);
        }

        m_PosicoesOcupadas.Add(posicaoPlana);

        // Orientação inicial vertical sem inclinações que causem corte no plano
        Quaternion rotacaoFinal = dado.rotacao != Quaternion.identity ? dado.rotacao : Quaternion.identity;

        // 3. Instanciação com salvaguarda contra referências quebradas
        GameObject instanciaObj = null;
        if (IsPrefabValid(prefabEscolhido))
        {
            try
            {
                instanciaObj = Instantiate(prefabEscolhido, posicaoPlana, rotacaoFinal, transform);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Pac-Man Semântico] Erro ao instanciar prefab para '{dado.classeOntologica}': {ex.Message}. Ativando fallback procedural.");
                instanciaObj = CriarProceduralPorClasse(posicaoPlana, dado);
            }
        }
        else
        {
            instanciaObj = CriarProceduralPorClasse(posicaoPlana, dado);
        }

        instanciaObj.name = $"Instancia_{dado.classeOntologica}_{dado.id}";

        if (dado.escala != Vector3.one && dado.escala != Vector3.zero)
        {
            instanciaObj.transform.localScale = dado.escala;
        }

        ConfigurarComponentesSemanticos(instanciaObj, dado);

        // 4. CÁLCULO DE ASSENTAMENTO PERFEITO NO PLANO (PLANE)
        // Detecta a superfície física real do Plane via Raycast ou referência explícita
        float chaoY = ObterAlturaSuperficieChao(posicaoPlana);

        // Sincroniza transformações para que o cálculo de Bounds seja perfeitamente exato
        Physics.SyncTransforms();

        float distanciaPivotParaBase = CalcularDistanciaPivotParaBase(instanciaObj);

        // Aplica o Y final: superfície do chão + distância até a base do modelo + offset opcional
        float offsetVerticalInstancia = dado.ehValida ? alturaGemas : alturaObstaculos;
        // Para fantasmas (NavMeshAgent), a coordenada Y DEVE ser exatamente a altura do chão (chaoY) para snap automático no NavMesh
        float yFinal = dado.ehValida ? (chaoY + distanciaPivotParaBase + (assentarPerfeitamenteNoChao ? 0f : offsetVerticalInstancia)) : chaoY;

        Vector3 posFinalAssentada = new Vector3(posicaoPlana.x, yFinal, posicaoPlana.z);
        instanciaObj.transform.position = posFinalAssentada;

        // Snap seguro do NavMeshAgent na malha de navegação para Fantasmas (Snap to Ground sem erro de Resume)
        if (ehFantasma)
        {
            UnityEngine.AI.NavMeshAgent agent = instanciaObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.baseOffset = 0f;
                agent.enabled = false;

                // Amostragem restrita de 1.2m para evitar snap em corredores externos do outro lado da parede da gaiola
                if (UnityEngine.AI.NavMesh.SamplePosition(posFinalAssentada, out UnityEngine.AI.NavMeshHit hit, 1.2f, UnityEngine.AI.NavMesh.AllAreas)
                    && Mathf.Abs(hit.position.x) <= 2.5f && hit.position.z >= -2.2f && hit.position.z <= 2.0f)
                {
                    instanciaObj.transform.position = hit.position;
                    agent.enabled = true;
                    agent.Warp(hit.position);
                }
                else
                {
                    instanciaObj.transform.position = posFinalAssentada;
                    agent.enabled = true;
                    agent.Warp(posFinalAssentada);
                }

                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                }
            }
        }

        // 5. Atualiza os componentes de animação para respeitar o ponto de apoio sem flutuação
        AjustarComportamentosDeFlutuacao(instanciaObj);

        return instanciaObj;
    }

    /// <summary>
    /// Calcula a altura da superfície superior do chão (Plane) na coordenada XZ especificada.
    /// Utiliza Raycast físico de cima para baixo com contingência inteligente para o Transform do Plane.
    /// </summary>
    public float ObterAlturaSuperficieChao(Vector3 posXZ)
    {
        Vector3 origemRaio = new Vector3(posXZ.x, 50f, posXZ.z);
        RaycastHit[] hits = Physics.RaycastAll(origemRaio, Vector3.down, 100f, ~0, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            // Prioriza o chão do labirinto (Plane / Floor / Piso)
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null || h.collider.isTrigger) continue;
                string nome = h.collider.name.ToLower();
                if (nome.Contains("plane") || nome.Contains("chao") || nome.Contains("floor") || nome.Contains("piso") || nome.Contains("ground"))
                {
                    return h.point.y;
                }
            }

            // Segundo filtro: qualquer superfície sólida que não seja o Player ou Fantasma
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null || h.collider.isTrigger) continue;
                if (h.collider.CompareTag("Player") || h.collider.name.Contains("XR") || h.collider.name.Contains("PacMan") || h.collider.name.Contains("Fantasma") || h.collider.name.Contains("Blinky"))
                {
                    continue;
                }
                return h.point.y;
            }
        }

        // Fallback: se o Plane foi identificado, utiliza a superfície superior do seu colisor ou posição
        if (planoChao != null)
        {
            Collider colChao = planoChao.GetComponent<Collider>();
            if (colChao != null)
            {
                return colChao.bounds.max.y;
            }
            return planoChao.position.y;
        }

        return 0f;
    }

    /// <summary>
    /// Calcula dinamicamente a distância vertical entre o Pivot (transform.position)
    /// e a base mais baixa (bounds.min.y) de todas as malhas ou colisores do modelo.
    /// </summary>
    public static float CalcularDistanciaPivotParaBase(GameObject obj)
    {
        if (obj == null) return 0f;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            bool boundsIniciado = false;
            Bounds b = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                {
                    if (!boundsIniciado)
                    {
                        b = renderers[i].bounds;
                        boundsIniciado = true;
                    }
                    else
                    {
                        b.Encapsulate(renderers[i].bounds);
                    }
                }
            }
            if (boundsIniciado)
            {
                return obj.transform.position.y - b.min.y;
            }
        }

        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        if (colliders != null && colliders.Length > 0)
        {
            bool boundsIniciado = false;
            Bounds b = default;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].enabled)
                {
                    if (!boundsIniciado)
                    {
                        b = colliders[i].bounds;
                        boundsIniciado = true;
                    }
                    else
                    {
                        b.Encapsulate(colliders[i].bounds);
                    }
                }
            }
            if (boundsIniciado)
            {
                return obj.transform.position.y - b.min.y;
            }
        }

        return 0f;
    }

    private void AjustarComportamentosDeFlutuacao(GameObject instanciaObj)
    {
        // Gema: Mantém rotação contínua elegante no eixo Y, eliminando oscilações que penetrem o chão
        RotacaoFlutuante rot = instanciaObj.GetComponent<RotacaoFlutuante>();
        if (rot != null)
        {
            rot.permitirFlutuacao = !assentarPerfeitamenteNoChao;
            rot.velocidadeRotacao = new Vector3(0f, 60f, 0f);
            rot.AtualizarPosicaoBase(instanciaObj.transform.localPosition);
        }

        // Fantasma: Desativa oscilação vertical invasiva que faça o corpo cortar o piso
        AnimacaoFantasma anim = instanciaObj.GetComponent<AnimacaoFantasma>();
        if (anim != null)
        {
            anim.permitirFlutuacao = !assentarPerfeitamenteNoChao;
            anim.inclinacaoLateral = 0f;
            anim.AtualizarPosicaoBase(instanciaObj.transform.localPosition);
        }
    }

    /// <summary>
    /// Verifica se uma coordenada 3D está totalmente livre de colisões com paredes do labirinto.
    /// Garante que nenhuma entidade (Gema ou Fantasma) nasça ou seja posicionada sobre paredes.
    /// </summary>
    public bool VerificarPosicaoLivreDeParedes(Vector3 pos, float raioLivre = 0.65f)
    {
        // Limites do labirinto perimetral
        float limite = (tamanhoLabirinto / 2f) - 0.9f;
        if (Mathf.Abs(pos.x - transform.position.x) > limite || Mathf.Abs(pos.z - transform.position.z) > limite)
        {
            return false;
        }

        // Teste de sobreposição com BoxColliders de paredes instanciadas
        if (m_ContainerParedes != null)
        {
            Collider[] hits = Physics.OverlapSphere(pos + Vector3.up * 0.45f, raioLivre, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit != null && (hit.transform.IsChildOf(m_ContainerParedes.transform) || hit.name.StartsWith("Parede_")))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private Vector3 ObterPosicaoValidaNoChao(bool ehGemaValida)
    {
        // Se for Fantasma (Obstaculo Invalido), spawn estritamente dentro da Gaiola Central no nivel exato do chao
        float chaoNivel = ObterAlturaSuperficieChao(Vector3.zero);
        if (!ehGemaValida)
        {
            Vector3 posGaiola = s_SpawnsGaiolaCentral[m_ContadorSpawnFantasmas % s_SpawnsGaiolaCentral.Length];
            m_ContadorSpawnFantasmas++;
            return new Vector3(posGaiola.x, chaoNivel, posGaiola.z);
        }

        float altura = transform.position.y + (ehGemaValida ? alturaGemas : alturaObstaculos);
        Vector3 posicaoCandidata = Vector3.zero;

        // Waypoints principais de corredores do labirinto clássico (100% navegáveis)
        Vector2[] corredoresSeguros = new Vector2[]
        {
            new Vector2(0f, 3.5f),
            new Vector2(0f, -5.5f),
            new Vector2(0f, 7.0f),
            new Vector2(0f, -7.0f),
            new Vector2(-5.5f, 0f),
            new Vector2(5.5f, 0f),
            new Vector2(-5.5f, 3.5f),
            new Vector2(5.5f, 3.5f),
            new Vector2(-5.5f, -4.0f),
            new Vector2(5.5f, -4.0f),
            new Vector2(-8.5f, 3.5f),
            new Vector2(8.5f, 3.5f),
            new Vector2(-8.5f, -3.5f),
            new Vector2(8.5f, -3.5f),
            new Vector2(-3.5f, 8.5f),
            new Vector2(3.5f, 8.5f),
            new Vector2(-3.5f, -8.5f),
            new Vector2(3.5f, -8.5f)
        };

        // 1. Tenta posicionamento ancorado nos corredores seguros navegáveis
        for (int i = 0; i < corredoresSeguros.Length; i++)
        {
            Vector2 baseCorredor = corredoresSeguros[(i + m_PosicoesOcupadas.Count) % corredoresSeguros.Length];
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * 0.7f;
            Vector3 cand = new Vector3(transform.position.x + baseCorredor.x + jitter.x, altura, transform.position.z + baseCorredor.y + jitter.y);

            if (VerificarPosicaoLivreDeParedes(cand, 0.65f))
            {
                bool livre = true;
                foreach (var pos in m_PosicoesOcupadas)
                {
                    if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(cand.x, cand.z)) < distanciaMinimaEntreInstancias)
                    {
                        livre = false;
                        break;
                    }
                }
                if (livre) return cand;
            }
        }

        // 2. Fallback procedural com validação rigorosa de paredes
        for (int i = 0; i < 60; i++)
        {
            Vector2 circulo = UnityEngine.Random.insideUnitCircle * raioEspalhamento;
            if (circulo.magnitude < 2.0f) circulo = circulo.normalized * 2.5f;

            posicaoCandidata = new Vector3(transform.position.x + circulo.x, altura, transform.position.z + circulo.y);

            if (!VerificarPosicaoLivreDeParedes(posicaoCandidata, 0.65f)) continue;

            bool posicaoLivre = true;
            foreach (var pos in m_PosicoesOcupadas)
            {
                if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(posicaoCandidata.x, posicaoCandidata.z)) < (distanciaMinimaEntreInstancias * 0.75f))
                {
                    posicaoLivre = false;
                    break;
                }
            }

            if (posicaoLivre) return posicaoCandidata;
        }

        return posicaoCandidata;
    }

    // =========================================================================
    // 3. SALVAGUARDA E RESOLUÇÃO ROBUSTA DE PREFABS
    // =========================================================================

    public static bool IsPrefabValid(GameObject prefab)
    {
        if (prefab == null) return false;
        if (ReferenceEquals(prefab, null)) return false;
        try
        {
            // Testa se a referência nativa interna do Unity está viva e válida
            return !string.IsNullOrEmpty(prefab.name) && prefab.transform != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validação e salvaguarda robusta de referências a prefabs.
    /// Recupera automaticamente qualquer prefab marcado como Missing ou nulo no Inspector.
    /// </summary>
    public void CarregarPrefabsAutomaticamente()
    {
        if (!IsPrefabValid(prefabGemaValida))
        {
            prefabGemaValida = LoadPrefabRobust("GemaValida");
        }

        if (!IsPrefabValid(prefabObstaculoInvalido))
        {
            prefabObstaculoInvalido = LoadPrefabRobust("ObstaculoInvalido");
        }

        if (!IsPrefabValid(prefabAgentePacMan))
        {
            prefabAgentePacMan = LoadPrefabRobust("PacManJogador");
        }

        if (!IsPrefabValid(prefabAgenteFantasma))
        {
            prefabAgenteFantasma = LoadPrefabRobust("ObstaculoInvalido");
        }

        if (!IsPrefabValid(prefabGemaEspecial))
        {
            prefabGemaEspecial = LoadPrefabRobust("GemaValida");
        }

        if (!IsPrefabValid(prefabPlacaSinalizacao))
        {
            prefabPlacaSinalizacao = LoadPrefabRobust("PlacaSinalizacaoSemantica");
        }

        // Garante mapeamentos canônicos das classes ontológicas OWL para modelos 3D
        GarantirMapeamentosCanonicos();
    }

    private void GarantirMapeamentosCanonicos()
    {
        string[] classesGemas = {
            "AscarisLumbricoides", "EnterobiusVermicularis", "TaeniaSolium",
            "Nematoda", "Platyhelminthes", "Parasite", "Host", "Human", "Pig",
            "SmallIntestine", "LargeIntestine", "PerianalRegion",
            "Cylindrical", "FlatSegments", "FecalOral", "UndercookedPork", "Autoinfection",
            "ConformingParasiteSpecimen", "AntiparasiticTreatment"
        };
        string[] classesFantasmas = {
            "InfectionVector", "TriatomineVector", "BiomphalariaVector", "AedesVector",
            "InconsistentParasiteSpecimen", "HostMismatchViolation", "ContaminatedHostVector",
            "ViolatedTaxon", "InconsistentSpecimen"
        };

        foreach (var cls in classesGemas)
        {
            RegistrarMapeamentoSeAusente(cls, prefabGemaValida);
        }

        foreach (var cls in classesFantasmas)
        {
            RegistrarMapeamentoSeAusente(cls, prefabObstaculoInvalido);
        }

        RegistrarMapeamentoSeAusente("BiomedicalResearcher", prefabAgentePacMan);
        RegistrarMapeamentoSeAusente("Pacman", prefabAgentePacMan);
        RegistrarMapeamentoSeAusente("Jogador", prefabAgentePacMan);
    }

    private void RegistrarMapeamentoSeAusente(string nomeClasse, GameObject prefabPadrao)
    {
        int idx = mapeamentoClassesCustomizadas.FindIndex(m => string.Equals(m.nomeClasseOntologica, nomeClasse, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
        {
            var m = mapeamentoClassesCustomizadas[idx];
            if (!IsPrefabValid(m.prefabCorrespondente) && IsPrefabValid(prefabPadrao))
            {
                m.prefabCorrespondente = prefabPadrao;
                mapeamentoClassesCustomizadas[idx] = m;
            }
        }
        else if (IsPrefabValid(prefabPadrao))
        {
            mapeamentoClassesCustomizadas.Add(new MapeamentoClassePrefab(nomeClasse, prefabPadrao));
        }
    }

    private GameObject LoadPrefabRobust(string prefabName)
    {
        // 1. Tenta carregar da pasta Resources
        GameObject loaded = Resources.Load<GameObject>($"Prefabs/{prefabName}") ?? Resources.Load<GameObject>(prefabName);

#if UNITY_EDITOR
        // 2. Se falhar, busca no AssetDatabase durante o editor
        if (loaded == null)
        {
            string[] possiblePaths = new string[]
            {
                $"Assets/Prefabs/{prefabName}.prefab",
                $"Assets/Resources/Prefabs/{prefabName}.prefab",
                $"Assets/{prefabName}.prefab"
            };

            foreach (var path in possiblePaths)
            {
                loaded = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (loaded != null) break;
            }
        }
#endif
        return loaded;
    }

    private GameObject ObterPrefabParaClasse(string nomeClasse, bool ehValida)
    {
        if (!string.IsNullOrEmpty(nomeClasse))
        {
            foreach (var m in mapeamentoClassesCustomizadas)
            {
                if (string.Equals(m.nomeClasseOntologica, nomeClasse, StringComparison.OrdinalIgnoreCase) && IsPrefabValid(m.prefabCorrespondente))
                {
                    return m.prefabCorrespondente;
                }
            }

            // Heurística de classificação ontológica pelo nome
            string nomeLower = nomeClasse.ToLower();
            if (nomeLower.Contains("pacman") || nomeLower.Contains("jogador"))
            {
                if (IsPrefabValid(prefabAgentePacMan)) return prefabAgentePacMan;
            }

            if (nomeLower.Contains("ghost") || nomeLower.Contains("fantasma") || nomeLower.Contains("blinky") || nomeLower.Contains("obstaculo"))
            {
                if (IsPrefabValid(prefabAgenteFantasma)) return prefabAgenteFantasma;
                if (IsPrefabValid(prefabObstaculoInvalido)) return prefabObstaculoInvalido;
            }

            if (nomeLower.Contains("special") || nomeLower.Contains("power"))
            {
                if (IsPrefabValid(prefabGemaEspecial)) return prefabGemaEspecial;
            }
        }

        return ehValida ? (IsPrefabValid(prefabGemaValida) ? prefabGemaValida : null)
                        : (IsPrefabValid(prefabObstaculoInvalido) ? prefabObstaculoInvalido : null);
    }

    private void ConfigurarComponentesSemanticos(GameObject obj, DadoInstanciaSemantica dado)
    {
        // Garante a presenca de colisor Trigger ampliado para interacao espacial
        Collider[] colisores = obj.GetComponentsInChildren<Collider>();
        if (colisores.Length == 0)
        {
            SphereCollider sc = obj.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = dado.ehValida ? 0.65f : 0.45f;
        }
        else
        {
            foreach (var c in colisores)
            {
                c.isTrigger = true;
                if (dado.ehValida && c is SphereCollider sc)
                {
                    sc.radius = Mathf.Max(sc.radius, 0.65f);
                }
            }
        }

        // Rigidbody cinematico para objetos de cenario
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Vincula a implementação especializada de IInstanciaSemantica (GemaValidaSemantica ou ObstaculoInvalidoSemantico)
        IInstanciaSemantica instSemantica;
        if (dado.ehValida)
        {
            GemaValidaSemantica gema = obj.GetComponent<GemaValidaSemantica>();
            if (gema == null)
            {
                GerenciadorInstancia gi = obj.GetComponent<GerenciadorInstancia>();
                if (gi != null)
                {
                    if (Application.isPlaying) Destroy(gi); else DestroyImmediate(gi);
                }
                gema = obj.AddComponent<GemaValidaSemantica>();
            }
            instSemantica = gema;
        }
        else
        {
            ObstaculoInvalidoSemantico obs = obj.GetComponent<ObstaculoInvalidoSemantico>();
            if (obs == null)
            {
                GerenciadorInstancia gi = obj.GetComponent<GerenciadorInstancia>();
                if (gi != null)
                {
                    if (Application.isPlaying) Destroy(gi); else DestroyImmediate(gi);
                }
                obs = obj.AddComponent<ObstaculoInvalidoSemantico>();
            }
            instSemantica = obs;

            int ghostIdx = (m_ContadorFantasmasConfigurados) % s_SpawnsGaiolaCentral.Length;
            obs.posicaoSlotGaiola = s_SpawnsGaiolaCentral[ghostIdx];
            obs.posicaoSaidaGaiola = s_PosicaoSaidaGaiola;
            obs.tempoAtrasoSaidaGaiola = s_AtrasosSaidaGaiola[ghostIdx];
        }

        instSemantica.InicializarSemantica(dado);

        if (dado.ehValida)
        {
            // NODOS DE DADOS OWL (Cubos/Prismas Translúcidos): Remoção de NavMeshAgent ou GemaFugitiva
            UnityEngine.AI.NavMeshAgent nav = obj.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (nav != null) Destroy(nav);

            GemaFugitiva fuga = obj.GetComponent<GemaFugitiva>();
            if (fuga != null) Destroy(fuga);

            SphereCollider sc = obj.GetComponent<SphereCollider>();
            if (sc == null) sc = obj.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.45f;

            if (obj.GetComponent<RotacaoFlutuante>() == null)
            {
                RotacaoFlutuante rot = obj.AddComponent<RotacaoFlutuante>();
                rot.permitirFlutuacao = false;
            }

            // Converte o MeshFilter do núcleo para Cubo/Prisma (Nodo de Dados) e aplica material translúcido
            Transform nucleo = obj.transform.Find("Nucleo_PacDot");
            if (nucleo != null)
            {
                nucleo.localScale = new Vector3(0.32f, 0.46f, 0.32f);
                nucleo.localRotation = Quaternion.Euler(35f, 45f, 0f);
                MeshFilter mf = nucleo.GetComponent<MeshFilter>();
                if (mf != null)
                {
                    GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    mf.sharedMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
                    if (Application.isPlaying) Destroy(tempCube);
                    else DestroyImmediate(tempCube);
                }
            }
            Transform halo = obj.transform.Find("Halo_Aura");
            if (halo != null)
            {
                halo.localScale = new Vector3(0.42f, 0.015f, 0.42f);
            }

            Color corNodoBase = dado.corDestaque != Color.clear ? dado.corDestaque : new Color(0.0f, 0.90f, 1.0f, 1f);
            Material matNodoTranslucido = CriarMaterialTranslúcidoNodo(corNodoBase, 0.72f);
            Renderer[] rendsNodo = obj.GetComponentsInChildren<Renderer>();
            foreach (var r in rendsNodo)
            {
                r.material = matNodoTranslucido;
            }

            // Pequena Point Light local (sem sombra) para destacar o Nodo Ontológico no chão Dark Grid
            AnexarLuzPontualDestaque(obj, corNodoBase, 2.2f, 1.15f);
        }
        else
        {
            // REASONER AGENTS (Antigos Fantasmas):
            // Estado inicial Patrol (CONSISTENTE): Material Azul Translúcido.
            // Ao detectar erro lógico (INCONSISTENTE), o MotorRaciocinioSemantico altera para Vermelho Sólido (Chase).
            int ghostIdx = (m_ContadorFantasmasConfigurados) % CoresClassicasFantasmas.Length;
            string nomeGhost = NomesFantasmasClassicos[ghostIdx];
            obj.name = $"ReasonerAgent_{nomeGhost}_{dado.id}";
            obj.transform.localScale = new Vector3(0.80f, 0.80f, 0.80f);

            Color corAzulPatrol = new Color(0.10f, 0.55f, 1.0f, 1f);
            Material matAgentPatrol = CriarMaterialTranslúcidoNodo(corAzulPatrol, 0.78f);
            Renderer[] rendsFantasma = obj.GetComponentsInChildren<Renderer>();
            foreach (var r in rendsFantasma)
            {
                string rName = r.name.ToLower();
                if (!rName.Contains("olho") && !rName.Contains("pupila") && !rName.Contains("eye"))
                {
                    r.material = matAgentPatrol;
                }
            }

            // Point Light local no Fantasma/Reasoner Agent para destacá-lo sobre a grade preta
            AnexarLuzPontualDestaque(obj, corAzulPatrol, 3.2f, 1.45f);

            // NavMeshAgent com Avoidance Priority diferenciada (30, 40, 50, 60)
            UnityEngine.AI.NavMeshAgent agent = obj.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null)
            {
                agent = obj.AddComponent<UnityEngine.AI.NavMeshAgent>();
            }
            agent.speed = 2.0f;
            agent.acceleration = 12f;
            agent.radius = 0.28f;
            agent.height = 1.0f;
            agent.stoppingDistance = 0.2f;
            agent.autoBraking = false;
            agent.baseOffset = 0f;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
            }
            
            int prioridade = 30 + ((m_ContadorFantasmasConfigurados++) % 4) * 10;
            agent.avoidancePriority = prioridade;
        }
    }




    // =========================================================================
    // 4. MODELAGEM PROCEDURAL DE CONTINGÊNCIA (FALLBACK INTELIGENTE)
    // =========================================================================

    private GameObject CriarProceduralPorClasse(Vector3 posicao, DadoInstanciaSemantica dado)
    {
        string nomeLower = dado.classeOntologica.ToLower();
        if (nomeLower.Contains("pacman") || nomeLower.Contains("jogador"))
        {
            // Vincula o nó ontológico diretamente ao jogador real (XR Origin) sem criar esfera dummy estática no chão
            GameObject realPlayer = GameObject.FindWithTag("Player");
            if (realPlayer == null)
            {
                var ctrlXR = FindFirstObjectByType<ControladorXRJogador>();
                if (ctrlXR != null) realPlayer = ctrlXR.gameObject;
            }
            if (realPlayer != null) return realPlayer;
            return CriarPacManProcedural(posicao, dado);
        }

        if (dado.ehValida)
        {
            return CriarGemaProcedural(posicao, dado);
        }
        else
        {
            return CriarObstaculoProcedural(posicao, dado);
        }
    }

    private GameObject CriarGemaProcedural(Vector3 posicao, DadoInstanciaSemantica dado = null)
    {
        GameObject nodoObj = new GameObject("NodoDados_Procedural");
        nodoObj.transform.position = posicao;
        nodoObj.transform.SetParent(transform);

        Color corBase = (dado != null && dado.corDestaque != Color.clear) ? dado.corDestaque : new Color(0.0f, 0.90f, 1.0f, 1f);
        Material matNodo = CriarMaterialTranslúcidoNodo(corBase, 0.58f);

        // Cubo / Prisma rotacionado (Nodo de Dados OWL)
        GameObject cuboPrisma = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cuboPrisma.name = "Nucleo_PacDot";
        cuboPrisma.transform.SetParent(nodoObj.transform, false);
        cuboPrisma.transform.localScale = new Vector3(0.32f, 0.46f, 0.32f);
        cuboPrisma.transform.localRotation = Quaternion.Euler(35f, 45f, 0f);
        Destroy(cuboPrisma.GetComponent<Collider>());
        cuboPrisma.GetComponent<Renderer>().material = matNodo;

        // Halo cibernético de base
        GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        halo.name = "Halo_Aura";
        halo.transform.SetParent(nodoObj.transform, false);
        halo.transform.localScale = new Vector3(0.42f, 0.015f, 0.42f);
        Destroy(halo.GetComponent<Collider>());
        halo.GetComponent<Renderer>().material = matNodo;

        SphereCollider sc = nodoObj.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.45f;

        nodoObj.AddComponent<GemaValidaSemantica>();
        RotacaoFlutuante rot = nodoObj.AddComponent<RotacaoFlutuante>();
        rot.permitirFlutuacao = false;

        AnexarLuzPontualDestaque(nodoObj, corBase, 2.2f, 1.15f);

        return nodoObj;
    }

    /// <summary>
    /// Anexa uma pequena Point Light sem sombras (para não afetar o Shadow Atlas no Quest 3)
    /// aos objetos principais (Nodos Ontológicos e Fantasmas/Reasoner Agents), destacando-os sobre o chão Dark Grid.
    /// </summary>
    public static Light AnexarLuzPontualDestaque(GameObject alvo, Color corLuz, float alcance = 2.4f, float intensidade = 1.2f)
    {
        if (alvo == null) return null;

        Transform tExistente = alvo.transform.Find("Luz_Destaque_Grid");
        GameObject luzObj;
        if (tExistente != null)
        {
            luzObj = tExistente.gameObject;
        }
        else
        {
            luzObj = new GameObject("Luz_Destaque_Grid");
            luzObj.transform.SetParent(alvo.transform, false);
            luzObj.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        }

        Light ptLight = luzObj.GetComponent<Light>();
        if (ptLight == null)
        {
            ptLight = luzObj.AddComponent<Light>();
        }

        ptLight.type = LightType.Point;
        ptLight.color = new Color(corLuz.r, corLuz.g, corLuz.b, 1f);
        ptLight.range = alcance;
        ptLight.intensity = intensidade;
        ptLight.shadows = LightShadows.None; // Essencial: sem sombra para manter 0 erros de Shadow Atlas no URP
        ptLight.renderMode = LightRenderMode.Auto;
        return ptLight;
    }

    /// <summary>
    /// Cria um material translúcido cibernético para os Nodos de Dados OWL e Reasoner Agents.
    /// </summary>
    public Material CriarMaterialTranslúcidoNodo(Color corBase, float alpha = 0.58f)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material mat = new Material(shader);
        Color corTranslucida = new Color(corBase.r, corBase.g, corBase.b, alpha);

        // Configuração de Transparência para URP e Standard
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // 1 = Transparent no URP
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", corTranslucida);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", corTranslucida);

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(corBase.r, corBase.g, corBase.b, 1f) * 0.65f);
        }

        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.92f);
        return mat;
    }

    private GameObject CriarObstaculoProcedural(Vector3 posicao, DadoInstanciaSemantica dado = null)
    {
        int ghostIdx = (m_ContadorFantasmasConfigurados) % CoresClassicasFantasmas.Length;
        string nomeGhost = NomesFantasmasClassicos[ghostIdx];
        Color corGhost = CoresClassicasFantasmas[ghostIdx];
        Vector3 slotGaiola = s_SpawnsGaiolaCentral[ghostIdx];
        float atrasoSaida = s_AtrasosSaidaGaiola[ghostIdx];

        GameObject fantasma = new GameObject($"ReasonerAgent_{nomeGhost}");
        fantasma.transform.position = slotGaiola;
        fantasma.transform.SetParent(transform);

        // Personagem em escala adequada ao corredor: 0.80f
        fantasma.transform.localScale = new Vector3(0.80f, 0.80f, 0.80f);

        // Material com a cor canônica clássica do fantasma (Blinky Vermelho, Pinky Rosa, Inky Ciano, Clyde Laranja)
        Material matCorpo = CriarMaterialTranslúcidoNodo(corGhost, 0.72f);

        // 1. CORPO EM FORMA DE CÁPSULA (CAPSULE) CONFORME EXIGÊNCIA ESTRITA
        GameObject corpoCapsula = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        corpoCapsula.name = "Corpo_Capsula";
        corpoCapsula.transform.SetParent(fantasma.transform, false);
        corpoCapsula.transform.localPosition = new Vector3(0f, 0.52f, 0f);
        corpoCapsula.transform.localScale = new Vector3(0.85f, 0.65f, 0.85f);
        Destroy(corpoCapsula.GetComponent<Collider>());
        corpoCapsula.GetComponent<Renderer>().material = matCorpo;

        // 2. DOIS OLHOS BRANCOS EMBUTIDOS DIRETAMENTE NO ROSTO (LOOK RETRÔ ARCADE FIRME, SEM PONTINHOS SOLTOS)
        Shader shaderUnlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        Material matOlho = new Material(shaderUnlit) { name = "Mat_Olho_Branco", color = Color.white };
        Material matPupila = new Material(shaderUnlit) { name = "Mat_Pupila_AzulMarinho", color = new Color(0.04f, 0.12f, 0.65f) };

        // Olho Esquerdo (Esfera achatada embutida na frente da cabeça do fantasma)
        GameObject olhoEsq = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        olhoEsq.name = "OlhoEsquerdo";
        olhoEsq.transform.SetParent(fantasma.transform, false);
        olhoEsq.transform.localPosition = new Vector3(-0.16f, 0.54f, 0.28f);
        olhoEsq.transform.localScale = new Vector3(0.18f, 0.22f, 0.08f);
        Destroy(olhoEsq.GetComponent<Collider>());
        olhoEsq.GetComponent<Renderer>().material = matOlho;

        GameObject pupilaEsq = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pupilaEsq.name = "PupilaEsquerda";
        pupilaEsq.transform.SetParent(olhoEsq.transform, false);
        pupilaEsq.transform.localPosition = new Vector3(0f, 0f, 0.035f);
        pupilaEsq.transform.localScale = new Vector3(0.50f, 0.50f, 0.20f);
        Destroy(pupilaEsq.GetComponent<Collider>());
        pupilaEsq.GetComponent<Renderer>().material = matPupila;

        // Olho Direito (Esfera achatada embutida na frente da cabeça do fantasma)
        GameObject olhoDir = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        olhoDir.name = "OlhoDireito";
        olhoDir.transform.SetParent(fantasma.transform, false);
        olhoDir.transform.localPosition = new Vector3(0.16f, 0.54f, 0.28f);
        olhoDir.transform.localScale = new Vector3(0.18f, 0.22f, 0.08f);
        Destroy(olhoDir.GetComponent<Collider>());
        olhoDir.GetComponent<Renderer>().material = matOlho;

        GameObject pupilaDir = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pupilaDir.name = "PupilaDireita";
        pupilaDir.transform.SetParent(olhoDir.transform, false);
        pupilaDir.transform.localPosition = new Vector3(0f, 0f, 0.035f);
        pupilaDir.transform.localScale = new Vector3(0.50f, 0.50f, 0.20f);
        Destroy(pupilaDir.GetComponent<Collider>());
        pupilaDir.GetComponent<Renderer>().material = matPupila;

        AnimacaoFantasma anim = fantasma.AddComponent<AnimacaoFantasma>();
        anim.pupilaEsquerda = pupilaEsq.transform;
        anim.pupilaDireita = pupilaDir.transform;
        anim.permitirFlutuacao = false;

        // NavMeshAgent para locomoção suave pelos corredores (inicia desligado até concluir nascimento e sair da gaiola)
        UnityEngine.AI.NavMeshAgent agent = fantasma.AddComponent<UnityEngine.AI.NavMeshAgent>();
        agent.speed = 2.4f;
        agent.acceleration = 12f;
        agent.radius = 0.28f;
        agent.height = 1.0f;
        agent.stoppingDistance = 0.2f;
        agent.autoBraking = false;
        agent.baseOffset = 0f;
        agent.avoidancePriority = 30 + ghostIdx * 10;
        agent.enabled = false;

        // Colisor Trigger para interação limpa
        SphereCollider col = fantasma.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.45f;
        col.center = new Vector3(0f, 0.5f, 0f);

        // Rigidbody cinemático
        Rigidbody rb = fantasma.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Componente semântico com controle de gaiola, nascimento procedural e dano
        ObstaculoInvalidoSemantico obs = fantasma.AddComponent<ObstaculoInvalidoSemantico>();
        if (dado != null) obs.InicializarSemantica(dado);
        obs.ConfigurarSpawnGaiola(slotGaiola, atrasoSaida, s_PosicaoSaidaGaiola);

        // Luz de destaque colorida
        AnexarLuzPontualDestaque(fantasma, corGhost, 2.8f, 1.3f);

        m_ContadorFantasmasConfigurados++;
        return fantasma;
    }

        private GameObject CriarPacManProcedural(Vector3 posicao, DadoInstanciaSemantica dado = null)
    {
        // 1. Tenta carregar o prefab clássico PacManJogador se presente
        GameObject prefabPac = LoadPrefabRobust("PacManJogador");
        if (prefabPac != null)
        {
            GameObject pacInst = Instantiate(prefabPac, posicao, Quaternion.identity, transform);
            pacInst.name = "PacMan_Jogador";
            pacInst.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
            return pacInst;
        }

        // 2. Modelo Procedural autêntico do Pac-Man: Esfera Clássica Perfeita Amarela Brilhante
        GameObject pacObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pacObj.name = "PacMan_Procedural";
        pacObj.transform.position = posicao;
        pacObj.transform.SetParent(transform);
        pacObj.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
        Destroy(pacObj.GetComponent<Collider>());

        // Material Amarelo Brilhante Clássico
        Material matPac = Resources.Load<Material>("Mat_PacMan_Amarelo");
#if UNITY_EDITOR
        if (matPac == null)
        {
            matPac = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_PacMan_Amarelo.mat");
        }
#endif
        if (matPac == null)
        {
            Color amarelo = new Color(1.0f, 0.92f, 0.0f);
            matPac = CriarMaterialDinamico(amarelo, amarelo * 1.5f, 0.95f);
        }
        pacObj.GetComponent<Renderer>().material = matPac;

        AnimacaoBocaPacMan anim = pacObj.AddComponent<AnimacaoBocaPacMan>();
        return pacObj;
    }

    private Texture2D m_TexturaParedeBiomedica;
    private Texture2D m_NormalMapParede;
    private Texture2D m_TexturaChaoLaboratorio;
    private Texture2D m_NormalMapChao;

    private void PrepararMateriaisProcedurais()
    {
        if (m_MaterialGemaProcedural == null)
        {
            m_MaterialGemaProcedural = CriarMaterialDinamico(
                corBase: new Color(0.1f, 1f, 0.45f),
                corEmissao: new Color(0f, 0.9f, 0.4f) * 1.5f,
                suavidade: 0.95f
            );
        }

        if (m_MaterialObstaculoProcedural == null)
        {
            m_MaterialObstaculoProcedural = CriarMaterialDinamico(
                corBase: new Color(1f, 0.12f, 0.2f),
                corEmissao: new Color(0.95f, 0.05f, 0.1f) * 1.4f,
                suavidade: 0.9f
            );
        }
    }

    private Material CriarMaterialDinamico(Color corBase, Color corEmissao, float suavidade)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material mat = new Material(shader);

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", corBase);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", corBase);

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", corEmissao);
        }

        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", suavidade);
        return mat;
    }

    /// <summary>
    /// Determina a Superclasse OWL associada à região espacial do labirinto (Topologia Semântica).
    /// Hemisfério Norte / NW / NE / Leste = "Parasitas" (Roxo);
    /// Hemisfério Sul / SW / SE / Oeste = "Hospedeiros" (Azul).
    /// </summary>
    public string DeterminarSuperclassePorRegiao(Vector3 posicao, string nomeBloco = "")
    {
        if (!string.IsNullOrEmpty(nomeBloco))
        {
            if (nomeBloco.Contains("Norte") || nomeBloco.Contains("NW") || nomeBloco.Contains("NE") || nomeBloco.Contains("Leste"))
            {
                return "Parasitas";
            }
            if (nomeBloco.Contains("Sul") || nomeBloco.Contains("SW") || nomeBloco.Contains("SE") || nomeBloco.Contains("Oeste"))
            {
                return "Hospedeiros";
            }
        }

        return (posicao.z >= 0f && posicao.x >= 0f) || posicao.z > 1.0f ? "Parasitas" : "Hospedeiros";
    }

    /// <summary>
    /// Retorna a Cor de Material associada à Superclasse da região gerada:
    /// - "Parasitas" (Nematoda / Platyhelminthes / Protozoa) = Roxo
    /// - "Hospedeiros" (Host / Human / Pig) = Azul
    /// </summary>
    public Color ObterCorTopologiaSuperclasse(string superclasse)
    {
        if (!string.IsNullOrEmpty(superclasse))
        {
            if (mapeamentoCoresSuperclasse != null)
            {
                for (int i = 0; i < mapeamentoCoresSuperclasse.Count; i++)
                {
                    if (string.Equals(mapeamentoCoresSuperclasse[i].nomeSuperclasse, superclasse, StringComparison.OrdinalIgnoreCase))
                    {
                        return mapeamentoCoresSuperclasse[i].corMaterialRegiao;
                    }
                }
            }

            if (superclasse.IndexOf("Parasit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                superclasse.IndexOf("Nematod", StringComparison.OrdinalIgnoreCase) >= 0 ||
                superclasse.IndexOf("Platyhelmint", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return corSuperclasseParasitas; // Roxo
            }

            if (superclasse.IndexOf("Hosped", StringComparison.OrdinalIgnoreCase) >= 0 ||
                superclasse.IndexOf("Host", StringComparison.OrdinalIgnoreCase) >= 0 ||
                superclasse.IndexOf("Human", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return corSuperclasseHospedeiros; // Azul
            }
        }

        return corSuperclasseHospedeiros;
    }

    private static Texture2D s_TexWallRoxoBase;
    private static Texture2D s_TexWallRoxoMask;
    private static Texture2D s_TexWallAzulBase;
    private static Texture2D s_TexWallAzulMask;
    private static Texture2D s_TexChaoBase;
    private static Texture2D s_TexChaoMask;

    /// <summary>
    /// Cria ou carrega o material físico DarkGrid.mat na estética "Dark Grid / Wireframe Retrowave":
    /// Fundo preto profundo (#000000) com textura emissiva de GRADE (Grid/Wireframe) na cor da Superclasse OWL
    /// (Roxo para Parasitas, Azul para Hospedeiros).
    /// </summary>
    public Material CriarMaterialParedePorSuperclasse(string superclasse, Vector2 tilingGrid = default)
    {
        if (tilingGrid == default || tilingGrid.x <= 0.01f)
        {
            tilingGrid = new Vector2(2f, 1f);
        }

        bool ehParasita = !string.IsNullOrEmpty(superclasse) &&
                          (superclasse.IndexOf("Parasit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           superclasse.IndexOf("Nematod", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           superclasse.IndexOf("Platyhelmint", StringComparison.OrdinalIgnoreCase) >= 0);

        if (!usarEsteticaDarkGrid)
        {
            Shader shWall = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
            Material matParedeLimpa = new Material(shWall);
            matParedeLimpa.name = $"Mat_Parede_Metalica_{superclasse}";
            // Parede cinza metálica clara com acabamento industrial que reflete a luz solar da tarde
            Color corParedeCinza = new Color(0.55f, 0.58f, 0.62f, 1f);
            if (matParedeLimpa.HasProperty("_BaseColor")) matParedeLimpa.SetColor("_BaseColor", corParedeCinza);
            if (matParedeLimpa.HasProperty("_Color")) matParedeLimpa.SetColor("_Color", corParedeCinza);
            if (matParedeLimpa.HasProperty("_Metallic")) matParedeLimpa.SetFloat("_Metallic", 0.65f);
            if (matParedeLimpa.HasProperty("_Smoothness")) matParedeLimpa.SetFloat("_Smoothness", 0.35f);
            matParedeLimpa.DisableKeyword("_EMISSION");
            if (matParedeLimpa.HasProperty("_EmissionColor")) matParedeLimpa.SetColor("_EmissionColor", Color.black);
            return matParedeLimpa;
        }

        string nomeAssetMat = ehParasita ? "DarkGridMaterial_Parasitas_Roxo" : "DarkGridMaterial_Hospedeiros_Azul";
        Color corSuperclasse = ObterCorTopologiaSuperclasse(superclasse);

        Material matFisico = Resources.Load<Material>(nomeAssetMat);
        if (matFisico == null) matFisico = Resources.Load<Material>("DarkGrid");
        if (matFisico == null) matFisico = Resources.Load<Material>("DarkGridMaterial");
#if UNITY_EDITOR
        if (matFisico == null)
        {
            matFisico = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/{nomeAssetMat}.mat");
            if (matFisico == null) matFisico = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DarkGrid.mat");
            if (matFisico == null) matFisico = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DarkGridMaterial.mat");
        }
#endif
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material mat = matFisico != null ? new Material(matFisico) : new Material(shader);
        mat.name = $"{nomeAssetMat}_Tiled";

        Texture2D texBase;
        Texture2D texEmissiva;
        if (ehParasita)
        {
            if (s_TexWallRoxoBase == null || s_TexWallRoxoMask == null)
            {
                GerarTexturasWireframeGrid(Color.black, corSuperclasse, 256, 6, 1, out s_TexWallRoxoBase, out s_TexWallRoxoMask);
            }
            texBase = s_TexWallRoxoBase;
            texEmissiva = s_TexWallRoxoMask;
        }
        else
        {
            if (s_TexWallAzulBase == null || s_TexWallAzulMask == null)
            {
                GerarTexturasWireframeGrid(Color.black, corSuperclasse, 256, 6, 1, out s_TexWallAzulBase, out s_TexWallAzulMask);
            }
            texBase = s_TexWallAzulBase;
            texEmissiva = s_TexWallAzulMask;
        }

        // Garante sempre _BaseMap e _EmissionMap válidos (evita o fallback "white" do URP Lit que deixava a parede inteira acesa)
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", texBase);
            mat.SetTextureScale("_BaseMap", tilingGrid);
        }
        mat.mainTexture = texBase;
        mat.mainTextureScale = tilingGrid;

        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.0f);
        if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0.0f);
        if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 0.0f);
        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionMap"))
            {
                mat.SetTexture("_EmissionMap", texEmissiva);
                mat.SetTextureScale("_EmissionMap", tilingGrid);
            }
            Color corLinhaBrilhante = Color.Lerp(corSuperclasse, Color.white, 0.10f) * 0.45f;
            corLinhaBrilhante.a = 1f;
            mat.SetColor("_EmissionColor", corLinhaBrilhante);
        }

        return mat;
    }

    /// <summary>
    /// Cria o material avançado Dark Grid / Wireframe para paredes.
    /// </summary>
    public Material CriarMaterialParedeBiomedicaSciFi()
    {
        return CriarMaterialParedePorSuperclasse("Parasitas", new Vector2(2f, 1f));
    }

    /// <summary>
    /// Cria o material néon luminoso (LED Trim) discreto para detalhes de portal/arestas.
    /// </summary>
    public Material CriarMaterialNeonLED(Color corLED)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material mat = new Material(shader);
        mat.name = "Mat_NeonLED_Trim";

        Color corFriso = new Color(corLED.r, corLED.g, corLED.b, 1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", corFriso);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", corFriso);

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", corFriso * 1.25f);
        }

        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.5f);
        return mat;
    }

    /// <summary>
    /// Carrega ou cria o material físico "DarkGridMaterial_Chao" para o chão (Plane):
    /// Fundo preto profundo (#020205) com grade azul emissiva nítida (estilo Retrowave da referência).
    /// </summary>
    public Material CriarMaterialChaoLaboratorio()
    {
        if (!usarEsteticaDarkGrid)
        {
            Shader shChao = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
            Material matChaoLimpo = new Material(shChao);
            matChaoLimpo.name = "Mat_Chao_Grama_Futebol";
            // Verde vivo como grama de campo de futebol sob a luz da tarde (fosco, natural e agradável)
            Color corGramaFutebol = new Color(0.22f, 0.58f, 0.25f, 1f);
            if (matChaoLimpo.HasProperty("_BaseColor")) matChaoLimpo.SetColor("_BaseColor", corGramaFutebol);
            if (matChaoLimpo.HasProperty("_Color")) matChaoLimpo.SetColor("_Color", corGramaFutebol);
            if (matChaoLimpo.HasProperty("_Metallic")) matChaoLimpo.SetFloat("_Metallic", 0.0f);
            if (matChaoLimpo.HasProperty("_Smoothness")) matChaoLimpo.SetFloat("_Smoothness", 0.05f);
            matChaoLimpo.DisableKeyword("_EMISSION");
            if (matChaoLimpo.HasProperty("_EmissionColor")) matChaoLimpo.SetColor("_EmissionColor", Color.black);
            return matChaoLimpo;
        }

        Material matChaoFisico = Resources.Load<Material>("DarkGridMaterial_Chao");
#if UNITY_EDITOR
        if (matChaoFisico == null)
        {
            matChaoFisico = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DarkGridMaterial_Chao.mat");
        }
#endif
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material mat = matChaoFisico != null ? new Material(matChaoFisico) : new Material(shader);
        mat.name = "Mat_Chao_DarkGrid_Azul";

        Color corGradeAzul = new Color(0.05f, 0.35f, 1.0f, 1f);
        Vector2 tilingChao = new Vector2(28f, 28f);

        if (s_TexChaoBase == null || s_TexChaoMask == null)
        {
            GerarTexturasWireframeGrid(Color.black, corGradeAzul, 256, 6, 1, out s_TexChaoBase, out s_TexChaoMask);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", s_TexChaoBase);
            mat.SetTextureScale("_BaseMap", tilingChao);
        }
        mat.mainTexture = s_TexChaoBase;
        mat.mainTextureScale = tilingChao;

        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.0f);
        if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0.0f);
        if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 0.0f);
        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionMap"))
            {
                mat.SetTexture("_EmissionMap", s_TexChaoMask);
                mat.SetTextureScale("_EmissionMap", tilingChao);
            }
            mat.SetColor("_EmissionColor", corGradeAzul * 0.40f);
        }

        return mat;
    }

    /// <summary>
    /// Gera o par de texturas procedurais (Base + Máscara Emissiva) para a estética Dark Grid / Wireframe.
    /// O interior das células é preto absoluto (0 de emissão) e apenas as linhas da grade brilham.
    /// </summary>
    private void GerarTexturasWireframeGrid(
        Color corFundo,
        Color corLinha,
        int tamanho,
        int espessuraLinha,
        int subdivisoes,
        out Texture2D texBase,
        out Texture2D texEmissiva)
    {
        texBase = new Texture2D(tamanho, tamanho, TextureFormat.RGBA32, true);
        texBase.wrapMode = TextureWrapMode.Repeat;
        texBase.filterMode = FilterMode.Trilinear;
        texBase.anisoLevel = 16;

        texEmissiva = new Texture2D(tamanho, tamanho, TextureFormat.RGBA32, true);
        texEmissiva.wrapMode = TextureWrapMode.Repeat;
        texEmissiva.filterMode = FilterMode.Trilinear;
        texEmissiva.anisoLevel = 16;

        Color[] pixelsBase = new Color[tamanho * tamanho];
        Color[] pixelsEmissivos = new Color[tamanho * tamanho];

        int passoSub = Mathf.Max(16, tamanho / Mathf.Max(1, subdivisoes));
        int meiaEspessura = Mathf.Max(1, espessuraLinha / 2);

        for (int y = 0; y < tamanho; y++)
        {
            for (int x = 0; x < tamanho; x++)
            {
                int modX = x % passoSub;
                int modY = y % passoSub;

                bool ehBordaPrincipal = (x < meiaEspessura || x >= tamanho - meiaEspessura ||
                                         y < meiaEspessura || y >= tamanho - meiaEspessura);
                bool ehLinhaSubdivisao = (modX < meiaEspessura || modX >= passoSub - meiaEspessura ||
                                          modY < meiaEspessura || modY >= passoSub - meiaEspessura);

                int idx = y * tamanho + x;
                if (ehBordaPrincipal)
                {
                    pixelsBase[idx] = corLinha;
                    pixelsEmissivos[idx] = Color.white;
                }
                else if (ehLinhaSubdivisao)
                {
                    pixelsBase[idx] = Color.Lerp(corFundo, corLinha, 0.75f);
                    pixelsEmissivos[idx] = new Color(0.65f, 0.65f, 0.65f, 1f);
                }
                else
                {
                    pixelsBase[idx] = corFundo;
                    pixelsEmissivos[idx] = Color.black;
                }
            }
        }

        texBase.SetPixels(pixelsBase);
        texBase.Apply(true);

        texEmissiva.SetPixels(pixelsEmissivos);
        texEmissiva.Apply(true);
    }




    private Texture2D GerarTexturaPainelBiomedico(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        Color darkTitanium = new Color(0.06f, 0.09f, 0.15f, 1f);
        Color slatePanel = new Color(0.12f, 0.17f, 0.26f, 1f);
        Color borderBevel = new Color(0.20f, 0.28f, 0.40f, 1f);
        Color circuitNeon = new Color(0.0f, 0.8f, 1.0f, 1f);

        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int px = x % (w / 2);
                int py = y;

                bool ehBordaExterna = (px < 4 || px >= (w / 2) - 4 || py < 4 || py >= h - 4);
                bool ehChanfro = (px >= 4 && px < 8) || (px >= (w / 2) - 8 && px < (w / 2) - 4) ||
                                 (py >= 4 && py < 8) || (py >= h - 8 && py < h - 4);
                bool ehCircuito = (px == 18 && py > 20 && py < h - 20) || (px == (w / 2) - 18 && py > 20 && py < h - 20);

                if (ehCircuito)
                {
                    pixels[y * w + x] = circuitNeon;
                }
                else if (ehBordaExterna)
                {
                    pixels[y * w + x] = darkTitanium;
                }
                else if (ehChanfro)
                {
                    pixels[y * w + x] = borderBevel;
                }
                else
                {
                    float noise = Mathf.PerlinNoise(x * 0.15f, y * 0.05f) * 0.08f;
                    pixels[y * w + x] = slatePanel + new Color(noise, noise, noise, 0f);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private Texture2D GerarTexturaChaoGrid(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;

        Color tileColor = new Color(0.04f, 0.06f, 0.10f, 1f);
        Color gridBorder = new Color(0.15f, 0.22f, 0.35f, 1f);
        Color guideTrack = new Color(0.0f, 0.85f, 0.95f, 1f);

        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool ehGridLinha = (x < 3 || x >= w - 3 || y < 3 || y >= h - 3);
                bool ehCentroGuide = (x >= w / 2 - 2 && x <= w / 2 + 2) && (y >= h / 2 - 2 && y <= h / 2 + 2);

                if (ehCentroGuide)
                {
                    pixels[y * w + x] = guideTrack;
                }
                else if (ehGridLinha)
                {
                    pixels[y * w + x] = gridBorder;
                }
                else
                {
                    float noise = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.03f;
                    pixels[y * w + x] = tileColor + new Color(noise, noise, noise * 1.5f, 0f);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private Texture2D GerarNormalMapDeTextura(Texture2D baseTex, float forca)
    {
        int w = baseTex.width;
        int h = baseTex.height;
        Texture2D normalMap = new Texture2D(w, h, TextureFormat.RGBA32, true);
        normalMap.wrapMode = TextureWrapMode.Repeat;

        Color[] basePixels = baseTex.GetPixels();
        Color[] normalPixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int xEsq = (x - 1 + w) % w;
                int xDir = (x + 1) % w;
                int yBaixo = (y - 1 + h) % h;
                int yCima = (y + 1) % h;

                float lEsq = basePixels[y * w + xEsq].grayscale;
                float lDir = basePixels[y * w + xDir].grayscale;
                float lBaixo = basePixels[yBaixo * w + x].grayscale;
                float lCima = basePixels[yCima * w + x].grayscale;

                float dX = (lDir - lEsq) * forca;
                float dY = (lCima - lBaixo) * forca;

                Vector3 normal = new Vector3(-dX, -dY, 1f).normalized;

                normalPixels[y * w + x] = new Color(
                    normal.x * 0.5f + 0.5f,
                    normal.y * 0.5f + 0.5f,
                    normal.z * 0.5f + 0.5f,
                    1f
                );
            }
        }

        normalMap.SetPixels(normalPixels);
        normalMap.Apply();
        return normalMap;
    }

    /// <summary>
    /// Configura a iluminação do ambiente conforme o tema ativo (Dark Grid Neon, Clássico Verde/Cinza ou Especial de Natal).
    /// </summary>
    public void ConfigurarIluminacaoSciFi()
    {
        switch (temaAtual)
        {
            case TipoTemaLabirinto.DarkGridNeon:
                ConfigurarIluminacaoDarkGrid();
                break;
            case TipoTemaLabirinto.ClassicoVerdeCinza:
                ConfigurarIluminacaoTardeClassica();
                break;
            case TipoTemaLabirinto.EspecialNatal:
                ConfigurarIluminacaoNatalInverno();
                break;
            default:
                ConfigurarIluminacaoDarkGrid();
                break;
        }
    }

    private Light ObterOuCriarLuzDirecional()
    {
        GameObject containerLuzesLegado = GameObject.Find("Iluminacao_Corredores_SciFi");
        if (containerLuzesLegado != null)
        {
            if (Application.isPlaying) Destroy(containerLuzesLegado);
            else DestroyImmediate(containerLuzesLegado);
        }

        Light dirLight = null;
        Light[] allLights = FindObjectsByType<Light>();
        foreach (var l in allLights)
        {
            if (l == null) continue;
            if (l.type == LightType.Directional && dirLight == null)
            {
                dirLight = l;
            }
            else if (l.type != LightType.Directional)
            {
                l.shadows = LightShadows.None;
            }
        }

        if (dirLight == null)
        {
            GameObject dlObj = new GameObject("Directional Light");
            dirLight = dlObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
        }
        return dirLight;
    }

    public void ConfigurarIluminacaoDarkGrid()
    {
        Light dirLight = ObterOuCriarLuzDirecional();
        if (dirLight != null)
        {
            dirLight.intensity = 0.90f;
            dirLight.color = new Color(0.80f, 0.92f, 1.0f, 1f);
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.40f;
            dirLight.transform.rotation = Quaternion.Euler(45f, 40f, 0f);
        }

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.12f, 0.16f, 0.28f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.06f, 0.08f, 0.18f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.01f, 0.02f, 0.06f, 1f);
        RenderSettings.ambientIntensity = 0.95f;

        Color corCeuDark = new Color(0.02f, 0.03f, 0.07f, 1f);
        Camera[] todasCameras = FindObjectsByType<Camera>();
        foreach (Camera cam in todasCameras)
        {
            if (cam == null) continue;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = corCeuDark;
        }

        OtimizadorGraficoQuest3URP.AplicarOtimizacaoAutomaticaNoBoot();
    }

    public void ConfigurarIluminacaoTardeClassica()
    {
        Light dirLight = ObterOuCriarLuzDirecional();
        if (dirLight != null)
        {
            dirLight.intensity = 1.65f;
            dirLight.color = new Color(1.0f, 0.93f, 0.84f, 1f);
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.32f;
            dirLight.transform.rotation = Quaternion.Euler(34f, 48f, 0f);
        }

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.83f, 0.95f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.95f, 0.86f, 0.74f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.30f, 0.58f, 0.32f, 1f);
        RenderSettings.ambientIntensity = 1.35f;

        Color corCeuTarde = new Color(0.65f, 0.78f, 0.92f, 1f);
        Camera[] todasCameras = FindObjectsByType<Camera>();
        foreach (Camera cam in todasCameras)
        {
            if (cam == null) continue;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = corCeuTarde;
        }

        OtimizadorGraficoQuest3URP.AplicarOtimizacaoAutomaticaNoBoot();
    }

    public void ConfigurarIluminacaoNatalInverno()
    {
        Light dirLight = ObterOuCriarLuzDirecional();
        if (dirLight != null)
        {
            dirLight.intensity = 1.45f;
            dirLight.color = new Color(0.96f, 0.98f, 1.0f, 1f);
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.28f;
            dirLight.transform.rotation = Quaternion.Euler(42f, 35f, 0f);
        }

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.85f, 0.90f, 1.0f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.92f, 0.94f, 0.98f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.88f, 0.90f, 0.95f, 1f);
        RenderSettings.ambientIntensity = 1.25f;

        Color corCeuInverno = new Color(0.80f, 0.87f, 0.96f, 1f);
        Camera[] todasCameras = FindObjectsByType<Camera>();
        foreach (Camera cam in todasCameras)
        {
            if (cam == null) continue;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = corCeuInverno;
        }

        OtimizadorGraficoQuest3URP.AplicarOtimizacaoAutomaticaNoBoot();
    }

    /// <summary>
    /// Altera o tema visual do labirinto em tempo real e atualiza chão, paredes, iluminação e acessórios.
    /// </summary>
    public void AplicarTemaLabirinto(TipoTemaLabirinto novoTema)
    {
        temaAtual = novoTema;
        LocalizarPlanoChao();

        Material matChao = null;
        Material matParede = null;

        switch (novoTema)
        {
            case TipoTemaLabirinto.DarkGridNeon:
                usarEsteticaDarkGrid = true;
                matChao = CriarMaterialChaoLaboratorio();
                matParede = CriarMaterialParedePorSuperclasse("Parasitas");
                ConfigurarIluminacaoDarkGrid();
                ConfigurarDecoracoesGrid(true);
                ConfigurarDecoracoesClassico(false);
                ConfigurarDecoracoesNatal(false);
                ConfigurarModelosTemaClassicoCIn(false);
                ConfigurarTotemPortaoGrid(true);
                ConfigurarChapeusNatalFantasmas(false);
                ConfigurarTrenoPacMan(false);
                break;

            case TipoTemaLabirinto.ClassicoVerdeCinza:
                usarEsteticaDarkGrid = false;
                matChao = CriarMaterialChaoLaboratorio(); // Retorna Mat_Chao_Grama_Futebol
                matParede = CriarMaterialParedePorSuperclasse("Parasitas"); // Retorna Mat_Parede_Metalica_Parasitas
                ConfigurarIluminacaoTardeClassica();
                ConfigurarDecoracoesGrid(false);
                ConfigurarDecoracoesClassico(true);
                ConfigurarDecoracoesNatal(false);
                ConfigurarModelosTemaClassicoCIn(true);
                ConfigurarTotemPortaoGrid(false);
                ConfigurarChapeusNatalFantasmas(false);
                ConfigurarTrenoPacMan(false);
                break;

            case TipoTemaLabirinto.EspecialNatal:
                usarEsteticaDarkGrid = false;
                matChao = CriarMaterialChaoNatal();
                matParede = CriarMaterialParedeNatal();
                ConfigurarIluminacaoNatalInverno();
                ConfigurarDecoracoesGrid(false);
                ConfigurarDecoracoesClassico(false);
                ConfigurarDecoracoesNatal(true);
                ConfigurarModelosTemaClassicoCIn(false);
                ConfigurarTotemPortaoGrid(false);
                ConfigurarChapeusNatalFantasmas(true);
                ConfigurarTrenoPacMan(true);
                break;
        }

        // Aplica material ao chão
        if (planoChao != null && matChao != null)
        {
            Renderer rendChao = planoChao.GetComponent<Renderer>();
            if (rendChao != null)
            {
                rendChao.material = matChao;
                rendChao.receiveShadows = true;
            }
        }

        // Aplica material a todas as paredes instanciadas
        if (m_ContainerParedes != null && matParede != null)
        {
            Renderer[] rends = m_ContainerParedes.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] != null && rends[i].gameObject.name.StartsWith("Parede_"))
                {
                    rends[i].material = matParede;
                }
            }
        }

        Debug.Log($"<color=#00FF99><b>[TEMA]</b></color> Tema visual alterado para: <b>{novoTema}</b>");
    }

    public Material CriarMaterialChaoNatal()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material matNeve = new Material(shader);
        matNeve.name = "Mat_Chao_NeveNatal";
        Color corNeve = new Color(0.94f, 0.96f, 1.0f, 1f);
        if (matNeve.HasProperty("_BaseColor")) matNeve.SetColor("_BaseColor", corNeve);
        if (matNeve.HasProperty("_Color")) matNeve.SetColor("_Color", corNeve);
        if (matNeve.HasProperty("_Metallic")) matNeve.SetFloat("_Metallic", 0.05f);
        if (matNeve.HasProperty("_Smoothness")) matNeve.SetFloat("_Smoothness", 0.40f);
        matNeve.DisableKeyword("_EMISSION");
        if (matNeve.HasProperty("_EmissionColor")) matNeve.SetColor("_EmissionColor", Color.black);
        return matNeve;
    }

    public Material CriarMaterialParedeNatal()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material matParedeNatal = new Material(shader);
        matParedeNatal.name = "Mat_Parede_NatalFestivo";
        Color corVermelhoNatal = new Color(0.80f, 0.10f, 0.16f, 1f);
        if (matParedeNatal.HasProperty("_BaseColor")) matParedeNatal.SetColor("_BaseColor", corVermelhoNatal);
        if (matParedeNatal.HasProperty("_Color")) matParedeNatal.SetColor("_Color", corVermelhoNatal);
        if (matParedeNatal.HasProperty("_Metallic")) matParedeNatal.SetFloat("_Metallic", 0.20f);
        if (matParedeNatal.HasProperty("_Smoothness")) matParedeNatal.SetFloat("_Smoothness", 0.35f);
        if (matParedeNatal.HasProperty("_EmissionColor"))
        {
            matParedeNatal.EnableKeyword("_EMISSION");
            matParedeNatal.SetColor("_EmissionColor", corVermelhoNatal * 0.15f);
        }
        return matParedeNatal;
    }

    public void ConfigurarChapeusNatalFantasmas(bool ativo)
    {
        var fantasmas = FindObjectsByType<ObstaculoInvalidoSemantico>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < fantasmas.Length; i++)
        {
            if (fantasmas[i] != null)
            {
                var todosFilhos = fantasmas[i].GetComponentsInChildren<Transform>(true);
                for (int c = 0; c < todosFilhos.Length; c++)
                {
                    if (todosFilhos[c] != null && todosFilhos[c].name.Contains("Chapeu"))
                    {
                        todosFilhos[c].gameObject.SetActive(ativo);
                    }
                }

                if (ativo)
                {
                    Transform tChapeu = fantasmas[i].transform.Find("Chapeu_Papai_Noel");
                    if (tChapeu == null)
                    {
                        CriarChapeuPapaiNoel(fantasmas[i].transform);
                    }
                    else
                    {
                        tChapeu.gameObject.SetActive(true);
                    }
                }
            }
        }
    }

    public static GameObject CriarChapeuPapaiNoel(Transform parent)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material matVermelho = new Material(shader) { name = "Mat_Gorro_Vermelho" };
        Color corVermelhoNatal = new Color(0.88f, 0.05f, 0.10f);
        if (matVermelho.HasProperty("_BaseColor")) matVermelho.SetColor("_BaseColor", corVermelhoNatal);
        if (matVermelho.HasProperty("_Color")) matVermelho.SetColor("_Color", corVermelhoNatal);
        if (matVermelho.HasProperty("_Smoothness")) matVermelho.SetFloat("_Smoothness", 0.35f);

        Material matBranco = new Material(shader) { name = "Mat_Gorro_Pelucia" };
        if (matBranco.HasProperty("_BaseColor")) matBranco.SetColor("_BaseColor", new Color(0.98f, 0.98f, 1.0f));
        if (matBranco.HasProperty("_Color")) matBranco.SetColor("_Color", new Color(0.98f, 0.98f, 1.0f));
        if (matBranco.HasProperty("_Smoothness")) matBranco.SetFloat("_Smoothness", 0.08f);

        GameObject root = new GameObject("Chapeu_Papai_Noel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0f, 0.85f, -0.04f);
        root.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);

        // 1. Borda branca fofa de pelúcia (anel arredondado na base)
        GameObject borda = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        borda.name = "Borda_Pelucia";
        borda.transform.SetParent(root.transform, false);
        borda.transform.localPosition = Vector3.zero;
        borda.transform.localScale = new Vector3(0.52f, 0.10f, 0.52f);
        RemoverColisorObjeto(borda);
        borda.GetComponent<Renderer>().material = matBranco;

        // 2. Corpo do gorro em arco suave que cai graciosamente para trás
        GameObject seg1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        seg1.name = "Cone_Seg1";
        seg1.transform.SetParent(root.transform, false);
        seg1.transform.localPosition = new Vector3(0f, 0.11f, -0.02f);
        seg1.transform.localRotation = Quaternion.Euler(-5f, 0f, 0f);
        seg1.transform.localScale = new Vector3(0.42f, 0.14f, 0.42f);
        RemoverColisorObjeto(seg1);
        seg1.GetComponent<Renderer>().material = matVermelho;

        GameObject seg2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        seg2.name = "Cone_Seg2";
        seg2.transform.SetParent(root.transform, false);
        seg2.transform.localPosition = new Vector3(0f, 0.22f, -0.06f);
        seg2.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
        seg2.transform.localScale = new Vector3(0.32f, 0.13f, 0.32f);
        RemoverColisorObjeto(seg2);
        seg2.GetComponent<Renderer>().material = matVermelho;

        GameObject seg3 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        seg3.name = "Cone_Seg3";
        seg3.transform.SetParent(root.transform, false);
        seg3.transform.localPosition = new Vector3(0f, 0.31f, -0.13f);
        seg3.transform.localRotation = Quaternion.Euler(-26f, 0f, 0f);
        seg3.transform.localScale = new Vector3(0.22f, 0.12f, 0.22f);
        RemoverColisorObjeto(seg3);
        seg3.GetComponent<Renderer>().material = matVermelho;

        GameObject seg4 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        seg4.name = "Cone_Seg4";
        seg4.transform.SetParent(root.transform, false);
        seg4.transform.localPosition = new Vector3(0f, 0.38f, -0.21f);
        seg4.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);
        seg4.transform.localScale = new Vector3(0.14f, 0.10f, 0.14f);
        RemoverColisorObjeto(seg4);
        seg4.GetComponent<Renderer>().material = matVermelho;

        // 3. Pompom branco de pelúcia na ponta caída
        GameObject pompom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pompom.name = "Pompom";
        pompom.transform.SetParent(root.transform, false);
        pompom.transform.localPosition = new Vector3(0f, 0.41f, -0.29f);
        pompom.transform.localScale = new Vector3(0.17f, 0.17f, 0.17f);
        RemoverColisorObjeto(pompom);
        pompom.GetComponent<Renderer>().material = matBranco;

        return root;
    }

    private static void RemoverColisorObjeto(GameObject go)
    {
        if (go == null) return;
        Collider c = go.GetComponent<Collider>();
        if (c != null)
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }
    }

    public void ConfigurarTrenoPacMan(bool ativo)
    {
        var treno = FindFirstObjectByType<TrenoNatalinoPacMan>();
        if (treno == null)
        {
            GameObject player = GameObject.FindWithTag("Player") ?? GameObject.Find("Visual_PacMan");
            if (player != null)
            {
                treno = player.AddComponent<TrenoNatalinoPacMan>();
            }
        }
        if (treno != null)
        {
            treno.DefinirAtivo(ativo);
        }
    }

    // =========================================================================
    // DECORAÇÕES TEMÁTICAS PROCEDURAIS (CLÁSSICO E NATAL)
    // =========================================================================

    public void ConfigurarDecoracoesClassico(bool ativo)
    {
        if (m_ContainerDecoracoesClassico == null)
        {
            Transform tExistente = transform.Find("Decoracoes_Classico_3D");
            if (tExistente != null)
            {
                m_ContainerDecoracoesClassico = tExistente.gameObject;
            }
            else if (ativo)
            {
                m_ContainerDecoracoesClassico = ConstruirDecoracoesClassico();
            }
        }

        if (m_ContainerDecoracoesClassico != null)
        {
            m_ContainerDecoracoesClassico.SetActive(ativo);
        }
    }

    private GameObject ConstruirDecoracoesClassico()
    {
        GameObject root = new GameObject("Decoracoes_Classico_3D");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;

        Shader shaderLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");

        // Materiais para árvores
        Material matTronco = new Material(shaderLit) { name = "Mat_Tronco_Madeira" };
        Color corTronco = new Color(0.32f, 0.18f, 0.08f);
        if (matTronco.HasProperty("_BaseColor")) matTronco.SetColor("_BaseColor", corTronco);
        if (matTronco.HasProperty("_Color")) matTronco.SetColor("_Color", corTronco);
        if (matTronco.HasProperty("_Smoothness")) matTronco.SetFloat("_Smoothness", 0.15f);

        Material matCopa1 = new Material(shaderLit) { name = "Mat_Copa_Verde1" };
        Color corVerde1 = new Color(0.12f, 0.46f, 0.15f);
        if (matCopa1.HasProperty("_BaseColor")) matCopa1.SetColor("_BaseColor", corVerde1);
        if (matCopa1.HasProperty("_Color")) matCopa1.SetColor("_Color", corVerde1);
        if (matCopa1.HasProperty("_Smoothness")) matCopa1.SetFloat("_Smoothness", 0.25f);

        Material matCopa2 = new Material(shaderLit) { name = "Mat_Copa_Verde2" };
        Color corVerde2 = new Color(0.18f, 0.54f, 0.22f);
        if (matCopa2.HasProperty("_BaseColor")) matCopa2.SetColor("_BaseColor", corVerde2);
        if (matCopa2.HasProperty("_Color")) matCopa2.SetColor("_Color", corVerde2);
        if (matCopa2.HasProperty("_Smoothness")) matCopa2.SetFloat("_Smoothness", 0.25f);

        // 1. FLORESTA INFINITA AO REDOR DO LABIRINTO (360° sem corte de cenário)
        GameObject grupoArvores = new GameObject("Grupo_Floresta_Infinita_Perimetro");
        grupoArvores.transform.SetParent(root.transform, false);

        // Terreno estendido (180m x 180m) eliminando corte abrupto do quadrado
        GameObject pisoExt = GameObject.CreatePrimitive(PrimitiveType.Plane);
        pisoExt.name = "Piso_Terreno_Exterior";
        pisoExt.transform.SetParent(grupoArvores.transform, false);
        pisoExt.transform.localPosition = new Vector3(0f, -0.04f, 0f);
        pisoExt.transform.localScale = new Vector3(18f, 1f, 18f);
        RemoverColisorObjeto(pisoExt);
        Material matChaoExt = new Material(shaderLit) { name = "Mat_Chao_Floresta_Ext" };
        Color corGramaExt = new Color(0.09f, 0.28f, 0.12f);
        if (matChaoExt.HasProperty("_BaseColor")) matChaoExt.SetColor("_BaseColor", corGramaExt);
        if (matChaoExt.HasProperty("_Color")) matChaoExt.SetColor("_Color", corGramaExt);
        if (matChaoExt.HasProperty("_Smoothness")) matChaoExt.SetFloat("_Smoothness", 0.10f);
        pisoExt.GetComponent<Renderer>().material = matChaoExt;

        // Materiais adicionais para pinheiros alpinos escuros
        Material matPinheiro = new Material(shaderLit) { name = "Mat_Pinheiro_VerdeEscuro" };
        Color corPinheiro = new Color(0.06f, 0.22f, 0.10f);
        if (matPinheiro.HasProperty("_BaseColor")) matPinheiro.SetColor("_BaseColor", corPinheiro);
        if (matPinheiro.HasProperty("_Color")) matPinheiro.SetColor("_Color", corPinheiro);
        if (matPinheiro.HasProperty("_Smoothness")) matPinheiro.SetFloat("_Smoothness", 0.18f);

        // Gera 128 árvores distribuídas em 4 camadas perimétricas profundas (18m, 30m, 48m, 72m)
        int[] arvoresPorCamada = new int[] { 28, 32, 34, 34 };
        float[] raiosCamada = new float[] { 19.5f, 32.0f, 48.0f, 72.0f };

        int idArvore = 0;
        for (int c = 0; c < raiosCamada.Length; c++)
        {
            int qtd = arvoresPorCamada[c];
            float raioBase = raiosCamada[c];

            for (int i = 0; i < qtd; i++)
            {
                float angulo = (i + (c * 0.35f)) * (Mathf.PI * 2f / qtd);
                float raio = raioBase + 3.8f * Mathf.Sin(i * 1.9f + c);
                float x = Mathf.Cos(angulo) * raio;
                float z = Mathf.Sin(angulo) * raio;

                GameObject arvore = new GameObject($"Arvore_{idArvore++}");
                arvore.transform.SetParent(grupoArvores.transform, false);
                arvore.transform.position = new Vector3(x, 0f, z);

                float escala = 0.85f + 0.40f * Mathf.Abs(Mathf.Sin(idArvore * 2.1f));
                if (c >= 2) escala *= 1.35f; // Árvores mais distantes são mais altas para cobrir o horizonte

                // Alterna entre Pinheiro Cônico e Árvore Frondosa de Carvalho
                bool ehPinheiro = (idArvore % 2 == 0);

                // Tronco
                GameObject tronco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tronco.name = "Tronco";
                tronco.transform.SetParent(arvore.transform, false);
                float altTronco = (ehPinheiro ? 1.8f : 1.4f) * escala;
                tronco.transform.localPosition = new Vector3(0f, altTronco, 0f);
                tronco.transform.localScale = new Vector3(0.50f * escala, altTronco, 0.50f * escala);
                RemoverColisorObjeto(tronco);
                tronco.GetComponent<Renderer>().material = matTronco;

                if (ehPinheiro)
                {
                    // Pinheiro em 3 camadas cônicas
                    for (int k = 0; k < 3; k++)
                    {
                        GameObject camadaPinheiro = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        camadaPinheiro.name = $"Pinheiro_Tier_{k}";
                        camadaPinheiro.transform.SetParent(arvore.transform, false);
                        float yOffset = (2.2f + k * 1.35f) * escala;
                        float raioCamada = (2.6f - k * 0.70f) * escala;
                        camadaPinheiro.transform.localPosition = new Vector3(0f, yOffset, 0f);
                        camadaPinheiro.transform.localScale = new Vector3(raioCamada, 0.90f * escala, raioCamada);
                        RemoverColisorObjeto(camadaPinheiro);
                        camadaPinheiro.GetComponent<Renderer>().material = matPinheiro;
                    }
                }
                else
                {
                    // Copa Frondosa Dupla
                    GameObject copa1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    copa1.name = "Copa_Tier1";
                    copa1.transform.SetParent(arvore.transform, false);
                    copa1.transform.localPosition = new Vector3(0f, 3.2f * escala, 0f);
                    copa1.transform.localScale = new Vector3(2.8f * escala, 1.8f * escala, 2.8f * escala);
                    RemoverColisorObjeto(copa1);
                    copa1.GetComponent<Renderer>().material = (i % 2 == 0) ? matCopa1 : matCopa2;

                    GameObject copa2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    copa2.name = "Copa_Tier2";
                    copa2.transform.SetParent(arvore.transform, false);
                    copa2.transform.localPosition = new Vector3(0f, 4.4f * escala, 0f);
                    copa2.transform.localScale = new Vector3(2.0f * escala, 1.6f * escala, 2.0f * escala);
                    RemoverColisorObjeto(copa2);
                    copa2.GetComponent<Renderer>().material = (i % 2 == 0) ? matCopa2 : matCopa1;
                }
            }
        }

        // 2. QUADROS DECORATIVOS NAS PAREDES CINZAS INTERNAS (Estilo Casa / Galeria de Arte)
        GameObject grupoQuadros = new GameObject("Grupo_Quadros_Paredes");
        grupoQuadros.transform.SetParent(root.transform, false);

        Material matMolduraQuadro = new Material(shaderLit) { name = "Mat_Moldura_MadeiraEscura" };
        Color corMoldura = new Color(0.20f, 0.10f, 0.05f);
        if (matMolduraQuadro.HasProperty("_BaseColor")) matMolduraQuadro.SetColor("_BaseColor", corMoldura);
        if (matMolduraQuadro.HasProperty("_Color")) matMolduraQuadro.SetColor("_Color", corMoldura);
        if (matMolduraQuadro.HasProperty("_Smoothness")) matMolduraQuadro.SetFloat("_Smoothness", 0.40f);

        Color[] coresTelas = new Color[]
        {
            new Color(0.15f, 0.40f, 0.55f), // Paisagem Marinha / Céu
            new Color(0.55f, 0.20f, 0.15f), // Retrato Clássico Vinho
            new Color(0.20f, 0.48f, 0.25f), // Botânica / Floresta
            new Color(0.50f, 0.42f, 0.18f), // Pergaminho / Parasitologia
            new Color(0.35f, 0.18f, 0.50f), // Arte Abstrata Roxa
            new Color(0.22f, 0.35f, 0.42f), // Ilustração Científica
            new Color(0.55f, 0.35f, 0.10f), // Pôr do Sol Dourado
            new Color(0.18f, 0.45f, 0.38f)  // Esmeralda Clássico
        };

        // Posições estratégicas nas paredes interiores a 1.55m de altura (nível dos olhos)
        (Vector3 pos, Quaternion rot)[] spotsQuadros = new (Vector3, Quaternion)[]
        {
            (new Vector3(-7f, 1.55f, 5.26f), Quaternion.Euler(0f, 180f, 0f)),
            (new Vector3(7f, 1.55f, 5.26f), Quaternion.Euler(0f, 180f, 0f)),
            (new Vector3(-7f, 1.55f, -5.26f), Quaternion.Euler(0f, 0f, 0f)),
            (new Vector3(7f, 1.55f, -5.26f), Quaternion.Euler(0f, 0f, 0f)),
            (new Vector3(0f, 1.55f, 7.76f), Quaternion.Euler(0f, 180f, 0f)),
            (new Vector3(0f, 1.55f, -7.76f), Quaternion.Euler(0f, 0f, 0f)),
            (new Vector3(-9.76f, 1.55f, 0f), Quaternion.Euler(0f, 90f, 0f)),
            (new Vector3(9.76f, 1.55f, 0f), Quaternion.Euler(0f, -90f, 0f))
        };

        for (int i = 0; i < spotsQuadros.Length; i++)
        {
            var spot = spotsQuadros[i];
            GameObject quadro = new GameObject($"Quadro_Parede_{i}");
            quadro.transform.SetParent(grupoQuadros.transform, false);
            quadro.transform.position = spot.pos;
            quadro.transform.rotation = spot.rot;

            // Moldura externa
            GameObject moldura = GameObject.CreatePrimitive(PrimitiveType.Cube);
            moldura.name = "Moldura";
            moldura.transform.SetParent(quadro.transform, false);
            moldura.transform.localPosition = Vector3.zero;
            moldura.transform.localScale = new Vector3(1.30f, 0.95f, 0.08f);
            RemoverColisorObjeto(moldura);
            moldura.GetComponent<Renderer>().material = matMolduraQuadro;

            // Tela de pintura interna
            Material matTela = new Material(shaderLit) { name = $"Mat_Tela_{i}" };
            Color corTela = coresTelas[i % coresTelas.Length];
            if (matTela.HasProperty("_BaseColor")) matTela.SetColor("_BaseColor", corTela);
            if (matTela.HasProperty("_Color")) matTela.SetColor("_Color", corTela);
            if (matTela.HasProperty("_Smoothness")) matTela.SetFloat("_Smoothness", 0.60f);

            GameObject tela = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tela.name = "Pintura";
            tela.transform.SetParent(quadro.transform, false);
            tela.transform.localPosition = new Vector3(0f, 0f, 0.035f);
            tela.transform.localScale = new Vector3(1.12f, 0.78f, 0.04f);
            RemoverColisorObjeto(tela);
            tela.GetComponent<Renderer>().material = matTela;
        }

        // 3. JANELAS DE CASA NAS PAREDES PERIMETRAIS (Estilo Interior de Residência)
        GameObject grupoJanelas = new GameObject("Grupo_Janelas_Paredes");
        grupoJanelas.transform.SetParent(root.transform, false);

        Material matMolduraJanela = new Material(shaderLit) { name = "Mat_Moldura_Janela_Branca" };
        Color corBranca = new Color(0.92f, 0.93f, 0.96f);
        if (matMolduraJanela.HasProperty("_BaseColor")) matMolduraJanela.SetColor("_BaseColor", corBranca);
        if (matMolduraJanela.HasProperty("_Color")) matMolduraJanela.SetColor("_Color", corBranca);
        if (matMolduraJanela.HasProperty("_Smoothness")) matMolduraJanela.SetFloat("_Smoothness", 0.30f);

        Material matVidroJanela = new Material(shaderLit) { name = "Mat_Vidro_Janela_Dia" };
        Color corVidro = new Color(0.68f, 0.88f, 1.0f);
        if (matVidroJanela.HasProperty("_BaseColor")) matVidroJanela.SetColor("_BaseColor", corVidro);
        if (matVidroJanela.HasProperty("_Color")) matVidroJanela.SetColor("_Color", corVidro);
        if (matVidroJanela.HasProperty("_Smoothness")) matVidroJanela.SetFloat("_Smoothness", 0.85f);
        if (matVidroJanela.HasProperty("_EmissionColor"))
        {
            matVidroJanela.EnableKeyword("_EMISSION");
            matVidroJanela.SetColor("_EmissionColor", corVidro * 0.40f);
        }

        (Vector3 pos, Quaternion rot)[] spotsJanelas = new (Vector3, Quaternion)[]
        {
            (new Vector3(-6.5f, 1.60f, 12.76f), Quaternion.Euler(0f, 180f, 0f)),
            (new Vector3(6.5f, 1.60f, 12.76f), Quaternion.Euler(0f, 180f, 0f)),
            (new Vector3(-6.5f, 1.60f, -12.76f), Quaternion.Euler(0f, 0f, 0f)),
            (new Vector3(6.5f, 1.60f, -12.76f), Quaternion.Euler(0f, 0f, 0f)),
            (new Vector3(12.76f, 1.60f, 5.0f), Quaternion.Euler(0f, -90f, 0f)),
            (new Vector3(12.76f, 1.60f, -5.0f), Quaternion.Euler(0f, -90f, 0f)),
            (new Vector3(-12.76f, 1.60f, 5.0f), Quaternion.Euler(0f, 90f, 0f)),
            (new Vector3(-12.76f, 1.60f, -5.0f), Quaternion.Euler(0f, 90f, 0f))
        };

        for (int i = 0; i < spotsJanelas.Length; i++)
        {
            var spot = spotsJanelas[i];
            GameObject janela = new GameObject($"Janela_Casa_{i}");
            janela.transform.SetParent(grupoJanelas.transform, false);
            janela.transform.position = spot.pos;
            janela.transform.rotation = spot.rot;

            // Vidro luminoso de dia
            GameObject vidro = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vidro.name = "Vidro_Dia";
            vidro.transform.SetParent(janela.transform, false);
            vidro.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            vidro.transform.localScale = new Vector3(1.40f, 1.25f, 0.04f);
            RemoverColisorObjeto(vidro);
            vidro.GetComponent<Renderer>().material = matVidroJanela;

            // Moldura externa branca
            GameObject moldura = GameObject.CreatePrimitive(PrimitiveType.Cube);
            moldura.name = "Moldura_Externa";
            moldura.transform.SetParent(janela.transform, false);
            moldura.transform.localPosition = Vector3.zero;
            moldura.transform.localScale = new Vector3(1.52f, 1.38f, 0.06f);
            RemoverColisorObjeto(moldura);
            moldura.GetComponent<Renderer>().material = matMolduraJanela;

            // Divisórias de 4 vidros (Cruzeta / Mullions)
            GameObject barraVert = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barraVert.name = "Divisoria_Vertical";
            barraVert.transform.SetParent(janela.transform, false);
            barraVert.transform.localPosition = new Vector3(0f, 0f, 0.045f);
            barraVert.transform.localScale = new Vector3(0.08f, 1.25f, 0.04f);
            RemoverColisorObjeto(barraVert);
            barraVert.GetComponent<Renderer>().material = matMolduraJanela;

            GameObject barraHoriz = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barraHoriz.name = "Divisoria_Horizontal";
            barraHoriz.transform.SetParent(janela.transform, false);
            barraHoriz.transform.localPosition = new Vector3(0f, 0f, 0.045f);
            barraHoriz.transform.localScale = new Vector3(1.40f, 0.08f, 0.04f);
            RemoverColisorObjeto(barraHoriz);
            barraHoriz.GetComponent<Renderer>().material = matMolduraJanela;
        }

        return root;
    }

    public void ConfigurarDecoracoesNatal(bool ativo)
    {
        if (m_ContainerDecoracoesNatal == null)
        {
            Transform tExistente = transform.Find("Decoracoes_Natal_3D");
            if (tExistente != null)
            {
                m_ContainerDecoracoesNatal = tExistente.gameObject;
            }
            else if (ativo)
            {
                m_ContainerDecoracoesNatal = ConstruirDecoracoesNatal();
            }
        }

        if (m_ContainerDecoracoesNatal != null)
        {
            m_ContainerDecoracoesNatal.SetActive(ativo);
        }
    }

    private GameObject ConstruirDecoracoesNatal()
    {
        GameObject root = new GameObject("Decoracoes_Natal_3D");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;

        Shader shaderLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");

        // Materiais festivos
        Material matPinheiro = new Material(shaderLit) { name = "Mat_Pinheiro_Natal" };
        Color corVerdePinheiro = new Color(0.06f, 0.38f, 0.14f);
        if (matPinheiro.HasProperty("_BaseColor")) matPinheiro.SetColor("_BaseColor", corVerdePinheiro);
        if (matPinheiro.HasProperty("_Color")) matPinheiro.SetColor("_Color", corVerdePinheiro);
        if (matPinheiro.HasProperty("_Smoothness")) matPinheiro.SetFloat("_Smoothness", 0.30f);

        Material matEstrelaOuro = new Material(shaderLit) { name = "Mat_Estrela_Ouro" };
        Color corOuro = new Color(1.0f, 0.85f, 0.15f);
        if (matEstrelaOuro.HasProperty("_BaseColor")) matEstrelaOuro.SetColor("_BaseColor", corOuro);
        if (matEstrelaOuro.HasProperty("_Color")) matEstrelaOuro.SetColor("_Color", corOuro);
        if (matEstrelaOuro.HasProperty("_Smoothness")) matEstrelaOuro.SetFloat("_Smoothness", 0.85f);
        if (matEstrelaOuro.HasProperty("_EmissionColor"))
        {
            matEstrelaOuro.EnableKeyword("_EMISSION");
            matEstrelaOuro.SetColor("_EmissionColor", corOuro * 0.90f);
        }

        Material matVermelhoNatal = new Material(shaderLit) { name = "Mat_Bolinhas_Vermelha" };
        Color corVermelha = new Color(0.85f, 0.08f, 0.12f);
        if (matVermelhoNatal.HasProperty("_BaseColor")) matVermelhoNatal.SetColor("_BaseColor", corVermelha);
        if (matVermelhoNatal.HasProperty("_Color")) matVermelhoNatal.SetColor("_Color", corVermelha);
        if (matVermelhoNatal.HasProperty("_Smoothness")) matVermelhoNatal.SetFloat("_Smoothness", 0.85f);

        Material matOuroPolido = new Material(shaderLit) { name = "Mat_Bolinhas_Ouro" };
        if (matOuroPolido.HasProperty("_BaseColor")) matOuroPolido.SetColor("_BaseColor", new Color(0.95f, 0.75f, 0.10f));
        if (matOuroPolido.HasProperty("_Smoothness")) matOuroPolido.SetFloat("_Smoothness", 0.90f);

        Material matCianoGelo = new Material(shaderLit) { name = "Mat_Bolinhas_Ciano" };
        if (matCianoGelo.HasProperty("_BaseColor")) matCianoGelo.SetColor("_BaseColor", new Color(0.10f, 0.85f, 1.0f));
        if (matCianoGelo.HasProperty("_Smoothness")) matCianoGelo.SetFloat("_Smoothness", 0.85f);

        Material matFitaPresente = new Material(shaderLit) { name = "Mat_Fita_Dourada" };
        if (matFitaPresente.HasProperty("_BaseColor")) matFitaPresente.SetColor("_BaseColor", new Color(1.0f, 0.90f, 0.20f));
        if (matFitaPresente.HasProperty("_Smoothness")) matFitaPresente.SetFloat("_Smoothness", 0.80f);

        Material matFitaVermelha = new Material(shaderLit) { name = "Mat_Fita_Vermelha" };
        if (matFitaVermelha.HasProperty("_BaseColor")) matFitaVermelha.SetColor("_BaseColor", new Color(0.85f, 0.10f, 0.15f));
        if (matFitaVermelha.HasProperty("_Smoothness")) matFitaVermelha.SetFloat("_Smoothness", 0.80f);

        Material[] matsBolinhas = new Material[] { matVermelhoNatal, matOuroPolido, matCianoGelo };

        // 1. ÁRVORES DE NATAL DECORADAS (4 nos cantos do labirinto + 2 na saída da gaiola)
        Vector3[] posicoesArvoresNatal = new Vector3[]
        {
            new Vector3(-11.5f, 0f, 11.5f),
            new Vector3(11.5f, 0f, 11.5f),
            new Vector3(-11.5f, 0f, -11.5f),
            new Vector3(11.5f, 0f, -11.5f),
            new Vector3(-4.2f, 0f, 0f),
            new Vector3(4.2f, 0f, 0f)
        };

        for (int i = 0; i < posicoesArvoresNatal.Length; i++)
        {
            GameObject arvoreNatal = new GameObject($"Arvore_Natal_{i}");
            arvoreNatal.transform.SetParent(root.transform, false);
            arvoreNatal.transform.position = posicoesArvoresNatal[i];

            // 3 Tiers cônicos de pinheiro
            float[] raiosTier = { 1.50f, 1.15f, 0.75f };
            float[] alturasTier = { 1.20f, 2.20f, 3.10f };

            for (int t = 0; t < 3; t++)
            {
                GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cone.name = $"Pinheiro_Tier_{t}";
                cone.transform.SetParent(arvoreNatal.transform, false);
                cone.transform.localPosition = new Vector3(0f, alturasTier[t], 0f);
                cone.transform.localScale = new Vector3(raiosTier[t], 0.65f, raiosTier[t]);
                RemoverColisorObjeto(cone);
                cone.GetComponent<Renderer>().material = matPinheiro;

                // 4 Bolinhas decorativas por tier
                for (int b = 0; b < 4; b++)
                {
                    float ang = b * (Mathf.PI * 0.5f) + (t * 0.4f);
                    float rb = (raiosTier[t] * 0.48f);
                    Vector3 posBolinha = new Vector3(Mathf.Cos(ang) * rb, alturasTier[t] - 0.22f, Mathf.Sin(ang) * rb);

                    GameObject bolinha = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    bolinha.name = $"Bolinha_{t}_{b}";
                    bolinha.transform.SetParent(arvoreNatal.transform, false);
                    bolinha.transform.localPosition = posBolinha;
                    bolinha.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
                    RemoverColisorObjeto(bolinha);
                    bolinha.GetComponent<Renderer>().material = matsBolinhas[(t + b) % matsBolinhas.Length];
                }
            }

            // Estrela dourada no topo
            GameObject estrela = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            estrela.name = "Estrela_Topo";
            estrela.transform.SetParent(arvoreNatal.transform, false);
            estrela.transform.localPosition = new Vector3(0f, 3.85f, 0f);
            estrela.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
            RemoverColisorObjeto(estrela);
            estrela.GetComponent<Renderer>().material = matEstrelaOuro;

            // 2. CAIXAS DE PRESENTES EMBRULHADOS AO PÉ DA ÁRVORE
            Color[] coresPresente = new Color[]
            {
                new Color(0.80f, 0.08f, 0.12f), // Vermelho
                new Color(0.08f, 0.50f, 0.18f), // Verde esmeralda
                new Color(0.10f, 0.35f, 0.75f), // Azul safira
                new Color(0.85f, 0.65f, 0.10f)  // Dourado
            };

            for (int p = 0; p < 2; p++)
            {
                float angP = (p == 0) ? 0.8f : -1.2f;
                Vector3 posP = new Vector3(Mathf.Cos(angP) * 1.1f, 0.22f, Mathf.Sin(angP) * 1.1f);

                GameObject presente = GameObject.CreatePrimitive(PrimitiveType.Cube);
                presente.name = $"Presente_{p}";
                presente.transform.SetParent(arvoreNatal.transform, false);
                presente.transform.localPosition = posP;
                presente.transform.localRotation = Quaternion.Euler(0f, (p * 35f), 0f);
                presente.transform.localScale = new Vector3(0.48f, 0.44f, 0.48f);
                RemoverColisorObjeto(presente);

                Material matCaixa = new Material(shaderLit) { name = $"Mat_Caixa_{i}_{p}" };
                Color corCx = coresPresente[(i + p) % coresPresente.Length];
                if (matCaixa.HasProperty("_BaseColor")) matCaixa.SetColor("_BaseColor", corCx);
                if (matCaixa.HasProperty("_Color")) matCaixa.SetColor("_Color", corCx);
                if (matCaixa.HasProperty("_Smoothness")) matCaixa.SetFloat("_Smoothness", 0.60f);
                presente.GetComponent<Renderer>().material = matCaixa;

                // Fita do presente
                GameObject fita = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fita.name = "Fita_Presente";
                fita.transform.SetParent(presente.transform, false);
                fita.transform.localPosition = new Vector3(0f, 0f, 0f);
                fita.transform.localScale = new Vector3(1.02f, 1.02f, 0.18f);
                RemoverColisorObjeto(fita);
                fita.GetComponent<Renderer>().material = (corCx == corOuro) ? matFitaVermelha : matFitaPresente;

                // Lacinho de fita no topo do presente
                GameObject laco = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                laco.name = "Laco_Topo";
                laco.transform.SetParent(presente.transform, false);
                laco.transform.localPosition = new Vector3(0f, 0.54f, 0f);
                laco.transform.localScale = new Vector3(0.32f, 0.18f, 0.32f);
                RemoverColisorObjeto(laco);
                laco.GetComponent<Renderer>().material = fita.GetComponent<Renderer>().material;
            }
        }

        // 3. FLORESTA INFINITA DE PINHEIROS DE NATAL 360° (180m x 180m com 128 pinheiros decorados)
        GameObject grupoPinheirosPerimetro = new GameObject("Grupo_Pinheiros_Natal_Perimetro");
        grupoPinheirosPerimetro.transform.SetParent(root.transform, false);

        // Terreno estendido de neve branca (180m x 180m)
        GameObject pisoNeveExt = GameObject.CreatePrimitive(PrimitiveType.Plane);
        pisoNeveExt.name = "Piso_Terreno_Exterior_Neve";
        pisoNeveExt.transform.SetParent(grupoPinheirosPerimetro.transform, false);
        pisoNeveExt.transform.localPosition = new Vector3(0f, -0.04f, 0f);
        pisoNeveExt.transform.localScale = new Vector3(18f, 1f, 18f);
        RemoverColisorObjeto(pisoNeveExt);

        Material matNeveExt = new Material(shaderLit) { name = "Mat_Chao_Neve_Ext" };
        Color corNeveExt = new Color(0.92f, 0.95f, 1.0f);
        if (matNeveExt.HasProperty("_BaseColor")) matNeveExt.SetColor("_BaseColor", corNeveExt);
        if (matNeveExt.HasProperty("_Color")) matNeveExt.SetColor("_Color", corNeveExt);
        if (matNeveExt.HasProperty("_Smoothness")) matNeveExt.SetFloat("_Smoothness", 0.40f);
        pisoNeveExt.GetComponent<Renderer>().material = matNeveExt;

        int[] pinheirosPorCamada = new int[] { 28, 32, 34, 34 };
        float[] raiosCamadaPinheiros = new float[] { 19.5f, 32.0f, 48.0f, 72.0f };

        int idPinheiro = 0;
        for (int c = 0; c < raiosCamadaPinheiros.Length; c++)
        {
            int qtd = pinheirosPorCamada[c];
            float raioBase = raiosCamadaPinheiros[c];

            for (int k = 0; k < qtd; k++)
            {
                float angulo = (k + (c * 0.35f)) * (Mathf.PI * 2f / qtd);
                float raio = raioBase + 3.5f * Mathf.Sin(k * 1.8f + c);
                float x = Mathf.Cos(angulo) * raio;
                float z = Mathf.Sin(angulo) * raio;

                GameObject pinheiro = new GameObject($"Pinheiro_Natal_Perimetro_{idPinheiro++}");
                pinheiro.transform.SetParent(grupoPinheirosPerimetro.transform, false);
                pinheiro.transform.position = new Vector3(x, 0f, z);

                float escala = 0.85f + 0.45f * Mathf.Abs(Mathf.Sin(idPinheiro * 2.3f));
                if (c >= 2) escala *= 1.35f;

                // Tronco
                GameObject tronco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tronco.name = "Tronco";
                tronco.transform.SetParent(pinheiro.transform, false);
                float altTr = 1.6f * escala;
                tronco.transform.localPosition = new Vector3(0f, altTr, 0f);
                tronco.transform.localScale = new Vector3(0.45f * escala, altTr, 0.45f * escala);
                RemoverColisorObjeto(tronco);
                Material matTroncoPinheiro = new Material(shaderLit) { color = new Color(0.28f, 0.16f, 0.08f) };
                tronco.GetComponent<Renderer>().material = matTroncoPinheiro;

                // 3 Tiers de Pinheiro com Bolinhas
                for (int t = 0; t < 3; t++)
                {
                    GameObject camadaCone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    camadaCone.name = $"Cone_Tier_{t}";
                    camadaCone.transform.SetParent(pinheiro.transform, false);
                    float yOffset = (2.2f + t * 1.30f) * escala;
                    float raioCamada = (2.6f - t * 0.70f) * escala;
                    camadaCone.transform.localPosition = new Vector3(0f, yOffset, 0f);
                    camadaCone.transform.localScale = new Vector3(raioCamada, 0.65f * escala, raioCamada);
                    RemoverColisorObjeto(camadaCone);
                    camadaCone.GetComponent<Renderer>().material = matPinheiro;

                    // Bolinhas
                    for (int b = 0; b < 4; b++)
                    {
                        float angB = b * (Mathf.PI * 0.5f) + (t * 0.45f);
                        float rb = raioCamada * 0.48f;
                        Vector3 posB = new Vector3(Mathf.Cos(angB) * rb, yOffset - 0.18f * escala, Mathf.Sin(angB) * rb);

                        GameObject bolinha = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        bolinha.name = $"Bolinha_{t}_{b}";
                        bolinha.transform.SetParent(pinheiro.transform, false);
                        bolinha.transform.localPosition = posB;
                        bolinha.transform.localScale = Vector3.one * (0.24f * escala);
                        RemoverColisorObjeto(bolinha);
                        bolinha.GetComponent<Renderer>().material = matsBolinhas[(t + b + idPinheiro) % matsBolinhas.Length];
                    }
                }

                // Estrela dourada no topo
                GameObject estrela = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                estrela.name = "Estrela_Topo";
                estrela.transform.SetParent(pinheiro.transform, false);
                estrela.transform.localPosition = new Vector3(0f, (5.8f) * escala, 0f);
                estrela.transform.localScale = Vector3.one * (0.45f * escala);
                RemoverColisorObjeto(estrela);
                estrela.GetComponent<Renderer>().material = matEstrelaOuro;
            }
        }



                // 3. EFEITO DE NEVE SUAVE CAINDO SOBRE O LABIRINTO (ParticleSystem)
        GameObject objNeve = new GameObject("Efeito_Nevasca_Natal");
        objNeve.transform.SetParent(root.transform, false);
        objNeve.transform.position = new Vector3(0f, 12f, 0f);

        ParticleSystem ps = objNeve.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 6.0f;
        main.startSpeed = 2.0f;
        main.startSize = 0.10f;
        main.maxParticles = 800;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(0.95f, 0.97f, 1.0f, 0.85f);

        var emission = ps.emission;
        emission.rateOverTime = 140f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 1f, 30f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

        ParticleSystemRenderer psr = objNeve.GetComponent<ParticleSystemRenderer>();
        if (psr != null)
        {
            Shader shaderParticula = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                                    Shader.Find("Mobile/Particles/Additive") ??
                                    Shader.Find("Sprites/Default");
            Material matFloco = new Material(shaderParticula) { name = "Mat_Flocos_Neve" };
            if (matFloco.HasProperty("_BaseColor")) matFloco.SetColor("_BaseColor", Color.white);
            if (matFloco.HasProperty("_Color")) matFloco.SetColor("_Color", Color.white);
            psr.material = matFloco;
        }

        return root;
    }

    /// <summary>
    /// Instancia as paredes perimetrais e corredores internos do labirinto 3D com BoxColliders e materiais polidos.
    /// </summary>
    
    /// <summary>
    /// Garante o componente RelationBuilder ativo no Player para traçar Object Properties (LineRenderer).
    /// As antigas Power Pellets estáticas de quina foram migradas para a regra ontológica estrita:
    /// estritamente 1 a cada 3 orbes sequenciais concede o poder de devorar fantasmas.
    /// </summary>
    public void InstanciarCerejasEPowerPellets()
    {
        if (!instanciarGemas) return;

        // Garante que o Player possua o componente RelationBuilder ativo para traçar Object Properties (LineRenderer)
        GameObject playerObj = GameObject.FindWithTag("Player") ?? GameObject.Find("Visual_PacMan");
        if (playerObj != null && playerObj.GetComponent<RelationBuilder>() == null)
        {
            playerObj.AddComponent<RelationBuilder>();
        }

        Debug.Log("<color=#A855F7><b>[Mapeamento Ontológico]</b></color> RelationBuilder (LineRenderer) ativo no Pac-Man.");
    }

    public void ConstruirLabirinto3D()
    {
        // Limpa com segurança qualquer container anterior de paredes
        if (m_ContainerParedes != null)
        {
            if (Application.isPlaying) Destroy(m_ContainerParedes);
            else DestroyImmediate(m_ContainerParedes);
            m_ContainerParedes = null;
        }

        Transform tAntigo = transform.Find("Labirinto_Paredes_3D");
        if (tAntigo != null)
        {
            if (Application.isPlaying) Destroy(tAntigo.gameObject);
            else DestroyImmediate(tAntigo.gameObject);
        }

        m_ContainerParedes = new GameObject("Labirinto_Paredes_3D");
        m_ContainerParedes.transform.SetParent(transform);
        m_ContainerParedes.transform.localPosition = Vector3.zero;

        Material matParede = materialParedes;
        if (matParede == null)
        {
            if (!usarEsteticaDarkGrid)
            {
                matParede = CriarMaterialParedePorSuperclasse("Parasitas");
            }
            else
            {
                matParede = Resources.Load<Material>("Mat_Parede_Labirinto_AzulNeon");
#if UNITY_EDITOR
                if (matParede == null)
                {
                    matParede = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Parede_Labirinto_AzulNeon.mat");
                }
#endif
                if (matParede == null)
                {
                    matParede = CriarMaterialParedeBiomedicaSciFi();
                }
            }
        }

        float metade = tamanhoLabirinto / 2f;
        float yCentro = transform.position.y + (alturaParedes / 2f);
        float yChao = ObterAlturaSuperficieChao(Vector3.zero);

        // 1. Paredes Perimetrais Externas com Túneis Warp Leste-Oeste e Portal de Escape Norte
        float larguraAsaNorte = (tamanhoLabirinto - 4.4f) / 2f;
        float offsetAsaNorte = (metade + 2.2f) / 2f;
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-offsetAsaNorte, yCentro, metade), new Vector3(larguraAsaNorte + espessuraParedes, alturaParedes, espessuraParedes), matParede, "Parede_Norte_Oeste");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(offsetAsaNorte, yCentro, metade), new Vector3(larguraAsaNorte + espessuraParedes, alturaParedes, espessuraParedes), matParede, "Parede_Norte_Leste");

        // Portal de Saída Norte
        ConstruirPortalSaidaProcedural(m_ContainerParedes.transform, new Vector3(0f, yCentro, metade), matParede);

        // Parede Sul Perimetral
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(0f, yCentro, -metade), new Vector3(tamanhoLabirinto + espessuraParedes, alturaParedes, espessuraParedes), matParede, "Parede_Sul");

        // Parede Leste dividida em duas asas com abertura para o Túnel Warp (Z entre -1.8 e +1.8)
        float meiaAberturaTunel = 1.8f;
        float comprimentoAsaTunel = (tamanhoLabirinto - (meiaAberturaTunel * 2f)) / 2f;
        float centroZAsaNorte = (metade + meiaAberturaTunel) / 2f;
        float centroZAsaSul = -(metade + meiaAberturaTunel) / 2f;

        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(metade, yCentro, centroZAsaNorte), new Vector3(espessuraParedes, alturaParedes, comprimentoAsaTunel), matParede, "Parede_Leste_Norte");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(metade, yCentro, centroZAsaSul), new Vector3(espessuraParedes, alturaParedes, comprimentoAsaTunel), matParede, "Parede_Leste_Sul");

        // Parede Oeste dividida em duas asas com abertura para o Túnel Warp
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-metade, yCentro, centroZAsaNorte), new Vector3(espessuraParedes, alturaParedes, comprimentoAsaTunel), matParede, "Parede_Oeste_Norte");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-metade, yCentro, centroZAsaSul), new Vector3(espessuraParedes, alturaParedes, comprimentoAsaTunel), matParede, "Parede_Oeste_Sul");

        // Gatilhos de Teletransporte Warp Leste-Oeste (Clássico Pac-Man)
        CriarGatilhoWarpTunel(m_ContainerParedes.transform, new Vector3(-metade - 0.4f, yCentro, 0f), new Vector3(1.2f, alturaParedes, 3.2f), new Vector3(metade - 1.4f, yChao, 0f), "Gatilho_Warp_OesteParaLeste");
        CriarGatilhoWarpTunel(m_ContainerParedes.transform, new Vector3(metade + 0.4f, yCentro, 0f), new Vector3(1.2f, alturaParedes, 3.2f), new Vector3(-metade + 1.4f, yChao, 0f), "Gatilho_Warp_LesteParaOeste");

        // 2. Gaiola Central dos Fantasmas (Ghost House) com Porta Neon Translúcida
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-2.6f, yCentro, 0f), new Vector3(espessuraParedes, alturaParedes, 4.4f), matParede, "Parede_Gaiola_Oeste");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(2.6f, yCentro, 0f), new Vector3(espessuraParedes, alturaParedes, 4.4f), matParede, "Parede_Gaiola_Leste");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(0f, yCentro, -2.2f), new Vector3(5.2f + espessuraParedes, alturaParedes, espessuraParedes), matParede, "Parede_Gaiola_Sul");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-1.8f, yCentro, 2.2f), new Vector3(1.8f, alturaParedes, espessuraParedes), matParede, "Parede_Gaiola_Norte_Esq");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(1.8f, yCentro, 2.2f), new Vector3(1.8f, alturaParedes, espessuraParedes), matParede, "Parede_Gaiola_Norte_Dir");

        // Barra/Portão Neon da Gaiola Central
        GameObject portaoBarra = GameObject.CreatePrimitive(PrimitiveType.Cube);
        portaoBarra.name = "Portao_Barra_Gaiola_Neon";
        portaoBarra.transform.SetParent(m_ContainerParedes.transform, false);
        portaoBarra.transform.position = new Vector3(0f, yCentro, 2.2f);
        portaoBarra.transform.localScale = new Vector3(1.8f, 0.28f, 0.28f);
        var colPortao = portaoBarra.GetComponent<Collider>();
        if (colPortao != null) colPortao.isTrigger = true;
        Material matPortao = CriarMaterialTranslúcidoNodo(new Color(0.0f, 0.95f, 1.0f, 1f), 0.85f);
        portaoBarra.GetComponent<Renderer>().material = matPortao;

        // 3. Topologia de Corredores e Blocos Internos do Labirinto (Multi-vias e Loops)
        // Blocos em L nos 4 quadrantes
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-7f, yCentro, 6f), new Vector3(6f, alturaParedes, espessuraParedes), matParede, "Parede_NW_H");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-7f, yCentro, 8.5f), new Vector3(espessuraParedes, alturaParedes, 5f), matParede, "Parede_NW_V");

        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(7f, yCentro, 6f), new Vector3(6f, alturaParedes, espessuraParedes), matParede, "Parede_NE_H");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(7f, yCentro, 8.5f), new Vector3(espessuraParedes, alturaParedes, 5f), matParede, "Parede_NE_V");

        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-7f, yCentro, -6f), new Vector3(6f, alturaParedes, espessuraParedes), matParede, "Parede_SW_H");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-7f, yCentro, -8.5f), new Vector3(espessuraParedes, alturaParedes, 5f), matParede, "Parede_SW_V");

        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(7f, yCentro, -6f), new Vector3(6f, alturaParedes, espessuraParedes), matParede, "Parede_SE_H");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(7f, yCentro, -8.5f), new Vector3(espessuraParedes, alturaParedes, 5f), matParede, "Parede_SE_V");

        // Divisórias centrais Norte e Sul
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(0f, yCentro, 8.5f), new Vector3(6f, alturaParedes, espessuraParedes), matParede, "Parede_Norte_T");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(0f, yCentro, -8.5f), new Vector3(6f, alturaParedes, espessuraParedes), matParede, "Parede_Sul_T");

        // Blocos de contorno lateral para corredores em anel (Loops de fuga)
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(-10.5f, yCentro, 0f), new Vector3(espessuraParedes, alturaParedes, 4.5f), matParede, "Parede_Loop_Oeste");
        CriarBlocoParede(m_ContainerParedes.transform, new Vector3(10.5f, yCentro, 0f), new Vector3(espessuraParedes, alturaParedes, 4.5f), matParede, "Parede_Loop_Leste");

        // 3. WAYFINDING SEMÂNTICO: Detecta bifurcações e inícios de corredores para instanciar Placas de Sinalização TBox
        if (instanciarPlacasSinalizacao)
        {
            DetectarBifurcacoesEInstanciarPlacas(m_ContainerParedes.transform);
        }

        Debug.Log($"<color=#A855F7><b>[Topologia Semântica - Labirinto 3D]</b></color> {m_ContainerParedes.transform.childCount} elementos do labirinto (paredes + placas de sinalização Wayfinding) instanciados.");
    }

    /// <summary>
    /// Detecta bifurcações e inícios de corredores no labirinto procedural e instancia Placas de Sinalização Semântica (Wayfinding)
    /// com TextMeshPro emissivo alimentado dinamicamente pela taxonomia TBox (agnóstico de domínio).
    /// </summary>
    public void DetectarBifurcacoesEInstanciarPlacas(Transform paiLabirinto)
    {
        m_PlacasWayfinding.Clear();

        if (paiLabirinto == null)
        {
            paiLabirinto = m_ContainerParedes != null ? m_ContainerParedes.transform : transform;
        }

        Transform containerAntigo = paiLabirinto.Find("Container_Placas_Wayfinding");
        if (containerAntigo != null)
        {
            if (Application.isPlaying) Destroy(containerAntigo.gameObject);
            else DestroyImmediate(containerAntigo.gameObject);
        }

        GameObject containerPlacas = new GameObject("Container_Placas_Wayfinding");
        containerPlacas.transform.SetParent(paiLabirinto, false);
        containerPlacas.transform.localPosition = Vector3.zero;

        float yChao = ObterAlturaSuperficieChao(Vector3.zero);

        string supNorte = (m_SuperclassesAtivas != null && m_SuperclassesAtivas.Length > 0) ? m_SuperclassesAtivas[0] : "Parasitas";
        string supSul = (m_SuperclassesAtivas != null && m_SuperclassesAtivas.Length > 1) ? m_SuperclassesAtivas[1] : "Hospedeiros";

        string sub0 = ObterSubclasseTaxonomia(0, "Nematoda");
        string sub1 = ObterSubclasseTaxonomia(1, "Platyhelminthes");
        string sub2 = ObterSubclasseTaxonomia(2, "Human");
        string sub3 = ObterSubclasseTaxonomia(3, "SmallIntestine");
        string sub4 = ObterSubclasseTaxonomia(4, "FecalOral");
        string sub5 = ObterSubclasseTaxonomia(5, "AntiparasiticTreatment");

        // Pontos de bifurcação e entradas de corredores detectados na malha do labirinto:
        // 1. Bifurcação Central Norte (Entrada do Hemisfério da Superclasse 1)
        InstanciarPlacaEmPontoDeDecisao(
            containerPlacas.transform,
            new Vector3(-2.6f, yChao, 3.2f),
            Quaternion.Euler(0f, 0f, 0f),
            "Bifurcacao_Central_Norte",
            "⬆", "Superclasse", supNorte,
            ObterCorTopologiaSuperclasse(supNorte));

        // 2. Bifurcação Central Sul (Entrada do Hemisfério da Superclasse 2)
        InstanciarPlacaEmPontoDeDecisao(
            containerPlacas.transform,
            new Vector3(2.6f, yChao, -3.8f),
            Quaternion.Euler(0f, 180f, 0f),
            "Bifurcacao_Central_Sul",
            "⬇", "Superclasse", supSul,
            ObterCorTopologiaSuperclasse(supSul));

        // 3. Corredor Noroeste (NW) - Subclasse 0 (ex: Nematoda)
        InstanciarPlacaEmPontoDeDecisao(
            containerPlacas.transform,
            new Vector3(-4.2f, yChao, 5.2f),
            Quaternion.Euler(0f, -35f, 0f),
            "Corredor_NW_Subclasse",
            "⬅", "Subclasse", sub0,
            corSuperclasseParasitas);

        // 4. Corredor Nordeste (NE) - Subclasse 1 (ex: Platyhelminthes)
        InstanciarPlacaEmPontoDeDecisao(
            containerPlacas.transform,
            new Vector3(4.2f, yChao, 5.2f),
            Quaternion.Euler(0f, 35f, 0f),
            "Corredor_NE_Subclasse",
            "➡", "Subclasse", sub1,
            corSuperclasseParasitas);

        // 5. Corredor Sudoeste (SW) - Subclasse 2 (ex: Human / Host)
        InstanciarPlacaEmPontoDeDecisao(
            containerPlacas.transform,
            new Vector3(-4.2f, yChao, -5.2f),
            Quaternion.Euler(0f, -145f, 0f),
            "Corredor_SW_Subclasse",
            "⬅", "Subclasse", sub2,
            corSuperclasseHospedeiros);

        // 6. Corredor Sudeste (SE) - Subclasse 3 (ex: SmallIntestine / Habitat)
        InstanciarPlacaEmPontoDeDecisao(
            containerPlacas.transform,
            new Vector3(4.2f, yChao, -5.2f),
            Quaternion.Euler(0f, 145f, 0f),
            "Corredor_SE_Subclasse",
            "➡", "Subclasse", sub3,
            corSuperclasseHospedeiros);

        // 7. Bifurcação T Norte (Acesso aos ramos extremos superiores)
        InstanciarPlacaEmPontoDeDecisao(
            containerPlacas.transform,
            new Vector3(-3.3f, yChao, 8.0f),
            Quaternion.Euler(0f, 0f, 0f),
            "Bifurcacao_T_Norte",
            "⬆", "Subclasse", sub4,
            corSuperclasseParasitas);

        // 8. Bifurcação T Sul (Acesso aos ramos extremos inferiores)
        InstanciarPlacaEmPontoDeDecisao(
            containerPlacas.transform,
            new Vector3(3.3f, yChao, -8.0f),
            Quaternion.Euler(0f, 180f, 0f),
            "Bifurcacao_T_Sul",
            "⬇", "Subclasse", sub5,
            corSuperclasseHospedeiros);
    }

    private PlacaSinalizacaoSemantica InstanciarPlacaEmPontoDeDecisao(
        Transform pai,
        Vector3 posicao,
        Quaternion rotacao,
        string idPonto,
        string seta,
        string prefixoHierarquia,
        string nomeClasse,
        Color corNeon)
    {
        GameObject placaObj = null;

        if (!IsPrefabValid(prefabPlacaSinalizacao))
        {
            prefabPlacaSinalizacao = LoadPrefabRobust("PlacaSinalizacaoSemantica");
        }

        if (IsPrefabValid(prefabPlacaSinalizacao))
        {
            placaObj = Instantiate(prefabPlacaSinalizacao, posicao, rotacao, pai);
            placaObj.name = $"Placa_Wayfinding_{idPonto}";
        }
        else
        {
            placaObj = CriarPlacaWayfindingProcedural(pai, posicao, rotacao, idPonto);
        }

        PlacaSinalizacaoSemantica compPlaca = placaObj.GetComponent<PlacaSinalizacaoSemantica>();
        if (compPlaca == null)
        {
            compPlaca = placaObj.AddComponent<PlacaSinalizacaoSemantica>();
        }

        compPlaca.idBifurcacao = idPonto;
        compPlaca.ConfigurarPlaca(seta, prefixoHierarquia, nomeClasse, corNeon);

        // Seção 3 da Especificação Mestra: Pequena Point Light em cima das Placas de Sinalização para iluminar o grid abaixo delas
        AnexarLuzPontualDestaque(placaObj, corNeon, 2.8f, 1.15f);

        m_PlacasWayfinding.Add(compPlaca);
        return compPlaca;
    }

    /// <summary>
    /// Constrói proceduralmente o poste/painel holográfico de sinalização com TextMeshPro 3D (frente e verso)
    /// caso o Prefab ainda não tenha sido salvo em disco.
    /// </summary>
    public static GameObject CriarPlacaWayfindingProcedural(Transform pai, Vector3 posicao, Quaternion rotacao, string idPonto)
    {
        GameObject raizPlaca = new GameObject($"Placa_Wayfinding_{idPonto}");
        if (pai != null) raizPlaca.transform.SetParent(pai, true);
        raizPlaca.transform.position = posicao;
        raizPlaca.transform.rotation = rotacao;

        // 1. Base Tecnológica Fixa no Chão
        GameObject basePedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        basePedestal.name = "Base_Pedestal";
        basePedestal.transform.SetParent(raizPlaca.transform, false);
        basePedestal.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        basePedestal.transform.localScale = new Vector3(0.45f, 0.04f, 0.45f);
        Collider colBase = basePedestal.GetComponent<Collider>();
        if (colBase != null)
        {
            if (Application.isPlaying) Destroy(colBase);
            else DestroyImmediate(colBase);
        }

        // 2. Poste Vertical Metálico
        GameObject poste = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        poste.name = "Poste_Suporte";
        poste.transform.SetParent(raizPlaca.transform, false);
        poste.transform.localPosition = new Vector3(0f, 0.56f, 0f);
        poste.transform.localScale = new Vector3(0.08f, 0.56f, 0.08f);
        Collider colPoste = poste.GetComponent<Collider>();
        if (colPoste != null)
        {
            if (Application.isPlaying) Destroy(colPoste);
            else DestroyImmediate(colPoste);
        }

        // 3. Painel Holográfico Escuro (Fundo da Placa na altura dos olhos no corredor)
        GameObject painel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        painel.name = "Painel_Holografico";
        painel.transform.SetParent(raizPlaca.transform, false);
        painel.transform.localPosition = new Vector3(0f, PlacaSinalizacaoSemantica.AlturaCentroPainel, 0f);
        painel.transform.localScale = new Vector3(
            PlacaSinalizacaoSemantica.LarguraPainelHolografico,
            PlacaSinalizacaoSemantica.AlturaPainelHolografico,
            PlacaSinalizacaoSemantica.EspessuraPainelHolografico);
        Collider colPainel = painel.GetComponent<Collider>();
        if (colPainel != null)
        {
            if (Application.isPlaying) Destroy(colPainel);
            else DestroyImmediate(colPainel);
        }

        // 4. Moldura Emissiva Néon Superior
        GameObject molduraNeon = GameObject.CreatePrimitive(PrimitiveType.Cube);
        molduraNeon.name = "Moldura_Neon_HDR";
        molduraNeon.transform.SetParent(raizPlaca.transform, false);
        float topoPainel = PlacaSinalizacaoSemantica.AlturaCentroPainel + (PlacaSinalizacaoSemantica.AlturaPainelHolografico * 0.5f) + 0.02f;
        molduraNeon.transform.localPosition = new Vector3(0f, topoPainel, 0f);
        molduraNeon.transform.localScale = new Vector3(
            PlacaSinalizacaoSemantica.LarguraPainelHolografico + 0.06f,
            0.05f,
            PlacaSinalizacaoSemantica.EspessuraPainelHolografico + 0.02f);
        Collider colMoldura = molduraNeon.GetComponent<Collider>();
        if (colMoldura != null)
        {
            if (Application.isPlaying) Destroy(colMoldura);
            else DestroyImmediate(colMoldura);
        }

        // 5. Texto TextMeshPro 3D Frontal (Perfeitamente centralizado e contido no Painel_Holografico)
        GameObject txtFrenteObj = new GameObject("TMP_Texto_Frontal", typeof(RectTransform));
        txtFrenteObj.transform.SetParent(raizPlaca.transform, false);
        TextMeshPro tmpFrente = txtFrenteObj.AddComponent<TextMeshPro>();
        PlacaSinalizacaoSemantica.CentralizarRectTransformNoPainel(tmpFrente, ehFaceTraseira: false);

        // 6. Texto TextMeshPro 3D Traseiro (Legível dos dois sentidos do corredor)
        GameObject txtVersoObj = new GameObject("TMP_Texto_Traseiro", typeof(RectTransform));
        txtVersoObj.transform.SetParent(raizPlaca.transform, false);
        TextMeshPro tmpVerso = txtVersoObj.AddComponent<TextMeshPro>();
        PlacaSinalizacaoSemantica.CentralizarRectTransformNoPainel(tmpVerso, ehFaceTraseira: true);

        PlacaSinalizacaoSemantica placaComp = raizPlaca.AddComponent<PlacaSinalizacaoSemantica>();
        placaComp.textoFrontal = tmpFrente;
        placaComp.textoTraseiro = tmpVerso;
        placaComp.rendererMolduraNeon = molduraNeon.GetComponent<Renderer>();
        placaComp.renderersEmissivos = new Renderer[]
        {
            molduraNeon.GetComponent<Renderer>(),
            basePedestal.GetComponent<Renderer>()
        };
        placaComp.rendererPainelFundo = painel.GetComponent<Renderer>();

        return raizPlaca;
    }

    private string ObterSubclasseTaxonomia(int indice, string fallback)
    {
        if (m_SubclassesAtivas != null && m_SubclassesAtivas.Length > 0)
        {
            return m_SubclassesAtivas[indice % m_SubclassesAtivas.Length];
        }
        return fallback;
    }

    /// <summary>
    /// Extrai dinamicamente as Superclasses e Subclasses do Grafo Ontológico ativo (VirtOnto),
    /// garantindo que o Wayfinding funcione para qualquer ontologia carregada (agnóstico de domínio).
    /// </summary>
    public void ExtrairTaxonomiaDoGrafoAtivo()
    {
        if (GrafoOntologico == null) return;

        List<string> superclasses = new List<string>();
        List<string> subclasses = new List<string>();

        var nosClasse = GrafoOntologico.GetNodesByType(NodeType.CLASS);
        if (nosClasse != null)
        {
            foreach (var n in nosClasse)
            {
                if (n == null || string.IsNullOrEmpty(n.Label)) continue;
                var arestasSaida = GrafoOntologico.GetOutgoingEdges(n.Id);
                bool ehSubclasse = false;
                if (arestasSaida != null)
                {
                    foreach (var e in arestasSaida)
                    {
                        if (e != null && !string.IsNullOrEmpty(e.RelationType) &&
                            (e.RelationType.Contains("subClassOf") || e.RelationType.Contains("is_a")))
                        {
                            ehSubclasse = true;
                            break;
                        }
                    }
                }

                if (ehSubclasse)
                {
                    if (!subclasses.Contains(n.Label)) subclasses.Add(n.Label);
                }
                else
                {
                    if (!superclasses.Contains(n.Label)) superclasses.Add(n.Label);
                }
            }
        }

        // Também coleta classes referenciadas pelos indivíduos se a lista de subclasses estiver curta
        var nosInd = GrafoOntologico.GetNodesByType(NodeType.INDIVIDUAL);
        if (nosInd != null)
        {
            foreach (var ind in nosInd)
            {
                if (ind != null && !string.IsNullOrEmpty(ind.ConceptClass) && !subclasses.Contains(ind.ConceptClass))
                {
                    subclasses.Add(ind.ConceptClass);
                }
            }
        }

        if (superclasses.Count == 0)
        {
            superclasses.Add("Parasitas");
            superclasses.Add("Hospedeiros");
        }
        else if (superclasses.Count == 1)
        {
            superclasses.Add("EntidadesRelacionadas");
        }

        AtualizarPlacasWayfindingTaxonomia(superclasses.ToArray(), subclasses.ToArray());
    }

    /// <summary>
    /// Atualiza em tempo real os textos das Placas de Sinalização Semântica quando uma nova taxonomia
    /// é recebida via WebSocket (Protégé 8085) ou carregada de arquivo OWL/JSON.
    /// </summary>
    public void AtualizarPlacasWayfindingTaxonomia(string[] superclasses, string[] subclasses)
    {
        if (superclasses != null && superclasses.Length > 0)
        {
            m_SuperclassesAtivas = superclasses;
        }
        if (subclasses != null && subclasses.Length > 0)
        {
            m_SubclassesAtivas = subclasses;
        }

        if (m_PlacasWayfinding == null || m_PlacasWayfinding.Count == 0) return;

        string supNorte = (m_SuperclassesAtivas != null && m_SuperclassesAtivas.Length > 0) ? m_SuperclassesAtivas[0] : "Parasitas";
        string supSul = (m_SuperclassesAtivas != null && m_SuperclassesAtivas.Length > 1) ? m_SuperclassesAtivas[1] : (m_SuperclassesAtivas != null && m_SuperclassesAtivas.Length > 0 ? m_SuperclassesAtivas[0] : "Hospedeiros");

        for (int i = 0; i < m_PlacasWayfinding.Count; i++)
        {
            PlacaSinalizacaoSemantica placa = m_PlacasWayfinding[i];
            if (placa == null) continue;

            if (i == 0)
            {
                placa.ConfigurarPlaca("⬆", "Superclasse", supNorte, ObterCorTopologiaSuperclasse(supNorte));
            }
            else if (i == 1)
            {
                placa.ConfigurarPlaca("⬇", "Superclasse", supSul, ObterCorTopologiaSuperclasse(supSul));
            }
            else
            {
                string subNome = ObterSubclasseTaxonomia(i - 2, $"Subclasse_{i - 1}");
                string seta = (i % 2 == 0) ? "⬅" : "➡";
                if (i == 6) seta = "⬆";
                if (i == 7) seta = "⬇";
                Color cor = (i == 2 || i == 3 || i == 6) ? corSuperclasseParasitas : corSuperclasseHospedeiros;
                placa.ConfigurarPlaca(seta, "Subclasse", subNome, cor);
            }
        }
    }

    private void CriarGatilhoWarpTunel(Transform pai, Vector3 posicao, Vector3 tamanho, Vector3 destinoWarp, string nome)
    {
        GameObject gatilho = new GameObject(nome);
        gatilho.transform.SetParent(pai, false);
        gatilho.transform.position = posicao;

        BoxCollider col = gatilho.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = tamanho;

        TunelTeletransporteWarp warp = gatilho.AddComponent<TunelTeletransporteWarp>();
        warp.pontoDestino = destinoWarp;
    }

    private GameObject CriarBlocoParede(Transform pai, Vector3 pos, Vector3 escala, Material mat, string nome)
    {
        GameObject parede = GameObject.CreatePrimitive(PrimitiveType.Cube);
        parede.name = nome;
        parede.transform.SetParent(pai);
        parede.transform.position = pos;
        parede.transform.localScale = escala;
        parede.layer = LayerMask.NameToLayer("Default");

        // Topologia Semântica Dark Grid: parede preta com linhas de grade emissivas (Roxo = Parasitas | Azul = Hospedeiros)
        string superclasseRegiao = DeterminarSuperclassePorRegiao(pos, nome);
        Vector2 tilingParede = new Vector2(
            Mathf.Max(1f, Mathf.Round(escala.x * 1.0f)),
            Mathf.Max(1f, Mathf.Round(escala.z * 1.0f)));

        Material matRegiao = usarTopologiaSemanticaPorSuperclasse
            ? CriarMaterialParedePorSuperclasse(superclasseRegiao, tilingParede)
            : mat;

        Renderer rend = parede.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = matRegiao;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            rend.receiveShadows = true;
        }

        parede.isStatic = true;
        // Colisor sólido estritamente não-trigger para bloquear 100% a travessia de jogadores e agentes
        BoxCollider col = parede.GetComponent<BoxCollider>();
        if (col != null)
        {
            col.isTrigger = false;
            col.size = new Vector3(1f, 1.6f, 1f);
            col.center = Vector3.zero;
        }

        return parede;
    }

    // =========================================================================
    // 5. INTEGRAÇÃO COM VIRTONTO C# GRAPH (OWL / SWRL)
    // =========================================================================

    /// <summary>
    /// Gera o Grafo Ontológico canônico com classes TBox, indivíduos ABox e axiomas ALCQ(D).
    /// </summary>
    
    public Graph ConstruirGrafoVirtOntoPadrao() => ConstruirGrafoOntologicoPadrao();
    public Graph ConstruirGrafoOntologicoPadrao()
    {
        Graph graph;

        TextAsset owlAsset = arquivoOntologiaOwl ?? Resources.Load<TextAsset>("parasites.owl") ?? Resources.Load<TextAsset>("parasites");
        if (owlAsset != null && !string.IsNullOrEmpty(owlAsset.text))
        {
            graph = Graph.LoadFromOwlXml(owlAsset.text);
        }
        else
        {
            graph = new Graph();
            graph.IndexarTBoxPadrao();
        }

        graph.NomeOntologia = "ParasitologyOntology_VirtOnto_CIn";
        graph.VersaoAxiomas = "v2.0-Parasites-ALCQ";

        CenarioSemanticoDTO padrao = BackendOntologiaBridge.GerarCenarioOntologiaPadrao(quantidadeValidos, quantidadeInvalidos, raioEspalhamento, alturaGemas);
        graph.BuildFromCenarioDTO(padrao);

        return graph;
    }

    [ContextMenu("Pac-Man Semântico/Construir e Indexar Grafo Ontológico (OWL-SWRL)")]
    public void MenuConstruirGrafoOntologico()
    {
        GrafoOntologico = ConstruirGrafoOntologicoPadrao();
        m_TotalNosGrafo = GrafoOntologico.GetNodeCount();
        m_TotalArestasGrafo = GrafoOntologico.GetEdgeCount();

        Debug.Log($"<color=#00DDFF><b>[Pac-Man Semântico C#]</b></color> Grafo construído com sucesso! " +
                  $"Nós: {m_TotalNosGrafo}, Arestas: {m_TotalArestasGrafo}.");
    }

    [ContextMenu("Pac-Man Semântico/Avaliar Regras SWRL no Grafo")]
    public void MenuAvaliarRegrasSWRL()
    {
        if (GrafoOntologico == null || GrafoOntologico.GetNodeCount() == 0)
        {
            MenuConstruirGrafoOntologico();
        }

        GrafoOntologico.AvaliarRegrasSWRL(msg => Debug.Log($"<color=#9B59B6><b>[Pac-Man Semântico SWRL]</b></color> {msg}"));
    }

    [ContextMenu("Pac-Man Semântico/Sincronizar com Backend Java (Spring Boot)")]
    public void MenuSincronizarComBackendJava()
    {
        InicializarApiClient();
        if (apiClient != null)
        {
            apiClient.SincronizarCenarioComBackend();
        }
    }

    /// <summary>
    /// Localiza uma instância ativa por seu identificador único de ontologia,
    /// consultando prioritariamente o Grafo Ontológico O(1) e depois a lista de instâncias.
    /// </summary>
    public IInstanciaSemantica ObterInstanciaPorId(string id)
    {
        Node node = GrafoOntologico != null ? GrafoOntologico.GetNode(id) : null;
        if (node != null && node.GameObjectInstance != null)
        {
            var inst = node.GameObjectInstance.GetComponent<IInstanciaSemantica>();
            if (inst != null) return inst;
        }
        return m_InstanciasSemanticas.Find(i => i.Id == id);
    }

    /// <summary>
    /// Retorna o nó ontológico correspondente no Grafo Ontológico O(1).
    /// </summary>
    public Node ObterNoDoGrafo(string id) => GrafoOntologico?.GetNode(id);

    /// <summary>
    /// Retorna todos os nós individuais ativos indexados no Grafo Ontológico.
    /// </summary>
    public IReadOnlyList<Node> ObterNosIndividuos() => GrafoOntologico?.GetNodesByType(NodeType.INDIVIDUAL);

    /// <summary>
    /// Retorna todas as relações (arestas) que partem do nó especificado.
    /// </summary>
    public IReadOnlyList<Edge> ObterRelacoesDoNo(string id) => GrafoOntologico?.GetOutgoingEdges(id);

    /// <summary>
    /// Limpa todas as instâncias ontológicas ativas no labirinto.
    /// </summary>
    [ContextMenu("Pac-Man Semântico/Limpar Cenário")]
    public void LimparCenarioExistente(bool destruirParedes = false)
    {
        if (destruirParedes && m_ContainerParedes != null)
        {
            if (Application.isPlaying) Destroy(m_ContainerParedes);
            else DestroyImmediate(m_ContainerParedes);
            m_ContainerParedes = null;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform filho = transform.GetChild(i);
            // PROTEÇÃO DO LABIRINTO: Nunca destrói as paredes do labirinto ou decorações temáticas ao limpar gemas/fantasmas
            if (!destruirParedes && (filho.name == "Labirinto_Paredes_3D" || filho.gameObject == m_ContainerParedes ||
                filho.name == "Decoracoes_Classico_3D" || filho.gameObject == m_ContainerDecoracoesClassico ||
                filho.name == "Decoracoes_Natal_3D" || filho.gameObject == m_ContainerDecoracoesNatal))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(filho.gameObject);
            }
            else
            {
                DestroyImmediate(filho.gameObject);
            }
        }

        m_ObjetosInstanciados.Clear();
        m_InstanciasSemanticas.Clear();
        m_PosicoesOcupadas.Clear();
        m_TotalInstanciasInstanciadas = 0;
        InstanciaSemanticaBase.instanciasValidasRestantes = 0;
        motorRaciocinio?.ResetarEstados();
    }

    /// <summary>
    /// Reinicia completamente as orbes, pontuação, fantasmas e posições para uma nova partida limpa.
    /// Garante que nenhuma orbe capturada anteriormente permaneça marcada como coletada.
    /// </summary>
    public void ReiniciarLabirintoEInstancias()
    {
        // 1. Destrói qualquer orbe remanescente de partidas anteriores (Zero Cache / Estado Limpo)
        var gemas = FindObjectsByType<GemaValidaSemantica>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < gemas.Length; i++)
        {
            if (gemas[i] != null)
            {
                Destroy(gemas[i].gameObject);
            }
        }
        if (m_OrbeAtivaAtual != null)
        {
            Destroy(m_OrbeAtivaAtual);
            m_OrbeAtivaAtual = null;
        }

        // 2. Reseta integralmente os contadores estáticos e a fila sequencial
        m_IndiceOrbeAtual = 0;
        m_TotalOrbesSequencia = m_FilaNodosOrbes.Count > 0 ? m_FilaNodosOrbes.Count : 12;
        InstanciaSemanticaBase.instanciasValidasRestantes = m_TotalOrbesSequencia;
        InstanciaSemanticaBase.totalColetadas = 0;
        InstanciaSemanticaBase.pontuacaoLogica = 0;
        GemaValidaSemantica.totalColetadas = 0;
        GemaValidaSemantica.pontuacaoLogica = 0;

        // 3. Reposiciona e reseta os fantasmas para o interior da gaiola central
        var fantasmas = FindObjectsByType<ObstaculoInvalidoSemantico>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < fantasmas.Length; i++)
        {
            if (fantasmas[i] != null)
            {
                int slotIdx = i % s_SpawnsGaiolaCentral.Length;
                Vector3 slotPos = s_SpawnsGaiolaCentral[slotIdx];
                float atraso = s_AtrasosSaidaGaiola[slotIdx];
                fantasmas[i].transform.position = slotPos;
                fantasmas[i].ConfigurarSpawnGaiola(slotPos, atraso, s_PosicaoSaidaGaiola);
                fantasmas[i].gameObject.SetActive(true);
            }
        }

        // 4. Reseta jogador para o spawn inicial seguro
        var ctrl = FindFirstObjectByType<ControladorXRJogador>();
        if (ctrl != null)
        {
            ctrl.transform.position = new Vector3(0f, 0.0f, -5.0f);
            ctrl.vidasRestantes = 3;
            ctrl.PararMovimentoTotal();
            ctrl.AlinharBaseAoChao();
        }

        // 5. Reseta motor de raciocínio ontológico
        if (motorRaciocinio != null)
        {
            motorRaciocinio.ResetarEstados();
        }

        // 6. Reseta painel do grafo Protégé
        if (PainelGrafoSemanticoProtege.Instancia != null)
        {
            PainelGrafoSemanticoProtege.Instancia.ReiniciarMetricas();
        }

        // 7. Spawna a PRIMEIRA orbe (#1) da nova partida
        if (m_FilaNodosOrbes.Count > 0)
        {
            SpawnarOrbeAtual();
        }

        Debug.Log("<color=#00FF99><b>[GerenciadorCenarioSemantico]</b></color> Partida reiniciada com sucesso! Estado 100% limpo, orbe #1 instanciada.");
    }

    private GameObject ConstruirPortalSaidaProcedural(Transform pai, Vector3 pos, Material matParede)
    {
        GameObject portalObj = new GameObject("Portal_Saida_Labirinto");
        portalObj.transform.SetParent(pai);
        portalObj.transform.position = pos;

        // 1. Pilares laterais luminosos
        GameObject pilarEsq = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pilarEsq.name = "Pilar_Esquerdo";
        pilarEsq.transform.SetParent(portalObj.transform, false);
        pilarEsq.transform.localPosition = new Vector3(-2.2f, 0f, 0f);
        pilarEsq.transform.localScale = new Vector3(0.6f, alturaParedes, 0.8f);
        if (matParede != null) pilarEsq.GetComponent<Renderer>().material = matParede;

        GameObject pilarDir = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pilarDir.name = "Pilar_Direito";
        pilarDir.transform.SetParent(portalObj.transform, false);
        pilarDir.transform.localPosition = new Vector3(2.2f, 0f, 0f);
        pilarDir.transform.localScale = new Vector3(0.6f, alturaParedes, 0.8f);
        if (matParede != null) pilarDir.GetComponent<Renderer>().material = matParede;

        // 2. Viga superior / Dintel do Portal com LED Trim
        GameObject vigaTopo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vigaTopo.name = "Viga_Topo";
        vigaTopo.transform.SetParent(portalObj.transform, false);
        vigaTopo.transform.localPosition = new Vector3(0f, alturaParedes * 0.42f, 0f);
        vigaTopo.transform.localScale = new Vector3(4.8f, 0.8f, 0.8f);
        if (matParede != null) vigaTopo.GetComponent<Renderer>().material = matParede;

        // Trim LED luminoso de portal
        GameObject trimTopo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trimTopo.name = "Trim_Neon_Portal";
        trimTopo.transform.SetParent(vigaTopo.transform, false);
        trimTopo.transform.localPosition = new Vector3(0f, -0.45f, 0f);
        trimTopo.transform.localScale = new Vector3(1.05f, 0.15f, 1.05f);
        Collider colTrim = trimTopo.GetComponent<Collider>();
        if (colTrim != null) { if (Application.isPlaying) Destroy(colTrim); else DestroyImmediate(colTrim); }
        trimTopo.GetComponent<Renderer>().material = CriarMaterialNeonLED(new Color(0f, 1f, 0.6f));

        // 3. Anel Holografico de Contencao (gira suavemente)
        GameObject anelContencao = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        anelContencao.name = "Anel_Contencao_Holografico";
        anelContencao.transform.SetParent(portalObj.transform, false);
        anelContencao.transform.localPosition = new Vector3(0f, 0f, 0f);
        anelContencao.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        anelContencao.transform.localScale = new Vector3(2.8f, 0.05f, 2.8f);
        Collider colAnel = anelContencao.GetComponent<Collider>();
        if (colAnel != null) { if (Application.isPlaying) Destroy(colAnel); else DestroyImmediate(colAnel); }
        anelContencao.GetComponent<Renderer>().material = CriarMaterialNeonLED(new Color(0.1f, 0.9f, 1f));

        // 4. Escudo de Quarentena / Barreira de Energia
        GameObject escudo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        escudo.name = "Escudo_Quarentena_Energia";
        escudo.transform.SetParent(portalObj.transform, false);
        escudo.transform.localPosition = new Vector3(0f, -0.4f, 0f);
        escudo.transform.localScale = new Vector3(3.6f, alturaParedes * 0.8f, 0.4f);
        BoxCollider colEscudo = escudo.GetComponent<BoxCollider>();
        if (colEscudo != null) colEscudo.isTrigger = true;
        Material matEscudo = CriarMaterialDinamico(new Color(1f, 0.35f, 0.05f, 0.5f), new Color(1f, 0.35f, 0.05f) * 1.5f, 0.9f);
        escudo.GetComponent<Renderer>().material = matEscudo;

        // 5. Luz indicadora do portal
        GameObject luzObj = new GameObject("Luz_Portal");
        luzObj.transform.SetParent(portalObj.transform, false);
        luzObj.transform.localPosition = new Vector3(0f, 0f, -0.5f);
        Light luzP = luzObj.AddComponent<Light>();
        luzP.type = LightType.Point;
        luzP.color = new Color(1f, 0.4f, 0.1f);
        luzP.range = 8.5f;
        luzP.intensity = 3.0f;

        // 6. Placa Sinalizadora Superior
        GameObject placa = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placa.name = "Placa_Sinalizadora_SAIDA";
        placa.transform.SetParent(portalObj.transform, false);
        placa.transform.localPosition = new Vector3(0f, alturaParedes * 0.55f, -0.1f);
        placa.transform.localScale = new Vector3(3.5f, 0.6f, 0.15f);
        Collider colPlaca = placa.GetComponent<Collider>();
        if (colPlaca != null) { if (Application.isPlaying) Destroy(colPlaca); else DestroyImmediate(colPlaca); }
        placa.GetComponent<Renderer>().material = CriarMaterialNeonLED(new Color(0.05f, 1f, 0.5f));

        // 7. Componente PortalSaidaLabirinto
        PortalSaidaLabirinto portalComp = portalObj.AddComponent<PortalSaidaLabirinto>();
        portalComp.escudoEnergia = escudo;
        portalComp.arcoPortal = anelContencao;
        portalComp.luzPortal = luzP;
        portalComp.materialEscudo = matEscudo;

        BoxCollider triggerGeral = portalObj.AddComponent<BoxCollider>();
        triggerGeral.isTrigger = true;
        triggerGeral.size = new Vector3(3.8f, alturaParedes, 1.5f);
        triggerGeral.center = Vector3.zero;

        return portalObj;
    }


    // =========================================================================
    // MODELOS 3D ESPECÍFICOS: ARCADES & PACMAN-GHOST (CIN / DARK GRID)
    // =========================================================================

    private GameObject ObterPrefabPacmanGhost()
    {
        if (m_PrefabPacmanGhost == null)
        {
            m_PrefabPacmanGhost = Resources.Load<GameObject>("Models/PacmanGhost/PacmanGhost");
#if UNITY_EDITOR
            if (m_PrefabPacmanGhost == null)
            {
                m_PrefabPacmanGhost = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/PacmanGhost/PacmanGhost.fbx") ??
                                      UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/PacmanGhost/source/PacmanGhost.fbx");
            }
#endif
        }
        return m_PrefabPacmanGhost;
    }

    private GameObject ObterPrefabArcade()
    {
        if (m_PrefabArcade == null)
        {
            m_PrefabArcade = Resources.Load<GameObject>("Models/Arcade/ArcadeMachine");
#if UNITY_EDITOR
            if (m_PrefabArcade == null)
            {
                m_PrefabArcade = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/Arcade/ArcadeMachine.fbx") ??
                                 UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/ArcadeAnimation/source/8cbdd9e5437a4a60bc30017ea0cf35b7.fbx.fbx");
            }
#endif
        }
        return m_PrefabArcade;
    }

    public void ConfigurarDecoracoesGrid(bool ativo)
    {
        if (m_ContainerDecoracoesGrid == null)
        {
            Transform tExistente = transform.Find("Decoracoes_Grid_3D");
            if (tExistente != null)
            {
                m_ContainerDecoracoesGrid = tExistente.gameObject;
            }
            else if (ativo)
            {
                m_ContainerDecoracoesGrid = ConstruirDecoracoesGrid();
            }
        }

        if (m_ContainerDecoracoesGrid != null)
        {
            m_ContainerDecoracoesGrid.SetActive(ativo);
        }
    }

    private GameObject ConstruirDecoracoesGrid()
    {
        GameObject root = new GameObject("Decoracoes_Grid_3D");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;

        Shader shaderLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");

        // 1. Piso exterior Dark Grid Neon 180m x 180m
        GameObject pisoExt = GameObject.CreatePrimitive(PrimitiveType.Plane);
        pisoExt.name = "Piso_Terreno_Exterior_Grid";
        pisoExt.transform.SetParent(root.transform, false);
        pisoExt.transform.localPosition = new Vector3(0f, -0.04f, 0f);
        pisoExt.transform.localScale = new Vector3(18f, 1f, 18f);
        RemoverColisorObjeto(pisoExt);

        Material matChaoGridExt = Resources.Load<Material>("DarkGridMaterial_Chao");
        if (matChaoGridExt == null)
        {
            matChaoGridExt = new Material(shaderLit) { name = "Mat_Chao_Grid_Ext" };
            Color corGridDark = new Color(0.01f, 0.02f, 0.05f);
            if (matChaoGridExt.HasProperty("_BaseColor")) matChaoGridExt.SetColor("_BaseColor", corGridDark);
            if (matChaoGridExt.HasProperty("_Color")) matChaoGridExt.SetColor("_Color", corGridDark);
            if (matChaoGridExt.HasProperty("_Smoothness")) matChaoGridExt.SetFloat("_Smoothness", 0.65f);
        }
        pisoExt.GetComponent<Renderer>().material = matChaoGridExt;

        // 2. APENAS UM ÚNICO FLIPERAMA DO LADO DE FORA DO MAPA COMO EASTER EGG (na mesma altura das paredes = 2.60m)
        GameObject prefabArcade = ObterPrefabArcade();
        Vector3 posEasterEgg = new Vector3(14.5f, 0f, 14.5f);
        Quaternion rotEasterEgg = Quaternion.Euler(0f, 225f, 0f);

        GameObject fliperama;
        if (prefabArcade != null)
        {
            fliperama = Instantiate(prefabArcade, root.transform, false);
            fliperama.name = "Fliperama_EasterEgg_Grid";
            fliperama.transform.position = posEasterEgg;
            fliperama.transform.rotation = rotEasterEgg;

            // Calibração de altura precisa baseada na geometria real do FBX
            MeshFilter[] mfs = fliperama.GetComponentsInChildren<MeshFilter>();
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int m = 0; m < mfs.Length; m++)
            {
                if (mfs[m] != null && mfs[m].sharedMesh != null)
                {
                    Bounds mb = mfs[m].sharedMesh.bounds;
                    Vector3 wMin = mfs[m].transform.TransformPoint(mb.min);
                    Vector3 wMax = mfs[m].transform.TransformPoint(mb.max);
                    Vector3 lMin = fliperama.transform.InverseTransformPoint(wMin);
                    Vector3 lMax = fliperama.transform.InverseTransformPoint(wMax);
                    if (lMin.y < minY) minY = lMin.y;
                    if (lMax.y > maxY) maxY = lMax.y;
                }
            }

            float rawH = maxY - minY;
            float escala = (rawH > 0.01f) ? (alturaParedes / rawH) : 3.65f;
            fliperama.transform.localScale = new Vector3(escala, escala, escala);
            if (minY < float.MaxValue && maxY > float.MinValue)
            {
                fliperama.transform.position = posEasterEgg - new Vector3(0f, minY * escala, 0f);
            }
            RemoverColisorObjeto(fliperama);
        }
        else
        {
            fliperama = CriarFliperamaProcedural(posEasterEgg, root.transform);
            fliperama.name = "Fliperama_EasterEgg_Grid_Procedural";
            fliperama.transform.rotation = rotEasterEgg;
        }

        // Ponto de luz sutil no Easter Egg para destaque neon
        GameObject luzEggObj = new GameObject("Luz_Fliperama_EasterEgg");
        luzEggObj.transform.SetParent(fliperama.transform, false);
        luzEggObj.transform.localPosition = new Vector3(0f, alturaParedes * 0.7f, 0.8f);
        Light luzEgg = luzEggObj.AddComponent<Light>();
        luzEgg.type = LightType.Point;
        luzEgg.color = new Color(0.0f, 0.9f, 1.0f);
        luzEgg.intensity = 2.0f;
        luzEgg.range = 5.0f;

        return root;
    }

    private GameObject CriarFliperamaProcedural(Vector3 pos, Transform parent)
    {
        Shader shaderLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        GameObject flip = new GameObject("Gabinete_Fliperama");
        flip.transform.SetParent(parent, false);
        flip.transform.position = pos;

        // Corpo com a mesma altura das paredes do Grid (alturaParedes = 2.60m)
        GameObject corpo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        corpo.transform.SetParent(flip.transform, false);
        corpo.transform.localPosition = new Vector3(0f, alturaParedes * 0.5f, 0f);
        corpo.transform.localScale = new Vector3(0.95f, alturaParedes, 1.05f);
        RemoverColisorObjeto(corpo);
        Material matGabinete = new Material(shaderLit) { color = new Color(0.04f, 0.05f, 0.09f) };
        corpo.GetComponent<Renderer>().material = matGabinete;

        // Tela brilhante
        GameObject tela = GameObject.CreatePrimitive(PrimitiveType.Quad);
        tela.transform.SetParent(flip.transform, false);
        tela.transform.localPosition = new Vector3(0f, alturaParedes * 0.65f, 0.53f);
        tela.transform.localScale = new Vector3(0.75f, 0.60f, 1f);
        RemoverColisorObjeto(tela);
        Material matTela = new Material(shaderLit);
        matTela.color = new Color(0.0f, 0.90f, 1.0f);
        if (matTela.HasProperty("_EmissionColor"))
        {
            matTela.EnableKeyword("_EMISSION");
            matTela.SetColor("_EmissionColor", new Color(0.0f, 0.70f, 1.0f) * 1.6f);
        }
        tela.GetComponent<Renderer>().material = matTela;

        return flip;
    }

    /// <summary>
    /// Revertido a pedido do usuário: Mantém 100% dos assets procedurais originais do Pac-Man e dos Fantasmas
    /// no Mapa do CIn (e em todos os mapas). Remove e destrói quaisquer instâncias residuais de modelos FBX.
    /// </summary>
    public void ConfigurarModelosTemaClassicoCIn(bool ativo)
    {
        // 1. Pac-Man: Garante restauração e ativação dos modelos procedurais originais
        GameObject visualPac = GameObject.Find("VisualComeCome") ?? GameObject.Find("Visual_PacMan");
        if (visualPac != null)
        {
            Transform tModeloCIn = visualPac.transform.Find("Visual_PacMan_CIn_FBX");
            if (tModeloCIn != null)
            {
                if (Application.isPlaying) Destroy(tModeloCIn.gameObject);
                else DestroyImmediate(tModeloCIn.gameObject);
            }

            var rendsRaiz = visualPac.GetComponents<Renderer>();
            for (int r = 0; r < rendsRaiz.Length; r++) rendsRaiz[r].enabled = true;

            var animBoca = visualPac.GetComponent<AnimacaoBocaPacMan>();
            if (animBoca != null) animBoca.enabled = true;

            for (int c = 0; c < visualPac.transform.childCount; c++)
            {
                visualPac.transform.GetChild(c).gameObject.SetActive(true);
            }
        }

        // 2. Os 4 Fantasmas: Restaura cápsula procedural, olhos e pupilas originais sem modelo FBX
        var fantasmas = FindObjectsByType<ObstaculoInvalidoSemantico>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < fantasmas.Length; i++)
        {
            if (fantasmas[i] == null) continue;
            Transform fTransform = fantasmas[i].transform;
            Transform tGhostCIn = fTransform.Find("Visual_Fantasma_CIn_FBX");
            if (tGhostCIn != null)
            {
                if (Application.isPlaying) Destroy(tGhostCIn.gameObject);
                else DestroyImmediate(tGhostCIn.gameObject);
            }

            for (int c = 0; c < fTransform.childCount; c++)
            {
                fTransform.GetChild(c).gameObject.SetActive(true);
            }

            var anim = fTransform.GetComponent<AnimacaoFantasma>();
            if (anim != null) anim.enabled = true;
        }
    }

    /// <summary>
    /// Configura o totem 3D do CIn (utilizando studio_mesh) EXCLUSIVAMENTE na entrada do portão do Mapa Grid.
    /// Nos demais temas, desativa este asset.
    /// </summary>
    public void ConfigurarTotemPortaoGrid(bool ativo)
    {
        if (m_TotemCInPortao == null)
        {
            Transform tExistente = (m_ContainerParedes != null) ? m_ContainerParedes.transform.Find("Totem_CIn_Portao_Grid") : transform.Find("Totem_CIn_Portao_Grid");
            if (tExistente != null)
            {
                m_TotemCInPortao = tExistente.gameObject;
            }
            else if (ativo)
            {
                m_TotemCInPortao = ConstruirTotemCInPortao();
            }
        }

        if (m_TotemCInPortao != null)
        {
            m_TotemCInPortao.SetActive(ativo);
        }

        // Se ativo no Grid, oculta a antiga placa wayfinding ou barreira na entrada do portão
        if (m_PlacasWayfinding != null)
        {
            for (int i = 0; i < m_PlacasWayfinding.Count; i++)
            {
                if (m_PlacasWayfinding[i] != null && m_PlacasWayfinding[i].idBifurcacao == "Bifurcacao_Central_Norte")
                {
                    m_PlacasWayfinding[i].gameObject.SetActive(!ativo);
                }
            }
        }

        // Oculta a placa/escudo do portal de saída norte se presente no Grid
        var portal = FindFirstObjectByType<PortalSaidaLabirinto>();
        if (portal != null && portal.escudoEnergia != null)
        {
            portal.escudoEnergia.SetActive(!ativo);
        }
    }

    private GameObject ConstruirTotemCInPortao()
    {
        Transform pai = (m_ContainerParedes != null) ? m_ContainerParedes.transform : transform;
        GameObject totemObj = new GameObject("Totem_CIn_Portao_Grid");
        totemObj.transform.SetParent(pai, false);
        // Posicionado na entrada do portão norte (X = -2.6m, Z = 11.5m), perfeitamente visível
        totemObj.transform.position = new Vector3(-2.6f, 0f, 11.5f);
        totemObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        Shader shaderLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");

        // 1. Base pedestal concreto 3D
        GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pedestal.name = "Base_Pedestal";
        pedestal.transform.SetParent(totemObj.transform, false);
        pedestal.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        pedestal.transform.localScale = new Vector3(2.20f, 0.24f, 0.50f);
        RemoverColisorObjeto(pedestal);
        pedestal.GetComponent<Renderer>().material = new Material(shaderLit) { color = new Color(0.22f, 0.24f, 0.28f) };

        // 2. Painel com textura de recorte studio_mesh (Totem CIn + Poste)
        Texture2D texTotem = Resources.Load<Texture2D>("Textures/studio_mesh");
#if UNITY_EDITOR
        if (texTotem == null)
        {
            texTotem = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/studio_mesh.png") ??
                       UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/studio_mesh.webp");
        }
#endif

        Material matTotem = new Material(shaderLit);
        matTotem.name = "Mat_Totem_CIn_Gate";
        if (texTotem != null)
        {
            matTotem.mainTexture = texTotem;
            if (matTotem.HasProperty("_BaseMap")) matTotem.SetTexture("_BaseMap", texTotem);
        }

        // Ativa recorte Alpha Cutout (Alpha Test)
        if (matTotem.HasProperty("_AlphaClip")) matTotem.SetFloat("_AlphaClip", 1f);
        if (matTotem.HasProperty("_Cutoff")) matTotem.SetFloat("_Cutoff", 0.35f);
        matTotem.EnableKeyword("_ALPHATEST_ON");
        matTotem.renderQueue = 2450;

        // Face Frontal
        GameObject faceFrente = GameObject.CreatePrimitive(PrimitiveType.Quad);
        faceFrente.name = "Face_Frontal";
        faceFrente.transform.SetParent(totemObj.transform, false);
        faceFrente.transform.localPosition = new Vector3(0f, 1.25f, 0.02f);
        faceFrente.transform.localScale = new Vector3(2.10f, 2.00f, 1f);
        RemoverColisorObjeto(faceFrente);
        faceFrente.GetComponent<Renderer>().material = matTotem;

        // Face Traseira
        GameObject faceTraseira = GameObject.CreatePrimitive(PrimitiveType.Quad);
        faceTraseira.name = "Face_Traseira";
        faceTraseira.transform.SetParent(totemObj.transform, false);
        faceTraseira.transform.localPosition = new Vector3(0f, 1.25f, -0.02f);
        faceTraseira.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        faceTraseira.transform.localScale = new Vector3(2.10f, 2.00f, 1f);
        RemoverColisorObjeto(faceTraseira);
        faceTraseira.GetComponent<Renderer>().material = matTotem;

        // 3. Luz do Poste de Iluminação do CIn
        GameObject luzPoste = new GameObject("Luz_Poste_CIn");
        luzPoste.transform.SetParent(totemObj.transform, false);
        luzPoste.transform.localPosition = new Vector3(-0.65f, 2.30f, 0.15f);
        Light pLight = luzPoste.AddComponent<Light>();
        pLight.type = LightType.Point;
        pLight.color = new Color(1.0f, 0.95f, 0.85f);
        pLight.intensity = 2.8f;
        pLight.range = 5.0f;

        return totemObj;
    }

}

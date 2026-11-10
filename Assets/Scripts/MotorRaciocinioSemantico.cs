using System;
using System.Collections.Generic;
using UnityEngine;
using VirtOnto.Model;

namespace PacMan.Semantics
{
    /// <summary>
    /// Estados ontológicos formais dos agentes no Pac-Man Semântico.
    /// </summary>
    public enum GameEntityState
    {
        Patrol,       // Modo Patrulha padrão (navegação sem ameaça imediata)
        Aggressive,   // Modo Agressivo / Perseguição (proximidade ao Pac-Man)
        Vulnerable,   // Modo Vulnerável (inferência SWRL: Pac-Man sob efeito de SpecialGem)
        Consumed      // Entidade consumida ou desativada logicamente
    }

    /// <summary>
    /// ============================================================================
    /// PAC-MAN SEMÂNTICO (CIn-UFPE) - Motor de Raciocínio Semântico
    /// ============================================================================
    /// Componente que governa os estados lógicos dos agentes (Pac-Man e Fantasmas)
    /// através de inferência dinâmica inspirada em regras SWRL e Lógica de Descrição.
    /// 
    /// Integrações:
    /// 1. Utiliza a estrutura do grafo VirtOnto (Graph, Node, Edge, Vector3D).
    /// 2. Conecta-se ao GerenciadorCenarioSemantico para sincronizar instâncias ativas.
    /// 3. Avalia distâncias euclidianas e propriedades de itens consumidos para
    ///    atualizar dinamicamente comportamentos (Patrol, Aggressive, Vulnerable).
    /// ============================================================================
    /// </summary>
    [DisallowMultipleComponent]
    public class MotorRaciocinioSemantico : MonoBehaviour
    {
        [Header("Grafo Semântico e Conexões")]
        [Tooltip("Referência ao Gerenciador Central de Cenário Semântico.")]
        public GerenciadorCenarioSemantico gerenciadorCenario;

        [Tooltip("Transform do Pac-Man (Jogador) para cálculo geométrico de distâncias.")]
        public Transform alvoPacMan;

        [Header("Estado de Poder do Pac-Man (SWRL Premise)")]
        [Tooltip("Indica se o Pac-Man está sob efeito de uma gema especial (Power Pellet).")]
        public bool pacmanIsPowered = false;

        [Tooltip("Tempo restante do estado de poder do Pac-Man em segundos.")]
        public float tempoPowerRestante = 0f;

        [Tooltip("Duração padrão do efeito de Power Pellet em segundos.")]
        public float duracaoPowerPellet = 8.0f;

        [Header("Limiares de Proximidade (Axiomas Espaciais SWRL)")]
        [Tooltip("Distância máxima para ativar regra SWRL de vulnerabilidade do fantasma.")]
        public float distanciaVulnerabilidade = 7.0f;

        [Tooltip("Distância de detecção para ativar comportamento agressivo (perseguição ativa no labirinto).")]
        public float distanciaAgressao = 45.0f;

        [Header("Comportamentos Dinâmicos dos Fantasmas (Runtime)")]
        [Tooltip("Habilita atualização dinâmica de movimentação e reações em tempo de execução.")]
        public bool moverFantasmasDinamicamente = true;

        [Tooltip("Velocidade do fantasma durante a patrulha contínua pelo labirinto.")]
        public float velocidadePatrulha = 2.0f;

        [Tooltip("Velocidade do fantasma ao perseguir o jogador no modo agressivo.")]
        public float velocidadeAgressao = 2.6f;

        [Tooltip("Velocidade de fuga do fantasma no modo vulnerável.")]
        public float velocidadeVulneravel = 1.4f;

        [Header("Visibilidade e Minimalismo")]
        [Tooltip("Exibe ou oculta os balões flutuantes 3D de texto explicativo acima dos fantasmas.")]
        public bool exibirRotulosExplicativosFantasmas = false;

        // Contador de fantasmas devorados durante o ciclo atual de Power Pellet (200, 400, 800, 1600 pts)
        private int m_FantasmasComidosNoCiclo = 0;

        [Header("Status do Reasoner (HermiT / Protégé WebSocket 8085)")]
        [Tooltip("Estado lógico atual do Reasoner (true = CONSISTENTE -> Patrol Azul Translúcido; false = INCONSISTENTE -> Chase Vermelho Sólido).")]
        public bool reasonerConsistente = true;

        [Header("Cores dos Estados Ontológicos (Reasoner Agents)")]
        [Tooltip("Patrol (CONSISTENTE): Material azul translúcido.")]
        public Color corPatrulha = new Color(0.10f, 0.55f, 1.0f, 0.52f);
        [Tooltip("Chase (INCONSISTENTE): Material Vermelho Sólido.")]
        public Color corAgressivo = new Color(1.0f, 0.08f, 0.12f, 1.0f);
        public Color corVulneravel = new Color(0.15f, 0.45f, 1f, 0.52f);

        [Header("Estratégia de Dispersão e Patrulha (Fim da Fila Indiana)")]
        [Tooltip("Tempo em segundos que os fantasmas passam em perseguição tática.")]
        public float tempoOndaPerseguicao = 18.0f;

        [Tooltip("Tempo em segundos que os fantasmas passam dispersos em patrulha pelos 4 cantos do labirinto.")]
        public float tempoOndaDispersao = 6.0f;

        private float m_TimerCicloModo = 0f;
        private bool m_ModoDispersaoAtivo = true; // Inicia disperso nos 4 quadrantes
        private System.Collections.Generic.List<GameObject> m_ListaFantasmasAtivos = new System.Collections.Generic.List<GameObject>();

        // Instância central do grafo ontológico
        private Graph semanticGraph;

        // Cache de estados ativos de cada entidade fantasma (Id -> Estado)
        private readonly Dictionary<string, GameEntityState> m_EstadosEntidades = new Dictionary<string, GameEntityState>(StringComparer.OrdinalIgnoreCase);

        // Eventos semânticos
        public event Action<string, GameEntityState> OnEstadoEntidadeAlterado;
        public event Action<bool> OnPacManPowerStateChanged;

        private void Awake()
        {
            semanticGraph = new Graph();
            InicializarTBoxABoxExemplo();

            if (gerenciadorCenario == null)
            {
                gerenciadorCenario = GetComponent<GerenciadorCenarioSemantico>() ?? FindAnyObjectByType<GerenciadorCenarioSemantico>();
            }
        }

        private void OnEnable()
        {
            InstanciaSemanticaBase.OnInstanciaInteragida += TratarInteracaoInstancia;
            if (PacMan.Network.ProtegeWebSocketBridge.Instance != null)
            {
                PacMan.Network.ProtegeWebSocketBridge.Instance.OnReasonerStatusChanged += AoMudarStatusReasoner;
            }
        }

        private void OnDisable()
        {
            InstanciaSemanticaBase.OnInstanciaInteragida -= TratarInteracaoInstancia;
            if (PacMan.Network.ProtegeWebSocketBridge.Instance != null)
            {
                PacMan.Network.ProtegeWebSocketBridge.Instance.OnReasonerStatusChanged -= AoMudarStatusReasoner;
            }
        }

        private void Start()
        {
            LocalizarAlvoPacMan();

            var ws = PacMan.Network.ProtegeWebSocketBridge.Instance ?? FindFirstObjectByType<PacMan.Network.ProtegeWebSocketBridge>();
            if (ws != null)
            {
                ws.OnReasonerStatusChanged -= AoMudarStatusReasoner;
                ws.OnReasonerStatusChanged += AoMudarStatusReasoner;
                reasonerConsistente = ws.ReasonerConsistent;
            }

            // Se o gerenciador possuir um grafo preenchido, sincroniza com ele
            if (gerenciadorCenario != null && gerenciadorCenario.GrafoOntologico != null && gerenciadorCenario.GrafoOntologico.GetNodeCount() > 0)
            {
                SetSemanticGraph(gerenciadorCenario.GrafoOntologico);
            }
        }

        private void AoMudarStatusReasoner(bool consistente, string detalhe)
        {
            reasonerConsistente = consistente;
            ProcessarRaciocinioFantasmas();
        }

        /// <summary>
        /// Permite ao jogador desfazer a última ação que causou inconsistência, retornando os Reasoner Agents para Patrol (CONSISTENTE).
        /// </summary>
        public void DesfazerAcaoInconsistente(string motivo = "")
        {
            reasonerConsistente = true;
            var ws = PacMan.Network.ProtegeWebSocketBridge.Instance ?? FindFirstObjectByType<PacMan.Network.ProtegeWebSocketBridge>();
            if (ws != null)
            {
                ws.DesfazerUltimaAcaoInconsistente();
            }
            else if (SemanticHUDManager.Instancia != null)
            {
                SemanticHUDManager.Instancia.DefinirStatusReasoner(true, "CONSISTENTE");
                SemanticHUDManager.Instancia.AdicionarLinhaLog("[-] Ação inconsistente desfeita. Reasoner Agents em modo Patrol (CONSISTENTE).");
            }
            ProcessarRaciocinioFantasmas();
        }

        private void Update()
        {
            // Atalho para desfazer a última ação que gerou inconsistência lógica (Tecla Z ou U)
            if (!reasonerConsistente && (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.U) || Input.GetKeyDown(KeyCode.Backspace)))
            {
                DesfazerAcaoInconsistente();
            }

            AtualizarCicloOndasDispersao();
            AplicarSeparacaoMatematicaIntransigivelFantasmas(alvoPacMan != null ? alvoPacMan.position : Vector3.zero);
            AtualizarTemporizadorPowerPellet();
            LocalizarAlvoPacMan();
            ProcessarRaciocinioFantasmas();
        }

        private void AtualizarCicloOndasDispersao()
        {
            m_TimerCicloModo += Time.deltaTime;
            float tempoTotalCiclo = tempoOndaPerseguicao + tempoOndaDispersao;
            float tempoNoCiclo = m_TimerCicloModo % tempoTotalCiclo;
            m_ModoDispersaoAtivo = tempoNoCiclo >= tempoOndaPerseguicao;
        }

        /// <summary>
        /// Localiza dinamicamente o alvo do Pac-Man (Jogador) na cena com múltiplos mecanismos de fallback.
        /// </summary>
        private void LocalizarAlvoPacMan()
        {
            if (alvoPacMan != null && alvoPacMan.gameObject.name == "Visual_PacMan") return;

            GameObject visual = GameObject.Find("Visual_PacMan");
            if (visual != null)
            {
                alvoPacMan = visual.transform;
                return;
            }

            if (alvoPacMan != null) return;

            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                alvoPacMan = player.transform;
                return;
            }

            var ctrlXR = FindFirstObjectByType<ControladorXRJogador>();
            if (ctrlXR != null)
            {
                alvoPacMan = ctrlXR.transform;
                return;
            }

            var ctrl = FindFirstObjectByType<PacMan.Player.ControladorJogador>();
            if (ctrl != null)
            {
                alvoPacMan = ctrl.transform;
                return;
            }

            var comeCome = FindFirstObjectByType<MovimentoComeCome>();
            if (comeCome != null)
            {
                alvoPacMan = comeCome.transform;
                return;
            }

            if (Camera.main != null)
            {
                // Prefere o Transform raiz (XR Origin / Corpo) se existir
                alvoPacMan = Camera.main.transform.parent != null && Camera.main.transform.parent.parent != null ? 
                             Camera.main.transform.parent.parent : 
                             (Camera.main.transform.parent != null ? Camera.main.transform.parent : Camera.main.transform);
            }
        }

        /// <summary>
        /// Trata eventos de interação com itens do cenário (ex: coleta de gemas especiais).
        /// Regra SWRL: Consumir(PacMan, SpecialGem) -> HasState(PacMan, Empowered).
        /// </summary>
        private void TratarInteracaoInstancia(IInstanciaSemantica inst, bool sucesso)
        {
            if (inst != null)
            {
                if (!sucesso || !inst.EhValida)
                {
                    // Detectou erro lógico / violação na ontologia -> Reasoner passa para INCONSISTENTE (Chase)
                    reasonerConsistente = false;
                    if (SemanticHUDManager.Instancia != null)
                    {
                        SemanticHUDManager.Instancia.DefinirStatusReasoner(false, $"HermiT: Violação em {inst.NomeClasse}");
                    }
                }
                else
                {
                    bool ehGemaEspecial = inst.NomeClasse.IndexOf("Special", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          inst.NomeClasse.IndexOf("Power", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          inst.NomeClasse.IndexOf("Pellet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          inst.NomeClasse.IndexOf("Treatment", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          (inst.PropriedadesDL != null && inst.PropriedadesDL.ContainsKey("isPowerPellet"));

                    if (ehGemaEspecial)
                    {
                        // Caso haja inconsistência ativa, interagir com o nodo de tratamento/reversão também desfaz a inconsistência
                        if (!reasonerConsistente)
                        {
                            DesfazerAcaoInconsistente();
                        }
                        AtivarPowerPellet(duracaoPowerPellet);
                    }
                }
            }
        }

        /// <summary>
        /// Atualiza o temporizador regressivo do estado Empowered do Pac-Man.
        /// </summary>
        private void AtualizarTemporizadorPowerPellet()
        {
            if (tempoPowerRestante > 0f)
            {
                tempoPowerRestante -= Time.deltaTime;
                AtualizarVisualVulneravelFantasmas(true, tempoPowerRestante);

                if (tempoPowerRestante <= 0f)
                {
                    tempoPowerRestante = 0f;
                    pacmanIsPowered = false;
                    m_FantasmasComidosNoCiclo = 0;
                    AtualizarVisualVulneravelFantasmas(false, 0f);
                    OnPacManPowerStateChanged?.Invoke(false);
                    Debug.Log("<color=#FF9900><b>[Motor Semântico]</b></color> Efeito do Power Pellet expirou. Pac-Man retornou ao estado normal.");
                }
            }
        }

        private void AtualizarVisualVulneravelFantasmas(bool ativo, float tempoRestante)
        {
            if (m_ListaFantasmasAtivos == null) return;
            for (int i = 0; i < m_ListaFantasmasAtivos.Count; i++)
            {
                GameObject g = m_ListaFantasmasAtivos[i];
                if (g != null && g.activeInHierarchy)
                {
                    var anim = g.GetComponent<AnimacaoFantasma>();
                    if (anim != null)
                    {
                        anim.AtualizarVisualVulneravel(ativo, tempoRestante);
                    }
                }
            }
        }

        /// <summary>
        /// TBox e ABox inicial (Exemplo Pac-Man Semântico).
        /// </summary>
        private void InicializarTBoxABoxExemplo()
        {
            Node pacman = new Node("pacman_01", "PacMan", NodeType.Individual);
            pacman.SetDisplayColor("#FFFF00");

            Node ghost = new Node("ghost_blinky", "Blinky", NodeType.Individual);
            ghost.SetDisplayColor("#FF0000");

            semanticGraph.AddNode(pacman);
            semanticGraph.AddNode(ghost);

            Edge relacao = new Edge("edge_01", "pacman_01", "ghost_blinky", "isNearTo");
            semanticGraph.AddEdge(relacao);
        }

        /// <summary>
        /// Avalia a Máquina de Estados Finitos (FSM) dos Reasoner Agents:
        /// 1. "Patrol" (CONSISTENTE): Material azul translúcido. Ignoram o jogador e vagam proceduralmente.
        /// 2. "Chase" / Aggressive (INCONSISTENTE): Material Vermelho Sólido. Perseguem o jogador ao detectar erro lógico.
        /// 3. Voltam para "Patrol" apenas quando o jogador desfizer a ação que causou a inconsistência (ou dentro do limiar SWRL).
        /// </summary>
        public GameEntityState AvaliarRegraEstadoFantasma(string ghostId, bool pacmanIsPowered, float distancia)
        {
            // 1. Quando o Pac-Man come uma Classe/Subclasse/Instância CERTA, os fantasmas ficam AZUIS (Vulnerable/Calmos)
            // e não vão atrás dele durante o período de validação!
            if (pacmanIsPowered)
            {
                return GameEntityState.Vulnerable;
            }

            // 2. Se o Reasoner está INCONSISTENTE ou o Pac-Man se aproximou no corredor (<= 11.5m),
            // o fantasma fica VERMELHO (Aggressive) e vai atrás do Pac-Man para tentar quebrar um pedaço da linha!
            if (!reasonerConsistente || distancia <= 11.5f)
            {
                return GameEntityState.Aggressive;
            }

            return GameEntityState.Patrol;
        }

        /// <summary>
        /// Processa continuamente o raciocínio ontológico e perseguição ativa para todas as instâncias de fantasmas na cena.
        /// </summary>
        private void ProcessarRaciocinioFantasmas()
        {
            if (alvoPacMan == null)
            {
                LocalizarAlvoPacMan();
                if (alvoPacMan == null) return;
            }

            Vector3 posPacMan = alvoPacMan.position;
            m_ListaFantasmasAtivos.Clear();

            // Pré-coleta todos os fantasmas da cena primeiro para conhecimento global mútuo
            ObstaculoInvalidoSemantico[] obstaculos = FindObjectsByType<ObstaculoInvalidoSemantico>(FindObjectsSortMode.None);
            foreach (var obs in obstaculos)
            {
                if (obs != null && obs.gameObject.activeInHierarchy && !m_ListaFantasmasAtivos.Contains(obs.gameObject))
                {
                    m_ListaFantasmasAtivos.Add(obs.gameObject);
                }
            }

            AnimacaoFantasma[] anims = FindObjectsByType<AnimacaoFantasma>(FindObjectsSortMode.None);
            foreach (var a in anims)
            {
                if (a != null && a.gameObject.activeInHierarchy && !m_ListaFantasmasAtivos.Contains(a.gameObject))
                {
                    m_ListaFantasmasAtivos.Add(a.gameObject);
                }
            }

            if (semanticGraph != null)
            {
                var individuos = semanticGraph.GetNodesByType(NodeType.INDIVIDUAL);
                foreach (var node in individuos)
                {
                    if (node != null && !node.IsValid && node.GameObjectInstance != null)
                    {
                        if (!m_ListaFantasmasAtivos.Contains(node.GameObjectInstance))
                        {
                            m_ListaFantasmasAtivos.Add(node.GameObjectInstance);
                        }
                    }
                }
            }

            // Agora processa cada fantasma com lista completa de vizinhos disponível
            for (int i = 0; i < m_ListaFantasmasAtivos.Count; i++)
            {
                GameObject gObj = m_ListaFantasmasAtivos[i];
                if (gObj != null && gObj.activeInHierarchy)
                {
                    string idFantasma = gObj.name;
                    var inst = gObj.GetComponent<IInstanciaSemantica>();
                    if (inst != null && !string.IsNullOrEmpty(inst.Id)) idFantasma = inst.Id;
                    ProcessarFantasmaIndividual(gObj, idFantasma, posPacMan);
                }
            }
        }

        private void ProcessarFantasmaIndividual(GameObject ghostObj, string id, Vector3 posPacMan)
        {
            if (ghostObj == null) return;

            Vector3 posFantasma = ghostObj.transform.position;
            float distancia = Vector3.Distance(posPacMan, posFantasma);

            GameEntityState estadoAtual = m_EstadosEntidades.TryGetValue(id, out var est) ? est : (reasonerConsistente ? GameEntityState.Patrol : GameEntityState.Aggressive);
            GameEntityState novoEstado = AvaliarRegraEstadoFantasma(id, pacmanIsPowered, distancia);

            if (novoEstado != estadoAtual)
            {
                m_EstadosEntidades[id] = novoEstado;
                AplicarEstadoNoGameObjectDirect(ghostObj, novoEstado);
                OnEstadoEntidadeAlterado?.Invoke(id, novoEstado);
            }
            else if (!m_EstadosEntidades.ContainsKey(id))
            {
                m_EstadosEntidades[id] = novoEstado;
                AplicarEstadoNoGameObjectDirect(ghostObj, novoEstado);
            }

            AtualizarRotuloExplicativoFantasma3D(ghostObj, novoEstado);
            AtualizarComportamentoFisicoFantasmaDirect(ghostObj, id, novoEstado, posPacMan, posFantasma);
        }

        private void AtualizarRotuloExplicativoFantasma3D(GameObject ghostObj, GameEntityState estado)
        {
            if (ghostObj == null) return;

            Transform trLabel = ghostObj.transform.Find("Rotulo_3D_Estado_Fantasma");
            if (!exibirRotulosExplicativosFantasmas)
            {
                if (trLabel != null) trLabel.gameObject.SetActive(false);
                return;
            }

            GameObject labelObj;
            if (trLabel != null)
            {
                labelObj = trLabel.gameObject;
            }
            else
            {
                labelObj = new GameObject("Rotulo_3D_Estado_Fantasma", typeof(RectTransform));
                labelObj.transform.SetParent(ghostObj.transform, false);
                labelObj.transform.localPosition = new Vector3(0f, 1.38f, 0f);
            }

            TMPro.TextMeshPro tmp = labelObj.GetComponent<TMPro.TextMeshPro>();
            if (tmp == null) tmp = labelObj.AddComponent<TMPro.TextMeshPro>();

            if (estado == GameEntityState.Vulnerable)
            {
                tmp.text = $"<color=#38BDF8><b>🟦 FANTASMA CALMO ({Mathf.CeilToInt(tempoPowerRestante)}s)</b></color>\n<size=75%><color=#FFFFFF>Você comeu item CERTO: Não persegue!</color></size>";
            }
            else if (estado == GameEntityState.Aggressive)
            {
                tmp.text = "<color=#FF3344><b>🟥 CAÇANDO SUA LINHA!</b></color>\n<size=75%><color=#FFD700>Se bater em você, quebra o último elo!</color></size>";
            }
            else
            {
                tmp.text = "<color=#F59E0B><b>🟧 VIGIANDO CORREDOR</b></color>\n<size=75%><color=#FFFFFF>Se chegar perto, fica Vermelho e persegue!</color></size>";
            }

            tmp.richText = true;
            tmp.fontSize = 1.95f;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.color = Color.white;
            tmp.outlineColor = new Color32(0, 0, 0, 255);
            tmp.outlineWidth = 0.25f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

            RectTransform rt = labelObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(5.0f, 1.1f);
            }

            if (labelObj.GetComponent<BillboardOlharCamera>() == null)
            {
                labelObj.AddComponent<BillboardOlharCamera>();
            }
        }

        /// <summary>
        /// Atualiza dinamicamente a movimentação e perseguição ativa do fantasma a cada frame.
        /// Configura o NavMeshAgent chamando SetDestination(posicaoPacMan) ativamente ou executa translação de perseguição por corredor.
        /// </summary>
        /// <summary>
        /// Calcula o destino tático inteligente para cada fantasma eliminando o alinhamento em fila indiana.
        /// Aplica papéis clássicos do Pac-Man (Blinky, Pinky, Inky, Clyde) com ondas periódicas de dispersão
        /// e repulsão mútua entre fantasmas para ocupar corredores paralelos.
        /// </summary>
        public Vector3 CalcularDestinoTaticoFantasma(GameObject ghostObj, string id, Vector3 posPacMan, Vector3 posAtual, List<GameObject> outrosFantasmas)
        {
            Vector3 fwdPacMan = alvoPacMan != null ? alvoPacMan.forward : Vector3.forward;
            Vector3 rgtPacMan = alvoPacMan != null ? alvoPacMan.right : Vector3.right;
            fwdPacMan.y = 0f; fwdPacMan.Normalize();
            rgtPacMan.y = 0f; rgtPacMan.Normalize();

            string idLower = (id ?? ghostObj.name).ToLower();
            int papelIndex = 0; // 0=Blinky (Perseguidor), 1=Pinky (Emboscada), 2=Inky (Flanco), 3=Clyde (Vagante)
            if (idLower.Contains("pinky") || idLower.Contains("invalido_02") || idLower.Contains("violacao_02") || idLower.Contains("hostmismatch") || idLower.Contains("2")) papelIndex = 1;
            else if (idLower.Contains("inky") || idLower.Contains("invalido_03") || idLower.Contains("violacao_03") || idLower.Contains("habitatinconsistency") || idLower.Contains("3")) papelIndex = 2;
            else if (idLower.Contains("clyde") || idLower.Contains("invalido_04") || idLower.Contains("violacao_04") || idLower.Contains("4")) papelIndex = 3;
            else
            {
                papelIndex = Mathf.Abs(ghostObj.GetHashCode()) % 4;
            }

            // Waypoints de patrulha livre distribuídos pelos 4 quadrantes do labirinto
            Vector3[][] rotasPatrulhaQuadrantes = new Vector3[][]
            {
                new Vector3[] { new Vector3(6.5f, posAtual.y, 6.5f), new Vector3(7.5f, posAtual.y, 2.0f), new Vector3(2.5f, posAtual.y, 7.5f), new Vector3(0.0f, posAtual.y, 7.0f) },   // NE (Blinky)
                new Vector3[] { new Vector3(-6.5f, posAtual.y, 6.5f), new Vector3(-7.5f, posAtual.y, 2.0f), new Vector3(-2.5f, posAtual.y, 7.5f), new Vector3(0.0f, posAtual.y, 7.0f) }, // NW (Pinky)
                new Vector3[] { new Vector3(-6.5f, posAtual.y, -6.5f), new Vector3(-7.5f, posAtual.y, -2.0f), new Vector3(-2.5f, posAtual.y, -7.5f), new Vector3(0.0f, posAtual.y, -7.0f) }, // SW (Inky)
                new Vector3[] { new Vector3(6.5f, posAtual.y, -6.5f), new Vector3(7.5f, posAtual.y, -2.0f), new Vector3(2.5f, posAtual.y, -7.5f), new Vector3(0.0f, posAtual.y, -7.0f) }   // SE (Clyde)
            };
            int subWaypoint = (int)((Time.time * 0.25f + papelIndex) % 4);
            Vector3[] cantosPatrulha = new Vector3[]
            {
                rotasPatrulhaQuadrantes[0][subWaypoint],
                rotasPatrulhaQuadrantes[1][subWaypoint],
                rotasPatrulhaQuadrantes[2][subWaypoint],
                rotasPatrulhaQuadrantes[3][subWaypoint]
            };

            Vector3 destinoTatico;

            // 1. Modo Patrol ou Vulnerable (Azul Calmo): Os fantasmas ignoram o jogador e vagam pelos quadrantes
            if (pacmanIsPowered || (reasonerConsistente && Vector3.Distance(posAtual, posPacMan) > 11.5f))
            {
                destinoTatico = cantosPatrulha[papelIndex % cantosPatrulha.Length];
            }
            else
            {
                // 2. Modo Chase (INCONSISTENTE): Perseguem o jogador ao detectar erro lógico
                switch (papelIndex)
                {
                    case 0: // Perseguição direta
                        destinoTatico = posPacMan;
                        break;

                    case 1: // Interceptador (antecipa 4.5m à frente do jogador)
                        destinoTatico = posPacMan + (fwdPacMan * 4.5f);
                        break;

                    case 2: // Flanqueador lateral
                        destinoTatico = posPacMan + (rgtPacMan * 3.8f);
                        break;

                    case 3: // Perseguição direta secundária quando inconsistente
                    default:
                        destinoTatico = posPacMan;
                        break;
                }
            }

            // 3. Repulsão mútua entre fantasmas (desvia trajetórias para impedir fila indiana)
            if (outrosFantasmas != null)
            {
                Vector3 forcaRepulsao = Vector3.zero;
                for (int i = 0; i < outrosFantasmas.Count; i++)
                {
                    GameObject outro = outrosFantasmas[i];
                    if (outro == null || outro == ghostObj) continue;

                    Vector3 delta = posAtual - outro.transform.position;
                    delta.y = 0f;
                    float distOutro = delta.magnitude;
                    if (distOutro > 0.05f && distOutro < 2.5f)
                    {
                        float intensidade = (2.5f - distOutro) / 2.5f;
                        forcaRepulsao += delta.normalized * (intensidade * 3.2f);
                    }
                }

                if (forcaRepulsao.sqrMagnitude > 0.01f)
                {
                    destinoTatico += forcaRepulsao;
                }
            }

            return destinoTatico;
        }

        private void AtualizarComportamentoFisicoFantasmaDirect(GameObject ghostObj, string id, GameEntityState estado, Vector3 posPacMan, Vector3 posFantasma)
        {
            if (!moverFantasmasDinamicamente || ghostObj == null) return;

            Transform tr = ghostObj.transform;
            Vector3 posAtual = tr.position;

            // Variação de velocidade por fantasma para quebrar qualquer sincronia
            int idxFantasma = Mathf.Abs(ghostObj.GetHashCode()) % 4;
            float variacaoVel = 0.9f + (idxFantasma * 0.2f); // 0.9x, 1.1x, 1.3x, 1.5x

            float velocidadeAtual = estado switch
            {
                GameEntityState.Vulnerable => velocidadeVulneravel,
                GameEntityState.Patrol => velocidadePatrulha * variacaoVel,
                _ => velocidadeAgressao * variacaoVel
            };

            // O alvo de perseguição deve permanecer estritamente no nível do chão (posAtual.y)
            posPacMan = new Vector3(posPacMan.x, posAtual.y, posPacMan.z);

            // Em modo Patrol (CONSISTENTE), os Reasoner Agents ignoram o jogador.
            // Em modo Vulnerable (Power Pellet ativo), o Pac-Man devora o fantasma ao encostar!
            if (estado == GameEntityState.Vulnerable && alvoPacMan != null)
            {
                float distHorizontal = Vector2.Distance(new Vector2(posAtual.x, posAtual.z), new Vector2(posPacMan.x, posPacMan.z));
                if (distHorizontal <= 0.95f && Mathf.Abs(posAtual.y - posPacMan.y) < 1.8f)
                {
                    var obs = ghostObj.GetComponent<ObstaculoInvalidoSemantico>() ?? ghostObj.GetComponentInParent<ObstaculoInvalidoSemantico>();
                    if (obs != null)
                    {
                        obs.DevorarFantasmaPorPowerPellet();
                        return;
                    }
                }
            }

            // Apenas causam colisão de dano se estiverem em modo Chase (Aggressive / INCONSISTENTE)
            if (estado == GameEntityState.Aggressive && alvoPacMan != null)
            {
                float distHorizontal = Vector2.Distance(new Vector2(posAtual.x, posAtual.z), new Vector2(posPacMan.x, posPacMan.z));
                if (distHorizontal <= 1.15f && Mathf.Abs(posAtual.y - posPacMan.y) < 1.8f)
                {
                    var ctrlXR = alvoPacMan.GetComponent<ControladorXRJogador>() ?? alvoPacMan.GetComponentInParent<ControladorXRJogador>();
                    if (ctrlXR != null) ctrlXR.ExecutarDanoPacMan(ghostObj);
                    else
                    {
                        var ctrlPadrao = alvoPacMan.GetComponent<PacMan.Player.ControladorJogador>() ?? alvoPacMan.GetComponentInParent<PacMan.Player.ControladorJogador>();
                        if (ctrlPadrao != null) ctrlPadrao.ExecutarDanoPacMan(ghostObj);
                    }

                    // Recuo tático de 6.0 metros para trás
                    Vector3 dirRecuo = (posAtual - posPacMan).normalized;
                    dirRecuo.y = 0f;
                    if (dirRecuo.sqrMagnitude < 0.01f) dirRecuo = -ghostObj.transform.forward;
                    ghostObj.transform.position += dirRecuo * 6.0f;
                    return;
                }
            }

            // 1. Navegação com NavMeshAgent inteligente e espalhado
            UnityEngine.AI.NavMeshAgent navAgent = ghostObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent != null)
            {
                if (!navAgent.enabled) navAgent.enabled = true;

                if (!navAgent.isOnNavMesh)
                {
                    if (UnityEngine.AI.NavMesh.SamplePosition(posAtual, out UnityEngine.AI.NavMeshHit hit, 2.5f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        navAgent.Warp(hit.position);
                    }
                }

                if (navAgent.isOnNavMesh)
                {
                    navAgent.isStopped = false;
                    navAgent.speed = velocidadeAtual;

                    if (estado == GameEntityState.Vulnerable)
                    {
                        Vector3 fleeDir = (posAtual - posPacMan).normalized;
                        navAgent.SetDestination(posAtual + fleeDir * 6.0f);
                    }
                    else
                    {
                        Vector3 destino = CalcularDestinoTaticoFantasma(ghostObj, id, posPacMan, posAtual, m_ListaFantasmasAtivos);
                        if (UnityEngine.AI.NavMesh.SamplePosition(destino, out var hitDest, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            navAgent.SetDestination(hitDest.position);
                        }
                        else
                        {
                            navAgent.SetDestination(destino);
                        }
                    }
                    return;
                }
            }

            // 2. Translação Direcional Ativa por Corredores (fallback)
            Vector3 melhorDirecao = Vector3.zero;

            if (estado == GameEntityState.Vulnerable)
            {
                Vector3 dirFuga = (posAtual - posPacMan);
                dirFuga.y = 0f;
                if (dirFuga.sqrMagnitude > 0.04f)
                {
                    melhorDirecao = CalcularDirecaoNavegacaoCorredor(posAtual, dirFuga.normalized, posPacMan, perseguir: false, ghostObj);
                }
            }
            else
            {
                Vector3 destino = CalcularDestinoTaticoFantasma(ghostObj, id, posPacMan, posAtual, m_ListaFantasmasAtivos);
                Vector3 dirDesejada = (destino - posAtual);
                dirDesejada.y = 0f;
                if (dirDesejada.sqrMagnitude > 0.04f)
                {
                    melhorDirecao = CalcularDirecaoNavegacaoCorredor(posAtual, dirDesejada.normalized, destino, perseguir: true, ghostObj);
                }
            }

            float distParaPacMan = Vector2.Distance(new Vector2(posAtual.x, posAtual.z), new Vector2(posPacMan.x, posPacMan.z));
            if (distParaPacMan < 1.2f && estado != GameEntityState.Vulnerable)
            {
                // Mantém distância de parada tática sem colar na tela
                Vector3 recuoPerto = (posAtual - posPacMan).normalized;
                recuoPerto.y = 0f;
                tr.position = new Vector3(posPacMan.x + recuoPerto.x * 1.35f, posAtual.y, posPacMan.z + recuoPerto.z * 1.35f);
                return;
            }

            if (melhorDirecao.sqrMagnitude > 0.01f)
            {
                melhorDirecao.Normalize();
                Vector3 deslocamento = melhorDirecao * (velocidadeAtual * Time.deltaTime);
                Vector3 novaPos = posAtual + deslocamento;

                float chaoY = posAtual.y;
                if (gerenciadorCenario != null) chaoY = gerenciadorCenario.ObterAlturaSuperficieChao(novaPos) + 0.45f;
                tr.position = new Vector3(novaPos.x, chaoY, novaPos.z);
                tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(melhorDirecao), Time.deltaTime * 8f);
            }
        }

        private bool ChecarCaminhoLivre(Vector3 origem, Vector3 direcao, float distancia, GameObject ignorarObj = null)
        {
            Vector3 pontoOrigem = origem + Vector3.up * 0.45f;

            // 1. Bloqueio por paredes físicas sólidas
            if (Physics.Raycast(pontoOrigem, direcao.normalized, out RaycastHit hit, distancia, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && !hit.collider.isTrigger)
                {
                    if (ignorarObj == null || (hit.collider.gameObject != ignorarObj && !hit.collider.transform.IsChildOf(ignorarObj.transform)))
                    {
                        return false;
                    }
                }
            }

            if (Physics.SphereCast(pontoOrigem, 0.22f, direcao.normalized, out RaycastHit hitSphere, distancia, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hitSphere.collider != null && !hitSphere.collider.isTrigger)
                {
                    if (ignorarObj == null || (hitSphere.collider.gameObject != ignorarObj && !hitSphere.collider.transform.IsChildOf(ignorarObj.transform)))
                    {
                        return false;
                    }
                }
            }

            // 2. Consciência de outros fantasmas à frente no mesmo corredor (FIM DA FILA INDIANA)
            if (m_ListaFantasmasAtivos != null)
            {
                for (int i = 0; i < m_ListaFantasmasAtivos.Count; i++)
                {
                    GameObject outro = m_ListaFantasmasAtivos[i];
                    if (outro == null || outro == ignorarObj) continue;

                    Vector3 toOutro = outro.transform.position - origem;
                    toOutro.y = 0f;
                    float distOutro = toOutro.magnitude;

                    // Se há outro fantasma à frente (distância < 2.5m e ângulo frontal < 50°), o corredor é considerado bloqueado
                    if (distOutro > 0.1f && distOutro < 3.2f)
                    {
                        if (Vector3.Angle(direcao, toOutro) < 50f)
                        {
                            return false; // Força este fantasma a escolher outro corredor!
                        }
                    }
                }
            }

            return true;
        }

        private Vector3 CalcularDirecaoNavegacaoCorredor(Vector3 posAtual, Vector3 dirPreferencial, Vector3 posPacMan, bool perseguir, GameObject ignorarObj = null)
        {
            if (ChecarCaminhoLivre(posAtual, dirPreferencial, 1.2f, ignorarObj))
            {
                return dirPreferencial;
            }

            // Avalia esquinas (90°) e retorno (180°) com prioridade para rotas alternativas desimpedidas
            float[] angulos = new float[] { 90f, -90f, 180f, 45f, -45f, 135f, -135f };
            Vector3 melhorDir = Vector3.zero;
            float melhorMetrica = perseguir ? float.MaxValue : float.MinValue;

            foreach (float ang in angulos)
            {
                Vector3 cand = Quaternion.Euler(0f, ang, 0f) * dirPreferencial;
                cand.y = 0f;
                cand.Normalize();

                if (ChecarCaminhoLivre(posAtual, cand, 1.0f, ignorarObj))
                {
                    Vector3 posFutura = posAtual + (cand * 1.5f);
                    float distAoPacMan = Vector3.Distance(new Vector3(posFutura.x, 0f, posFutura.z), new Vector3(posPacMan.x, 0f, posPacMan.z));

                    // Se a rota direta estava bloqueada por outro fantasma, aceita qualquer corredor aberto sem empacar
                    if (perseguir)
                    {
                        if (distAoPacMan < melhorMetrica || melhorDir == Vector3.zero)
                        {
                            melhorMetrica = distAoPacMan;
                            melhorDir = cand;
                        }
                    }
                    else
                    {
                        if (distAoPacMan > melhorMetrica || melhorDir == Vector3.zero)
                        {
                            melhorMetrica = distAoPacMan;
                            melhorDir = cand;
                        }
                    }
                }
            }

            return melhorDir;
        }

        private void AplicarEstadoNoGameObjectDirect(GameObject ghostObj, GameEntityState novoEstado)
        {
            if (ghostObj == null) return;

            Renderer[] renderers = ghostObj.GetComponentsInChildren<Renderer>();
            if (renderers != null)
            {
                bool ehChaseInconsistente = novoEstado == GameEntityState.Aggressive;
                Color corAlvo;
                if (novoEstado == GameEntityState.Vulnerable)
                {
                    corAlvo = new Color(0.08f, 0.55f, 1.0f, 0.60f); // Azul Translúcido Calmo (quando Pac-Man come item certo)
                }
                else if (ehChaseInconsistente)
                {
                    corAlvo = new Color(1.0f, 0.10f, 0.14f, 1.0f); // Vermelho Sólido (Caçando para quebrar a linha!)
                }
                else
                {
                    corAlvo = new Color(1.0f, 0.50f, 0.08f, 0.82f); // Laranja/Âmbar (Vigiando corredor)
                }

                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer rend = renderers[i];
                    if (rend == null || rend.material == null) continue;
                    string rName = rend.name.ToLower();
                    if (rName.Contains("olho") || rName.Contains("pupila") || rName.Contains("eye")) continue;

                    Material mat = rend.material;
                    if (ehChaseInconsistente)
                    {
                        // Material Vermelho Sólido (Opaque)
                        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
                        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 1);
                        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.renderQueue = -1;
                    }
                    else
                    {
                        // Material Azul Translúcido (Transparent)
                        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    }

                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", corAlvo);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", corAlvo);
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", new Color(corAlvo.r, corAlvo.g, corAlvo.b, 1f) * (ehChaseInconsistente ? 0.95f : 0.45f));
                    }
                }
            }

            AnimacaoFantasma anim = ghostObj.GetComponent<AnimacaoFantasma>();
            if (anim != null && novoEstado == GameEntityState.Vulnerable)
            {
                anim.DispararSusto(0.5f);
            }
        }

        /// <summary>
        /// Ativa o estado de poder do Pac-Man (consumo de SpecialGem / Power Pellet),
        /// disparando imediatamente a regra SWRL de vulnerabilidade.
        /// </summary>
        public void AtivarPowerPellet(float duracao = 8.0f)
        {
            m_FantasmasComidosNoCiclo = 0;
            pacmanIsPowered = true;
            tempoPowerRestante = duracao;
            OnPacManPowerStateChanged?.Invoke(true);

            Debug.Log($"<color=#FFD700><b>[Motor Semântico - SpecialGem]</b></color> Pac-Man consumiu Power Pellet! " +
                      $"Estado Empowered ativo por {duracao:F1}s. Raciocinador avaliando vulnerabilidade dos fantasmas...");

            AtualizarVisualVulneravelFantasmas(true, duracao);

            // Força avaliação imediata das regras
            ProcessarRaciocinioFantasmas();
        }

        /// <summary>
        /// Registra que um fantasma vulnerável foi devorado pelo Pac-Man durante o ciclo atual de Power Pellet.
        /// Retorna a pontuação progressiva clássica: 200 -> 400 -> 800 -> 1600 pts.
        /// </summary>
        public int RegistrarFantasmaDevorado()
        {
            int pts = 200 * (1 << Mathf.Clamp(m_FantasmasComidosNoCiclo, 0, 3));
            m_FantasmasComidosNoCiclo++;
            return pts;
        }

        /// <summary>
        /// Retorna o estado ontológico atual de uma entidade pelo ID.
        /// </summary>
        public GameEntityState ObterEstadoEntidade(string id)
        {
            return m_EstadosEntidades.TryGetValue(id, out var estado) ? estado : GameEntityState.Patrol;
        }

        /// <summary>
        /// Retorna o estado ontológico atual de um fantasma pelo ID (compatibilidade com Telemetria).
        /// </summary>
        public GameEntityState ObterEstadoAtualFantasma(string id) => ObterEstadoEntidade(id);

        /// <summary>
        /// Reseta os estados internos das entidades no motor de inferência.
        /// </summary>
        public void ResetarEstados()
        {
            m_EstadosEntidades.Clear();
            pacmanIsPowered = false;
            tempoPowerRestante = 0f;
            OnPacManPowerStateChanged?.Invoke(false);
        }

        public Graph GetSemanticGraph() => semanticGraph;

        public void SetSemanticGraph(Graph graph)
        {
            if (graph != null)
            {
                semanticGraph = graph;
                m_EstadosEntidades.Clear();
            }
        }

        /// <summary>
        /// FORÇA DE SEPARAÇÃO MATEMÁTICA INTRANSIGÍVEL (Vector3.Lerp / Repulsão Lateral):
        /// Se dois fantasmas estiverem a menos de 3 metros um do outro, o fantasma de trás
        /// é forçado a recalcular a rota para o entroncamento/corredor paralelo mais próximo,
        /// impedindo totalmente que andem em fila indiana.
        /// </summary>
        private void AplicarSeparacaoMatematicaIntransigivelFantasmas(Vector3 posPacMan)
        {
            if (m_ListaFantasmasAtivos == null || m_ListaFantasmasAtivos.Count < 2) return;

            for (int i = 0; i < m_ListaFantasmasAtivos.Count; i++)
            {
                GameObject ghostA = m_ListaFantasmasAtivos[i];
                if (ghostA == null || !ghostA.activeInHierarchy) continue;

                for (int j = i + 1; j < m_ListaFantasmasAtivos.Count; j++)
                {
                    GameObject ghostB = m_ListaFantasmasAtivos[j];
                    if (ghostB == null || !ghostB.activeInHierarchy) continue;

                    Vector3 posA = ghostA.transform.position;
                    Vector3 posB = ghostB.transform.position;
                    Vector3 delta = posA - posB;
                    delta.y = 0f;
                    float dist = delta.magnitude;

                    // Se dois fantasmas estiverem a menos de 3 metros um do outro
                    if (dist < 3.0f && dist > 0.001f)
                    {
                        // 1. Identifica matematicamente qual fantasma está atrás (mais longe do Pac-Man)
                        float distPacA = Vector2.Distance(new Vector2(posA.x, posA.z), new Vector2(posPacMan.x, posPacMan.z));
                        float distPacB = Vector2.Distance(new Vector2(posB.x, posB.z), new Vector2(posPacMan.x, posPacMan.z));

                        GameObject fantasmaFrente = distPacA <= distPacB ? ghostA : ghostB;
                        GameObject fantasmaTras = distPacA <= distPacB ? ghostB : ghostA;

                        Transform trFrente = fantasmaFrente.transform;
                        Transform trTras = fantasmaTras.transform;

                        // 2. Calcula vetor lateral perpendicular intransigível
                        Vector3 fwdLider = trFrente.forward;
                        fwdLider.y = 0f;
                        if (fwdLider.sqrMagnitude < 0.01f) fwdLider = Vector3.forward;
                        fwdLider.Normalize();

                        Vector3 lateralCorredor = Vector3.Cross(Vector3.up, fwdLider).normalized;

                        Vector3 relPos = trTras.position - trFrente.position;
                        relPos.y = 0f;
                        float dotLado = Vector3.Dot(lateralCorredor, relPos);
                        Vector3 dirLateralRepulsao = (dotLado >= 0f ? lateralCorredor : -lateralCorredor);

                        // 3. Força matemática de separação lateral + recuo longitudinal
                        float fatorForca = (3.0f - dist) / 3.0f; // 0..1
                        float magnitudeRepulsao = Mathf.Clamp(fatorForca * 4.2f, 1.5f, 5.0f);

                        Vector3 vetorRepulsaoTotal = (dirLateralRepulsao * magnitudeRepulsao) - (fwdLider * (magnitudeRepulsao * 0.85f));
                        Vector3 posDesejadaTras = trTras.position + (vetorRepulsaoTotal * Time.deltaTime);

                        // Aplica translação matemática intransigível via Vector3.Lerp
                        trTras.position = Vector3.Lerp(trTras.position, posDesejadaTras, Time.deltaTime * 12.0f);

                        // Se houver NavMeshAgent, aplica deslocamento de evasão
                        UnityEngine.AI.NavMeshAgent navTras = fantasmaTras.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        if (navTras != null && navTras.isOnNavMesh)
                        {
                            navTras.Move(vetorRepulsaoTotal * Time.deltaTime);
                        }

                        // 4. RECALCULO FORÇADO DE ROTA:
                        // O fantasma de trás é forçado a recalcular a rota para o entroncamento/corredor paralelo
                        Vector3 desvioEntroncamento = dirLateralRepulsao;
                        if (!ChecarCaminhoLivre(trTras.position, desvioEntroncamento, 1.2f, fantasmaTras))
                        {
                            // Se a lateral estiver bloqueada pela parede do corredor, força inversão de marcha (180°)
                            desvioEntroncamento = -fwdLider;
                        }

                        if (desvioEntroncamento.sqrMagnitude > 0.01f)
                        {
                            trTras.rotation = Quaternion.Slerp(trTras.rotation, Quaternion.LookRotation(desvioEntroncamento), Time.deltaTime * 14.0f);
                        }

                        // Direciona NavMeshAgent para o corredor alternativo
                        if (navTras != null && navTras.isOnNavMesh)
                        {
                            Vector3 waypointParalelo = trTras.position + (desvioEntroncamento * 6.0f);
                            if (UnityEngine.AI.NavMesh.SamplePosition(waypointParalelo, out var hitAlt, 3.5f, UnityEngine.AI.NavMesh.AllAreas))
                            {
                                navTras.SetDestination(hitAlt.position);
                            }
                        }
                    }
                }
            }
        }
    }
}

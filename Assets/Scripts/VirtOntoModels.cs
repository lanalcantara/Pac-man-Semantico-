using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VirtOnto.Model
{
    // =========================================================================
    // 1. ESTRUTURA VECTOR3D
    // =========================================================================

    /// <summary>
    /// Representação vetorial tridimensional matemática compatível com a arquitetura VirtOnto (C++).
    /// Oferece operadores algébricos completos (+, -, *, /), distância euclidiana (DistanceTo)
    /// e conversão bidirecional implícita com UnityEngine.Vector3.
    /// </summary>
    [Serializable]
    public struct Vector3D : IEquatable<Vector3D>
    {
        public float x;
        public float y;
        public float z;

        public Vector3D(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3D Zero => new Vector3D(0f, 0f, 0f);
        public static Vector3D One => new Vector3D(1f, 1f, 1f);
        public static Vector3D Up => new Vector3D(0f, 1f, 0f);
        public static Vector3D Forward => new Vector3D(0f, 0f, 1f);
        public static Vector3D Right => new Vector3D(1f, 0f, 0f);

        /// <summary>
        /// Calcula a distância euclidiana até outro ponto tridimensional.
        /// </summary>
        public float DistanceTo(Vector3D other)
        {
            float dx = x - other.x;
            float dy = y - other.y;
            float dz = z - other.z;
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public float Magnitude => Mathf.Sqrt(x * x + y * y + z * z);
        public float SqrMagnitude => x * x + y * y + z * z;

        public Vector3D Normalized
        {
            get
            {
                float mag = Magnitude;
                return mag > 1e-5f ? new Vector3D(x / mag, y / mag, z / mag) : Zero;
            }
        }

        // Operadores algébricos completos
        public static Vector3D operator +(Vector3D a, Vector3D b) => new Vector3D(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3D operator -(Vector3D a, Vector3D b) => new Vector3D(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3D operator *(Vector3D a, float scalar) => new Vector3D(a.x * scalar, a.y * scalar, a.z * scalar);
        public static Vector3D operator *(float scalar, Vector3D a) => new Vector3D(a.x * scalar, a.y * scalar, a.z * scalar);
        public static Vector3D operator /(Vector3D a, float scalar) => new Vector3D(a.x / scalar, a.y / scalar, a.z / scalar);
        public static Vector3D operator *(Vector3D a, Vector3D b) => new Vector3D(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3D operator -(Vector3D a) => new Vector3D(-a.x, -a.y, -a.z);

        public static bool operator ==(Vector3D a, Vector3D b) =>
            Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y) && Mathf.Approximately(a.z, b.z);

        public static bool operator !=(Vector3D a, Vector3D b) => !(a == b);

        // Conversão implícita ergonômica com UnityEngine.Vector3
        public static implicit operator Vector3(Vector3D v) => new Vector3(v.x, v.y, v.z);
        public static implicit operator Vector3D(Vector3 v) => new Vector3D(v.x, v.y, v.z);

        public bool Equals(Vector3D other) => this == other;
        public override bool Equals(object obj) => obj is Vector3D other && Equals(other);
        public override int GetHashCode() => (x, y, z).GetHashCode();
        public override string ToString() => $"Vector3D({x:F2}, {y:F2}, {z:F2})";
    }

    // =========================================================================
    // 2. CLASSE BASE ABSTRATA ONTOLOGYELEMENT
    // =========================================================================

    /// <summary>
    /// Classe base abstrata para todos os elementos ontológicos do VirtOnto (C++).
    /// Define a identidade única (Id / URI), rótulo semântico (Label) e propriedades.
    /// </summary>
    [Serializable]
    public abstract class OntologyElement
    {
        [SerializeField] protected string id_;
        [SerializeField] protected string label_;
        [SerializeField] protected Dictionary<string, string> properties_ = new Dictionary<string, string>();

        public string Id
        {
            get => id_;
            set => id_ = value;
        }

        public string Label
        {
            get => label_;
            set => label_ = value;
        }

        public string GetLabel() => label_;
        public string GetId() => id_;

        public Dictionary<string, string> Properties
        {
            get => properties_;
            set => properties_ = value ?? new Dictionary<string, string>();
        }

        protected OntologyElement(string id, string label)
        {
            id_ = id ?? string.Empty;
            label_ = label ?? string.Empty;
        }

        /// <summary>
        /// Método polimórfico obrigatório: Retorna o código hexadecimal da cor de exibição.
        /// </summary>
        public abstract string GetDisplayColor();

        public virtual Color GetColorRGB()
        {
            string hex = GetDisplayColor();
            if (ColorUtility.TryParseHtmlString(hex, out Color cor))
            {
                return cor;
            }
            return Color.white;
        }

        public override string ToString() => $"[{GetType().Name}] Id: '{id_}', Label: '{label_}'";
    }

    // =========================================================================
    // 3. ENUM NODETYPE E CLASSE NODE
    // =========================================================================

    public enum NodeType
    {
        CLASS = 0,         // Conceito TBox (ex: Pacman, Ghost, Item, StandardGem, Obstacle)
        INDIVIDUAL = 1,    // Indivíduo ABox instanciado no labirinto
        PROPERTY = 2,      // Object/Data Property (ex: consumes, isNearTo)
        RULE = 3,          // Regra SWRL
        AXIOM = 4,         // Axioma de Description Logic

        // Aliases em PascalCase para interoperabilidade
        Class = 0,
        Individual = 1,
        Property = 2,
        Rule = 3,
        Axiom = 4
    }

    /// <summary>
    /// Representa um Nó no grafo ontológico VirtOnto em C#.
    /// Possui suporte a tipos NodeType, posições 3D (Vector3D), dinâmica física
    /// (velocidade, massa) e cor de exibição visual em RV/RA.
    /// </summary>
    [Serializable]
    public class Node : OntologyElement
    {
        [SerializeField] private NodeType type_;
        [SerializeField] private Vector3D position_;
        [SerializeField] private Vector3D velocity_;
        [SerializeField] private float mass_ = 1.0f;
        [SerializeField] private string customDisplayColor_ = null;

        // Propriedades semânticas Description Logic
        public string ConceptClass { get; set; } = string.Empty;
        public string OntologyClass { get => ConceptClass; set => ConceptClass = value; }
        public string DLExpression { get; set; } = string.Empty;
        public bool IsValid { get; set; } = true;
        public int ScoreValue { get; set; } = 10;
        public string SemanticDescription { get; set; } = string.Empty;
        public Color HighlightColor { get; set; } = Color.green;

        // Vínculo opcional de tempo de execução com o GameObject físico no Unity
        [NonSerialized] public GameObject GameObjectInstance;

        public NodeType Type
        {
            get => type_;
            set => type_ = value;
        }

        public Vector3D Position
        {
            get => position_;
            set => position_ = value;
        }

        public void SetPosition(Vector3D pos)
        {
            position_ = pos;
        }

        public Vector3D GetPosition() => position_;

        public Vector3D Velocity
        {
            get => velocity_;
            set => velocity_ = value;
        }

        public float Mass
        {
            get => mass_;
            set => mass_ = value;
        }

        public Node(string id, string label, NodeType type) : base(id, label)
        {
            type_ = type;
            position_ = Vector3D.Zero;
            velocity_ = Vector3D.Zero;
            mass_ = 1.0f;
        }

        public string DisplayColor
        {
            get => GetDisplayColor();
            set => SetDisplayColor(value);
        }

        public void SetDisplayColor(string colorHex)
        {
            customDisplayColor_ = colorHex;
        }

        public override string GetDisplayColor()
        {
            if (!string.IsNullOrEmpty(customDisplayColor_))
            {
                return customDisplayColor_;
            }

            switch (type_)
            {
                case NodeType.CLASS:
                    return "#FFA500"; // Laranja (TBox)
                case NodeType.INDIVIDUAL:
                    return IsValid ? "#00FF66" : "#FF3344"; // Verde (Válida) / Vermelho (Violação)
                case NodeType.PROPERTY:
                    return "#0088FF"; // Azul (Relações)
                case NodeType.RULE:
                    return "#9B59B6"; // Roxo (SWRL)
                case NodeType.AXIOM:
                    return "#F39C12"; // Âmbar (Axiomas)
                default:
                    return "#FFFFFF";
            }
        }
    }

    // =========================================================================
    // 4. CLASSE EDGE (ARESTAS E AXIOMAS LÓGICOS)
    // =========================================================================

    /// <summary>
    /// Representa uma Aresta direcionada ou simétrica entre nós ontológicos.
    /// Modela axiomas ontológicos OWL e relações SWRL (consumes, isNearTo, rdfs:subClassOf, etc.).
    /// </summary>
    [Serializable]
    public class Edge : OntologyElement
    {
        [SerializeField] private string sourceId_;
        [SerializeField] private string targetId_;
        [SerializeField] private string relationType_;
        [SerializeField] private float weight_ = 1.0f;
        [SerializeField] private bool isSymmetric_ = false;

        public string SourceId
        {
            get => sourceId_;
            set => sourceId_ = value;
        }

        public string TargetId
        {
            get => targetId_;
            set => targetId_ = value;
        }

        public string RelationType
        {
            get => relationType_;
            set => relationType_ = value;
        }

        public string Predicate
        {
            get => relationType_;
            set => relationType_ = value;
        }

        public float Weight
        {
            get => weight_;
            set => weight_ = value;
        }

        public bool IsSymmetric
        {
            get => isSymmetric_;
            set => isSymmetric_ = value;
        }

        public Edge(string id, string source, string target, string relation, float weight = 1.0f, bool isSymmetric = false)
            : base(id, relation)
        {
            sourceId_ = source ?? string.Empty;
            targetId_ = target ?? string.Empty;
            relationType_ = relation ?? string.Empty;
            weight_ = weight;
            isSymmetric_ = isSymmetric;
        }

        public override string GetDisplayColor()
        {
            if (string.Equals(relationType_, "rdfs:subClassOf", StringComparison.OrdinalIgnoreCase))
                return "#7B68EE";
            if (string.Equals(relationType_, "consumes", StringComparison.OrdinalIgnoreCase))
                return "#FFD700";
            if (string.Equals(relationType_, "isNearTo", StringComparison.OrdinalIgnoreCase))
                return "#00E5FF";
            if (string.Equals(relationType_, "violates", StringComparison.OrdinalIgnoreCase))
                return "#FF3344";
            if (string.Equals(relationType_, "satisfies", StringComparison.OrdinalIgnoreCase))
                return "#00FF66";

            return "#888888";
        }

        public override string ToString() => $"Edge({sourceId_} --[{relationType_}]--> {targetId_})";
    }

    // =========================================================================
    // 5. CLASSE GRAPH (GESTOR CENTRAL DO GRAFO ONTO-ESPACIAL)
    // =========================================================================

    /// <summary>
    /// Gestor central do grafo ontológico VirtOnto em C#.
    /// Mantém um dicionário indexado de nós por ID e uma lista de arestas relacionais,
    /// suportando consultas em tempo constante O(1), inferência de regras SWRL
    /// e conversão de dados para o GerenciadorCenarioSemantico.
    /// </summary>
    [Serializable]
    public class Graph
    {
        private readonly Dictionary<string, Node> nodes_ = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Edge> edges_ = new List<Edge>();

        // Índices otimizados para consultas ontológicas
        private readonly Dictionary<string, List<Edge>> outgoingEdges_ = new Dictionary<string, List<Edge>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Edge>> incomingEdges_ = new Dictionary<string, List<Edge>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<NodeType, List<Node>> nodesByType_ = new Dictionary<NodeType, List<Node>>();
        private readonly Dictionary<string, List<Node>> individualsByClass_ = new Dictionary<string, List<Node>>(StringComparer.OrdinalIgnoreCase);

        public string NomeOntologia { get; set; } = "OntologiaPacManSemantico_VirtOnto";
        public string VersaoAxiomas { get; set; } = "ALCQ(D)-VirtOnto-v2.0";

        public Graph()
        {
            foreach (NodeType type in Enum.GetValues(typeof(NodeType)))
            {
                nodesByType_[type] = new List<Node>();
            }
        }

        public void AddNode(Node node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id)) return;

            if (nodes_.ContainsKey(node.Id))
            {
                nodes_[node.Id] = node;
                return;
            }

            nodes_[node.Id] = node;

            if (!nodesByType_.TryGetValue(node.Type, out var list))
            {
                list = new List<Node>();
                nodesByType_[node.Type] = list;
            }
            list.Add(node);

            if (node.Type == NodeType.INDIVIDUAL && !string.IsNullOrEmpty(node.ConceptClass))
            {
                if (!individualsByClass_.TryGetValue(node.ConceptClass, out var indList))
                {
                    indList = new List<Node>();
                    individualsByClass_[node.ConceptClass] = indList;
                }
                indList.Add(node);
            }
        }

        public void AddEdge(Edge edge)
        {
            if (edge == null || string.IsNullOrEmpty(edge.SourceId) || string.IsNullOrEmpty(edge.TargetId)) return;

            edges_.Add(edge);

            if (!outgoingEdges_.TryGetValue(edge.SourceId, out var outList))
            {
                outList = new List<Edge>();
                outgoingEdges_[edge.SourceId] = outList;
            }
            outList.Add(edge);

            if (!incomingEdges_.TryGetValue(edge.TargetId, out var inList))
            {
                inList = new List<Edge>();
                incomingEdges_[edge.TargetId] = inList;
            }
            inList.Add(edge);

            if (edge.IsSymmetric)
            {
                if (!outgoingEdges_.TryGetValue(edge.TargetId, out var symOutList))
                {
                    symOutList = new List<Edge>();
                    outgoingEdges_[edge.TargetId] = symOutList;
                }
                symOutList.Add(edge);

                if (!incomingEdges_.TryGetValue(edge.SourceId, out var symInList))
                {
                    symInList = new List<Edge>();
                    incomingEdges_[edge.SourceId] = symInList;
                }
                symInList.Add(edge);
            }
        }

        public Node GetNode(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            nodes_.TryGetValue(id, out Node node);
            return node;
        }

        public bool TryGetNode(string id, out Node node)
        {
            return nodes_.TryGetValue(id, out node);
        }

        public IReadOnlyDictionary<string, Node> GetNodes() => nodes_;
        public IReadOnlyList<Edge> GetEdges() => edges_;

        public int GetNodeCount() => nodes_.Count;
        public int GetEdgeCount() => edges_.Count;

        public void Clear()
        {
            nodes_.Clear();
            edges_.Clear();
            outgoingEdges_.Clear();
            incomingEdges_.Clear();
            individualsByClass_.Clear();

            foreach (var key in nodesByType_.Keys)
            {
                nodesByType_[key].Clear();
            }
        }

        public IReadOnlyList<Node> GetNodesByType(NodeType type)
        {
            return nodesByType_.TryGetValue(type, out var list) ? list : (IReadOnlyList<Node>)new List<Node>();
        }

        public IReadOnlyList<Node> GetIndividualsOfClass(string className)
        {
            return individualsByClass_.TryGetValue(className, out var list) ? list : (IReadOnlyList<Node>)new List<Node>();
        }

        public IReadOnlyList<Edge> GetOutgoingEdges(string nodeId)
        {
            return outgoingEdges_.TryGetValue(nodeId, out var list) ? list : (IReadOnlyList<Edge>)new List<Edge>();
        }

        public IReadOnlyList<Edge> GetIncomingEdges(string nodeId)
        {
            return incomingEdges_.TryGetValue(nodeId, out var list) ? list : (IReadOnlyList<Edge>)new List<Edge>();
        }

        public List<Node> GetNeighbors(string nodeId)
        {
            List<Node> vizinhos = new List<Node>();
            if (outgoingEdges_.TryGetValue(nodeId, out var outList))
            {
                foreach (var e in outList)
                {
                    string target = e.SourceId == nodeId ? e.TargetId : e.SourceId;
                    if (TryGetNode(target, out var n) && !vizinhos.Contains(n))
                    {
                        vizinhos.Add(n);
                    }
                }
            }
            return vizinhos;
        }

        /// <summary>
        /// Popula e indexa o grafo a partir de um CenarioSemanticoDTO.
        /// </summary>
        public void BuildFromCenarioDTO(CenarioSemanticoDTO cenario)
        {
            Clear();
            if (cenario == null) return;

            NomeOntologia = string.IsNullOrEmpty(cenario.nomeOntologia) ? "OntologiaPacMan" : cenario.nomeOntologia;
            VersaoAxiomas = string.IsNullOrEmpty(cenario.versaoAxiomas) ? "v2.0-VirtOnto" : cenario.versaoAxiomas;

            IndexarTBoxPadrao();

            if (cenario.instancias != null)
            {
                int edgeCounter = 1;
                foreach (var inst in cenario.instancias)
                {
                    if (inst == null) continue;

                    Node nodeInd = new Node(inst.id, inst.classeOntologica, NodeType.INDIVIDUAL)
                    {
                        ConceptClass = inst.classeOntologica,
                        DLExpression = inst.expressaoDL,
                        IsValid = inst.ehValida,
                        Position = inst.posicao,
                        ScoreValue = inst.valorPontuacao,
                        SemanticDescription = inst.descricaoSemantica,
                        HighlightColor = inst.corDestaque
                    };

                    AddNode(nodeInd);

                    if (!nodes_.ContainsKey(inst.classeOntologica))
                    {
                        Node classNode = new Node(inst.classeOntologica, inst.classeOntologica, NodeType.CLASS);
                        AddNode(classNode);
                    }

                    AddEdge(new Edge($"e_type_{edgeCounter++}", inst.id, inst.classeOntologica, "rdf:type", 1.0f));

                    string relRegra = inst.ehValida ? "satisfies" : "violates";
                    AddEdge(new Edge($"e_rule_{edgeCounter++}", inst.id, "ALCQ_Axioma", relRegra, 1.0f));
                }
            }

            AtualizarRelacoesProximidade(raioProximidade: 3.5f);
        }

        /// <summary>
        /// Converte o grafo ontológico para o formato de transferência DTO.
        /// </summary>
        public CenarioSemanticoDTO ToCenarioDTO()
        {
            CenarioSemanticoDTO cenario = new CenarioSemanticoDTO
            {
                nomeOntologia = NomeOntologia,
                versaoAxiomas = VersaoAxiomas,
                ontologiaConsistente = true,
                instancias = new List<DadoInstanciaSemantica>()
            };

            foreach (var node in GetNodesByType(NodeType.INDIVIDUAL))
            {
                cenario.instancias.Add(new DadoInstanciaSemantica
                {
                    id = node.Id,
                    classeOntologica = string.IsNullOrEmpty(node.ConceptClass) ? node.Label : node.ConceptClass,
                    expressaoDL = node.DLExpression,
                    ehValida = node.IsValid,
                    posicao = node.Position,
                    valorPontuacao = node.ScoreValue,
                    descricaoSemantica = node.SemanticDescription,
                    corDestaque = node.HighlightColor
                });
            }

            return cenario;
        }

        /// <summary>
        /// Indexa a hierarquia TBox e regras SWRL da Ontologia de Parasitologia e Biomedicina (Protégé / ALCQ(D)).
        /// </summary>
        public void IndexarTBoxPadrao()
        {
            NomeOntologia = "OntologiaParasitologia_Protege_ALCQ";
            VersaoAxiomas = "ALCQ(D)-Parasitology-v2.0";

            // 1. Conceito Raiz OWL
            Node thing = new Node("owl:Thing", "Thing", NodeType.CLASS);

            // 2. Taxonomia de Parasitas e Organismos (Classes Principais)
            Node parasite = new Node("Parasite", "Parasite", NodeType.CLASS) { DLExpression = "Parasite ⊑ owl:Thing" };
            Node nematoda = new Node("Nematoda", "Nematoda (Helmintos Cilíndricos)", NodeType.CLASS) { DLExpression = "Nematoda ≡ Parasite ⊓ ∃has_morphology.Cylindrical" };
            Node platyhelminthes = new Node("Platyhelminthes", "Platyhelminthes (Vermes Achatados)", NodeType.CLASS) { DLExpression = "Platyhelminthes ≡ Parasite ⊓ ∃has_morphology.FlatSegments" };
            Node protozoa = new Node("Protozoa", "Protozoa (Protozoários)", NodeType.CLASS) { DLExpression = "Protozoa ⊑ Parasite" };

            // 3. Espécies Reais de Parasitas
            Node ascaris = new Node("AscarisLumbricoides", "Ascaris lumbricoides", NodeType.CLASS) 
            { 
                DLExpression = "AscarisLumbricoides ≡ Nematoda ⊓ ∃has_host.Human ⊓ ∃has_habitat.SmallIntestine ⊓ ∃has_transmission.FecalOral",
                SemanticDescription = "Nematódeo parasita do intestino delgado humano, transmitido por via fecal-oral."
            };
            Node enterobius = new Node("EnterobiusVermicularis", "Enterobius vermicularis", NodeType.CLASS) 
            { 
                DLExpression = "EnterobiusVermicularis ≡ Nematoda ⊓ ∃has_host.Human ⊓ ∃has_habitat.PerianalRegion ⊓ ∃has_transmission.Autoinfection",
                SemanticDescription = "Oxiúro causador de prurido anal, com ciclo de autoinfecção."
            };
            Node taenia = new Node("TaeniaSolium", "Taenia solium", NodeType.CLASS) 
            { 
                DLExpression = "TaeniaSolium ≡ Platyhelminthes ⊓ ∃has_host.Human ⊓ ∃has_habitat.SmallIntestine ⊓ ∃has_transmission.UndercookedPork",
                SemanticDescription = "Cestódeo transmitido pela ingestão de carne suína crua/malcozida."
            };

            // 4. Hospedeiros (Hosts)
            Node host = new Node("Host", "Host", NodeType.CLASS);
            Node human = new Node("Human", "Human (Homo sapiens)", NodeType.CLASS);
            Node pig = new Node("Pig", "Pig (Sus domesticus)", NodeType.CLASS);

            // 5. Habitats e Sítios Anatômicos
            Node habitat = new Node("Habitat", "Habitat", NodeType.CLASS);
            Node intestine = new Node("Intestine", "Intestine", NodeType.CLASS);
            Node smallIntestine = new Node("SmallIntestine", "SmallIntestine (Intestino Delgado)", NodeType.CLASS);
            Node perianalRegion = new Node("PerianalRegion", "PerianalRegion (Região Perianal)", NodeType.CLASS);
            Node largeIntestine = new Node("LargeIntestine", "LargeIntestine (Intestino Grosso)", NodeType.CLASS);

            // 6. Morfologias
            Node morphology = new Node("Morphology", "Morphology", NodeType.CLASS);
            Node cylindrical = new Node("Cylindrical", "Cylindrical (Cilíndrico)", NodeType.CLASS);
            Node flatSegments = new Node("FlatSegments", "FlatSegments (Segmentado Achatado)", NodeType.CLASS);

            // 7. Vias de Transmissão
            Node transmission = new Node("Transmission", "Transmission", NodeType.CLASS);
            Node fecalOral = new Node("FecalOral", "FecalOral (Fecal-Oral)", NodeType.CLASS);
            Node undercookedPork = new Node("UndercookedPork", "UndercookedPork (Carne Suína Contaminada)", NodeType.CLASS);
            Node autoinfection = new Node("Autoinfection", "Autoinfection (Autoinfecção)", NodeType.CLASS);

            // 8. Sintomas e Patologia
            Node symptom = new Node("Symptom", "Symptom", NodeType.CLASS);
            Node abdominalPain = new Node("AbdominalPain", "AbdominalPain (Dor Abdominal)", NodeType.CLASS);
            Node pruritusAni = new Node("PruritusAni", "PruritusAni (Prurido Anal)", NodeType.CLASS);
            Node malnutrition = new Node("Malnutrition", "Malnutrition (Desnutrição)", NodeType.CLASS);

            // 9. Agentes de Simulação e Vetores
            Node agent = new Node("Agent", "Agent (Agente Investigador)", NodeType.CLASS);
            Node researcher = new Node("BiomedicalResearcher", "BiomedicalResearcher (Pacman)", NodeType.CLASS);
            Node vector = new Node("InfectionVector", "InfectionVector (Vetor Hostil / Blinky)", NodeType.CLASS);
            Node vulnerableVector = new Node("VulnerableVector", "VulnerableVector (Vetor Neutralizado)", NodeType.CLASS);
            Node antiparasitic = new Node("AntiparasiticTreatment", "AntiparasiticTreatment (PowerPellet)", NodeType.CLASS);
            Node inconsistentSpecimen = new Node("InconsistentSpecimen", "InconsistentSpecimen (Obstáculo Ontológico)", NodeType.CLASS);

            // 10. Regras SWRL
            Node swrlR1 = new Node("SWRL_R1", "Regra 1: Neutralização de Vetor por Tratamento Antiparasitário", NodeType.RULE)
            {
                SemanticDescription = "BiomedicalResearcher(?p) ^ consumes(?p, ?t) ^ AntiparasiticTreatment(?t) ^ InfectionVector(?v) ^ isNearTo(?v, ?p) -> VulnerableVector(?v)"
            };
            Node swrlR2 = new Node("SWRL_R2", "Regra 2: Classificação Taxonômica HermiT / DL", NodeType.RULE)
            {
                SemanticDescription = "Parasite(?s) ^ has_morphology(?s, Cylindrical) ^ has_host(?s, Human) ^ has_habitat(?s, SmallIntestine) ^ has_transmission(?s, FecalOral) -> AscarisLumbricoides(?s)"
            };
            Node swrlR3 = new Node("SWRL_R3", "Regra 3: Detecção de Inconsistência Ontológica / Obstáculo", NodeType.RULE)
            {
                SemanticDescription = "InconsistentSpecimen(?o) ^ (A ⊓ ¬A ≡ ⊥) -> Inconsistent(?o)"
            };
            Node alcqAxiom = new Node("ALCQ_Axioma", "ALCQ(D) Consistency Reasoner", NodeType.AXIOM);

            // Adiciona nós ao grafo
            AddNode(thing);
            AddNode(parasite);
            AddNode(nematoda);
            AddNode(platyhelminthes);
            AddNode(protozoa);
            AddNode(ascaris);
            AddNode(enterobius);
            AddNode(taenia);
            AddNode(host);
            AddNode(human);
            AddNode(pig);
            AddNode(habitat);
            AddNode(intestine);
            AddNode(smallIntestine);
            AddNode(perianalRegion);
            AddNode(largeIntestine);
            AddNode(morphology);
            AddNode(cylindrical);
            AddNode(flatSegments);
            AddNode(transmission);
            AddNode(fecalOral);
            AddNode(undercookedPork);
            AddNode(autoinfection);
            AddNode(symptom);
            AddNode(abdominalPain);
            AddNode(pruritusAni);
            AddNode(malnutrition);
            AddNode(agent);
            AddNode(researcher);
            AddNode(vector);
            AddNode(vulnerableVector);
            AddNode(antiparasitic);
            AddNode(inconsistentSpecimen);
            AddNode(swrlR1);
            AddNode(swrlR2);
            AddNode(swrlR3);
            AddNode(alcqAxiom);

            // Relacionamentos de Herança TBox (rdfs:subClassOf)
            AddEdge(new Edge("h_par", "Parasite", "owl:Thing", "rdfs:subClassOf"));
            AddEdge(new Edge("h_nem", "Nematoda", "Parasite", "rdfs:subClassOf"));
            AddEdge(new Edge("h_pla", "Platyhelminthes", "Parasite", "rdfs:subClassOf"));
            AddEdge(new Edge("h_pro", "Protozoa", "Parasite", "rdfs:subClassOf"));
            AddEdge(new Edge("h_asc", "AscarisLumbricoides", "Nematoda", "rdfs:subClassOf"));
            AddEdge(new Edge("h_ent", "EnterobiusVermicularis", "Nematoda", "rdfs:subClassOf"));
            AddEdge(new Edge("h_tae", "TaeniaSolium", "Platyhelminthes", "rdfs:subClassOf"));
            AddEdge(new Edge("h_host", "Host", "owl:Thing", "rdfs:subClassOf"));
            AddEdge(new Edge("h_hum", "Human", "Host", "rdfs:subClassOf"));
            AddEdge(new Edge("h_pig", "Pig", "Host", "rdfs:subClassOf"));
            AddEdge(new Edge("h_hab", "Habitat", "owl:Thing", "rdfs:subClassOf"));
            AddEdge(new Edge("h_int", "Intestine", "Habitat", "rdfs:subClassOf"));
            AddEdge(new Edge("h_sint", "SmallIntestine", "Intestine", "rdfs:subClassOf"));
            AddEdge(new Edge("h_lint", "LargeIntestine", "Intestine", "rdfs:subClassOf"));
            AddEdge(new Edge("h_peri", "PerianalRegion", "Habitat", "rdfs:subClassOf"));
            AddEdge(new Edge("h_mor", "Morphology", "owl:Thing", "rdfs:subClassOf"));
            AddEdge(new Edge("h_cyl", "Cylindrical", "Morphology", "rdfs:subClassOf"));
            AddEdge(new Edge("h_flat", "FlatSegments", "Morphology", "rdfs:subClassOf"));
            AddEdge(new Edge("h_tra", "Transmission", "owl:Thing", "rdfs:subClassOf"));
            AddEdge(new Edge("h_fec", "FecalOral", "Transmission", "rdfs:subClassOf"));
            AddEdge(new Edge("h_pork", "UndercookedPork", "Transmission", "rdfs:subClassOf"));
            AddEdge(new Edge("h_auto", "Autoinfection", "Transmission", "rdfs:subClassOf"));
            AddEdge(new Edge("h_sym", "Symptom", "owl:Thing", "rdfs:subClassOf"));
            AddEdge(new Edge("h_abdp", "AbdominalPain", "Symptom", "rdfs:subClassOf"));
            AddEdge(new Edge("h_prur", "PruritusAni", "Symptom", "rdfs:subClassOf"));
            AddEdge(new Edge("h_maln", "Malnutrition", "Symptom", "rdfs:subClassOf"));
            AddEdge(new Edge("h_ag", "Agent", "owl:Thing", "rdfs:subClassOf"));
            AddEdge(new Edge("h_res", "BiomedicalResearcher", "Agent", "rdfs:subClassOf"));
            AddEdge(new Edge("h_vec", "InfectionVector", "Agent", "rdfs:subClassOf"));
            AddEdge(new Edge("h_vvec", "VulnerableVector", "InfectionVector", "rdfs:subClassOf"));
            AddEdge(new Edge("h_obs", "InconsistentSpecimen", "owl:Thing", "rdfs:subClassOf"));
        }

        /// <summary>
        /// Carrega e analisa uma ontologia diretamente a partir do XML/RDF de um arquivo .owl exportado do Protégé.
        /// Extrai classes TBox, indivíduos ABox, tipagem rdf:type e Object Properties (has_host, has_habitat, has_transmission, etc.).
        /// </summary>
        public static Graph LoadFromOwlXml(string owlXmlContent)
        {
            Graph graph = new Graph();
            graph.IndexarTBoxPadrao();

            if (string.IsNullOrEmpty(owlXmlContent)) return graph;

            // Extrai classes declaradas
            MatchCollection classMatches = Regex.Matches(owlXmlContent, @"<owl:Class\s+rdf:about=""#?([^""]+)""");
            foreach (Match m in classMatches)
            {
                string className = m.Groups[1].Value;
                if (!graph.TryGetNode(className, out _))
                {
                    graph.AddNode(new Node(className, className, NodeType.CLASS));
                }
            }

            // Extrai indivíduos nomeados
            MatchCollection indMatches = Regex.Matches(owlXmlContent, @"<owl:NamedIndividual\s+rdf:about=""#?([^""]+)""[^>]*>(.*?)</owl:NamedIndividual>", RegexOptions.Singleline);
            int indIdx = 1;
            int edgeIdx = 1;

            foreach (Match m in indMatches)
            {
                string indId = m.Groups[1].Value;
                string indContent = m.Groups[2].Value;

                // Tenta descobrir a classe taxonômica do indivíduo
                Match typeMatch = Regex.Matches(indContent, @"<rdf:type\s+rdf:resource=""#?([^""]+)""").Cast<Match>().LastOrDefault();
                string conceptClass = typeMatch != null ? typeMatch.Groups[1].Value : "Parasite";

                int score = 10;
                string dl = $"{conceptClass} ⊑ Parasite";
                string desc = $"Instância biomédica autêntica '{indId}' classificada como {conceptClass} a partir da ontologia Protégé.";
                Color cor = new Color(0.1f, 1f, 0.45f);
                bool valida = true;

                switch (conceptClass)
                {
                    case "AscarisLumbricoides":
                        score = 25;
                        dl = "Nematoda ⊓ ∃has_host.Human ⊓ ∃has_habitat.SmallIntestine ⊓ ∃has_transmission.FecalOral";
                        desc = "Ascaris lumbricoides: Helminto nematódeo parasita do intestino delgado humano transmitido por via fecal-oral.";
                        cor = new Color(0.0f, 1.0f, 0.8f);
                        break;

                    case "EnterobiusVermicularis":
                        score = 20;
                        dl = "Nematoda ⊓ ∃has_host.Human ⊓ ∃has_habitat.PerianalRegion ⊓ ∃has_transmission.Autoinfection";
                        desc = "Enterobius vermicularis (oxiúro): Helminto cilíndrico associado a prurido perianal e ciclo de autoinfecção.";
                        cor = new Color(0.2f, 0.75f, 1.0f);
                        break;

                    case "TaeniaSolium":
                        score = 30;
                        dl = "Platyhelminthes ⊓ ∃has_host.Human ⊓ ∃has_habitat.SmallIntestine ⊓ ∃has_transmission.UndercookedPork";
                        desc = "Taenia solium: Platelminto segmentado achatado do intestino delgado com transmissão suína.";
                        cor = new Color(1.0f, 0.84f, 0.0f);
                        break;

                    case "InfectionVector":
                    case "InconsistentSpecimen":
                    case "InconsistentParasiteSpecimen":
                        valida = false;
                        score = -15;
                        dl = "Parasite ⊓ ∃has_morphology.(Cylindrical ⊓ FlatSegments) ≡ ⊥";
                        desc = "Violação ontológica taxonômica detectada no espécime pelo raciocinador ALCQ(D).";
                        cor = new Color(1.0f, 0.15f, 0.2f);
                        break;
                }

                Node indNode = new Node(indId, indId, NodeType.INDIVIDUAL)
                {
                    ConceptClass = conceptClass,
                    IsValid = valida,
                    DLExpression = dl,
                    ScoreValue = score,
                    SemanticDescription = desc,
                    HighlightColor = cor
                };
                graph.AddNode(indNode);
                graph.AddEdge(new Edge($"e_owl_type_{indIdx++}", indId, conceptClass, "rdf:type"));

                // Extrai Object Properties reais: has_host, has_habitat, has_transmission, has_morphology, has_symptom
                string[] propriedadesOwl = new string[] { "has_host", "has_habitat", "has_transmission", "has_morphology", "has_symptom" };
                foreach (var prop in propriedadesOwl)
                {
                    MatchCollection propMatches = Regex.Matches(indContent, $@"<{prop}\s+rdf:resource=""#?([^""]+)""");
                    foreach (Match pm in propMatches)
                    {
                        string targetVal = pm.Groups[1].Value;
                        graph.AddEdge(new Edge($"e_owl_prop_{edgeIdx++}", indId, targetVal, prop));
                    }
                }
            }

            return graph;
        }

        /// <summary>
        /// Calcula arestas de proximidade simétricas (isNearTo) com base na distância geométrica dos nós 3D.
        /// </summary>
        public void AtualizarRelacoesProximidade(float raioProximidade = 3.5f)
        {
            var individuos = GetNodesByType(NodeType.INDIVIDUAL);
            int count = individuos.Count;

            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    Node a = individuos[i];
                    Node b = individuos[j];

                    float dist = a.Position.DistanceTo(b.Position);
                    if (dist <= raioProximidade)
                    {
                        string edgeId = $"near_{a.Id}_{b.Id}";
                        AddEdge(new Edge(edgeId, a.Id, b.Id, "isNearTo", dist, isSymmetric: true));
                    }
                }
            }
        }

        public void AvaliarRegrasSWRL(Action<string> onRegraDisparada = null)
        {
            AtualizarRelacoesProximidade();

            var individuos = GetNodesByType(NodeType.INDIVIDUAL);
            foreach (var ind in individuos)
            {
                var arestasSaida = GetOutgoingEdges(ind.Id);
                foreach (var e in arestasSaida)
                {
                    if (string.Equals(e.RelationType, "isNearTo", StringComparison.OrdinalIgnoreCase))
                    {
                        onRegraDisparada?.Invoke($"[SWRL - Regra 4] isNearTo detectada entre '{ind.Id}' e '{e.TargetId}' (Dist: {e.Weight:F2}m).");
                    }
                }
            }
        }
    }
}

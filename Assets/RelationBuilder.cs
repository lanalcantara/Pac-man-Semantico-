using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using PacMan.Network;

/// <summary>
/// ============================================================================
/// REDE NEURAL / GRAFO ONTOLÓGICO DO PAC-MAN (RelationBuilder)
/// ============================================================================
/// Conecta tudo o que o Pac-Man come (Classes -> Subclasses -> Instâncias)
/// formando uma Rede Neural / Grafo Ontológico luminoso pelo labirinto:
/// 1. Cada item válido comido vira um Nó Sináptico fixo no corredor e é conectado
///    ao item anterior por um segmento de linha neon + rótulo da relação OWL.
/// 2. Uma linha guia ativa sai do último item comido e segue atrás do Pac-Man,
///    mostrando o fio da rede neural sendo tecido em tempo real.
/// 3. Quando um Fantasma bate no Pac-Man, ele QUEBRA o último pedaço dessa linha!
///    O item comido reaparece no corredor com aviso "🔄 COMA NOVAMENTE", exigindo
///    que o jogador volte lá para comer aquela classe/subclasse/instância de novo.
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class RelationBuilder : MonoBehaviour
{
    public static RelationBuilder Instance { get; private set; }

    [Header("Configuração de Object Property OWL")]
    public string propriedadeAtiva = "subClassOf / has_host";

    public string[] catalogoPropriedades = new string[]
    {
        "subClassOf",
        "instanceOf",
        "has_host",
        "has_habitat",
        "has_transmission"
    };

    [Header("Estado da Rede Neural Ontológica")]
    public bool emModoConexao = false;
    public string nodoOrigemId;
    public string nodoOrigemClasse;
    public Vector3 nodoOrigemPosicao;

    [Header("Configuração Visual dos Fios da Rede Neural")]
    public float larguraLinha = 0.075f;
    public Color corLinhaGuiaPacMan = new Color(0.0f, 0.95f, 1.0f, 0.95f);
    public Color corLinhaRedeFixada = new Color(0.20f, 1.0f, 0.55f, 0.95f);
    public Color corLinhaQuebradaAlerta = new Color(1.0f, 0.20f, 0.25f, 0.95f);

    [Serializable]
    public class RelacaoCriada
    {
        public string sourceId;
        public string sourceClass;
        public string propertyName;
        public string targetId;
        public string targetClass;
        public GameObject lineObject;
        public GameObject labelObject;
    }

    [Serializable]
    public class EloRedeNeuralOntologica
    {
        public int indiceOrdem;
        public string nodeId;
        public string nomeClasse;
        public string tipoElemento; // "Classe", "Subclasse" ou "Instância"
        public string relacaoComAnterior;
        public Vector3 posicaoMundo;
        public Vector3 escalaOriginal;
        public GameObject objetoNodoNoMapa;
        public InstanciaSemanticaBase componenteInstancia;
        public GameObject objetoLinhaAteEsteNodo;
        public GameObject objetoRotuloLinha;
    }

    private readonly List<RelacaoCriada> m_RelacoesFixadas = new List<RelacaoCriada>();
    public IReadOnlyList<RelacaoCriada> RelacoesFixadas => m_RelacoesFixadas;

    private readonly List<EloRedeNeuralOntologica> m_CadeiaRedeNeural = new List<EloRedeNeuralOntologica>();
    public IReadOnlyList<EloRedeNeuralOntologica> CadeiaRedeNeural => m_CadeiaRedeNeural;

    [Header("Visibilidade e Minimalismo")]
    public static bool exibirRotulosFlutuantes = false;
    public bool exibirLinhasEspaciais = false;

    private LineRenderer m_LinhaGuiaPacMan;
    private Material m_MaterialLinha;
    private int m_IndicePropriedade = 0;

    private void Awake()
    {
        Instance = this;
        PrepararMaterialLinha();
        LimparLinhasTemporariasOrfas();
        if (!exibirLinhasEspaciais)
        {
            LineRenderer[] todasLinhas = FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < todasLinhas.Length; i++)
            {
                if (todasLinhas[i] != null) todasLinhas[i].enabled = false;
            }
        }
    }

    private void OnEnable()
    {
        InstanciaSemanticaBase.OnInstanciaInteragida += AoInteragirComNodo;
    }

    private void OnDisable()
    {
        InstanciaSemanticaBase.OnInstanciaInteragida -= AoInteragirComNodo;
    }

    private void LimparLinhasTemporariasOrfas()
    {
        LineRenderer[] todasLinhas = FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < todasLinhas.Length; i++)
        {
            if (todasLinhas[i] != null && todasLinhas[i].gameObject.name.StartsWith("Linha_Conexao_Ativa_", StringComparison.OrdinalIgnoreCase))
            {
                todasLinhas[i].enabled = false;
                if (Application.isPlaying) Destroy(todasLinhas[i].gameObject);
                else DestroyImmediate(todasLinhas[i].gameObject);
            }
        }
    }

    private Vector3 ObterPosicaoPacManReal()
    {
        GameObject visPac = GameObject.Find("Visual_PacMan");
        if (visPac != null)
        {
            return visPac.transform.position + Vector3.up * 0.42f;
        }
        return transform.position + Vector3.up * 0.42f;
    }

    private void Update()
    {
        // Alterna propriedade com Tab
        if (Input.GetKeyDown(KeyCode.Tab) && catalogoPropriedades != null && catalogoPropriedades.Length > 0)
        {
            m_IndicePropriedade = (m_IndicePropriedade + 1) % catalogoPropriedades.Length;
            propriedadeAtiva = catalogoPropriedades[m_IndicePropriedade];
            if (SemanticHUDManager.Instancia != null)
            {
                SemanticHUDManager.Instancia.AdicionarLinhaLog($"[OWL] Relação ativa: {propriedadeAtiva}");
            }
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            InstanciarNodoCriacaoInGame();
        }

        // Atualiza a Linha Guia da Rede Neural que sai do ÚLTIMO item comido e segue atrás do Pac-Man
        if (m_CadeiaRedeNeural.Count > 0 && exibirLinhasEspaciais)
        {
            EloRedeNeuralOntologica ultimoElo = m_CadeiaRedeNeural[m_CadeiaRedeNeural.Count - 1];
            emModoConexao = true;
            nodoOrigemId = ultimoElo.nodeId;
            nodoOrigemClasse = ultimoElo.nomeClasse;
            nodoOrigemPosicao = ultimoElo.posicaoMundo + Vector3.up * 0.38f;

            GarantirLinhaGuiaPacMan();
            m_LinhaGuiaPacMan.enabled = true;
            m_LinhaGuiaPacMan.SetPosition(0, nodoOrigemPosicao);
            m_LinhaGuiaPacMan.SetPosition(1, ObterPosicaoPacManReal());
        }
        else
        {
            emModoConexao = false;
            if (m_LinhaGuiaPacMan != null)
            {
                m_LinhaGuiaPacMan.enabled = false;
            }
        }

        // Billboard: faz todos os rótulos 3D das linhas olharem para a câmera
        if (Camera.main != null)
        {
            Transform camTr = Camera.main.transform;
            for (int i = 0; i < m_RelacoesFixadas.Count; i++)
            {
                var rel = m_RelacoesFixadas[i];
                if (rel != null && rel.labelObject != null)
                {
                    Vector3 dir = rel.labelObject.transform.position - camTr.position;
                    if (dir.sqrMagnitude > 0.001f)
                    {
                        rel.labelObject.transform.rotation = Quaternion.LookRotation(dir);
                    }
                }
            }
        }
    }

    private void GarantirLinhaGuiaPacMan()
    {
        if (m_LinhaGuiaPacMan == null)
        {
            GameObject lineObj = new GameObject("Linha_RedeNeural_PacMan", typeof(LineRenderer));
            m_LinhaGuiaPacMan = ConfigurarLineRenderer(lineObj, corLinhaGuiaPacMan, larguraLinha * 0.85f);
        }
    }

    private void AoInteragirComNodo(IInstanciaSemantica instancia, bool sucesso)
    {
        if (instancia == null) return;

        // Apenas itens VÁLIDOS (Classes, Subclasses e Instâncias certas) entram na Rede Neural do Pac-Man.
        // Obstáculos/Fantasmas não viram nós da rede; ao baterem no Pac-Man, eles quebram o último elo!
        if (!sucesso || !instancia.EhValida) return;

        GameObject objMapa = (instancia is Component c && c != null) ? c.gameObject : null;
        AdicionarItemComidoNaRedeNeural(instancia, objMapa);
    }

    /// <summary>
    /// Chamado quando o Pac-Man come uma Classe, Subclasse ou Instância certa no labirinto.
    /// Conecta este item ao anterior formando a Rede Neural / Grafo Ontológico atrás do Pac-Man.
    /// </summary>
    public void AdicionarItemComidoNaRedeNeural(IInstanciaSemantica instancia, GameObject objNodo = null)
    {
        if (instancia == null || string.IsNullOrEmpty(instancia.Id)) return;

        // Evita duplicar se já for o último elo conectado
        for (int i = 0; i < m_CadeiaRedeNeural.Count; i++)
        {
            if (string.Equals(m_CadeiaRedeNeural[i].nodeId, instancia.Id, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        string nomeFormatado = InstanciaSemanticaBase.FormatarNomeClasse(instancia.NomeClasse);
        string tipoHierarquico = ClassificarTipoElementoOntologico(instancia.NomeClasse, instancia.ExpressaoDL);
        Vector3 posNodo = objNodo != null ? objNodo.transform.position : instancia.Posicao;
        Vector3 escalaOrig = objNodo != null ? objNodo.transform.localScale : new Vector3(0.45f, 0.45f, 0.45f);

        int novoIndice = m_CadeiaRedeNeural.Count + 1;
        string relacao = DeterminarRelacaoComEloAnterior(
            m_CadeiaRedeNeural.Count > 0 ? m_CadeiaRedeNeural[m_CadeiaRedeNeural.Count - 1] : null,
            nomeFormatado,
            tipoHierarquico);

        GameObject linhaSegmentoObj = null;
        GameObject rotuloSegmentoObj = null;

        // Se já havia um item comido antes, cria o segmento de linha fixo da Rede Neural entre o anterior e este!
        if (m_CadeiaRedeNeural.Count > 0)
        {
            EloRedeNeuralOntologica anterior = m_CadeiaRedeNeural[m_CadeiaRedeNeural.Count - 1];
            Vector3 pOrigem = anterior.posicaoMundo + Vector3.up * 0.38f;
            Vector3 pDestino = posNodo + Vector3.up * 0.38f;

            linhaSegmentoObj = new GameObject($"EloRede_{anterior.nomeClasse}_para_{nomeFormatado}", typeof(LineRenderer));
            LineRenderer lrFixo = ConfigurarLineRenderer(linhaSegmentoObj, corLinhaRedeFixada, larguraLinha);
            lrFixo.enabled = exibirLinhasEspaciais;
            lrFixo.SetPosition(0, pOrigem);
            lrFixo.SetPosition(1, pDestino);

            Vector3 pontoMedio = (pOrigem + pDestino) * 0.5f + Vector3.up * 0.34f;
            string textoAresta = $"🔗 {relacao}\n<size=75%>({anterior.nomeClasse} ➔ {nomeFormatado})</size>";
            if (exibirLinhasEspaciais && exibirRotulosFlutuantes)
            {
                rotuloSegmentoObj = InstanciarRotuloFlutuanteTMP(linhaSegmentoObj.transform, pontoMedio, textoAresta, corLinhaRedeFixada);
            }

            m_RelacoesFixadas.Add(new RelacaoCriada
            {
                sourceId = anterior.nodeId,
                sourceClass = anterior.nomeClasse,
                propertyName = relacao,
                targetId = instancia.Id,
                targetClass = nomeFormatado,
                lineObject = linhaSegmentoObj,
                labelObject = rotuloSegmentoObj
            });

            // Envia a tripla via WebSocket para o Protégé se ativo
            var ws = ProtegeWebSocketBridge.Instance ?? FindFirstObjectByType<ProtegeWebSocketBridge>();
            if (ws != null)
            {
                ws.EnviarRelacaoObjectProperty(anterior.nodeId, anterior.nomeClasse, relacao, instancia.Id, nomeFormatado, true);
            }
        }

        // Atualiza o rótulo 3D sobre o item no mapa para mostrar que ele agora faz parte da Rede Neural
        if (objNodo != null)
        {
            AtualizarRotulo3DNodoNoMapa(
                objNodo,
                $"<color=#00FF88><b>✔ CONECTADO #{novoIndice} ({tipoHierarquico})</b></color>\n<color=#FFFFFF><b>{nomeFormatado}</b></color>");
        }

        EloRedeNeuralOntologica novoElo = new EloRedeNeuralOntologica
        {
            indiceOrdem = novoIndice,
            nodeId = instancia.Id,
            nomeClasse = nomeFormatado,
            tipoElemento = tipoHierarquico,
            relacaoComAnterior = relacao,
            posicaoMundo = posNodo,
            escalaOriginal = escalaOrig,
            objetoNodoNoMapa = objNodo,
            componenteInstancia = instancia as InstanciaSemanticaBase,
            objetoLinhaAteEsteNodo = linhaSegmentoObj,
            objetoRotuloLinha = rotuloSegmentoObj
        };

        m_CadeiaRedeNeural.Add(novoElo);

        // Atualiza o HUD explicando o que o Pac-Man acabou de comer e por que os fantasmas ficaram azuis!
        if (SemanticHUDManager.Instancia != null)
        {
            SemanticHUDManager.Instancia.DefinirAxiomasInseridos(m_CadeiaRedeNeural.Count);
            SemanticHUDManager.Instancia.RegistrarCapturaExplicativa(
                nomeFormatado,
                tipoHierarquico,
                instancia.ExpressaoDL,
                m_CadeiaRedeNeural.Count,
                relacao);
        }
    }

    /// <summary>
    /// MECÂNICA SOLICITADA PELA USUÁRIA:
    /// Quando o Fantasma bate no Pac-Man, ele QUEBRA um pedaço da linha da Rede Neural!
    /// O último item comido é desconectado e REAPARECE no labirinto para que o jogador
    /// tenha que voltar lá e comer aquela Classe, Subclasse ou Instância certa novamente!
    /// </summary>
    public bool QuebrarUltimoPedacoDaRedePorFantasma(string nomeFantasma = "Fantasma Caçador")
    {
        if (m_CadeiaRedeNeural.Count == 0)
        {
            if (SemanticHUDManager.Instancia != null)
            {
                SemanticHUDManager.Instancia.ExibirAlertaQuebraDeLinha(
                    nomeFantasma,
                    "Nenhum elo ativo",
                    "Sua rede ainda estava vazia! Siga as placas de trânsito e coma uma Classe/Subclasse válida (Verde/Ciano).");
            }
            return false;
        }

        int idxUltimo = m_CadeiaRedeNeural.Count - 1;
        EloRedeNeuralOntologica eloQuebrado = m_CadeiaRedeNeural[idxUltimo];
        m_CadeiaRedeNeural.RemoveAt(idxUltimo);

        // 1. Destrói o pedaço da linha que conectava até esse item
        if (eloQuebrado.objetoLinhaAteEsteNodo != null)
        {
            for (int i = m_RelacoesFixadas.Count - 1; i >= 0; i--)
            {
                if (m_RelacoesFixadas[i].lineObject == eloQuebrado.objetoLinhaAteEsteNodo)
                {
                    m_RelacoesFixadas.RemoveAt(i);
                }
            }
            Destroy(eloQuebrado.objetoLinhaAteEsteNodo);
        }

        // 2. Reativa aquela Classe / Subclasse / Instância lá no corredor original para o Pac-Man voltar e comer de novo!
        if (eloQuebrado.objetoNodoNoMapa != null)
        {
            GameObject nodoObj = eloQuebrado.objetoNodoNoMapa;
            nodoObj.SetActive(true);
            nodoObj.transform.position = eloQuebrado.posicaoMundo;
            nodoObj.transform.localScale = eloQuebrado.escalaOriginal.sqrMagnitude > 0.01f
                ? eloQuebrado.escalaOriginal
                : new Vector3(0.45f, 0.45f, 0.45f);

            if (eloQuebrado.componenteInstancia != null)
            {
                eloQuebrado.componenteInstancia.ReativarInstanciaParaReconexao();
            }
            else
            {
                Collider[] cols = nodoObj.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] != null) cols[i].enabled = true;
                }
            }

            AtualizarRotulo3DNodoNoMapa(
                nodoObj,
                $"<color=#FFD700><b>🔄 LINHA QUEBRADA! VOLTE E COMA:</b></color>\n<color=#FFFFFF><b>[{eloQuebrado.tipoElemento}] {eloQuebrado.nomeClasse}</b></color>");
        }

        // 3. Atualiza o contador e mostra o aviso grande na tela explicando qual pedaço da linha quebrou
        if (SemanticHUDManager.Instancia != null)
        {
            SemanticHUDManager.Instancia.DefinirAxiomasInseridos(m_CadeiaRedeNeural.Count);
            SemanticHUDManager.Instancia.ExibirAlertaQuebraDeLinha(
                nomeFantasma,
                $"{eloQuebrado.tipoElemento}: {eloQuebrado.nomeClasse}",
                $"O fantasma bateu em você e QUEBROU a conexão com '{eloQuebrado.nomeClasse}'! Volte ao corredor e coma '{eloQuebrado.nomeClasse}' novamente para reconstruir sua rede!");
            SemanticHUDManager.Instancia.AdicionarLinhaLog(
                $"<color=#FF4455>[💥 LINHA QUEBRADA]</color> {nomeFantasma} rompeu o elo '{eloQuebrado.nomeClasse}'. Volte para comer novamente!");
        }

        Debug.LogWarning($"<color=#FF3344><b>[Rede Neural Quebrada]</b></color> {nomeFantasma} quebrou o elo #{eloQuebrado.indiceOrdem} ('{eloQuebrado.nomeClasse}'). O item reapareceu em {eloQuebrado.posicaoMundo} para reconexão!");
        return true;
    }

    public static string ClassificarTipoElementoOntologico(string nomeClasse, string expressaoDL)
    {
        string c = (nomeClasse ?? "").ToLowerInvariant();
        string dl = (expressaoDL ?? "").ToLowerInvariant();

        if (c.Contains("parasite") && !c.Contains("specimen") || c == "host" || c.Contains("super"))
            return "Superclasse";
        if (c.Contains("nematod") || c.Contains("platyhelmint") || c.Contains("cestod") || c.Contains("trematod") || c.Contains("protozo"))
            return "Subclasse";
        if (c.Contains("ascaris") || c.Contains("taenia") || c.Contains("enterobius") || c.Contains("schistosoma") || dl.Contains("individu"))
            return "Instância";
        if (c.Contains("human") || c.Contains("pig") || c.Contains("intestine") || c.Contains("fecal"))
            return "Subclasse/Propriedade";
        return "Classe OWL";
    }

    private string DeterminarRelacaoComEloAnterior(EloRedeNeuralOntologica anterior, string novoNome, string novoTipo)
    {
        if (anterior == null) return "owl:Thing";
        if (novoTipo == "Subclasse") return "rdfs:subClassOf";
        if (novoTipo == "Instância") return "rdf:type (instanceOf)";
        string nLower = (novoNome ?? "").ToLowerInvariant();
        if (nLower.Contains("human") || nLower.Contains("pig") || nLower.Contains("host")) return "has_host";
        if (nLower.Contains("intestin")) return "has_habitat";
        if (nLower.Contains("fecal") || nLower.Contains("pork")) return "has_transmission";
        return propriedadeAtiva;
    }

    /// <summary>
    /// Cria ou atualiza o rótulo 3D flutuante (Billboard) acima de uma gema/nodo no labirinto.
    /// </summary>
    public static void AtualizarRotulo3DNodoNoMapa(GameObject objNodo, string textoRico)
    {
        if (objNodo == null) return;

        Transform trLabel = objNodo.transform.Find("Rotulo_3D_Item_Ontologico");
        if (!exibirRotulosFlutuantes)
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
            labelObj = new GameObject("Rotulo_3D_Item_Ontologico", typeof(RectTransform));
            labelObj.transform.SetParent(objNodo.transform, false);
            labelObj.transform.localPosition = new Vector3(0f, 0.92f, 0f);
        }

        TextMeshPro tmp = labelObj.GetComponent<TextMeshPro>();
        if (tmp == null) tmp = labelObj.AddComponent<TextMeshPro>();

        tmp.text = textoRico;
        tmp.richText = true;
        tmp.fontSize = 2.15f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        tmp.outlineWidth = 0.25f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform rt = labelObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(4.5f, 1.1f);
        }

        if (labelObj.GetComponent<BillboardOlharCamera>() == null)
        {
            labelObj.AddComponent<BillboardOlharCamera>();
        }
    }

    // Compatibilidade com chamadas diretas
    public void RegistrarInteracaoNodo(string nodeId, string nodeClass, Vector3 nodePos, bool ehValido = true)
    {
        if (!ehValido) return;
        var ger = FindFirstObjectByType<GerenciadorCenarioSemantico>();
        IInstanciaSemantica inst = ger != null ? ger.ObterInstanciaPorId(nodeId) : null;
        if (inst != null)
        {
            GameObject obj = (inst is Component c && c != null) ? c.gameObject : null;
            AdicionarItemComidoNaRedeNeural(inst, obj);
        }
    }

    private LineRenderer ConfigurarLineRenderer(GameObject host, Color cor, float largura)
    {
        LineRenderer lr = host.GetComponent<LineRenderer>();
        if (lr == null)
        {
            lr = host.AddComponent<LineRenderer>();
        }
        lr.useWorldSpace = true;
        lr.startWidth = largura;
        lr.endWidth = largura;
        lr.numCapVertices = 4;
        lr.positionCount = 2;

        PrepararMaterialLinha();
        Material matInst = new Material(m_MaterialLinha);
        if (matInst.HasProperty("_BaseColor")) matInst.SetColor("_BaseColor", cor);
        if (matInst.HasProperty("_Color")) matInst.SetColor("_Color", cor);
        if (matInst.HasProperty("_EmissionColor"))
        {
            matInst.EnableKeyword("_EMISSION");
            matInst.SetColor("_EmissionColor", cor * 1.35f);
        }

        lr.material = matInst;
        lr.startColor = cor;
        lr.endColor = cor;
        return lr;
    }

    private GameObject InstanciarRotuloFlutuanteTMP(Transform pai, Vector3 posicaoMundo, string textoPropriedade, Color corDestaque)
    {
        GameObject labelObj = new GameObject($"TMP_Label_Rede", typeof(RectTransform));
        labelObj.transform.SetParent(pai, true);
        labelObj.transform.position = posicaoMundo;

        TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = textoPropriedade;
        tmp.richText = true;
        tmp.fontSize = 2.0f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        tmp.outlineWidth = 0.24f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform rt = labelObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(4.2f, 1.0f);
        }

        if (labelObj.GetComponent<BillboardOlharCamera>() == null)
        {
            labelObj.AddComponent<BillboardOlharCamera>();
        }

        return labelObj;
    }

    private int m_ContadorNodosCriadosInGame = 0;

    public GameObject InstanciarNodoCriacaoInGame(string nomeClasseCustom = null)
    {
        m_ContadorNodosCriadosInGame++;
        string[] classesSugestao = { "Parasite", "Host", "Nematoda", "Platyhelminthes", "Human", "SmallIntestine" };
        string nomeClasse = !string.IsNullOrEmpty(nomeClasseCustom)
            ? nomeClasseCustom
            : classesSugestao[(m_ContadorNodosCriadosInGame - 1) % classesSugestao.Length];
        string idNodo = $"node_ingame_{nomeClasse}_{m_ContadorNodosCriadosInGame:D2}";

        Vector3 dirFrente = transform.forward.sqrMagnitude > 0.01f ? transform.forward.normalized : Vector3.forward;
        Vector3 posSpawn = ObterPosicaoPacManReal() + dirFrente * 1.8f;
        posSpawn.y = 0.45f;

        GameObject nodoObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nodoObj.name = $"OntologyNode_{idNodo}";
        nodoObj.transform.position = posSpawn;
        nodoObj.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
        nodoObj.transform.rotation = Quaternion.Euler(35f, 45f, 0f);

        var ger = FindFirstObjectByType<GerenciadorCenarioSemantico>();
        Renderer rend = nodoObj.GetComponent<Renderer>();
        if (rend != null && ger != null)
        {
            rend.material = ger.CriarMaterialTranslúcidoNodo(new Color(0.0f, 0.85f, 1.0f, 1f), 0.65f);
        }

        GerenciadorCenarioSemantico.AnexarLuzPontualDestaque(nodoObj, new Color(0.0f, 0.85f, 1.0f, 1f), 2.5f, 1.25f);

        BoxCollider boxCol = nodoObj.GetComponent<BoxCollider>();
        if (boxCol != null) boxCol.isTrigger = true;

        GerenciadorInstancia compNodo = nodoObj.AddComponent<GerenciadorInstancia>();
        compNodo.ConfigurarComDados(new DadoInstanciaSemantica
        {
            id = idNodo,
            classeOntologica = nomeClasse,
            expressaoDL = $"{nomeClasse}({idNodo})",
            ehValida = true,
            posicao = posSpawn,
            valorPontuacao = 50,
            descricaoSemantica = $"Nodo ontológico criado in-game ({nomeClasse})"
        });

        AtualizarRotulo3DNodoNoMapa(nodoObj, $"<color=#00E5FF>[Subclasse]</color>\n<b>{nomeClasse}</b>");
        return nodoObj;
    }

    private void PrepararMaterialLinha()
    {
        if (m_MaterialLinha == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            m_MaterialLinha = new Material(s);
        }
    }
}

/// <summary>
/// Componente leve para manter rótulos 3D sempre virados para a câmera do jogador.
/// </summary>
public class BillboardOlharCamera : MonoBehaviour
{
    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 dir = transform.position - cam.transform.position;
        if (dir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}

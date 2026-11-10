using TMPro;
using UnityEngine;

/// <summary>
/// ============================================================================
/// INTERFACE ESPACIAL BIDIRECIONAL OWL - Placa de Trânsito Semântica (Wayfinding)
/// ============================================================================
/// Placa direcional em formato de Placa de Trânsito Rodoviária/Urbana (com poste,
/// borda refletiva e ponta em formato de seta direcional física) posicionada nas
/// bifurcações do labirinto para indicar claramente:
/// 1. A DIREÇÃO do corredor (⬅ ESQUERDA, ➡ DIREITA, ⬆ FRENTE, ⬇ RETORNO).
/// 2. Quais CLASSES, SUBCLASSES e INSTÂNCIAS certas existem naquela direção.
/// 3. Quais classes disjuntas/erradas EVITAR naquele corredor para construir a ontologia.
/// ============================================================================
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class PlacaSinalizacaoSemantica : MonoBehaviour
{
    public const float AlturaCentroPainel = 1.34f;
    public const float LarguraPainelHolografico = 2.50f;
    public const float AlturaPainelHolografico = 0.78f;
    public const float EspessuraPainelHolografico = 0.06f;

    public const float LarguraRectTexto = 2.30f;
    public const float AlturaRectTexto = 0.68f;
    public const float OffsetZTexto = 0.045f;

    [Header("Visibilidade e Minimalismo")]
    public static bool exibirPlacas = false;

    [Header("Identificação da Bifurcação e Hierarquia OWL")]
    public string idBifurcacao = "Bifurcacao_01";
    public string setaDirecao = "⬆";
    public string tipoHierarquia = "Superclasse";
    public string nomeClasseExibida = "Parasitas";
    public string itensCertosCorredor = "Nematoda, Ascaris";
    public string itensErradosEvitar = "Hospedeiro Inválido";

    [Header("Referências Visuais e TextMeshPro")]
    public TMP_Text textoFrontalTMP;
    public TMP_Text textoTraseiroTMP;
    public Renderer rendererPainelHolografico;
    public Renderer rendererMolduraNeon;
    public Renderer rendererPosteBase;

    // Aliases de compatibilidade direta com GerenciadorCenarioSemantico e GeradorPrefabsEditor
    public TMP_Text textoFrontal
    {
        get => textoFrontalTMP;
        set => textoFrontalTMP = value;
    }

    public TMP_Text textoTraseiro
    {
        get => textoTraseiroTMP;
        set => textoTraseiroTMP = value;
    }

    public Renderer rendererPainelFundo
    {
        get => rendererPainelHolografico;
        set => rendererPainelHolografico = value;
    }

    public Renderer[] renderersEmissivos;

    [Header("Cores de Placa de Trânsito Semântica")]
    public Color corNeon = new Color(0.10f, 0.75f, 0.40f, 1.0f);
    [Range(0.4f, 1.8f)]
    public float intensidadeEmissaoHDR = 0.75f;

    private static TMP_FontAsset s_FontePadraoTMP;

    private void Awake()
    {
        if (!exibirPlacas)
        {
            gameObject.SetActive(false);
            return;
        }
        SincronizarGeometriaPlacaTransito();
        GarantirReferenciasTextMeshPro();
        GarantirTextoPadraoOffline();
        AtualizarVisualPlaca();
    }

    private void OnEnable()
    {
        if (!exibirPlacas)
        {
            gameObject.SetActive(false);
            return;
        }
        SincronizarGeometriaPlacaTransito();
        GarantirReferenciasTextMeshPro();
        GarantirTextoPadraoOffline();
        AtualizarVisualPlaca();
    }

    private void Start()
    {
        if (!exibirPlacas)
        {
            gameObject.SetActive(false);
            return;
        }
        SincronizarGeometriaPlacaTransito();
        GarantirReferenciasTextMeshPro();
        GarantirTextoPadraoOffline();
        AtualizarVisualPlaca();
    }

    /// <summary>
    /// Sobrecarga canônica: Inicializa a placa recebendo o nome da classe OWL e o tipo de relação/hierarquia.
    /// </summary>
    public void ConfigurarPlaca(string nomeClasse, string tipoRelacao = "Subclasse")
    {
        Color corAutomatica = DeterminarCorPorClasseOuRelacao(nomeClasse, tipoRelacao);
        string seta = !string.IsNullOrEmpty(setaDirecao) ? setaDirecao : "⬆";
        ConfigurarPlaca(seta, tipoRelacao, nomeClasse, corAutomatica);
    }

    public void ConfigurarPlaca(string nomeClasse, string tipoRelacao, Color corCategoria)
    {
        string seta = !string.IsNullOrEmpty(setaDirecao) ? setaDirecao : "⬆";
        ConfigurarPlaca(seta, tipoRelacao, nomeClasse, corCategoria);
    }

    /// <summary>
    /// Atualiza a placa de trânsito com a direção, a classe/subclasse principal e dicas do que pegar/evitar no corredor.
    /// </summary>
    public void ConfigurarPlaca(string seta, string prefixoHierarquia, string nomeClasse, Color corCategoria, string dicasCertas = null, string dicasEvitar = null)
    {
        setaDirecao = string.IsNullOrEmpty(seta) ? "⬆" : seta;
        tipoHierarquia = NormalizarTipoRelacao(prefixoHierarquia);
        nomeClasseExibida = string.IsNullOrEmpty(nomeClasse) ? ObterNomeClasseOfflinePadrao(tipoHierarquia) : nomeClasse;
        corNeon = corCategoria;

        PreencherDicasSemanticasDoCorredor(nomeClasseExibida, tipoHierarquia, dicasCertas, dicasEvitar);

        SincronizarGeometriaPlacaTransito();
        GarantirReferenciasTextMeshPro();
        AtualizarVisualPlaca();
    }

    private void PreencherDicasSemanticasDoCorredor(string classe, string hierarquia, string certasCustom, string evitarCustom)
    {
        if (!string.IsNullOrEmpty(certasCustom))
        {
            itensCertosCorredor = certasCustom;
        }
        else
        {
            string cLower = (classe ?? "").ToLowerInvariant();
            if (cLower.Contains("parasit"))
                itensCertosCorredor = "Subclasses: Nematoda, Platyhelminthes";
            else if (cLower.Contains("hosped") || cLower.Contains("host"))
                itensCertosCorredor = "Subclasses: Human, SmallIntestine";
            else if (cLower.Contains("nematod"))
                itensCertosCorredor = "Instâncias: Ascaris, Enterobius";
            else if (cLower.Contains("platy") || cLower.Contains("cestod"))
                itensCertosCorredor = "Instância: Taenia solium";
            else if (cLower.Contains("human"))
                itensCertosCorredor = "Relação: has_host -> Human";
            else if (cLower.Contains("intestin"))
                itensCertosCorredor = "Relação: has_habitat -> Intestine";
            else if (cLower.Contains("fecal"))
                itensCertosCorredor = "Relação: has_transmission -> FecalOral";
            else
                itensCertosCorredor = $"Itens de {classe}";
        }

        if (!string.IsNullOrEmpty(evitarCustom))
        {
            itensErradosEvitar = evitarCustom;
        }
        else
        {
            string cLower = (classe ?? "").ToLowerInvariant();
            if (cLower.Contains("nematod"))
                itensErradosEvitar = "FlatSegments (Disjunto)";
            else if (cLower.Contains("platy"))
                itensErradosEvitar = "Cylindrical (Disjunto)";
            else if (cLower.Contains("parasit"))
                itensErradosEvitar = "Classes Disjuntas (Vermelhas)";
            else
                itensErradosEvitar = "Anomalia Disjunta (Vermelha)";
        }
    }

    private void GarantirTextoPadraoOffline()
    {
        if (string.IsNullOrEmpty(tipoHierarquia)) tipoHierarquia = "Superclasse";
        if (string.IsNullOrEmpty(nomeClasseExibida)) nomeClasseExibida = ObterNomeClasseOfflinePadrao(tipoHierarquia);
        if (string.IsNullOrEmpty(setaDirecao)) setaDirecao = "⬆";
        if (string.IsNullOrEmpty(itensCertosCorredor) || string.IsNullOrEmpty(itensErradosEvitar))
        {
            PreencherDicasSemanticasDoCorredor(nomeClasseExibida, tipoHierarquia, null, null);
        }
    }

    private static string NormalizarTipoRelacao(string prefixo)
    {
        if (string.IsNullOrEmpty(prefixo)) return "Superclasse";
        if (prefixo.IndexOf("Caminho", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            prefixo.IndexOf("Super", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Superclasse";
        }
        if (prefixo.IndexOf("Sub", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Subclasse";
        }
        return prefixo;
    }

    private string ObterNomeClasseOfflinePadrao(string relacao)
    {
        if (!string.IsNullOrEmpty(relacao) && relacao.IndexOf("Super", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Parasitas";
        }
        return "Nematoda";
    }

    private string ObterTextoDirecaoTransito(string seta)
    {
        if (string.IsNullOrEmpty(seta)) return "⬆ SIGA EM FRENTE";
        if (seta.Contains("⬅")) return "⬅ VIRE À ESQUERDA";
        if (seta.Contains("➡")) return "➡ VIRE À DIREITA";
        if (seta.Contains("⬇")) return "⬇ CORREDOR SUL";
        return "⬆ SIGA EM FRENTE";
    }

    private Color DeterminarCorPorClasseOuRelacao(string nomeClasse, string tipoRelacao)
    {
        string combinado = $"{nomeClasse} {tipoRelacao}";
        if (combinado.IndexOf("Hosped", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            combinado.IndexOf("Host", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            combinado.IndexOf("Human", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            combinado.IndexOf("Intestine", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new Color(0.08f, 0.55f, 1.0f, 1.0f); // Placa Rodoviária Azul
        }
        return new Color(0.12f, 0.78f, 0.42f, 1.0f); // Placa Rodoviária Verde-Esmeralda
    }

    /// <summary>
    /// Modela a placa em formato autêntico de Placa de Trânsito Direcional:
    /// - Poste cilíndrico firme no chão
    /// - Placa retangular com bordas refletivas (superior e inferior)
    /// - Ponta triangular (losango 45°) apontando na direção da seta (Esquerda, Direita ou Frente)!
    /// </summary>
    private void SincronizarGeometriaPlacaTransito()
    {
        Transform trBase = transform.Find("Base_Pedestal");
        if (trBase != null)
        {
            trBase.localPosition = new Vector3(0f, 0.04f, 0f);
            trBase.localScale = new Vector3(0.42f, 0.04f, 0.42f);
        }

        Transform trPoste = transform.Find("Poste_Suporte");
        if (trPoste != null)
        {
            trPoste.localPosition = new Vector3(0f, 0.62f, 0f);
            trPoste.localScale = new Vector3(0.10f, 0.62f, 0.10f);
        }

        Transform trPainel = transform.Find("Painel_Holografico");
        if (trPainel != null)
        {
            trPainel.localPosition = new Vector3(0f, AlturaCentroPainel, 0f);
            trPainel.localRotation = Quaternion.identity;
            trPainel.localScale = new Vector3(LarguraPainelHolografico, AlturaPainelHolografico, EspessuraPainelHolografico);
            if (rendererPainelHolografico == null)
            {
                rendererPainelHolografico = trPainel.GetComponent<Renderer>();
            }
        }

        Transform trMoldura = transform.Find("Moldura_Neon_HDR");
        if (trMoldura != null)
        {
            float topoPainel = AlturaCentroPainel + (AlturaPainelHolografico * 0.5f) + 0.025f;
            trMoldura.localPosition = new Vector3(0f, topoPainel, 0f);
            trMoldura.localRotation = Quaternion.identity;
            trMoldura.localScale = new Vector3(LarguraPainelHolografico + 0.08f, 0.055f, EspessuraPainelHolografico + 0.02f);
            if (rendererMolduraNeon == null)
            {
                rendererMolduraNeon = trMoldura.GetComponent<Renderer>();
            }
        }

        // Borda inferior de placa de trânsito
        Transform trMolduraInf = transform.Find("Moldura_Inferior_Transito");
        if (trMolduraInf == null)
        {
            GameObject objInf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objInf.name = "Moldura_Inferior_Transito";
            objInf.transform.SetParent(transform, false);
            Collider c = objInf.GetComponent<Collider>();
            if (c != null)
            {
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }
            trMolduraInf = objInf.transform;
        }
        float basePainel = AlturaCentroPainel - (AlturaPainelHolografico * 0.5f) - 0.025f;
        trMolduraInf.localPosition = new Vector3(0f, basePainel, 0f);
        trMolduraInf.localRotation = Quaternion.identity;
        trMolduraInf.localScale = new Vector3(LarguraPainelHolografico + 0.08f, 0.055f, EspessuraPainelHolografico + 0.02f);

        // Ponta de Seta Física de Placa de Trânsito (aponta para Esquerda ⬅, Direita ➡ ou Topo ⬆)
        Transform trPontaSeta = transform.Find("Ponta_Seta_Transito");
        if (trPontaSeta == null)
        {
            GameObject objSeta = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objSeta.name = "Ponta_Seta_Transito";
            objSeta.transform.SetParent(transform, false);
            Collider c = objSeta.GetComponent<Collider>();
            if (c != null)
            {
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }
            trPontaSeta = objSeta.transform;
        }

        float ladoSeta = AlturaPainelHolografico * 0.68f;
        trPontaSeta.localScale = new Vector3(ladoSeta, ladoSeta, EspessuraPainelHolografico + 0.01f);
        trPontaSeta.localRotation = Quaternion.Euler(0f, 0f, 45f);

        if (!string.IsNullOrEmpty(setaDirecao) && setaDirecao.Contains("⬅"))
        {
            trPontaSeta.localPosition = new Vector3(-(LarguraPainelHolografico * 0.5f), AlturaCentroPainel, 0f);
        }
        else if (!string.IsNullOrEmpty(setaDirecao) && setaDirecao.Contains("➡"))
        {
            trPontaSeta.localPosition = new Vector3((LarguraPainelHolografico * 0.5f), AlturaCentroPainel, 0f);
        }
        else
        {
            trPontaSeta.localPosition = new Vector3(0f, AlturaCentroPainel + (AlturaPainelHolografico * 0.5f), 0f);
        }
    }

    private void GarantirReferenciasTextMeshPro()
    {
        if (textoFrontalTMP == null)
        {
            Transform trFrente = transform.Find("TMP_Texto_Frontal");
            if (trFrente != null) textoFrontalTMP = trFrente.GetComponent<TMP_Text>();
        }
        if (textoTraseiroTMP == null)
        {
            Transform trVerso = transform.Find("TMP_Texto_Traseiro");
            if (trVerso != null) textoTraseiroTMP = trVerso.GetComponent<TMP_Text>();
        }

        if (textoFrontalTMP == null || textoTraseiroTMP == null)
        {
            TMP_Text[] textos = GetComponentsInChildren<TMP_Text>(true);
            if (textos != null && textos.Length > 0)
            {
                if (textoFrontalTMP == null) textoFrontalTMP = textos[0];
                if (textos.Length > 1 && textoTraseiroTMP == null) textoTraseiroTMP = textos[1];
            }
        }

        if (textoFrontalTMP == null)
        {
            GameObject txtFrenteObj = new GameObject("TMP_Texto_Frontal", typeof(RectTransform));
            txtFrenteObj.transform.SetParent(transform, false);
            textoFrontalTMP = txtFrenteObj.AddComponent<TextMeshPro>();
        }

        if (textoTraseiroTMP == null)
        {
            GameObject txtVersoObj = new GameObject("TMP_Texto_Traseiro", typeof(RectTransform));
            txtVersoObj.transform.SetParent(transform, false);
            textoTraseiroTMP = txtVersoObj.AddComponent<TextMeshPro>();
        }

        CentralizarRectTransformNoPainel(textoFrontalTMP, ehFaceTraseira: false);
        CentralizarRectTransformNoPainel(textoTraseiroTMP, ehFaceTraseira: true);
    }

    public static void CentralizarRectTransformNoPainel(TMP_Text tmp, bool ehFaceTraseira)
    {
        if (tmp == null) return;

        RectTransform rt = tmp.rectTransform;
        if (rt == null) return;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(LarguraRectTexto, AlturaRectTexto);
        rt.localScale = Vector3.one;
        rt.localRotation = ehFaceTraseira ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

        Vector3 posCentralizada = new Vector3(0f, AlturaCentroPainel, ehFaceTraseira ? OffsetZTexto : -OffsetZTexto);
        rt.localPosition = posCentralizada;
        rt.anchoredPosition3D = posCentralizada;
    }

    public void AtualizarVisualPlaca()
    {
        SincronizarGeometriaPlacaTransito();
        CentralizarRectTransformNoPainel(textoFrontalTMP, ehFaceTraseira: false);
        CentralizarRectTransformNoPainel(textoTraseiroTMP, ehFaceTraseira: true);

        string direcaoTxt = ObterTextoDirecaoTransito(setaDirecao);

        // Formato de Placa de Trânsito Direcional:
        // Linha 1: Seta + Direção em Amarelo Trânsito (#FFD700)
        // Linha 2: Tipo (Classe/Subclasse) + Nome em Branco Puro Grande (#FFFFFF)
        // Linha 3: Guia do que pegar (✔) e do que evitar (✖) naquele corredor
        string conteudoFormatado =
            $"<color=#FFD700><size=70%><b>{direcaoTxt} ({tipoHierarquia.ToUpperInvariant()})</b></size></color>\n" +
            $"<color=#FFFFFF><size=105%><b>{nomeClasseExibida}</b></size></color>\n" +
            $"<size=62%><color=#4ADE80>✔ {itensCertosCorredor}</color>  |  <color=#FCA5A5>✖ Evite: {itensErradosEvitar}</color></size>";

        AplicarTextoBrancoContornoPretoTMP(textoFrontalTMP, conteudoFormatado);
        AplicarTextoBrancoContornoPretoTMP(textoTraseiroTMP, conteudoFormatado);
        AplicarMateriaisPlacaTransito(corNeon);
    }

    private static TMP_FontAsset ObterFontePadraoTMP()
    {
        if (s_FontePadraoTMP != null) return s_FontePadraoTMP;

        if (TMP_Settings.defaultFontAsset != null)
        {
            s_FontePadraoTMP = TMP_Settings.defaultFontAsset;
            return s_FontePadraoTMP;
        }

        s_FontePadraoTMP = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF")
                           ?? Resources.Load<TMP_FontAsset>("LiberationSans SDF");
        return s_FontePadraoTMP;
    }

    private void AplicarTextoBrancoContornoPretoTMP(TMP_Text tmp, string texto)
    {
        if (tmp == null) return;

        TMP_FontAsset fonte = ObterFontePadraoTMP();
        if (tmp.font == null && fonte != null)
        {
            tmp.font = fonte;
        }

        tmp.text = texto;
        tmp.richText = true;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.58f;
        tmp.fontSizeMax = 1.42f;
        tmp.fontSize = 1.30f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.lineSpacing = -4f;
        tmp.margin = new Vector4(0.06f, 0.03f, 0.06f, 0.03f);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        tmp.color = Color.white;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        tmp.outlineWidth = 0.24f;

        if (tmp.fontSharedMaterial != null)
        {
            Material matTexto = new Material(tmp.fontSharedMaterial);
            if (matTexto.HasProperty("_FaceColor")) matTexto.SetColor("_FaceColor", Color.white);
            if (matTexto.HasProperty("_OutlineColor")) matTexto.SetColor("_OutlineColor", Color.black);
            if (matTexto.HasProperty("_OutlineWidth")) matTexto.SetFloat("_OutlineWidth", 0.24f);
            tmp.fontMaterial = matTexto;
        }

        MeshRenderer mr = tmp.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 10;
        }

        tmp.ForceMeshUpdate();
    }

    private void AplicarMateriaisPlacaTransito(Color corBase)
    {
        Shader shaderLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        if (shaderLit == null) return;

        // Moldura refletiva estilo Placa de Trânsito (Amarelo/Neon)
        Color corBordaTransito = Color.Lerp(corBase, new Color(1.0f, 0.85f, 0.10f, 1f), 0.35f);
        Color corEmissaoHDR = corBordaTransito * intensidadeEmissaoHDR;

        Material matMoldura = new Material(shaderLit);
        matMoldura.name = $"Mat_MolduraTransito_{nomeClasseExibida}";
        if (matMoldura.HasProperty("_BaseColor")) matMoldura.SetColor("_BaseColor", corBordaTransito);
        if (matMoldura.HasProperty("_Color")) matMoldura.SetColor("_Color", corBordaTransito);
        if (matMoldura.HasProperty("_EmissionColor"))
        {
            matMoldura.EnableKeyword("_EMISSION");
            matMoldura.SetColor("_EmissionColor", corEmissaoHDR);
        }

        if (rendererMolduraNeon != null) rendererMolduraNeon.sharedMaterial = matMoldura;

        Transform trInf = transform.Find("Moldura_Inferior_Transito");
        if (trInf != null && trInf.TryGetComponent<Renderer>(out Renderer rInf))
        {
            rInf.sharedMaterial = matMoldura;
        }

        Transform trSeta = transform.Find("Ponta_Seta_Transito");
        if (trSeta != null && trSeta.TryGetComponent<Renderer>(out Renderer rSeta))
        {
            rSeta.sharedMaterial = matMoldura;
        }

        // Poste cinza metálico de placa de rua
        Transform trPoste = transform.Find("Poste_Suporte");
        if (trPoste != null && trPoste.TryGetComponent<Renderer>(out Renderer rPoste))
        {
            Material matPoste = new Material(shaderLit);
            Color cinzaPoste = new Color(0.22f, 0.25f, 0.30f, 1f);
            if (matPoste.HasProperty("_BaseColor")) matPoste.SetColor("_BaseColor", cinzaPoste);
            if (matPoste.HasProperty("_Color")) matPoste.SetColor("_Color", cinzaPoste);
            rPoste.sharedMaterial = matPoste;
        }

        // Fundo da Placa de Trânsito: Verde Rodoviário Escuro ou Azul Rodoviário Escuro (alto contraste para letras brancas/amarelas)
        if (rendererPainelHolografico != null)
        {
            Material matPainel = new Material(shaderLit);
            matPainel.name = $"Mat_FundoPlacaTransito_{nomeClasseExibida}";
            Color corFundoPlaca = Color.Lerp(new Color(0.01f, 0.03f, 0.05f, 1f), corBase, 0.18f);
            corFundoPlaca.a = 1.0f;
            if (matPainel.HasProperty("_BaseColor")) matPainel.SetColor("_BaseColor", corFundoPlaca);
            if (matPainel.HasProperty("_Color")) matPainel.SetColor("_Color", corFundoPlaca);
            if (matPainel.HasProperty("_Smoothness")) matPainel.SetFloat("_Smoothness", 0.0f);
            if (matPainel.HasProperty("_SpecularHighlights")) matPainel.SetFloat("_SpecularHighlights", 0.0f);
            if (matPainel.HasProperty("_EnvironmentReflections")) matPainel.SetFloat("_EnvironmentReflections", 0.0f);
            matPainel.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            matPainel.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            rendererPainelHolografico.sharedMaterial = matPainel;
        }
    }
}

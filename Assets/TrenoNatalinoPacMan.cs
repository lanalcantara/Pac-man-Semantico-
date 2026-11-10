using System;
using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (VirtOnto - CIn-UFPE) - Trenó Natalino com Renas do Pac-Man
/// ============================================================================
/// Gerencia a representação natalina especial do jogador:
/// 1. Acoplamento dinâmico ao avatar do Pac-Man (Visual_PacMan).
/// 2. Renderização de trenó festivo com patins dourados e renas dianteiras.
/// 3. Suporte automático ao modelo 3D (FBX/OBJ) baixado pelo usuário (slot de modelo customizado).
/// 4. Ativação contextual vinculada ao tema Especial de Natal.
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class TrenoNatalinoPacMan : MonoBehaviour
{
    public static TrenoNatalinoPacMan Instancia { get; private set; }

    [Header("Configurações do Trenó")]
    [Tooltip("Modelo 3D customizado fornecido pelo usuário (FBX/Prefab/OBJ).")]
    public GameObject modeloTrenoCustomizado;

    [Tooltip("Indica se o modo trenó de natal está ativo.")]
    public bool trenoAtivo = false;

    // Objeto container do trenó acoplado
    private GameObject m_ContainerTreno;
    private Transform m_VisualPacMan;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(this);
            return;
        }
        Instancia = this;
    }

    private void Start()
    {
        LocalizarPacMan();
    }

    private void Update()
    {
        // Se o Pac-Man visual for recriado dinamicamente, reancora com segurança
        if (trenoAtivo && (m_VisualPacMan == null || m_ContainerTreno == null))
        {
            LocalizarPacMan();
            if (m_VisualPacMan != null && m_ContainerTreno == null)
            {
                ConstruirOuCarregarTreno();
            }
        }
    }

    private void LocalizarPacMan()
    {
        if (m_VisualPacMan == null)
        {
            GameObject v = GameObject.Find("Visual_PacMan");
            if (v != null) m_VisualPacMan = v.transform;
            else
            {
                var xr = FindFirstObjectByType<ControladorXRJogador>();
                if (xr != null) m_VisualPacMan = xr.transform;
            }
        }
    }

    /// <summary>
    /// Liga ou desliga o trenó de Natal no Pac-Man.
    /// </summary>
    public void DefinirAtivo(bool ativo)
    {
        trenoAtivo = ativo;
        LocalizarPacMan();

        if (ativo)
        {
            if (m_ContainerTreno == null)
            {
                ConstruirOuCarregarTreno();
            }
            if (m_ContainerTreno != null)
            {
                m_ContainerTreno.SetActive(true);
            }
        }
        else
        {
            if (m_ContainerTreno != null)
            {
                m_ContainerTreno.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Instancia o modelo fornecido pelo usuário ou gera o trenó natalino procedural.
    /// </summary>
    private void ConstruirOuCarregarTreno()
    {
        if (m_VisualPacMan == null) return;

        // Se já existir um container anterior, remove
        if (m_ContainerTreno != null)
        {
            if (Application.isPlaying) Destroy(m_ContainerTreno);
            else DestroyImmediate(m_ContainerTreno);
            m_ContainerTreno = null;
        }

        m_ContainerTreno = new GameObject("Anchor_TrenoNatalino");
        m_ContainerTreno.transform.SetParent(m_VisualPacMan, false);
        m_ContainerTreno.transform.localPosition = Vector3.zero;
        m_ContainerTreno.transform.localRotation = Quaternion.identity;

        // 1. Tenta usar modelo customizado do usuário
        if (modeloTrenoCustomizado != null)
        {
            GameObject inst = Instantiate(modeloTrenoCustomizado, m_ContainerTreno.transform, false);
            inst.name = "Modelo_Treno_Usuario";
            RemoverColisoresRecursivo(inst);
            Debug.Log("<color=#FFD700><b>[Natal]</b></color> Modelo 3D customizado de trenó acoplado ao Pac-Man!");
            return;
        }

        // 2. Tenta carregar automaticamente de Resources ou busca dinâmica
        GameObject prefabAuto = Resources.Load<GameObject>("TrenoNatalino") ?? Resources.Load<GameObject>("SantaSleigh");
        if (prefabAuto != null)
        {
            GameObject inst = Instantiate(prefabAuto, m_ContainerTreno.transform, false);
            inst.name = "Modelo_Treno_Resources";
            RemoverColisoresRecursivo(inst);
            return;
        }

        // 3. Monta o Trenó Procedural com Renas enquanto o usuário não insere o arquivo FBX
        ConstruirTrenoProcedural(m_ContainerTreno.transform);
    }

    /// <summary>
    /// Constrói um belo trenó natalino estilizado com patins dourados, caçamba vermelha e renas frontais.
    /// </summary>
    private void ConstruirTrenoProcedural(Transform root)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");

        // Materiais
        Material matVermelho = new Material(shader) { name = "Mat_Treno_Vermelho" };
        if (matVermelho.HasProperty("_BaseColor")) matVermelho.SetColor("_BaseColor", new Color(0.85f, 0.08f, 0.12f));
        if (matVermelho.HasProperty("_Metallic")) matVermelho.SetFloat("_Metallic", 0.35f);
        if (matVermelho.HasProperty("_Smoothness")) matVermelho.SetFloat("_Smoothness", 0.65f);

        Material matDourado = new Material(shader) { name = "Mat_Patins_Dourados" };
        if (matDourado.HasProperty("_BaseColor")) matDourado.SetColor("_BaseColor", new Color(1.0f, 0.82f, 0.15f));
        if (matDourado.HasProperty("_Metallic")) matDourado.SetFloat("_Metallic", 0.85f);
        if (matDourado.HasProperty("_Smoothness")) matDourado.SetFloat("_Smoothness", 0.80f);

        Material matRenaMarrom = new Material(shader) { name = "Mat_Rena_Marrom" };
        if (matRenaMarrom.HasProperty("_BaseColor")) matRenaMarrom.SetColor("_BaseColor", new Color(0.48f, 0.28f, 0.16f));

        Material matNarizVermelho = new Material(shader) { name = "Mat_Rudolph_Nariz" };
        if (matNarizVermelho.HasProperty("_BaseColor")) matNarizVermelho.SetColor("_BaseColor", new Color(1.0f, 0.05f, 0.05f));
        if (matNarizVermelho.HasProperty("_EmissionColor"))
        {
            matNarizVermelho.EnableKeyword("_EMISSION");
            matNarizVermelho.SetColor("_EmissionColor", new Color(1.0f, 0.1f, 0.1f) * 1.5f);
        }

        // --- A. PATINS DO TRENÓ (Skis dourados abaixo do Pac-Man) ---
        CriarPatim(root, new Vector3(-0.42f, -0.28f, -0.05f), matDourado);
        CriarPatim(root, new Vector3(0.42f, -0.28f, -0.05f), matDourado);

        // --- B. CAÇAMBA DO TRENÓ (Atrás e nas laterais do Pac-Man) ---
        // Base inferior
        GameObject baseTreno = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseTreno.name = "Piso_Treno";
        baseTreno.transform.SetParent(root, false);
        baseTreno.transform.localPosition = new Vector3(0f, -0.20f, -0.10f);
        baseTreno.transform.localScale = new Vector3(0.82f, 0.08f, 0.95f);
        RemoverColisor(baseTreno);
        baseTreno.GetComponent<Renderer>().material = matVermelho;

        // Encosto Traseiro
        GameObject encosto = GameObject.CreatePrimitive(PrimitiveType.Cube);
        encosto.name = "Encosto_Traseiro";
        encosto.transform.SetParent(root, false);
        encosto.transform.localPosition = new Vector3(0f, 0.02f, -0.55f);
        encosto.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
        encosto.transform.localScale = new Vector3(0.82f, 0.42f, 0.08f);
        RemoverColisor(encosto);
        encosto.GetComponent<Renderer>().material = matVermelho;

        // Laterais curvadas do trenó
        CriarAbaLateral(root, new Vector3(-0.42f, -0.02f, -0.05f), matVermelho);
        CriarAbaLateral(root, new Vector3(0.42f, -0.02f, -0.05f), matVermelho);

        // --- C. RENAS DIANTEIRAS (Puxando o trenó à frente) ---
        CriarRena(root, new Vector3(-0.35f, -0.08f, 0.95f), matRenaMarrom, matDourado, null);
        CriarRena(root, new Vector3(0.35f, -0.08f, 0.95f), matRenaMarrom, matDourado, matNarizVermelho); // Rudolph na direita

        // --- D. RÉDEAS (Linhas douradas conectando o trenó às renas) ---
        CriarRedea(root, new Vector3(-0.38f, -0.05f, 0.15f), new Vector3(-0.35f, 0.02f, 0.85f), matDourado);
        CriarRedea(root, new Vector3(0.38f, -0.05f, 0.15f), new Vector3(0.35f, 0.02f, 0.85f), matDourado);
    }

    private static void CriarPatim(Transform root, Vector3 posLocal, Material mat)
    {
        // Barra reta
        GameObject barra = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barra.name = "Patim_Barra";
        barra.transform.SetParent(root, false);
        barra.transform.localPosition = posLocal;
        barra.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        barra.transform.localScale = new Vector3(0.05f, 0.55f, 0.05f);
        RemoverColisor(barra);
        barra.GetComponent<Renderer>().material = mat;

        // Ponta frontal curvada para cima
        GameObject curva = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        curva.name = "Patim_PontaCurva";
        curva.transform.SetParent(root, false);
        curva.transform.localPosition = posLocal + new Vector3(0f, 0.08f, 0.55f);
        curva.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
        curva.transform.localScale = new Vector3(0.05f, 0.14f, 0.05f);
        RemoverColisor(curva);
        curva.GetComponent<Renderer>().material = mat;
    }

    private static void CriarAbaLateral(Transform root, Vector3 posLocal, Material mat)
    {
        GameObject lateral = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lateral.name = "Aba_Lateral";
        lateral.transform.SetParent(root, false);
        lateral.transform.localPosition = posLocal;
        lateral.transform.localScale = new Vector3(0.06f, 0.28f, 0.88f);
        RemoverColisor(lateral);
        lateral.GetComponent<Renderer>().material = mat;
    }

    private static void CriarRena(Transform root, Vector3 posLocal, Material matCorpo, Material matChifres, Material matNariz)
    {
        GameObject rootRena = new GameObject("Rena_Natalina");
        rootRena.transform.SetParent(root, false);
        rootRena.transform.localPosition = posLocal;

        // Corpo
        GameObject corpo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        corpo.name = "Corpo_Rena";
        corpo.transform.SetParent(rootRena.transform, false);
        corpo.transform.localPosition = Vector3.zero;
        corpo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        corpo.transform.localScale = new Vector3(0.20f, 0.22f, 0.20f);
        RemoverColisor(corpo);
        corpo.GetComponent<Renderer>().material = matCorpo;

        // Cabeça
        GameObject cabeca = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cabeca.name = "Cabeca_Rena";
        cabeca.transform.SetParent(rootRena.transform, false);
        cabeca.transform.localPosition = new Vector3(0f, 0.16f, 0.22f);
        cabeca.transform.localScale = new Vector3(0.16f, 0.18f, 0.18f);
        RemoverColisor(cabeca);
        cabeca.GetComponent<Renderer>().material = matCorpo;

        // Chifres dourados
        GameObject chifreEsq = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        chifreEsq.name = "Chifre_Esq";
        chifreEsq.transform.SetParent(cabeca.transform, false);
        chifreEsq.transform.localPosition = new Vector3(-0.06f, 0.14f, -0.02f);
        chifreEsq.transform.localRotation = Quaternion.Euler(20f, -25f, -25f);
        chifreEsq.transform.localScale = new Vector3(0.025f, 0.12f, 0.025f);
        RemoverColisor(chifreEsq);
        chifreEsq.GetComponent<Renderer>().material = matChifres;

        GameObject chifreDir = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        chifreDir.name = "Chifre_Dir";
        chifreDir.transform.SetParent(cabeca.transform, false);
        chifreDir.transform.localPosition = new Vector3(0.06f, 0.14f, -0.02f);
        chifreDir.transform.localRotation = Quaternion.Euler(20f, 25f, 25f);
        chifreDir.transform.localScale = new Vector3(0.025f, 0.12f, 0.025f);
        RemoverColisor(chifreDir);
        chifreDir.GetComponent<Renderer>().material = matChifres;

        // Nariz iluminado (Rudolph) se fornecido
        if (matNariz != null)
        {
            GameObject nariz = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nariz.name = "Nariz_Rudolph";
            nariz.transform.SetParent(cabeca.transform, false);
            nariz.transform.localPosition = new Vector3(0f, 0.02f, 0.10f);
            nariz.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
            RemoverColisor(nariz);
            nariz.GetComponent<Renderer>().material = matNariz;
        }
    }

    private static void CriarRedea(Transform root, Vector3 de, Vector3 para, Material mat)
    {
        GameObject redea = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        redea.name = "Redea_Linha";
        redea.transform.SetParent(root, false);
        Vector3 meio = (de + para) / 2f;
        redea.transform.localPosition = meio;
        redea.transform.up = (para - de).normalized;
        float dist = Vector3.Distance(de, para);
        redea.transform.localScale = new Vector3(0.015f, dist / 2f, 0.015f);
        RemoverColisor(redea);
        redea.GetComponent<Renderer>().material = mat;
    }

    private static void RemoverColisor(GameObject obj)
    {
        if (obj == null) return;
        Collider c = obj.GetComponent<Collider>();
        if (c != null)
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }
    }

    private static void RemoverColisoresRecursivo(GameObject obj)
    {
        if (obj == null) return;
        Collider[] cols = obj.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
            {
                if (Application.isPlaying) Destroy(cols[i]);
                else DestroyImmediate(cols[i]);
            }
        }
    }
}


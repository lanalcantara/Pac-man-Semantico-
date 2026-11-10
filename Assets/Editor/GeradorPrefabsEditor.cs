#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Realidade Virtual e Aumentada (RV/RA)
/// ============================================================================
/// Utilitário do Unity Editor para geração automática dos modelos 3D estilizados:
/// 1. Pac-Man 3D com mandíbulas animadas (Upper/Lower Jaw).
/// 2. Pílulas / Pac-Dots (Gemas Válidas com aura brilhante).
/// 3. Fantasma Blinky 3D (Obstáculos Inválidos com olhos móveis).
/// ============================================================================
/// </summary>
[InitializeOnLoad]
public static class GeradorPrefabsEditor
{
    private const string PastaPrefabs = "Assets/Prefabs";
    private const string PastaResourcesPrefabs = "Assets/Resources/Prefabs";

    private const string CaminhoGemaValida = PastaPrefabs + "/GemaValida.prefab";
    private const string CaminhoObstaculoInvalido = PastaPrefabs + "/ObstaculoInvalido.prefab";
    private const string CaminhoPacManJogador = PastaPrefabs + "/PacManJogador.prefab";
    private const string CaminhoPlacaSinalizacao = PastaPrefabs + "/PlacaSinalizacaoSemantica.prefab";

    private const string CaminhoGemaResources = PastaResourcesPrefabs + "/GemaValida.prefab";
    private const string CaminhoObstaculoResources = PastaResourcesPrefabs + "/ObstaculoInvalido.prefab";
    private const string CaminhoPlacaResources = PastaResourcesPrefabs + "/PlacaSinalizacaoSemantica.prefab";

    static GeradorPrefabsEditor()
    {
        EditorApplication.delayCall += VerificarECriarPrefabsAutomaticamente;
    }

    [MenuItem("Pac-Man Semântico/1. Configurar Rig XR e Pac-Man na Cena", false, 1)]
    public static void ConfigurarXROriginNaCena()
    {
        // 1. Limpa objetos antigos avulsos
        GameObject cameraAntiga = GameObject.Find("Main Camera");
        if (cameraAntiga != null && cameraAntiga.transform.parent == null)
        {
            Undo.DestroyObjectImmediate(cameraAntiga);
        }

        GameObject esferaAntiga = GameObject.Find("Sphere");
        if (esferaAntiga != null)
        {
            Undo.DestroyObjectImmediate(esferaAntiga);
        }

        // 2. Garante os Prefabs gerados
        CriarPrefabs(forcarRecriacao: true);

        // 3. Localiza ou cria o XR Origin
        GameObject xrOriginObj = GameObject.Find("XR Origin");
        if (xrOriginObj == null)
        {
            xrOriginObj = new GameObject("XR Origin");
            Undo.RegisterCreatedObjectUndo(xrOriginObj, "Criar XR Origin Pac-Man");
        }

        xrOriginObj.transform.position = new Vector3(0f, 0f, -6f);
        xrOriginObj.tag = "Player";

        CharacterController cc = xrOriginObj.GetComponent<CharacterController>();
        if (cc == null) cc = Undo.AddComponent<CharacterController>(xrOriginObj);
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.height = 1.8f;
        cc.radius = 0.35f;

        ControladorXRJogador controladorXR = xrOriginObj.GetComponent<ControladorXRJogador>();
        if (controladorXR == null) controladorXR = Undo.AddComponent<ControladorXRJogador>(xrOriginObj);
        controladorXR.characterController = cc;

        // 4. Camera Offset
        Transform cameraOffset = xrOriginObj.transform.Find("Camera Offset");
        if (cameraOffset == null)
        {
            GameObject offsetObj = new GameObject("Camera Offset");
            offsetObj.transform.SetParent(xrOriginObj.transform, false);
            offsetObj.transform.localPosition = new Vector3(0f, 1.36f, 0f);
            cameraOffset = offsetObj.transform;
            Undo.RegisterCreatedObjectUndo(offsetObj, "Criar Camera Offset");
        }
        controladorXR.cameraOffset = cameraOffset;

        // 5. Main Camera XR
        Transform mainCameraXR = cameraOffset.Find("Main Camera");
        Camera cam;
        if (mainCameraXR == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.transform.SetParent(cameraOffset, false);
            camObj.transform.localPosition = Vector3.zero;
            camObj.tag = "MainCamera";

            cam = camObj.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1000f;
            cam.fieldOfView = 60f;

            camObj.AddComponent<AudioListener>();

            SphereCollider sc = camObj.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.2f;

            mainCameraXR = camObj.transform;
            Undo.RegisterCreatedObjectUndo(camObj, "Criar Main Camera XR");
        }
        else
        {
            cam = mainCameraXR.GetComponent<Camera>();
        }
        controladorXR.cameraPrincipalXR = cam;

        // 6. Modelo 3D Visual do Pac-Man anexado à visão do jogador
        Transform visualPacMan = mainCameraXR.Find("Visual_PacMan");
        if (visualPacMan == null)
        {
            GameObject pacManPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoPacManJogador);
            if (pacManPrefab != null)
            {
                GameObject pacObj = (GameObject)PrefabUtility.InstantiatePrefab(pacManPrefab, mainCameraXR);
                pacObj.name = "Visual_PacMan";
                pacObj.transform.localPosition = new Vector3(0f, -0.35f, 0.65f); // Posicionado à frente no campo de visão
                pacObj.transform.localScale = Vector3.one * 0.45f;
                Undo.RegisterCreatedObjectUndo(pacObj, "Instanciar Pac-Man Visual");
            }
        }

        // 7. Controladores Esquerdo e Direito
        Transform maoEsquerda = cameraOffset.Find("Left Hand Controller");
        if (maoEsquerda == null)
        {
            GameObject esqObj = new GameObject("Left Hand Controller");
            esqObj.transform.SetParent(cameraOffset, false);
            esqObj.transform.localPosition = new Vector3(-0.25f, -0.1f, 0.4f);
            esqObj.tag = "GameController";

            SphereCollider colEsq = esqObj.AddComponent<SphereCollider>();
            colEsq.isTrigger = true;
            colEsq.radius = 0.1f;

            InteracaoEspacialXR interacaoEsq = esqObj.AddComponent<InteracaoEspacialXR>();
            interacaoEsq.tipoMao = InteracaoEspacialXR.MaoXR.MaoEsquerda;

            maoEsquerda = esqObj.transform;
            Undo.RegisterCreatedObjectUndo(esqObj, "Criar Left Hand Controller");
        }
        controladorXR.controladorEsquerdo = maoEsquerda;

        Transform maoDireita = cameraOffset.Find("Right Hand Controller");
        if (maoDireita == null)
        {
            GameObject dirObj = new GameObject("Right Hand Controller");
            dirObj.transform.SetParent(cameraOffset, false);
            dirObj.transform.localPosition = new Vector3(0.25f, -0.1f, 0.4f);
            dirObj.tag = "GameController";

            SphereCollider colDir = dirObj.AddComponent<SphereCollider>();
            colDir.isTrigger = true;
            colDir.radius = 0.1f;

            InteracaoEspacialXR interacaoDir = dirObj.AddComponent<InteracaoEspacialXR>();
            interacaoDir.tipoMao = InteracaoEspacialXR.MaoXR.MaoDireita;

            maoDireita = dirObj.transform;
            Undo.RegisterCreatedObjectUndo(dirObj, "Criar Right Hand Controller");
        }
        controladorXR.controladorDireito = maoDireita;

        // 7.1 HUD Holográfico Espacial de Pulso (Relógio Biomédico Antienjoo)
        HUDHolograficoXR hudXR = Object.FindFirstObjectByType<HUDHolograficoXR>();
        if (hudXR == null)
        {
            GameObject hudObj = new GameObject("HUD_Holografico_Pulso");
            hudObj.transform.SetParent(maoEsquerda, false);
            hudXR = hudObj.AddComponent<HUDHolograficoXR>();
            hudXR.cameraXR = cam.transform;
            hudXR.maoEsquerda = maoEsquerda;
            hudXR.maoDireita = maoDireita;
            hudXR.modoAncoragem = HUDHolograficoXR.ModoAncoragemHUD.PulsoEsquerdo;
            hudXR.ConfigurarAncoragemInicial();
            Undo.RegisterCreatedObjectUndo(hudObj, "Criar HUD Holografico Pulso");
        }

        // 8. Gerenciador de Efeitos Juice (Partículas e Vibrações)
        GameObject juiceObj = GameObject.Find("GerenciadorEfeitosJuice");
        if (juiceObj == null)
        {
            juiceObj = new GameObject("GerenciadorEfeitosJuice");
            juiceObj.AddComponent<GerenciadorEfeitosJuice>();
            Undo.RegisterCreatedObjectUndo(juiceObj, "Criar Gerenciador Juice");
        }

        // 9. Gerenciador de Cenário Semântico
        GameObject cenarioObj = GameObject.Find("GerenciadorCenarioSemantico");
        if (cenarioObj == null)
        {
            cenarioObj = new GameObject("GerenciadorCenarioSemantico");
            Undo.RegisterCreatedObjectUndo(cenarioObj, "Criar Gerenciador Cenario");
        }
        GerenciadorCenarioSemantico gerCenario = cenarioObj.GetComponent<GerenciadorCenarioSemantico>();
        if (gerCenario == null)
        {
            gerCenario = Undo.AddComponent<GerenciadorCenarioSemantico>(cenarioObj);
        }
        // 10. Piso da Arena (Plane)
        GameObject plane = GameObject.Find("Plane");
        if (plane != null)
        {
            plane.transform.position = Vector3.zero;
            plane.transform.localScale = new Vector3(4f, 1f, 4f);
            gerCenario.planoChao = plane.transform;
        }
        gerCenario.assentarPerfeitamenteNoChao = true;
        gerCenario.alturaGemas = 0f;
        gerCenario.alturaObstaculos = 0f;
        gerCenario.CarregarPrefabsAutomaticamente();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("<color=#00FF99><b>[Pac-Man Semântico]</b></color> Configuração do Rig XR, Personagem Pac-Man e Gerenciadores concluída com sucesso!");
    }

    [MenuItem("Pac-Man Semântico/2. Gerar Todos os Prefabs 3D (Pac-Man, Pílulas, Fantasmas)", false, 2)]
    public static void MenuGerarPrefabs()
    {
        CriarPrefabs(forcarRecriacao: true);
    }

    [MenuItem("Pac-Man Semântico/3. Gerar Arquivo JSON Exemplo (OWL Bridge)", false, 3)]
    public static void MenuGerarExemploJson()
    {
        CenarioSemanticoDTO cenario = BackendOntologiaBridge.GerarCenarioOntologiaPadrao(12, 6, 14f, 0.85f);
        cenario.nomeOntologia = "PacMan_ALCQ_Ontology";
        cenario.versaoAxiomas = "v2.0-Export";

        string json = BackendOntologiaBridge.ExportarParaJson(cenario, true);
        string caminhoArquivo = "Assets/OntologiaExemplo_DL.json";
        File.WriteAllText(caminhoArquivo, json);

        AssetDatabase.ImportAsset(caminhoArquivo);
        Debug.Log($"<color=#00DDFF><b>[Pac-Man Semântico]</b></color> Arquivo de exemplo JSON ontológico gerado em: {caminhoArquivo}");
    }

    [MenuItem("Pac-Man Semântico/4. Gerar Prefab Placa de Sinalização Semântica (Wayfinding TMP)", false, 4)]
    public static void MenuGerarPrefabPlacaSinalizacao()
    {
        GarantirPastasExistentes();
        CriarPrefabPlacaSinalizacao();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void VerificarECriarPrefabsAutomaticamente()
    {
        if (Application.isPlaying) return;

        GarantirPastasExistentes();
        // Sempre regenera o Prefab da Placa de Sinalização Semântica para garantir o RectTransform centralizado no painel
        CriarPrefabPlacaSinalizacao();

        if (!File.Exists(CaminhoGemaValida) || !File.Exists(CaminhoObstaculoInvalido) ||
            !File.Exists(CaminhoPacManJogador) || !File.Exists(CaminhoGemaResources) || !File.Exists(CaminhoObstaculoResources))
        {
            CriarPrefabs(forcarRecriacao: false);
        }

        // Atualiza todas as placas já presentes na cena ativa
        PlacaSinalizacaoSemantica[] placasCena = Object.FindObjectsByType<PlacaSinalizacaoSemantica>(FindObjectsSortMode.None);
        for (int i = 0; i < placasCena.Length; i++)
        {
            if (placasCena[i] != null)
            {
                placasCena[i].AtualizarVisualPlaca();
            }
        }
    }

    private static void CriarPrefabs(bool forcarRecriacao)
    {
        GarantirPastasExistentes();

        if (forcarRecriacao || !File.Exists(CaminhoGemaValida) || !File.Exists(CaminhoGemaResources))
        {
            CriarPrefabPilulaPacDot();
        }

        if (forcarRecriacao || !File.Exists(CaminhoObstaculoInvalido) || !File.Exists(CaminhoObstaculoResources))
        {
            CriarPrefabFantasmaBlinky();
        }

        if (forcarRecriacao || !File.Exists(CaminhoPacManJogador))
        {
            CriarPrefabPacManJogador();
        }

        if (forcarRecriacao || !File.Exists(CaminhoPlacaSinalizacao) || !File.Exists(CaminhoPlacaResources))
        {
            CriarPrefabPlacaSinalizacao();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void GarantirPastasExistentes()
    {
        if (!Directory.Exists(PastaPrefabs)) Directory.CreateDirectory(PastaPrefabs);
        if (!Directory.Exists(PastaResourcesPrefabs)) Directory.CreateDirectory(PastaResourcesPrefabs);
        if (!Directory.Exists("Assets/Materials")) Directory.CreateDirectory("Assets/Materials");
    }

    // =========================================================================
    // 1. MODELO 3D DO NODO DE DADOS (CUBO/PRISMA TRANSLÚCIDO OWL)
    // =========================================================================
    private static void CriarPrefabPilulaPacDot()
    {
        GameObject nodoRaiz = new GameObject("GemaValida");

        // Núcleo Cúbico/Prismático Translúcido (Nodo de Dados OWL)
        GameObject cuboPrisma = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cuboPrisma.name = "Nucleo_PacDot";
        cuboPrisma.transform.SetParent(nodoRaiz.transform, false);
        cuboPrisma.transform.localScale = new Vector3(0.36f, 0.48f, 0.36f);
        cuboPrisma.transform.localRotation = Quaternion.Euler(35f, 45f, 0f);
        Object.DestroyImmediate(cuboPrisma.GetComponent<Collider>());

        Color corCianoTranslucido = new Color(0.0f, 0.90f, 1.0f, 0.58f);
        Color emissaoCiano = new Color(0.0f, 0.70f, 0.95f);
        Material matNodo = CriarOuObterMaterial("Mat_PacDot_Glow", corCianoTranslucido, emissaoCiano, 0.92f);

        Renderer rend = cuboPrisma.GetComponent<Renderer>();
        if (rend != null && matNodo != null)
        {
            rend.sharedMaterial = matNodo;
        }

        // Anel / Halo cibernético de base
        GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        halo.name = "Halo_Aura";
        halo.transform.SetParent(nodoRaiz.transform, false);
        halo.transform.localScale = new Vector3(0.55f, 0.015f, 0.55f);
        Object.DestroyImmediate(halo.GetComponent<Collider>());
        Renderer rendHalo = halo.GetComponent<Renderer>();
        if (rendHalo != null && matNodo != null)
        {
            rendHalo.sharedMaterial = matNodo;
        }

        // Colisor Trigger
        SphereCollider col = nodoRaiz.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.45f;

        // Scripts Semânticos e de Animação
        nodoRaiz.AddComponent<RotacaoFlutuante>();
        GerenciadorInstancia ger = nodoRaiz.AddComponent<GerenciadorInstancia>();
        ger.ehValida = true;
        ger.valorPontuacao = 10;
        ger.classeOntologica = "AscarisLumbricoides";
        ger.expressaoDL = "Nematoda ⊓ ∃has_host.Human";
        ger.descricaoSemantica = "Nodo de Dados OWL. Ao interagir, instancia o indivíduo na ABox via WebSocket (8085).";

        PrefabUtility.SaveAsPrefabAsset(nodoRaiz, CaminhoGemaValida);
        PrefabUtility.SaveAsPrefabAsset(nodoRaiz, CaminhoGemaResources);
        Object.DestroyImmediate(nodoRaiz);

        Debug.Log($"<color=#00FF99><b>[Interface OWL]</b></color> Prefab de Nodo de Dados (Cubo/Prisma Translúcido) criado em: {CaminhoGemaValida}");
    }

    // =========================================================================
    // 2. MODELO 3D DO FANTASMA BLINKY (OBSTÁCULO INVÁLIDO)
    // =========================================================================
    private static void CriarPrefabFantasmaBlinky()
    {
        GameObject fantasmaRaiz = new GameObject("ObstaculoInvalido");

        Color corFantasma = new Color(1f, 0.12f, 0.2f);
        Color emissaoFantasma = new Color(0.85f, 0.05f, 0.1f);
        Material matCorpo = CriarOuObterMaterial("Mat_Fantasma_Blinky", corFantasma, emissaoFantasma, 0.9f);

        Color corOlhoBranco = Color.white;
        Material matOlho = CriarOuObterMaterial("Mat_Olho_Branco", corOlhoBranco, Color.clear, 0.8f);

        Color corPupilaAzul = new Color(0.05f, 0.2f, 0.95f);
        Material matPupila = CriarOuObterMaterial("Mat_Pupila_Azul", corPupilaAzul, corPupilaAzul * 0.4f, 0.95f);

        // 1. Cabeça (Cúpula Esférica Superior)
        GameObject cabeca = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cabeca.name = "Cabeca_Cupula";
        cabeca.transform.SetParent(fantasmaRaiz.transform, false);
        cabeca.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        cabeca.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
        Object.DestroyImmediate(cabeca.GetComponent<Collider>());
        cabeca.GetComponent<Renderer>().sharedMaterial = matCorpo;

        // 2. Tronco / Saia Cilíndrica
        GameObject corpo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        corpo.name = "Corpo_Saia";
        corpo.transform.SetParent(fantasmaRaiz.transform, false);
        corpo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        corpo.transform.localScale = new Vector3(0.85f, 0.4f, 0.85f);
        Object.DestroyImmediate(corpo.GetComponent<Collider>());
        corpo.GetComponent<Renderer>().sharedMaterial = matCorpo;

        // 3. Franjas da Saia (3 esferas na base)
        for (int i = 0; i < 3; i++)
        {
            GameObject franja = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            franja.name = $"Franja_{i + 1}";
            franja.transform.SetParent(fantasmaRaiz.transform, false);
            float offsetX = (i - 1) * 0.28f;
            franja.transform.localPosition = new Vector3(offsetX, -0.3f, 0f);
            franja.transform.localScale = new Vector3(0.3f, 0.25f, 0.3f);
            Object.DestroyImmediate(franja.GetComponent<Collider>());
            franja.GetComponent<Renderer>().sharedMaterial = matCorpo;
        }

        // 4. Olho Esquerdo
        GameObject olhoEsq = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        olhoEsq.name = "OlhoEsquerdo";
        olhoEsq.transform.SetParent(fantasmaRaiz.transform, false);
        olhoEsq.transform.localPosition = new Vector3(-0.18f, 0.3f, 0.35f);
        olhoEsq.transform.localScale = new Vector3(0.22f, 0.28f, 0.15f);
        Object.DestroyImmediate(olhoEsq.GetComponent<Collider>());
        olhoEsq.GetComponent<Renderer>().sharedMaterial = matOlho;

        GameObject pupilaEsq = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pupilaEsq.name = "PupilaEsquerda";
        pupilaEsq.transform.SetParent(olhoEsq.transform, false);
        pupilaEsq.transform.localPosition = new Vector3(0f, 0f, 0.04f);
        pupilaEsq.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        Object.DestroyImmediate(pupilaEsq.GetComponent<Collider>());
        pupilaEsq.GetComponent<Renderer>().sharedMaterial = matPupila;

        // 5. Olho Direito
        GameObject olhoDir = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        olhoDir.name = "OlhoDireito";
        olhoDir.transform.SetParent(fantasmaRaiz.transform, false);
        olhoDir.transform.localPosition = new Vector3(0.18f, 0.3f, 0.35f);
        olhoDir.transform.localScale = new Vector3(0.22f, 0.28f, 0.15f);
        Object.DestroyImmediate(olhoDir.GetComponent<Collider>());
        olhoDir.GetComponent<Renderer>().sharedMaterial = matOlho;

        GameObject pupilaDir = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pupilaDir.name = "PupilaDireita";
        pupilaDir.transform.SetParent(olhoDir.transform, false);
        pupilaDir.transform.localPosition = new Vector3(0f, 0f, 0.04f);
        pupilaDir.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        Object.DestroyImmediate(pupilaDir.GetComponent<Collider>());
        pupilaDir.GetComponent<Renderer>().sharedMaterial = matPupila;

        // Colisor Trigger
        CapsuleCollider col = fantasmaRaiz.AddComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.height = 1.4f;
        col.radius = 0.45f;
        col.center = new Vector3(0f, 0.15f, 0f);

        // Scripts de Animação e Ontologia
        AnimacaoFantasma anim = fantasmaRaiz.AddComponent<AnimacaoFantasma>();
        anim.pupilaEsquerda = pupilaEsq.transform;
        anim.pupilaDireita = pupilaDir.transform;

        GerenciadorInstancia ger = fantasmaRaiz.AddComponent<GerenciadorInstancia>();
        ger.ehValida = false;
        ger.valorPontuacao = -5;
        ger.classeOntologica = "Fantasma_Blinky";
        ger.expressaoDL = "Fantasma ⊑ Perigo ⊓ (A ⊓ ¬A ≡ ⊥)";
        ger.descricaoSemantica = "Fantasma Blinky. Violação ontológica detectada!";

        PrefabUtility.SaveAsPrefabAsset(fantasmaRaiz, CaminhoObstaculoInvalido);
        PrefabUtility.SaveAsPrefabAsset(fantasmaRaiz, CaminhoObstaculoResources);
        Object.DestroyImmediate(fantasmaRaiz);

        Debug.Log($"<color=#00FF99><b>[Pac-Man Semântico]</b></color> Prefab Fantasma Blinky 3D criado em: {CaminhoObstaculoInvalido}");
    }

    // =========================================================================
    // 3. MODELO 3D DO PERSONAGEM PAC-MAN JOGADOR (ESFERA CLÁSSICA PERFEITA)
    // =========================================================================
    private static void CriarPrefabPacManJogador()
    {
        GameObject pacManRaiz = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pacManRaiz.name = "PacManJogador";
        Object.DestroyImmediate(pacManRaiz.GetComponent<Collider>());

        Color corAmareloPacMan = new Color(1f, 0.92f, 0.05f);
        Color emissaoAmarelo = new Color(0.85f, 0.75f, 0.02f);
        Material matPacMan = CriarOuObterMaterial("Mat_PacMan_Amarelo", corAmareloPacMan, emissaoAmarelo, 0.95f);
        pacManRaiz.GetComponent<Renderer>().sharedMaterial = matPacMan;
        pacManRaiz.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);

        // Script de Animação e feedback sonoro Chomp
        pacManRaiz.AddComponent<AnimacaoBocaPacMan>();

        PrefabUtility.SaveAsPrefabAsset(pacManRaiz, CaminhoPacManJogador);
        Object.DestroyImmediate(pacManRaiz);

        Debug.Log($"<color=#00FF99><b>[Pac-Man Semântico]</b></color> Prefab Pac-Man Jogador (Esfera Clássica Perfeita) criado em: {CaminhoPacManJogador}");
    }

    // =========================================================================
    // 4. PREFAB PLACA DE SINALIZAÇÃO SEMÂNTICA (WAYFINDING HOLOGRÁFICO TMP)
    // =========================================================================
    private static void CriarPrefabPlacaSinalizacao()
    {
        GameObject placaRaiz = GerenciadorCenarioSemantico.CriarPlacaWayfindingProcedural(null, Vector3.zero, Quaternion.identity, "Prefab");
        placaRaiz.name = "PlacaSinalizacaoSemantica";

        Color corNeon = new Color(0.55f, 0.16f, 0.90f, 1f);
        Material matMoldura = CriarOuObterMaterial("Mat_Placa_Neon_HDR", corNeon, corNeon * 0.85f, 0.1f);
        Material matPainel = CriarOuObterMaterial("Mat_Placa_Painel_Escuro", new Color(0.01f, 0.015f, 0.03f, 1f), Color.clear, 0.0f);

        PlacaSinalizacaoSemantica comp = placaRaiz.GetComponent<PlacaSinalizacaoSemantica>();
        if (comp != null)
        {
            if (comp.renderersEmissivos != null)
            {
                foreach (var r in comp.renderersEmissivos)
                {
                    if (r != null) r.sharedMaterial = matMoldura;
                }
            }
            if (comp.rendererPainelFundo != null)
            {
                comp.rendererPainelFundo.sharedMaterial = matPainel;
            }
            comp.ConfigurarPlaca("Parasitas", "Superclasse", corNeon);
        }

        PrefabUtility.SaveAsPrefabAsset(placaRaiz, CaminhoPlacaSinalizacao);
        PrefabUtility.SaveAsPrefabAsset(placaRaiz, CaminhoPlacaResources);
        Object.DestroyImmediate(placaRaiz);

        Debug.Log($"<color=#00FF99><b>[Wayfinding OWL]</b></color> Prefab de Placa de Sinalização Semântica com TextMeshPro criado em: {CaminhoPlacaSinalizacao}");
    }

    private static Material CriarOuObterMaterial(string nome, Color corBase, Color corEmissao, float suavidade = 0.9f)
    {
        string caminhoMat = $"Assets/Materials/{nome}.mat";
        Material matExistente = AssetDatabase.LoadAssetAtPath<Material>(caminhoMat);
        if (matExistente != null)
        {
            matExistente.SetColor("_BaseColor", corBase);
            if (matExistente.HasProperty("_EmissionColor") && corEmissao != Color.clear)
            {
                matExistente.EnableKeyword("_EMISSION");
                matExistente.SetColor("_EmissionColor", corEmissao);
            }
            return matExistente;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material mat = new Material(shader) { name = nome };

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", corBase);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", corBase);

        if (corEmissao != Color.clear && mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", corEmissao);
        }

        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", suavidade);

        AssetDatabase.CreateAsset(mat, caminhoMat);
        return mat;
    }
}
#endif

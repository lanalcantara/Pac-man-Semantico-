#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ============================================================================
/// UTILITÁRIO DE EDITOR: GERADOR FÍSICO DO MATERIAL DARK GRID (WIREFRAME NEON)
/// ============================================================================
/// Cria em disco os arquivos PNG de grade (Base Preta + Máscara Emissiva de Linhas)
/// e os arquivos físicos de Material:
/// - Assets/Materials/DarkGridMaterial.mat
/// - Assets/Materials/DarkGridMaterial_Parasitas_Roxo.mat
/// - Assets/Materials/DarkGridMaterial_Hospedeiros_Azul.mat
/// - Assets/Materials/DarkGridMaterial_Chao.mat
/// Além disso, aplica automaticamente aos objetos da cena (Plane, Paredes e Câmeras com Skybox Preto).
/// ============================================================================
// Utilitário de geração de materiais DarkGrid (Desativado de auto-execução para
// preservar o visual diurno de grama verde e paredes metálicas solicitado pelo usuário).
// ============================================================================
public static class GeradorMaterialDarkGridEditor
{
    public const string CaminhoPastaMateriais = "Assets/Materials";
    public const string CaminhoPastaResources = "Assets/Resources";

    public const string CaminhoTexBaseRoxo = CaminhoPastaMateriais + "/Tex_DarkGrid_Base_PretoRoxo.png";
    public const string CaminhoTexBaseAzul = CaminhoPastaMateriais + "/Tex_DarkGrid_Base_PretoAzul.png";
    public const string CaminhoTexEmissao = CaminhoPastaMateriais + "/Tex_DarkGrid_EmissionMask.png";

    public const string CaminhoMatDarkGridPrincipal = CaminhoPastaMateriais + "/DarkGridMaterial.mat";
    public const string CaminhoMatParedeRoxo = CaminhoPastaMateriais + "/DarkGridMaterial_Parasitas_Roxo.mat";
    public const string CaminhoMatParedeAzul = CaminhoPastaMateriais + "/DarkGridMaterial_Hospedeiros_Azul.mat";
    public const string CaminhoMatChao = CaminhoPastaMateriais + "/DarkGridMaterial_Chao.mat";

    // Auto-inicialização removida para evitar que a cena volte para o tema escuro/preto no reload.

    [MenuItem("Pac-Man Semântico/5. Gerar e Aplicar Material DarkGrid Físico (Wireframe + Skybox Preto)", false, 5)]
    public static void MenuGerarEAplicarDarkGrid()
    {
        GarantirEAplicarDarkGridFisico(forcarRecriacao: true);
        Debug.Log("<color=#00FFCC><b>[DarkGrid Tech Art]</b></color> Materiais físicos DarkGridMaterial criados em Assets/Materials e aplicados ao chão, paredes e câmeras!");
    }

    public static void GarantirEAplicarDarkGridFisico(bool forcarRecriacao = false)
    {
        if (!Directory.Exists(CaminhoPastaMateriais)) Directory.CreateDirectory(CaminhoPastaMateriais);
        if (!Directory.Exists(CaminhoPastaResources)) Directory.CreateDirectory(CaminhoPastaResources);

        // 0. Limpa imediatamente qualquer Semantic_HUD_Canvas ou Texto_AxiomasABox_TMP corrompido salvo na cena
        SemanticHUDManager.RemoverCanvasArcadeLegado();
        GameObject hudAntigo = GameObject.Find("Semantic_HUD_Canvas");
        if (hudAntigo != null)
        {
            Object.DestroyImmediate(hudAntigo);
        }
        SemanticHUDManager.GarantirHUDAtivoNaCena();

        // 1. Gera e salva as Texturas PNG de Grade em disco com Refresh síncrono garantido
        SalvarTexturasDarkGridPNG();

        Texture2D texRoxo = AssetDatabase.LoadAssetAtPath<Texture2D>(CaminhoTexBaseRoxo);
        Texture2D texAzul = AssetDatabase.LoadAssetAtPath<Texture2D>(CaminhoTexBaseAzul);
        Texture2D texMask = AssetDatabase.LoadAssetAtPath<Texture2D>(CaminhoTexEmissao);

        // 2. Cria/Atualiza os Materiais físicos (.mat) no projeto (emissão moderada para linhas nítidas sem estourar)
        Color roxoNeon = new Color(0.68f, 0.20f, 1.0f, 1f);
        Color azulNeon = new Color(0.08f, 0.45f, 1.0f, 1f);
        Color azulChao = new Color(0.05f, 0.32f, 1.0f, 1f);

        Material matParedeRoxo = CriarOuAtualizarMaterialDarkGrid(
            CaminhoMatParedeRoxo,
            "DarkGridMaterial_Parasitas_Roxo",
            texRoxo,
            texMask,
            roxoNeon * 0.55f,
            new Vector2(2f, 1f));

        Material matParedeAzul = CriarOuAtualizarMaterialDarkGrid(
            CaminhoMatParedeAzul,
            "DarkGridMaterial_Hospedeiros_Azul",
            texAzul,
            texMask,
            azulNeon * 0.55f,
            new Vector2(2f, 1f));

        Material matDarkGridPrincipal = CriarOuAtualizarMaterialDarkGrid(
            CaminhoMatDarkGridPrincipal,
            "DarkGridMaterial",
            texAzul,
            texMask,
            azulNeon * 0.45f,
            new Vector2(2f, 1f));

        CriarOuAtualizarMaterialDarkGrid(
            CaminhoPastaMateriais + "/DarkGrid.mat",
            "DarkGrid",
            texAzul,
            texMask,
            azulNeon * 0.45f,
            new Vector2(2f, 1f));

        Material matChao = CriarOuAtualizarMaterialDarkGrid(
            CaminhoMatChao,
            "DarkGridMaterial_Chao",
            texAzul,
            texMask,
            azulChao * 0.45f,
            new Vector2(28f, 28f));

        // Atualiza também o material legado Mat_Parede_Labirinto_AzulNeon.mat
        CriarOuAtualizarMaterialDarkGrid(
            CaminhoPastaMateriais + "/Mat_Parede_Labirinto_AzulNeon.mat",
            "Mat_Parede_Labirinto_AzulNeon",
            texAzul,
            texMask,
            azulNeon * 0.55f,
            new Vector2(2f, 1f));

        // Copia para Resources para carregamento garantido em runtime
        CriarOuAtualizarMaterialDarkGrid(CaminhoPastaResources + "/DarkGridMaterial.mat", "DarkGridMaterial", texAzul, texMask, azulNeon * 0.55f, new Vector2(2f, 1f));
        CriarOuAtualizarMaterialDarkGrid(CaminhoPastaResources + "/DarkGridMaterial_Parasitas_Roxo.mat", "DarkGridMaterial_Parasitas_Roxo", texRoxo, texMask, roxoNeon * 0.55f, new Vector2(2f, 1f));
        CriarOuAtualizarMaterialDarkGrid(CaminhoPastaResources + "/DarkGridMaterial_Hospedeiros_Azul.mat", "DarkGridMaterial_Hospedeiros_Azul", texAzul, texMask, azulNeon * 0.55f, new Vector2(2f, 1f));
        CriarOuAtualizarMaterialDarkGrid(CaminhoPastaResources + "/DarkGridMaterial_Chao.mat", "DarkGridMaterial_Chao", texAzul, texMask, azulChao * 0.45f, new Vector2(28f, 28f));

        // 3. Garante Skybox Preto e Câmeras com Fundo Sólido Preto na cena ativa
        ForcarSkyboxECameraPretosNaCena();

        // 4. Atribui ao GerenciadorCenarioSemantico e aplica ao Plane e Paredes na cena aberta
        GerenciadorCenarioSemantico ger = Object.FindFirstObjectByType<GerenciadorCenarioSemantico>();
        if (ger != null)
        {
            ger.materialParedes = matDarkGridPrincipal;
            ger.alturaParedes = 1.15f;
            ger.LocalizarPlanoChao();
            if (ger.planoChao != null)
            {
                Renderer rendChao = ger.planoChao.GetComponent<Renderer>();
                if (rendChao != null) rendChao.sharedMaterial = matChao;
            }
            ger.ConstruirLabirinto3D();
            EditorUtility.SetDirty(ger);
        }
        else
        {
            GameObject planeObj = GameObject.Find("Plane");
            if (planeObj != null && planeObj.TryGetComponent<Renderer>(out Renderer rend))
            {
                rend.sharedMaterial = matChao;
            }
        }

        AssetDatabase.SaveAssets();
    }

    public static void ForcarSkyboxECameraPretosNaCena()
    {
        // Desativa completamente o Skybox e zera a luz ambiente na cena
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientSkyColor = Color.black;
        RenderSettings.ambientEquatorColor = Color.black;
        RenderSettings.ambientGroundColor = Color.black;
        RenderSettings.subtractiveShadowColor = Color.black;

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera cam in cameras)
        {
            if (cam == null) continue;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            UniversalAdditionalCameraData urpCamData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (urpCamData == null)
            {
                urpCamData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            urpCamData.renderPostProcessing = true;

            // Garante via SerializedObject que m_BackGroundColor e m_ClearFlags fiquem salvos como SolidColor Preto no URP
            SerializedObject soCam = new SerializedObject(cam);
            SerializedProperty propClearFlags = soCam.FindProperty("m_ClearFlags");
            if (propClearFlags != null) propClearFlags.intValue = 2; // 2 = SolidColor
            SerializedProperty propBg = soCam.FindProperty("m_BackGroundColor");
            if (propBg != null) propBg.colorValue = Color.black;
            soCam.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(cam);
        }
    }

    private static void SalvarTexturasDarkGridPNG()
    {
        int tam = 256;
        int espessura = 6;
        int subdivisoes = 2;
        int passo = tam / subdivisoes;
        int meia = espessura / 2;

        Color fundoPreto = new Color(0.01f, 0.01f, 0.018f, 1f);
        Color linhaRoxa = new Color(0.65f, 0.20f, 1.0f, 1f);
        Color linhaAzul = new Color(0.05f, 0.38f, 1.0f, 1f);

        Texture2D texRoxo = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        Texture2D texAzul = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        Texture2D texMask = new Texture2D(tam, tam, TextureFormat.RGBA32, false);

        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                int modX = x % passo;
                int modY = y % passo;

                bool bordaExterna = (x < meia || x >= tam - meia || y < meia || y >= tam - meia);
                bool linhaInterna = (modX < meia || modX >= passo - meia || modY < meia || modY >= passo - meia);

                if (bordaExterna)
                {
                    texRoxo.SetPixel(x, y, linhaRoxa);
                    texAzul.SetPixel(x, y, linhaAzul);
                    texMask.SetPixel(x, y, Color.white);
                }
                else if (linhaInterna)
                {
                    texRoxo.SetPixel(x, y, Color.Lerp(fundoPreto, linhaRoxa, 0.80f));
                    texAzul.SetPixel(x, y, Color.Lerp(fundoPreto, linhaAzul, 0.80f));
                    texMask.SetPixel(x, y, new Color(0.75f, 0.75f, 0.75f, 1f));
                }
                else
                {
                    texRoxo.SetPixel(x, y, fundoPreto);
                    texAzul.SetPixel(x, y, fundoPreto);
                    texMask.SetPixel(x, y, Color.black);
                }
            }
        }

        texRoxo.Apply();
        texAzul.Apply();
        texMask.Apply();

        File.WriteAllBytes(CaminhoTexBaseRoxo, texRoxo.EncodeToPNG());
        File.WriteAllBytes(CaminhoTexBaseAzul, texAzul.EncodeToPNG());
        File.WriteAllBytes(CaminhoTexEmissao, texMask.EncodeToPNG());

        Object.DestroyImmediate(texRoxo);
        Object.DestroyImmediate(texAzul);
        Object.DestroyImmediate(texMask);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        ConfigurarImporterTexturaGrid(CaminhoTexBaseRoxo);
        ConfigurarImporterTexturaGrid(CaminhoTexBaseAzul);
        ConfigurarImporterTexturaGrid(CaminhoTexEmissao);
    }

    private static void ConfigurarImporterTexturaGrid(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 16;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
    }

    private static Material CriarOuAtualizarMaterialDarkGrid(
        string assetPath,
        string matName,
        Texture2D texBase,
        Texture2D texMask,
        Color corEmissaoHDR,
        Vector2 tiling)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        bool criarNovo = (mat == null);

        if (criarNovo)
        {
            mat = new Material(shader);
            mat.name = matName;
        }
        else
        {
            mat.shader = shader;
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", texBase);
            mat.SetTextureScale("_BaseMap", tiling);
        }
        mat.mainTexture = texBase;
        mat.mainTextureScale = tiling;

        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.0f);
        if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0.0f);
        if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 0.0f);
        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");

        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (mat.HasProperty("_EmissionMap"))
        {
            mat.SetTexture("_EmissionMap", texMask);
            mat.SetTextureScale("_EmissionMap", tiling);
        }
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", corEmissaoHDR);
        }

        if (criarNovo)
        {
            AssetDatabase.CreateAsset(mat, assetPath);
        }
        else
        {
            EditorUtility.SetDirty(mat);
        }

        return mat;
    }
}
#endif


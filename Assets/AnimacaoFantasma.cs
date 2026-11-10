using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Animações e Feedback Visual (Juice)
/// ============================================================================
/// Controla o ciclo de vida visual dos Fantasmas (Obstáculos Semânticos / Reasoner Agents):
/// 1. Animação de Nascimento / Materialização (escala 0 -> 1, pulso de emissão, subida do chão).
/// 2. Assentamento preciso no chão do Plane sem flutuação indesejada.
/// 3. Orientação dinâmica dos olhos e pupilas em direção ao Pac-Man (jogador).
/// 4. Reação de susto/vibração quando ocorre colisão ou infração ontológica.
/// 5. Sequência de derrota por Power Pellet e retorno dos olhos à gaiola central.
/// ============================================================================
/// </summary>
public class AnimacaoFantasma : MonoBehaviour
{
    [Header("Flutuação e Movimento")]
    [Tooltip("Habilita flutuação vertical (padrão desabilitado para manter a base assentada no Plane).")]
    public bool permitirFlutuacao = false;

    [Tooltip("Distância de oscilação vertical.")]
    public float amplitudeFlutuacao = 0.08f;

    [Tooltip("Velocidade da oscilação vertical.")]
    public float frequenciaFlutuacao = 2.8f;

    [Tooltip("Ângulo de balanço lateral (roll).")]
    public float inclinacaoLateral = 0f;

    [Tooltip("Garante que a oscilação não penetre o plano do chão.")]
    public bool oscilarApenasParaCima = true;

    [Header("Olhos e Pupilas")]
    [Tooltip("Transform da pupila esquerda.")]
    public Transform pupilaEsquerda;

    [Tooltip("Transform da pupila direita.")]
    public Transform pupilaDireita;

    [Tooltip("Deslocamento máximo das pupilas ao olhar para o alvo.")]
    public float deslocamentoPupila = 0.035f;

    [Header("Alvo de Rastreamento")]
    [Tooltip("Transform do Pac-Man / Jogador.")]
    public Transform alvoJogador;

    [Header("Animação de Nascimento / Materialização")]
    [Tooltip("Duração da animação de materialização na gaiola central.")]
    public float duracaoNascimento = 1.2f;

    // Estado interno
    private Vector3 m_PosicaoInicial;
    private float m_DesvioFase;
    private float m_TimerSusto = 0f;
    private bool m_BaseDefinida = false;

    // Materialização / Nascimento
    private bool m_EstaNascendo = false;
    private float m_TimerNascimento = 0f;
    private Vector3 m_EscalaAlvoCorpo = new Vector3(0.80f, 0.80f, 0.80f);
    private Action m_CallbackNascimentoConcluido;
    private Renderer[] m_RenderersFantasma;
    private Color m_CorEmissaoBase = Color.clear;
    private bool m_CorEmissaoCapturada = false;
    private Color m_CorOriginalBase = Color.clear;
    private Color m_CorOriginalEmissao = Color.clear;
    private bool m_CoresOriginaisSalvas = false;

    public bool EstaNascendo => m_EstaNascendo;

    private void Awake()
    {
        if (transform.localScale != Vector3.zero)
        {
            m_EscalaAlvoCorpo = transform.localScale;
        }
    }

    private void Start()
    {
        if (!m_BaseDefinida)
        {
            m_PosicaoInicial = transform.localPosition;
            m_BaseDefinida = true;
        }
        m_DesvioFase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

        LocalizarAlvoEOlhos();
        CapturarRenderersEEmissao();
    }

    /// <summary>
    /// Sincroniza a posição base com a posição calculada para assentar no chão.
    /// </summary>
    public void AtualizarPosicaoBase(Vector3 novaPosicaoLocal)
    {
        m_PosicaoInicial = novaPosicaoLocal;
        m_BaseDefinida = true;
    }

    private void Update()
    {
        if (m_EstaNascendo)
        {
            ProcessarAnimacaoNascimento();
            return;
        }

        ProcessarFlutuacao();
        ProcessarOlharPupilas();
        ProcessarReacaoSusto();
    }

    // =========================================================================
    // 1. ANIMAÇÃO DE NASCIMENTO / MATERIALIZAÇÃO (SPAWN ANIMATION)
    // =========================================================================

    /// <summary>
    /// Inicia a animação de nascimento: o fantasma materializa do chão escalando de 0 a 1
    /// com pulso emissivo radiante e ascensão suave.
    /// </summary>
    public void ExecutarAnimacaoNascimento(float duracao = 1.2f, Action onComplete = null)
    {
        m_EstaNascendo = true;
        duracaoNascimento = Mathf.Max(0.1f, duracao);
        m_TimerNascimento = duracaoNascimento;
        m_CallbackNascimentoConcluido = onComplete;

        if (m_EscalaAlvoCorpo == Vector3.zero || m_EscalaAlvoCorpo.sqrMagnitude < 0.01f)
        {
            m_EscalaAlvoCorpo = new Vector3(0.80f, 0.80f, 0.80f);
        }

        transform.localScale = Vector3.zero;

        // Desabilita temporariamente o NavMeshAgent durante a materialização
        var nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav != null)
        {
            nav.enabled = false;
        }

        CapturarRenderersEEmissao();
    }

    private void ProcessarAnimacaoNascimento()
    {
        m_TimerNascimento -= Time.deltaTime;
        float progresso = 1f - Mathf.Clamp01(m_TimerNascimento / duracaoNascimento);

        // Curva suave com leve overshoot no final (estilo bounce suave)
        float c4 = (2f * Mathf.PI) / 3f;
        float escalaFator;
        if (progresso <= 0f) escalaFator = 0f;
        else if (progresso >= 1f) escalaFator = 1f;
        else
        {
            // Smooth ease out cubic
            float tInv = 1f - progresso;
            escalaFator = 1f - (tInv * tInv * tInv);
        }

        transform.localScale = m_EscalaAlvoCorpo * escalaFator;

        // Pulso emissivo radiante: pico de emissão no meio da transição
        float pulsoEmissao = Mathf.Sin(progresso * Mathf.PI);
        AplicarBrilhoEmissivo(pulsoEmissao * 2.5f);

        if (m_TimerNascimento <= 0f)
        {
            m_EstaNascendo = false;
            transform.localScale = m_EscalaAlvoCorpo;
            RestaurarBrilhoEmissivo();

            // Reabilita o NavMeshAgent suavemente no chão da gaiola
            var nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (nav != null)
            {
                if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    nav.Warp(hit.position);
                    nav.enabled = true;
                }
            }

            var callback = m_CallbackNascimentoConcluido;
            m_CallbackNascimentoConcluido = null;
            callback?.Invoke();
        }
    }

    private void CapturarRenderersEEmissao()
    {
        if (m_RenderersFantasma == null || m_RenderersFantasma.Length == 0)
        {
            m_RenderersFantasma = GetComponentsInChildren<Renderer>(true);
        }

        if (!m_CorEmissaoCapturada && m_RenderersFantasma != null)
        {
            for (int i = 0; i < m_RenderersFantasma.Length; i++)
            {
                var r = m_RenderersFantasma[i];
                if (r == null || r.material == null) continue;
                string nome = r.name.ToLower();
                if (nome.Contains("olho") || nome.Contains("pupila")) continue;

                if (r.material.HasProperty("_EmissionColor"))
                {
                    m_CorEmissaoBase = r.material.GetColor("_EmissionColor");
                    if (m_CorEmissaoBase != Color.clear)
                    {
                        m_CorEmissaoCapturada = true;
                        break;
                    }
                }
            }

            if (!m_CorEmissaoCapturada)
            {
                m_CorEmissaoBase = new Color(0.10f, 0.55f, 1.0f, 1f);
                m_CorEmissaoCapturada = true;
            }
        }
    }

    private void AplicarBrilhoEmissivo(float intensidadeExtra)
    {
        if (m_RenderersFantasma == null) return;

        Color corPulsada = m_CorEmissaoBase * (1.0f + intensidadeExtra);
        for (int i = 0; i < m_RenderersFantasma.Length; i++)
        {
            var r = m_RenderersFantasma[i];
            if (r == null || r.material == null) continue;
            string n = r.name.ToLower();
            if (n.Contains("olho") || n.Contains("pupila")) continue;

            if (r.material.HasProperty("_EmissionColor"))
            {
                r.material.EnableKeyword("_EMISSION");
                r.material.SetColor("_EmissionColor", corPulsada);
            }
        }
    }

    private void RestaurarBrilhoEmissivo()
    {
        if (m_RenderersFantasma == null) return;

        for (int i = 0; i < m_RenderersFantasma.Length; i++)
        {
            var r = m_RenderersFantasma[i];
            if (r == null || r.material == null) continue;
            string n = r.name.ToLower();
            if (n.Contains("olho") || n.Contains("pupila")) continue;

            if (r.material.HasProperty("_EmissionColor"))
            {
                r.material.SetColor("_EmissionColor", m_CorEmissaoBase);
            }
        }
    }

    // =========================================================================
    // 2. CICLO DE VULNERABILIDADE E DERROTA POR POWER PELLET
    // =========================================================================

    public void SalvarCoresOriginaisSeNecessario()
    {
        if (m_CoresOriginaisSalvas) return;
        CapturarRenderersEEmissao();
        if (m_RenderersFantasma != null)
        {
            for (int i = 0; i < m_RenderersFantasma.Length; i++)
            {
                var r = m_RenderersFantasma[i];
                if (r == null || r.material == null) continue;
                string n = r.name.ToLower();
                if (n.Contains("olho") || n.Contains("pupila") || n.Contains("eye")) continue;

                if (r.material.HasProperty("_BaseColor"))
                {
                    m_CorOriginalBase = r.material.GetColor("_BaseColor");
                }
                else if (r.material.HasProperty("_Color"))
                {
                    m_CorOriginalBase = r.material.GetColor("_Color");
                }

                if (r.material.HasProperty("_EmissionColor"))
                {
                    m_CorOriginalEmissao = r.material.GetColor("_EmissionColor");
                }
                m_CoresOriginaisSalvas = true;
                break;
            }
        }
    }

    /// <summary>
    /// Atualiza dinamicamente o visual do fantasma no modo Frightened / Vulnerável clássico:
    /// - Azul escuro neon com emissão ciano durante o efeito ativo.
    /// - Pisca em 5 Hz entre azul e branco durante os últimos 2.5s antes de expirar o Power Pellet.
    /// - Restaura as cores originais (Blinky, Pinky, Inky, Clyde) ao terminar.
    /// </summary>
    public void AtualizarVisualVulneravel(bool ativo, float tempoRestante)
    {
        CapturarRenderersEEmissao();
        SalvarCoresOriginaisSeNecessario();

        if (m_RenderersFantasma == null) return;

        Color corAlvo;
        Color corEmissao;

        if (ativo)
        {
            if (tempoRestante <= 2.5f && tempoRestante > 0.01f)
            {
                // Pisca entre Azul e Branco a 5 Hz
                bool flashBranco = (Mathf.FloorToInt(tempoRestante * 5.0f) % 2) == 0;
                if (flashBranco)
                {
                    corAlvo = new Color(0.95f, 0.95f, 1.0f, 0.95f);
                    corEmissao = new Color(1.0f, 1.0f, 1.0f, 1.0f) * 1.5f;
                }
                else
                {
                    corAlvo = new Color(0.06f, 0.20f, 0.85f, 0.88f);
                    corEmissao = new Color(0.0f, 0.75f, 1.0f, 1.0f) * 0.9f;
                }
            }
            else
            {
                // Azul Frightened clássico com emissão ciano
                corAlvo = new Color(0.06f, 0.20f, 0.85f, 0.88f);
                corEmissao = new Color(0.0f, 0.75f, 1.0f, 1.0f) * 0.9f;
            }
        }
        else
        {
            corAlvo = m_CorOriginalBase != Color.clear ? m_CorOriginalBase : new Color(1.0f, 0.15f, 0.2f);
            corEmissao = m_CorOriginalEmissao != Color.clear ? m_CorOriginalEmissao : (corAlvo * 0.5f);
        }

        for (int i = 0; i < m_RenderersFantasma.Length; i++)
        {
            var r = m_RenderersFantasma[i];
            if (r == null || r.material == null) continue;
            string n = r.name.ToLower();
            if (n.Contains("olho") || n.Contains("pupila") || n.Contains("eye")) continue;

            Material mat = r.material;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", corAlvo);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", corAlvo);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", corEmissao);
            }
        }
    }

    /// <summary>
    /// Quando o Pac-Man come o fantasma vulnerável: oculta o corpo, voa os olhos até a gaiola e renasce.
    /// Desabilita colisores durante o voo dos olhos para impedir que machuquem o jogador.
    /// </summary>
    public void ExecutarDerrotaERespawn(Vector3 posGaiola, Action onRespawnCompleto = null)
    {
        DispararSusto(0.5f);
        StartCoroutine(RotinaRetornoGaiola(posGaiola, onRespawnCompleto));
    }

    private void DefinirVisibilidadeCorpo(bool visivel)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child == null) continue;
            string n = child.name.ToLower();
            if (n.Contains("olho") || n.Contains("pupila") || n.Contains("eye"))
            {
                child.gameObject.SetActive(true); // Olhos sempre visíveis no retorno estilo arcade
            }
            else
            {
                child.gameObject.SetActive(visivel);
            }
        }
    }

    private IEnumerator RotinaRetornoGaiola(Vector3 posGaiola, Action onRespawnCompleto)
    {
        // Desativa colisores durante o trânsito dos olhos de volta para a gaiola
        Collider[] cols = GetComponentsInChildren<Collider>();
        if (cols != null)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].enabled = false;
            }
        }

        // Oculta corpo e franjas, deixando apenas os olhos visíveis (estilo clássico arcade)
        DefinirVisibilidadeCorpo(false);

        var nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav != null) nav.enabled = false;

        Vector3 posOrigem = transform.position;
        float duracaoRetorno = 0.9f;
        float decorrido = 0f;

        while (decorrido < duracaoRetorno)
        {
            decorrido += Time.deltaTime;
            float f = Mathf.SmoothStep(0f, 1f, decorrido / duracaoRetorno);
            transform.position = Vector3.Lerp(posOrigem, posGaiola, f);
            yield return null;
        }

        transform.position = posGaiola;

        // Restaura corpo ao chegar na gaiola
        DefinirVisibilidadeCorpo(true);

        // Restaura cores originais
        AtualizarVisualVulneravel(false, 0f);

        // Reativa colisores
        if (cols != null)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].enabled = true;
            }
        }

        ExecutarAnimacaoNascimento(1.2f, onRespawnCompleto);
    }

    // =========================================================================
    // 3. RASTREAMENTO OCULAR E FEEDBACK DE IMPACTO
    // =========================================================================

    private void LocalizarAlvoEOlhos()
    {
        if (alvoJogador == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) alvoJogador = player.transform;
            else
            {
                var ctrlXR = FindFirstObjectByType<ControladorXRJogador>();
                if (ctrlXR != null) alvoJogador = ctrlXR.transform;
                else
                {
                    var ctrl = FindFirstObjectByType<PacMan.Player.ControladorJogador>();
                    if (ctrl != null) alvoJogador = ctrl.transform;
                    else if (Camera.main != null) alvoJogador = Camera.main.transform;
                }
            }
        }

        if (pupilaEsquerda == null)
        {
            pupilaEsquerda = transform.Find("OlhoEsquerdo/PupilaEsquerda") ?? transform.Find("Pupila_Esq");
        }

        if (pupilaDireita == null)
        {
            pupilaDireita = transform.Find("OlhoDireito/PupilaDireita") ?? transform.Find("Pupila_Dir");
        }
    }

    private void ProcessarFlutuacao()
    {
        if (!permitirFlutuacao && amplitudeFlutuacao <= 0.001f)
        {
            return;
        }

        float tempo = (Time.time * frequenciaFlutuacao) + m_DesvioFase;
        float seno = Mathf.Sin(tempo);
        float offsetVertical = oscilarApenasParaCima ? Mathf.Abs(seno) * amplitudeFlutuacao : seno * amplitudeFlutuacao;
        Vector3 posAtual = transform.localPosition;
        transform.localPosition = new Vector3(posAtual.x, m_PosicaoInicial.y + offsetVertical, posAtual.z);

        if (inclinacaoLateral > 0.01f)
        {
            float balancoZ = Mathf.Sin(tempo * 0.7f) * inclinacaoLateral;
            transform.localRotation = Quaternion.Euler(0f, transform.localEulerAngles.y, balancoZ);
        }
    }

    private void ProcessarOlharPupilas()
    {
        if (alvoJogador == null) return;

        Vector3 direcaoParaJogador = (alvoJogador.position - transform.position).normalized;
        Vector3 direcaoLocal = transform.InverseTransformDirection(direcaoParaJogador);

        Vector3 offsetOlhar = new Vector3(
            Mathf.Clamp(direcaoLocal.x, -1f, 1f) * deslocamentoPupila,
            Mathf.Clamp(direcaoLocal.y, -1f, 1f) * (deslocamentoPupila * 0.6f),
            0f
        );

        if (pupilaEsquerda != null)
        {
            pupilaEsquerda.localPosition = new Vector3(0f, 0f, 0.04f) + offsetOlhar;
        }

        if (pupilaDireita != null)
        {
            pupilaDireita.localPosition = new Vector3(0f, 0f, 0.04f) + offsetOlhar;
        }
    }

    /// <summary>
    /// Dispara uma reação de tremor visual quando o Pac-Man toca no fantasma.
    /// </summary>
    public void DispararSusto(float duracao = 0.4f)
    {
        m_TimerSusto = duracao;
    }

    private void ProcessarReacaoSusto()
    {
        if (m_TimerSusto > 0f)
        {
            m_TimerSusto -= Time.deltaTime;
            float tremorX = UnityEngine.Random.Range(-0.04f, 0.04f);
            float tremorZ = UnityEngine.Random.Range(-0.04f, 0.04f);
            transform.localPosition += new Vector3(tremorX, 0f, tremorZ);
        }
    }
}

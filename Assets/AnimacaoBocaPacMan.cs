using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Animações e Feedback Visual (Juice)
/// ============================================================================
/// Controla a clássica animação cíclica de mastigação (abrir e fechar da boca)
/// do personagem 3D Pac-Man.
/// 
/// Funcionalidades:
/// 1. Rotação sincronizada das mandíbulas superior e inferior (chomp).
/// 2. Ajuste dinâmico de frequência de mastigação conforme a velocidade de movimento.
/// 3. Efeito sonoro retrô gerado proceduralmente a cada mordida.
/// ============================================================================
/// </summary>
public class AnimacaoBocaPacMan : MonoBehaviour
{
    [Header("Mandíbulas do Pac-Man")]
    [Tooltip("Transform da metade superior da cabeça/boca.")]
    public Transform mandibulaSuperior;

    [Tooltip("Transform da metade inferior da cabeça/boca.")]
    public Transform mandibulaInferior;

    [Header("Parâmetros de Animação")]
    [Tooltip("Ângulo máximo de abertura da boca em graus.")]
    public float anguloMaximoAbertura = 38f;

    [Tooltip("Velocidade base dos ciclos de mastigação (ciclos por segundo).")]
    public float velocidadeMordida = 4.5f;

    [Tooltip("Aumentar velocidade da boca quando o jogador estiver andando.")]
    public bool sincronizarComMovimento = true;

    [Header("Áudio Retrô Chomp (Opcional)")]
    [Tooltip("Tocar som 'waka-waka' a cada ciclo de fechamento.")]
    public bool habilitarSomChomp = true;

    private AudioSource m_AudioSource;
    private AudioClip m_SomChomp;
    private float m_FaseAnimacao = 0f;
    private bool m_EstavaAberta = false;
    private Vector3 m_PosicaoAnterior;
    private Vector3 m_EscalaBase;
    private bool m_EscalaInicializada = false;
    private Animator m_Animator;

    private void Awake()
    {
        m_PosicaoAnterior = transform.position;
        m_Animator = GetComponentInChildren<Animator>();

        if (!m_EscalaInicializada)
        {
            m_EscalaBase = transform.localScale != Vector3.zero ? transform.localScale : new Vector3(0.85f, 0.85f, 0.85f);
            m_EscalaInicializada = true;
        }

        // Cria ou obtém AudioSource para o feedback sonoro
        if (habilitarSomChomp)
        {
            m_AudioSource = GetComponent<AudioSource>();
            if (m_AudioSource == null)
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
                m_AudioSource.spatialBlend = 0.5f;
                m_AudioSource.volume = 0.25f;
                m_AudioSource.playOnAwake = false;
            }
            GerarAudioClipRetro();
        }

        LocalizarMandibulasSeNecessario();
    }

    private void Update()
    {
        CalcularAnimacaoBoca();
    }

    private void LocalizarMandibulasSeNecessario()
    {
        if (mandibulaSuperior == null)
        {
            Transform sup = transform.Find("MandibulaSuperior") ?? transform.Find("UpperJaw");
            mandibulaSuperior = sup;
        }

        if (mandibulaInferior == null)
        {
            Transform inf = transform.Find("MandibulaInferior") ?? transform.Find("LowerJaw");
            mandibulaInferior = inf;
        }
    }

    private void CalcularAnimacaoBoca()
    {
        float velocidadeAtual = 1f;
        bool seMovendo = false;

        if (sincronizarComMovimento)
        {
            float deltaPos = (transform.position - m_PosicaoAnterior).magnitude;
            float vel = deltaPos / Mathf.Max(Time.deltaTime, 0.001f);
            m_PosicaoAnterior = transform.position;
            velocidadeAtual = Mathf.Clamp(vel * 0.8f, 0.6f, 2.5f);
            seMovendo = vel > 0.05f;
        }

        m_FaseAnimacao += Time.deltaTime * velocidadeMordida * velocidadeAtual * Mathf.PI * 2f;

        // Onda senoidal de 0 a 1 para abrir e fechar
        float aberturaNormalizada = (Mathf.Sin(m_FaseAnimacao) + 1f) * 0.5f;
        float anguloAtual = aberturaNormalizada * anguloMaximoAbertura;

        if (mandibulaSuperior != null)
        {
            mandibulaSuperior.localRotation = Quaternion.Euler(-anguloAtual, 0f, 0f);
        }

        if (mandibulaInferior != null)
        {
            mandibulaInferior.localRotation = Quaternion.Euler(anguloAtual, 0f, 0f);
        }

        // Se houver Animator com esqueleto ósseo (FBX Pac-Man animado), sincroniza a velocidade da mordida
        if (m_Animator != null)
        {
            m_Animator.speed = seMovendo ? Mathf.Clamp(velocidadeAtual * 1.5f, 1.2f, 2.5f) : 0f;
        }
        else if (mandibulaSuperior == null && mandibulaInferior == null && m_EscalaInicializada)
        {
            // Fallback para esfera procedural pura sem esqueleto
            if (seMovendo)
            {
                float pulso = (aberturaNormalizada - 0.5f) * 0.04f;
                transform.localScale = new Vector3(
                    m_EscalaBase.x * (1f - pulso),
                    m_EscalaBase.y * (1f + pulso * 1.2f),
                    m_EscalaBase.z * (1f - pulso)
                );
            }
            else
            {
                transform.localScale = m_EscalaBase;
            }
        }

        // Detecta momento em que a boca fecha (mordida) para emitir som
        bool estaQuaseFechada = aberturaNormalizada < 0.15f;
        if (estaQuaseFechada && m_EstavaAberta)
        {
            if (habilitarSomChomp && m_AudioSource != null && m_SomChomp != null && Application.isPlaying)
            {
                m_AudioSource.pitch = Random.Range(0.95f, 1.15f);
                m_AudioSource.PlayOneShot(m_SomChomp);
            }
            m_EstavaAberta = false;
        }
        else if (aberturaNormalizada > 0.6f)
        {
            m_EstavaAberta = true;
        }
    }

    /// <summary>
    /// Gera proceduralmente um tom clássico 8-bit 'pop/chomp' sem depender de arquivos .wav externos.
    /// </summary>
    private void GerarAudioClipRetro()
    {
        int sampleRate = 44100;
        float duration = 0.06f;
        int numSamples = (int)(sampleRate * duration);
        float[] samples = new float[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(480f, 220f, t / duration); // Frequência descendente suave
            float envelope = 1f - (t / duration); // Fade out linear
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.4f;
        }

        m_SomChomp = AudioClip.Create("PacMan_Chomp_Procedural", numSamples, 1, sampleRate, false);
        m_SomChomp.SetData(samples, 0);
    }
}

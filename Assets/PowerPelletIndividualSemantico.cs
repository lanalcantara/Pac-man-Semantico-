using System;
using UnityEngine;
using PacMan.Semantics;

/// <summary>
/// Power Pellet do Arcade Clássico - Representa Named Individuals da Ontologia Protégé.
/// Esferas maiores piscantes posicionadas nos 4 cantos do mapa. Ao serem comidas,
/// ativam o estado de vulnerabilidade dos fantasmas e registram o indivíduo ontológico no HUD.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class PowerPelletIndividualSemantico : InstanciaSemanticaBase
{
    [Header("Configuração de Indivíduo Ontológico (Individual)")]
    public string nomeIndividuo = "Individual: Specimen_Enterobius_01";
    public string expressaoDLIndividuo = "Enterobius_Specimen_A : EnterobiusVermicularis";

    private Material m_MaterialPellet;
    private float m_TempoPiscar = 0f;

    protected override void Awake()
    {
        base.Awake();
        m_Id = "power_pellet_individual_" + Mathf.Abs(GetHashCode());
        m_NomeClasse = nomeIndividuo;
        m_ExpressaoDL = expressaoDLIndividuo;
        m_ValorPontuacao = 50;
        m_EhValida = true;
        m_CorDestaque = Color.white;

        SphereCollider col = GetComponent<SphereCollider>();
        if (col != null)
        {
            col.isTrigger = true;
            col.radius = 0.85f;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        Renderer rend = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            m_MaterialPellet = rend.material;
        }
    }

    protected override void Update()
    {
        base.Update();
        // Efeito piscante clássico dos 4 cantos do Arcade
        m_TempoPiscar += Time.deltaTime * 6.0f;
        float pulso = (Mathf.Sin(m_TempoPiscar) + 1f) * 0.5f;
        transform.localScale = Vector3.one * (0.42f + pulso * 0.16f);

        if (m_MaterialPellet != null && m_MaterialPellet.HasProperty("_EmissionColor"))
        {
            m_MaterialPellet.EnableKeyword("_EMISSION");
            m_MaterialPellet.SetColor("_EmissionColor", Color.Lerp(new Color(0.2f, 0.2f, 0.2f), Color.white * 2.0f, pulso));
        }
    }

    protected override void AoExecutarColeta(GameObject interator, bool ehRaycast)
    {
        if (m_Coletada) return;
        m_Coletada = true;

        totalColetadas++;
        pontuacaoLogica += m_ValorPontuacao;

        // Ativa estado vulnerável de Power Pellet em MotorRaciocinioSemantico
        var motor = FindFirstObjectByType<MotorRaciocinioSemantico>();
        if (motor != null)
        {
            motor.AtivarPowerPellet(8.0f);
        }

        Debug.Log($"<color=#38BDF8><b>[POWER PELLET - INDIVIDUAL CONSUMIDO]</b></color> {nomeIndividuo} (+{m_ValorPontuacao} pts)");

        RegistrarConsumoOntologico(m_Id, nomeIndividuo);
        AtualizarHUDs(m_Id, nomeIndividuo, m_ValorPontuacao, m_ExpressaoDL, "Individual Ontológico (Power Pellet)");

        if (GerenciadorEfeitosJuice.Instancia != null)
        {
            GerenciadorEfeitosJuice.Instancia.CriarExplosaoBrilhoColeta(transform.position, Color.cyan);
        }

        Collider[] cols = GetComponentsInChildren<Collider>();
        if (cols != null)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].enabled = false;
            }
        }

        Renderer[] rends = GetComponentsInChildren<Renderer>();
        if (rends != null)
        {
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] != null) rends[i].enabled = false;
            }
        }

        Destroy(gameObject, 0.25f);
        NotificarInteracao(sucesso: true);
    }

    protected override void AoDetectarViolacao(GameObject interator, bool ehRaycast) { }
}

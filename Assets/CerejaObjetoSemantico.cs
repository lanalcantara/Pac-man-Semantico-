using System;
using UnityEngine;
using PacMan.Semantics;

/// <summary>
/// Cereja Semântica do Arcade Clássico - Representa Object Properties da Ontologia Protégé.
/// Posicionada em local fixo (abaixo da gaiola central dos fantasmas). Ao ser consumida pelo Pac-Man,
/// concede pontuação de bônus (+100 pts) e atualiza o HUD com o nome da Object Property lida.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class CerejaObjetoSemantico : InstanciaSemanticaBase
{
    [Header("Configuração de Objeto de Ontologia (Object Property)")]
    public string nomePropriedadeObjeto = "ObjectProperty: has_habitat";
    public string expressaoDLPropriedade = "has_habitat ⊑ ParasiteHostRelationship";

    protected override void Awake()
    {
        base.Awake();
        m_Id = "cereja_object_property_" + Mathf.Abs(GetHashCode());
        m_NomeClasse = nomePropriedadeObjeto;
        m_ExpressaoDL = expressaoDLPropriedade;
        m_ValorPontuacao = 100;
        m_EhValida = true;
        m_CorDestaque = new Color(1f, 0.08f, 0.15f); // Vermelho Cereja Clássico

        SphereCollider col = GetComponent<SphereCollider>();
        if (col != null)
        {
            col.isTrigger = true;
            col.radius = 0.65f;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    protected override void AoExecutarColeta(GameObject interator, bool ehRaycast)
    {
        if (m_Coletada) return;
        m_Coletada = true;

        totalColetadas++;
        pontuacaoLogica += m_ValorPontuacao;

        Debug.Log($"<color=#FF0055><b>[CEREJA SEMÂNTICA COLETADA]</b></color> {nomePropriedadeObjeto} (+{m_ValorPontuacao} pts)");

        // Registra e atualiza HUD com a Object Property da ontologia
        RegistrarConsumoOntologico(m_Id, nomePropriedadeObjeto);
        AtualizarHUDs(m_Id, nomePropriedadeObjeto, m_ValorPontuacao, m_ExpressaoDL, "Object Property da Ontologia (Cereja)");

        // Efeito juice de coleta
        if (GerenciadorEfeitosJuice.Instancia != null)
        {
            GerenciadorEfeitosJuice.Instancia.CriarExplosaoBrilhoColeta(transform.position, Color.red);
        }

        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    protected override void AoDetectarViolacao(GameObject interator, bool ehRaycast) { }
}

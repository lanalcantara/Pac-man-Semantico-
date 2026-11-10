using System;
using UnityEngine;
using PacMan.Semantics;

namespace PacMan.XR
{
    /// <summary>
    /// ============================================================================
    /// PAC-MAN SEMÂNTICO (CIn-UFPE) - Deteção de Colisão e Interação Espacial XR
    /// ============================================================================
    /// Componente integrado no XR Origin / Jogador para gerir a deteção física e
    /// de gatilhos (triggers) com as gemas do labirinto procedural, comunicando
    /// os eventos de consumo diretamente ao MotorRaciocinioSemantico e Grafo Ontológico.
    /// ============================================================================
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Pac-Man Semântico/XR/Interação XR Colisão")]
    public class InteracaoXRColisao : MonoBehaviour
    {
        [Header("Referências Semânticas")]
        [Tooltip("Referência ao motor central de raciocínio ontológico.")]
        public MotorRaciocinioSemantico motorSemantico;

        [Header("Configuração de Feedback")]
        [Tooltip("Efeito sonoro disparado ao consumir uma gema no labirinto.")]
        public AudioClip somConsumoGema;

        private AudioSource audioSource;

        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0f;
                audioSource.playOnAwake = false;
            }

            if (motorSemantico == null)
            {
                motorSemantico = FindFirstObjectByType<MotorRaciocinioSemantico>();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other != null)
            {
                ProcessarInteracaoOntologica(other.gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision != null && collision.gameObject != null)
            {
                ProcessarInteracaoOntologica(collision.gameObject);
            }
        }

        /// <summary>
        /// Processa a interação ontológica com a entidade colidida no labirinto procedural.
        /// </summary>
        public void ProcessarInteracaoOntologica(GameObject alvo)
        {
            if (alvo == null) return;

            IInstanciaSemantica inst = alvo.GetComponent<IInstanciaSemantica>() ?? alvo.GetComponentInParent<IInstanciaSemantica>();

            bool ehTagGema = false;
            bool ehTagObstaculo = false;
            try { ehTagGema = alvo.CompareTag("GemaValida"); } catch { }
            try { ehTagObstaculo = alvo.CompareTag("ObstaculoInvalido"); } catch { }

            // 1. Verifica se o objeto tocado é uma Gema baseada no grafo ontológico
            GemaValidaSemantica gemaValida = alvo.GetComponent<GemaValidaSemantica>() ?? alvo.GetComponentInParent<GemaValidaSemantica>();
            if (gemaValida != null)
            {
                // A GemaValidaSemantica gerencia sua própria captura estrita por contato físico real (raio <= 0.45m),
                // sequência 1 a 1, banner inferior, radar e poder de devorar fantasmas.
                return;
            }

            if (ehTagGema || alvo.name.Contains("Gema") || alvo.name.Contains("Gem") || alvo.name.Contains("PacDot") || (inst != null && inst.EhValida))
            {
                string idItem = inst != null && !string.IsNullOrEmpty(inst.Id) ? inst.Id : alvo.name;
                string nomeClasseOriginal = inst != null && !string.IsNullOrEmpty(inst.NomeClasse) ? inst.NomeClasse : alvo.name;
                string nomeClasseFormatado = InstanciaSemanticaBase.FormatarNomeClasse(nomeClasseOriginal);
                int pontos = inst != null ? inst.ValorPontuacao : 10;
                string expressaoDL = inst != null ? inst.ExpressaoDL : "";
                string descSemantica = inst != null ? inst.DescricaoSemantica : "";

                Debug.Log($"[InteracaoXR] Gema ontológica '{nomeClasseFormatado}' consumida via XR.");

                // Comunica com o MotorRaciocinioSemantico se for uma gema especial (Power Pellet)
                bool ehGemaEspecial = (inst != null && (nomeClasseOriginal.IndexOf("Special", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                        nomeClasseOriginal.IndexOf("Power", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                        nomeClasseOriginal.IndexOf("Pellet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                        (inst.PropriedadesDL != null && inst.PropriedadesDL.ContainsKey("isPowerPellet")))) ||
                                      alvo.name.IndexOf("Special", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      alvo.name.IndexOf("Power", StringComparison.OrdinalIgnoreCase) >= 0;

                if (motorSemantico != null && ehGemaEspecial)
                {
                    motorSemantico.AtivarPowerPellet(8.0f);
                }

                // Envia instanciação via WebSocket (porta 8085) e registra no Grafo Ontológico (SEM som de arcade)
                InstanciaSemanticaBase.RegistrarConsumoOntologico(idItem, nomeClasseOriginal, true, expressaoDL);

                // Atualiza HUDs em tempo real
                InstanciaSemanticaBase.AtualizarHUDs(idItem, nomeClasseFormatado, pontos, expressaoDL, descSemantica);

                // Métrica de axiomas
                InstanciaSemanticaBase.pontuacaoLogica += pontos;
                InstanciaSemanticaBase.totalColetadas++;
                InstanciaSemanticaBase.instanciasValidasRestantes = Mathf.Max(0, InstanciaSemanticaBase.instanciasValidasRestantes - 1);

                // Dispara Feedback Visual Cibernético (sem som de arcade)
                if (GerenciadorEfeitosJuice.Instancia != null)
                {
                    Color cor = inst != null ? inst.CorDestaque : new Color(0.0f, 0.90f, 1.0f, 0.9f);
                    GerenciadorEfeitosJuice.Instancia.CriarExplosaoBrilhoColeta(alvo.transform.position, cor);
                }

                // Desativa os colisores do Nodo mantendo sua malha prismática visível para ancorar as arestas do RelationBuilder
                Collider[] cols = alvo.GetComponentsInChildren<Collider>();
                if (cols != null)
                {
                    for (int i = 0; i < cols.Length; i++)
                    {
                        if (cols[i] != null) cols[i].enabled = false;
                    }
                }
                alvo.transform.localScale = alvo.transform.localScale * 0.88f;
            }
            // 2. Obstáculo / Fantasma: Ignorado no XR Origin (tela)
            // O dano só pode ocorrer quando o fantasma atinge o Pac-Man físico no chão, NUNCA na tela/câmera!
            else if (ehTagObstaculo || alvo.name.Contains("Obstaculo") || alvo.name.Contains("Fantasma") || alvo.name.Contains("Blinky") || (inst != null && !inst.EhValida))
            {
                // Ignora contato na tela/câmera. Apenas repele o fantasma se tentar invadir a visão
                Vector3 afastamento = (alvo.transform.position - transform.position).normalized;
                afastamento.y = 0f;
                if (afastamento.sqrMagnitude > 0.01f)
                {
                    alvo.transform.position += afastamento * 3.5f;
                }
            }
        }
    }
}
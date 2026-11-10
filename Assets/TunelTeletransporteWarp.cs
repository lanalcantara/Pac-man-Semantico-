using UnityEngine;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (Semantic Pac-Man) - Túnel de Teletransporte Warp
/// ============================================================================
/// Implementa os clássicos túneis de fuga laterais (Leste-Oeste) do Pac-Man.
/// Ao atravessar o portal de uma extremidade do labirinto, o jogador é
/// transportado instantaneamente para a saída oposta mantendo sua inércia.
/// ============================================================================
/// </summary>
public class TunelTeletransporteWarp : MonoBehaviour
{
    [Tooltip("Ponto de destino onde o jogador reaparecerá.")]
    public Vector3 pontoDestino;

    [Tooltip("Cooldown em segundos para evitar loops de teletransporte.")]
    public float tempoCooldown = 0.8f;

    private static float s_TimerUltimoTeletransporte = 0f;

    private void Update()
    {
        if (s_TimerUltimoTeletransporte > 0f)
        {
            s_TimerUltimoTeletransporte -= Time.deltaTime;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (s_TimerUltimoTeletransporte > 0f) return;
        if (other == null) return;

        bool ehJogador = other.CompareTag("Player") ||
                         other.GetComponent<ControladorXRJogador>() != null ||
                         other.GetComponentInParent<ControladorXRJogador>() != null ||
                         other.GetComponent<AvatarPacManFisico>() != null ||
                         other.name.Contains("PacMan");

        if (ehJogador)
        {
            s_TimerUltimoTeletransporte = tempoCooldown;
            var ctrlXR = FindFirstObjectByType<ControladorXRJogador>();
            if (ctrlXR != null)
            {
                ctrlXR.transform.position = pontoDestino;
                ctrlXR.DispararHapticsDualController(0.45f, 0.15f);
            }
            else
            {
                other.transform.root.position = pontoDestino;
            }

            if (GerenciadorEfeitosJuice.Instancia != null)
            {
                GerenciadorEfeitosJuice.Instancia.CriarExplosaoBrilhoColeta(pontoDestino, Color.cyan);
            }
        }
    }
}


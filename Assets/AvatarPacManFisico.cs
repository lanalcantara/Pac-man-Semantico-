using System;
using UnityEngine;
using PacMan.Semantics;

/// <summary>
/// ============================================================================
/// PAC-MAN SEMÂNTICO (CIn-UFPE) - Avatar Físico do Pac-Man (Come-Come Amarelo)
/// ============================================================================
/// Centro físico e visual de jogabilidade:
/// 1. Restauração e Visibilidade: Mantém o modelo 3D do Pac-Man (Mandíbula e Olhos)
///    sempre ativo (SetActive(true)), visível (MeshRenderers enabled) e na escala ~0.8f.
/// 2. Assentamento Perfeito no Solo: Mantém o Pac-Man rente ao chão (y = 0.0f na base, 0.40m no centro).
/// 3. Religação dos Controles: Lê Input.GetAxis("Horizontal") e Input.GetAxis("Vertical")
///    e aplica ao Rigidbody.velocity nos eixos X e Z com navegação clássica de labirinto.
/// 4. Trava de Eixo Y: RigidbodyConstraints.FreezePositionY ativo contra quedas e flutuação.
/// 5. Colisão e Consumo Real no Come-Come:
///    - Gemas tocadas pela boca são consumidas IMEDIATAMENTE (varredura contínua de 1.35m).
///    - Fantasmas que tocam o Pac-Man causam dano e recuam.
/// ============================================================================
/// </summary>
[DisallowMultipleComponent]
public class AvatarPacManFisico : MonoBehaviour
{
    [Header("Configuração de Escala e Solo")]
    [Tooltip("Escala física proporcional do Pac-Man (aprox 0.8f para caber folgado nos corredores).")]
    public float escalaPacMan = 0.80f;

    [Tooltip("Altura do centro da esfera do Pac-Man em relação ao solo.")]
    public float alturaAssentamentoSolo = 0.40f;

    [Header("Locomoção e Física")]
    [Tooltip("Velocidade linear de navegação pelo labirinto.")]
    public float velocidadeMovimento = 4.5f;

    [Tooltip("Raio do colisor Trigger do Pac-Man para consumo das gemas e contato com fantasmas.")]
    public float raioColisorPacMan = 0.50f;

    [Header("Referências")]
    public Transform xrOriginTransform;
    public Transform cameraTransform;
    public ControladorXRJogador controladorXR;

    private SphereCollider m_Colisor;
    private Rigidbody m_Rigidbody;
    private GerenciadorCenarioSemantico m_GerenciadorCenario;
    private float m_TimerDanoRecente = 0f;
    private Vector3 m_DirecaoEntrada = Vector3.zero;

    private void Awake()
    {
        gameObject.tag = "Player";
        if (GetComponent<RelationBuilder>() == null)
        {
            gameObject.AddComponent<RelationBuilder>();
        }
        LocalizarReferencias();
        GarantirDesvinculacaoDaCamera();
        GarantirVisibilidadeEMeshes();
        GarantirFisicaEColisores();
        AssentarNoSoloImediato();
    }

    private void Start()
    {
        LocalizarReferencias();
        GarantirDesvinculacaoDaCamera();
        GarantirVisibilidadeEMeshes();
        GarantirFisicaEColisores();
        AssentarNoSoloImediato();
    }

    public bool JogoAtivoEControlesHabilitados()
    {
        if (HubInicialAmbiente.Instancia != null)
        {
            if (HubInicialAmbiente.Instancia.hubAberto ||
                HubInicialAmbiente.Instancia.GameOverAberto ||
                HubInicialAmbiente.Instancia.VitoriaAberta ||
                !HubInicialAmbiente.Instancia.partidaEmAndamento)
            {
                return false;
            }
        }
        if (controladorXR != null && !controladorXR.controlesHabilitados)
        {
            return false;
        }
        return true;
    }

    private void Update()
    {
        if (m_TimerDanoRecente > 0f) m_TimerDanoRecente -= Time.deltaTime;

        LocalizarReferencias();
        GarantirVisibilidadeEMeshes();
        AtualizarPosicionamentoNoSolo();

        if (!JogoAtivoEControlesHabilitados())
        {
            m_DirecaoEntrada = Vector3.zero;
            if (m_Rigidbody != null)
            {
                m_Rigidbody.linearVelocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
            }
            return;
        }

        LerEntradasControles();
        VarreduraProximidadeGemasBoca();
        VarreduraProximidadeFantasmasCorpo();
    }

    private void FixedUpdate()
    {
        if (!JogoAtivoEControlesHabilitados())
        {
            if (m_Rigidbody != null)
            {
                m_Rigidbody.linearVelocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
            }
            return;
        }
        AplicarMovimentoFisico();
    }

    /// <summary>
    /// Garante que o GameObject e todas as partes da malha (Mandíbula e Olhos) estejam habilitados
    /// e com a escala global correta de ~0.8f para visualização clara na Câmera Pinball.
    /// </summary>
    public void GarantirVisibilidadeEMeshes()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        transform.localScale = new Vector3(escalaPacMan, escalaPacMan, escalaPacMan);

        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = true;
                    if (!renderers[i].gameObject.activeSelf)
                    {
                        renderers[i].gameObject.SetActive(true);
                    }
                }
            }
        }
    }

    public void GarantirDesvinculacaoDaCamera()
    {
        if (transform.parent != null && (transform.parent.name.Contains("Camera") || transform.parent.name.Contains("Offset")))
        {
            GameObject xrOrigin = GameObject.Find("XR Origin") ?? GameObject.FindWithTag("Player");
            if (xrOrigin != null && xrOrigin != gameObject)
            {
                transform.SetParent(xrOrigin.transform, false);
                transform.localPosition = new Vector3(0f, alturaAssentamentoSolo, 0f);
                Debug.Log("<color=#00FF99><b>[Avatar Pac-Man]</b></color> Pac-Man desvinculado da câmera e ancorado ao solo no XR Origin.");
            }
        }
    }

    private void GarantirFisicaEColisores()
    {
        bool ehFilhoDeXROrigin = transform.parent != null && (transform.parent.name.Contains("XR") || transform.parent.CompareTag("Player"));

        if (!ehFilhoDeXROrigin)
        {
            // Se for o objeto raiz do jogador:
            m_Colisor = GetComponent<SphereCollider>();
            if (m_Colisor == null) m_Colisor = gameObject.AddComponent<SphereCollider>();
            m_Colisor.isTrigger = false;
            m_Colisor.radius = 0.38f;
            m_Colisor.center = new Vector3(0f, alturaAssentamentoSolo, 0f);

            m_Rigidbody = GetComponent<Rigidbody>();
            if (m_Rigidbody == null) m_Rigidbody = gameObject.AddComponent<Rigidbody>();
            m_Rigidbody.isKinematic = false;
            m_Rigidbody.useGravity = false;
            m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            m_Rigidbody.constraints = RigidbodyConstraints.FreezePositionY |
                                      RigidbodyConstraints.FreezeRotationX |
                                      RigidbodyConstraints.FreezeRotationY |
                                      RigidbodyConstraints.FreezeRotationZ;
        }

        // Trigger da Boca para consumo instantâneo de gemas
        Transform colBocaTr = transform.Find("Trigger_Boca_PacMan");
        if (colBocaTr == null)
        {
            GameObject triggerBoca = new GameObject("Trigger_Boca_PacMan");
            triggerBoca.transform.SetParent(transform, false);
            triggerBoca.transform.localPosition = Vector3.zero;
            SphereCollider scBoca = triggerBoca.AddComponent<SphereCollider>();
            scBoca.isTrigger = true;
            scBoca.radius = raioColisorPacMan;
        }
    }

    private void LocalizarReferencias()
    {
        if (m_GerenciadorCenario == null)
        {
            m_GerenciadorCenario = FindFirstObjectByType<GerenciadorCenarioSemantico>();
        }

        if (controladorXR == null)
        {
            controladorXR = FindFirstObjectByType<ControladorXRJogador>();
        }

        if (controladorXR != null)
        {
            xrOriginTransform = controladorXR.transform;
            if (controladorXR.cameraPrincipalXR != null)
            {
                cameraTransform = controladorXR.cameraPrincipalXR.transform;
            }
        }

        if (xrOriginTransform == null)
        {
            GameObject p = GameObject.Find("XR Origin") ?? GameObject.FindWithTag("Player");
            if (p != null && p != gameObject) xrOriginTransform = p.transform;
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void AssentarNoSoloImediato()
    {
        if (transform.parent != null)
        {
            transform.localPosition = new Vector3(0f, alturaAssentamentoSolo, 0f);
        }
        else
        {
            transform.position = new Vector3(transform.position.x, 0.0f, transform.position.z);
        }
    }

    private void AtualizarPosicionamentoNoSolo()
    {
        if (transform.parent != null)
        {
            transform.localPosition = new Vector3(0f, alturaAssentamentoSolo, 0f);
        }
        else
        {
            Vector3 p = transform.position;
            if (Mathf.Abs(p.y - 0.0f) > 0.001f)
            {
                transform.position = new Vector3(p.x, 0.0f, p.z);
            }
        }
    }

    /// <summary>
    /// Lê controles clássicos de teclado, gamepad do Xbox e VR.
    /// </summary>
    private void LerEntradasControles()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) inputZ = Mathf.Max(inputZ, 1f);
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) inputZ = Mathf.Min(inputZ, -1f);
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) inputX = Mathf.Min(inputX, -1f);
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inputX = Mathf.Max(inputX, 1f);

        Vector2 dir2D = new Vector2(inputX, inputZ);

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Gamepad.all.Count > 0)
        {
            for (int i = 0; i < UnityEngine.InputSystem.Gamepad.all.Count; i++)
            {
                var gp = UnityEngine.InputSystem.Gamepad.all[i];
                if (gp == null) continue;
                Vector2 stickEsq = gp.leftStick.ReadValue();
                if (stickEsq.sqrMagnitude > 0.02f && stickEsq.sqrMagnitude > dir2D.sqrMagnitude)
                {
                    dir2D = stickEsq;
                }
                Vector2 dpad = gp.dpad.ReadValue();
                if (dpad.sqrMagnitude > 0.05f && dpad.sqrMagnitude > dir2D.sqrMagnitude)
                {
                    dir2D = dpad;
                }
            }
        }
#endif
        if (dir2D.sqrMagnitude > 1f) dir2D.Normalize();

        Vector3 camFwd = Vector3.forward;
        Vector3 camRgt = Vector3.right;
        if (cameraTransform != null)
        {
            camFwd = cameraTransform.forward;
            camRgt = cameraTransform.right;
        }
        else if (Camera.main != null)
        {
            camFwd = Camera.main.transform.forward;
            camRgt = Camera.main.transform.right;
        }

        camFwd.y = 0f; camFwd.Normalize();
        camRgt.y = 0f; camRgt.Normalize();
        if (camFwd.sqrMagnitude < 0.01f) camFwd = Vector3.forward;
        if (camRgt.sqrMagnitude < 0.01f) camRgt = Vector3.right;

        m_DirecaoEntrada = (camFwd * dir2D.y + camRgt * dir2D.x);
        if (m_DirecaoEntrada.sqrMagnitude > 1f) m_DirecaoEntrada.Normalize();

        if (m_DirecaoEntrada.sqrMagnitude > 0.05f)
        {
            Quaternion rotAlvo = Quaternion.LookRotation(m_DirecaoEntrada.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotAlvo, Time.deltaTime * 14f);
        }
    }

    /// <summary>
    /// Aplica vetor de movimento diretamente ao Rigidbody.velocity (e linearVelocity).
    /// </summary>
    private void AplicarMovimentoFisico()
    {
        if (m_Rigidbody == null) m_Rigidbody = GetComponent<Rigidbody>();
        if (m_Rigidbody != null && !m_Rigidbody.isKinematic)
        {
            m_Rigidbody.constraints = RigidbodyConstraints.FreezePositionY |
                                      RigidbodyConstraints.FreezeRotationX |
                                      RigidbodyConstraints.FreezeRotationY |
                                      RigidbodyConstraints.FreezeRotationZ;

            Vector3 vel = m_DirecaoEntrada * velocidadeMovimento;
            m_Rigidbody.linearVelocity = new Vector3(vel.x, 0f, vel.z);
#pragma warning disable CS0618
            m_Rigidbody.velocity = new Vector3(vel.x, 0f, vel.z);
#pragma warning restore CS0618
        }
    }

    /// <summary>
    /// Varredura contínua de proximidade na boca do Pac-Man para consumo imediato de gemas (raio 1.35m).
    /// As gemas NUNCA conseguem bater e empurrar o jogador ou fugir sem pontuar.
    /// </summary>
    private void VarreduraProximidadeGemasBoca()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 1.35f, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null || col.gameObject == gameObject || col.transform.IsChildOf(transform)) continue;

            IInstanciaSemantica inst = col.GetComponent<IInstanciaSemantica>() ?? col.GetComponentInParent<IInstanciaSemantica>();
            bool ehGema = col.CompareTag("GemaValida") || col.name.Contains("Gema") || col.name.Contains("PacDot") || (inst != null && inst.EhValida);

            if (ehGema)
            {
                TratarConsumoGemaNoPacMan(col, inst);
                return;
            }
        }
    }

    /// <summary>
    /// Varredura contínua de contato de fantasmas no corpo do Pac-Man (raio 1.15m).
    /// </summary>
    private void VarreduraProximidadeFantasmasCorpo()
    {
        if (m_TimerDanoRecente > 0f) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, 1.15f, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null || col.gameObject == gameObject || col.transform.IsChildOf(transform)) continue;

            IInstanciaSemantica inst = col.GetComponent<IInstanciaSemantica>() ?? col.GetComponentInParent<IInstanciaSemantica>();
            bool ehFantasma = col.CompareTag("ObstaculoInvalido") || col.name.Contains("Obstaculo") || col.name.Contains("Fantasma") || col.name.Contains("Blinky") || (inst != null && !inst.EhValida);

            if (ehFantasma)
            {
                TratarAtaqueFantasmaNoPacMan(col, inst);
                return;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TratarColisaoGenerica(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TratarColisaoGenerica(other);
    }

    private void TratarColisaoGenerica(Collider other)
    {
        if (other == null || other.gameObject == gameObject || other.transform.IsChildOf(transform)) return;

        IInstanciaSemantica inst = other.GetComponent<IInstanciaSemantica>() ?? other.GetComponentInParent<IInstanciaSemantica>();

        if (other.CompareTag("GemaValida") || other.name.Contains("Gema") || other.name.Contains("PacDot") || (inst != null && inst.EhValida))
        {
            TratarConsumoGemaNoPacMan(other, inst);
        }
        else if (other.CompareTag("ObstaculoInvalido") || other.name.Contains("Obstaculo") || other.name.Contains("Fantasma") || other.name.Contains("Blinky") || (inst != null && !inst.EhValida))
        {
            TratarAtaqueFantasmaNoPacMan(other, inst);
        }
    }

    private void TratarConsumoGemaNoPacMan(Collider col, IInstanciaSemantica inst)
    {
        GameObject gemaObj = (inst is Component c && c != null) ? c.gameObject : col.gameObject;
        if (gemaObj == null) return;

        // Desativa colisores da gema imediatamente para anular qualquer repulsão física
        Collider[] cols = gemaObj.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null) cols[i].enabled = false;
        }

        if (controladorXR != null)
        {
            controladorXR.ProcessarColisaoEntidade(col);
        }
        else if (inst != null)
        {
            inst.Interagir(gameObject, false);
            if (RelationBuilder.Instance != null)
            {
                RelationBuilder.Instance.AdicionarItemComidoNaRedeNeural(inst, gemaObj);
            }
        }
    }

    private void TratarAtaqueFantasmaNoPacMan(Collider col, IInstanciaSemantica inst)
    {
        if (m_TimerDanoRecente > 0f) return;
        m_TimerDanoRecente = 1.2f;

        GameObject fantasmaObj = (inst is Component c && c != null) ? c.gameObject : col.gameObject;

        if (controladorXR != null)
        {
            controladorXR.ExecutarDanoPacMan(fantasmaObj);
        }
        else
        {
            if (RelationBuilder.Instance != null)
            {
                RelationBuilder.Instance.QuebrarUltimoPedacoDaRedePorFantasma(fantasmaObj != null ? fantasmaObj.name : "Fantasma");
            }
            var web = FindFirstObjectByType<PacMan.Web.ControladorDemoWeb>();
            if (web != null) web.ExecutarResetColisao(0.5f);
        }

        // Repele o fantasma 5.5 metros para trás ao atingir o Pac-Man
        if (fantasmaObj != null)
        {
            Vector3 dirRecuo = (fantasmaObj.transform.position - transform.position).normalized;
            dirRecuo.y = 0f;
            if (dirRecuo.sqrMagnitude < 0.01f) dirRecuo = -transform.forward;
            fantasmaObj.transform.position += dirRecuo * 5.5f;
        }
    }
}

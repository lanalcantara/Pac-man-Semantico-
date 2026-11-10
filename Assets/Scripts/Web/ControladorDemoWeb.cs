using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using PacMan.Semantics;

namespace PacMan.Web
{
    /// <summary>
    /// ============================================================================
    /// PAC-MAN SEMÂNTICO (CIn-UFPE) - Controlador de Demonstração WebGL & Arcade
    /// ============================================================================
    /// Interface Canônica e Limpa (Conforme Solicitado pelo Usuário):
    /// - Escala de Referência 1920x1080 (Scale With Screen Size).
    /// - Fontes reduzidas, sem textos sobrepostos.
    /// - Textos estritamente limpos: Botão PLAY, Pontuação, Vidas e Entidade Ativa.
    /// - Sem textos inúteis de debug (sem 'PATROL', sem distâncias, sem lista de atalhos).
    /// - Botão [ X ] no canto superior direito para fechar/minimizar o painel.
    /// ============================================================================
    /// </summary>
    [AddComponentMenu("Pac-Man Semântico/Web/Controlador Demo Web")]
    public class ControladorDemoWeb : MonoBehaviour
    {
        public enum EstadoJogoDemo
        {
            MenuInicial,
            Jogando,
            GameOver,
            Vitoria
        }

        public enum ModoCamera
        {
            Pinball3D,             // Padrão: Câmera de cima inclinada estilo Space Cadet Pinball 3D
            MesaCompletaPinball,   // Visão panorâmica estática da mesa inteira
            TerceiraPessoaBaixa,   // 3ª pessoa clássica atrás do Pac-Man
            PrimeiraPessoaVR       // Visão em 1ª pessoa cockpit (Meta Quest)
        }

        [Header("Estado do Jogo")]
        public EstadoJogoDemo estadoAtual = EstadoJogoDemo.MenuInicial;

        [Header("Modo de Visualização da Câmera")]
        public ModoCamera modoCamera = ModoCamera.TerceiraPessoaBaixa;

        [Header("Referências")]
        public MotorRaciocinioSemantico motorSemantico;
        public AvaliadorRegrasRuntime avaliadorRegras;
        public Transform pacmanTransform;
        public Transform ghostTransform;
        public Transform ghostBlinkyTransform;

        [Header("Configuração de Interface")]
        public bool exibirGuiWebGL = true;
        [Tooltip("Indica se o painel HUD está minimizado/fechado pelo usuário.")]
        public bool hudMinimizado = false;
        public float duracaoPowerPelletSimulado = 8.0f;

        [Header("Colisão e Vidas")]
        public float distanciaColisaoSegura = 1.0f;
        public Vector3 pontoSpawnInicial = new Vector3(0f, 0f, -6f);
        public int vidasRestantes = 3;
        private float temporizadorAvisoColisao = 0f;
        private string mensagemAvisoColisao = "";

        [Header("Feedback de Captura Semântica (Tempo Real)")]
        public string ultimaInstanciaCapturada = "Nenhuma";
        public string ultimaClasseCapturada = "Aguardando item...";
        public string ultimoAxiomaDL = "";
        public bool ultimoItemValido = true;
        public int pontuacaoAcumulada = 0;
        public int totalItensCapturados = 0;

        private CharacterController m_CharacterController;
        private Camera m_CameraPrincipal;
        private GameObject m_VisualPacMan;

        private void OnEnable()
        {
            InstanciaSemanticaBase.OnInstanciaInteragida += AoInteragirInstanciaSemantica;
            PacMan.Player.ControladorJogador.OnVidasAlteradas += AoAlterarVidasJogador;
        }

        private void OnDisable()
        {
            InstanciaSemanticaBase.OnInstanciaInteragida -= AoInteragirInstanciaSemantica;
            PacMan.Player.ControladorJogador.OnVidasAlteradas -= AoAlterarVidasJogador;
        }

        private void AoAlterarVidasJogador(int novasVidas)
        {
            vidasRestantes = novasVidas;
            if (vidasRestantes <= 0)
            {
                FinalizarJogoGameOver();
            }
        }

        private void Start()
        {
            if (motorSemantico == null) motorSemantico = FindFirstObjectByType<MotorRaciocinioSemantico>();
            if (avaliadorRegras == null) avaliadorRegras = FindFirstObjectByType<AvaliadorRegrasRuntime>();

            LocalizarElementosCena();
            ConfigurarVisualEPerspectivaCamera();
            GarantirSemanticHUD();

            estadoAtual = EstadoJogoDemo.Jogando;
            PausarEntidadesParaMenu(false);
            Time.timeScale = 1.0f;

            Debug.Log("<color=#00E5FF><b>[Interface Espacial OWL]</b></color> SemanticHUDManager ativo e sessão iniciada.");
        }

        private void GarantirSemanticHUD()
        {
            SemanticHUDManager.GarantirHUDAtivoNaCena();
            if (SemanticHUDManager.Instancia != null)
            {
                SemanticHUDManager.Instancia.DefinirModalPlayVisivel(false);
                SemanticHUDManager.Instancia.DefinirPainelVisivel(!hudMinimizado);
            }
        }

        private void LocalizarElementosCena()
        {
            if (pacmanTransform == null)
            {
                GameObject xrOrigin = GameObject.Find("XR Origin") ?? GameObject.FindWithTag("Player");
                if (xrOrigin != null) pacmanTransform = xrOrigin.transform;
            }

            if (pacmanTransform != null)
            {
                // Se ControladorXRJogador estiver gerenciando o jogador, delega integralmente a física e spawn
                if (pacmanTransform.GetComponent<ControladorXRJogador>() != null)
                {
                    m_VisualPacMan = GameObject.Find("Visual_PacMan");
                    if (ghostBlinkyTransform == null)
                    {
                        GameObject blinky = GameObject.Find("Fantasma_Blinky") ?? GameObject.Find("Blinky") ?? GameObject.Find("Fantasma_0");
                        if (blinky != null) ghostBlinkyTransform = blinky.transform;
                    }
                    if (ghostTransform == null && ghostBlinkyTransform != null)
                    {
                        ghostTransform = ghostBlinkyTransform;
                    }
                    m_CameraPrincipal = Camera.main;
                    return;
                }

                pacmanTransform.gameObject.SetActive(true);
                pontoSpawnInicial = new Vector3(0f, 0.0f, -5.0f);
                pacmanTransform.position = pontoSpawnInicial;

                m_CharacterController = pacmanTransform.GetComponent<CharacterController>();
                if (m_CharacterController != null)
                {
                    m_CharacterController.enabled = false;
                }

                Rigidbody rb = pacmanTransform.GetComponent<Rigidbody>();
                if (rb == null) rb = pacmanTransform.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.constraints = RigidbodyConstraints.FreezePositionY |
                                 RigidbodyConstraints.FreezeRotationX |
                                 RigidbodyConstraints.FreezeRotationY |
                                 RigidbodyConstraints.FreezeRotationZ;

                SphereCollider sc = pacmanTransform.GetComponent<SphereCollider>();
                if (sc == null) sc = pacmanTransform.gameObject.AddComponent<SphereCollider>();
                sc.isTrigger = false;
                sc.radius = 0.38f;
                sc.center = new Vector3(0f, 0.40f, 0f);
            }

            if (ghostBlinkyTransform == null)
            {
                GameObject blinky = GameObject.Find("Fantasma_Blinky") ?? GameObject.Find("Blinky") ?? GameObject.Find("Fantasma_0");
                if (blinky != null) ghostBlinkyTransform = blinky.transform;
            }

            if (ghostTransform == null && ghostBlinkyTransform != null)
            {
                ghostTransform = ghostBlinkyTransform;
            }

            m_CameraPrincipal = Camera.main;

            m_VisualPacMan = GameObject.Find("Visual_PacMan");
            if (m_VisualPacMan == null && pacmanTransform != null)
            {
                Transform tVis = pacmanTransform.Find("Visual_PacMan");
                if (tVis != null) m_VisualPacMan = tVis.gameObject;
            }
            if (m_VisualPacMan != null)
            {
                m_VisualPacMan.SetActive(true);
                m_VisualPacMan.transform.localPosition = new Vector3(0f, 0.40f, 0f);
                m_VisualPacMan.transform.localScale = new Vector3(0.80f, 0.80f, 0.80f);

                MeshRenderer[] renderers = m_VisualPacMan.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        renderers[i].enabled = true;
                        if (!renderers[i].gameObject.activeSelf) renderers[i].gameObject.SetActive(true);
                    }
                }
            }
        }

        public void ConfigurarVisualEPerspectivaCamera()
        {
            if (FindFirstObjectByType<ControladorXRJogador>() != null) return;

            if (m_CameraPrincipal == null) m_CameraPrincipal = Camera.main;
            // Seção 2 da Especificação Mestra: O usuário está DENTRO do labirinto (visão imersiva rente ao corredor)
            if (modoCamera == ModoCamera.Pinball3D || modoCamera == ModoCamera.MesaCompletaPinball)
            {
                modoCamera = ModoCamera.TerceiraPessoaBaixa;
            }
            AplicarModoCamera(modoCamera);
        }

        public void AlternarModoCamera()
        {
            modoCamera = (ModoCamera)(((int)modoCamera + 1) % 4);
            AplicarModoCamera(modoCamera);
        }

        public void AplicarModoCamera(ModoCamera modo)
        {
            if (m_CameraPrincipal == null) m_CameraPrincipal = Camera.main;
            if (m_CameraPrincipal == null) return;

            Transform camTransform = m_CameraPrincipal.transform;

            switch (modo)
            {
                case ModoCamera.Pinball3D:
                    m_CameraPrincipal.fieldOfView = 62f;
                    if (pacmanTransform != null)
                    {
                        camTransform.position = pacmanTransform.position + new Vector3(0f, 15.5f, -11.5f);
                    }
                    else
                    {
                        camTransform.position = new Vector3(0f, 15.5f, -11.5f);
                    }
                    camTransform.rotation = Quaternion.Euler(54f, 0f, 0f);
                    if (m_VisualPacMan != null) m_VisualPacMan.SetActive(true);
                    break;

                case ModoCamera.MesaCompletaPinball:
                    m_CameraPrincipal.fieldOfView = 65f;
                    camTransform.position = new Vector3(0f, 22.0f, -14.5f);
                    camTransform.rotation = Quaternion.Euler(55f, 0f, 0f);
                    if (m_VisualPacMan != null) m_VisualPacMan.SetActive(true);
                    break;

                case ModoCamera.TerceiraPessoaBaixa:
                    m_CameraPrincipal.fieldOfView = 68f;
                    if (pacmanTransform != null)
                    {
                        camTransform.position = pacmanTransform.position + new Vector3(0f, 1.45f, -2.5f);
                    }
                    camTransform.rotation = Quaternion.Euler(14f, 0f, 0f);
                    if (m_VisualPacMan != null) m_VisualPacMan.SetActive(true);
                    break;

                case ModoCamera.PrimeiraPessoaVR:
                    m_CameraPrincipal.fieldOfView = 75f;
                    if (pacmanTransform != null)
                    {
                        camTransform.position = pacmanTransform.position + new Vector3(0f, 1.25f, 0f);
                        camTransform.rotation = pacmanTransform.rotation;
                    }
                    if (m_VisualPacMan != null) m_VisualPacMan.SetActive(false);
                    break;
            }
        }

        private void LateUpdate()
        {
            // Se o ControladorXRJogador estiver na cena, ele gerencia a câmera soberanamente
            if (FindFirstObjectByType<ControladorXRJogador>() != null) return;

            if (m_CameraPrincipal == null || pacmanTransform == null) return;

            Transform camTransform = m_CameraPrincipal.transform;

            if (modoCamera == ModoCamera.Pinball3D)
            {
                Vector3 targetPos = pacmanTransform.position + new Vector3(0f, 15.5f, -11.5f);
                camTransform.position = Vector3.Lerp(camTransform.position, targetPos, Time.deltaTime * 8f);
                camTransform.rotation = Quaternion.Euler(54f, 0f, 0f);
            }
            else if (modoCamera == ModoCamera.MesaCompletaPinball)
            {
                camTransform.position = Vector3.Lerp(camTransform.position, new Vector3(0f, 22.0f, -14.5f), Time.deltaTime * 6f);
                camTransform.rotation = Quaternion.Euler(55f, 0f, 0f);
            }
            else if (modoCamera == ModoCamera.TerceiraPessoaBaixa)
            {
                Vector3 targetPos = pacmanTransform.position + new Vector3(0f, 1.45f, -2.5f);
                camTransform.position = Vector3.Lerp(camTransform.position, targetPos, Time.deltaTime * 10f);
                camTransform.rotation = Quaternion.Euler(14f, 0f, 0f);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.C)) AlternarModoCamera();
            if (Input.GetKeyDown(KeyCode.H)) hudMinimizado = !hudMinimizado;
            if (Input.GetKeyDown(KeyCode.P)) AlternarPowerPellet();

            if (temporizadorAvisoColisao > 0f) temporizadorAvisoColisao -= Time.deltaTime;

            if (estadoAtual == EstadoJogoDemo.Jogando)
            {
                VerificarColisaoPacmanBlinky();
            }
        }

        public void IniciarJogoPlay()
        {
            estadoAtual = EstadoJogoDemo.Jogando;
            PausarEntidadesParaMenu(false);
            if (SemanticHUDManager.Instancia != null)
            {
                SemanticHUDManager.Instancia.DefinirModalPlayVisivel(false);
                SemanticHUDManager.Instancia.DefinirPainelVisivel(!hudMinimizado);
            }
            Debug.Log("<color=#00FF99><b>[Jogo Iniciado]</b></color> Corrida semântica iniciada!");
        }

        public void ReiniciarJogoPartida()
        {
            Time.timeScale = 1.0f;
            Time.timeScale = 1.0f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void FinalizarJogoGameOver()
        {
            estadoAtual = EstadoJogoDemo.GameOver;
            PausarEntidadesParaMenu(true);
            Debug.Log("<color=#FF3333><b>[GAME OVER]</b></color> Fim de jogo!");
        }

        private void PausarEntidadesParaMenu(bool pausar)
        {
            if (pacmanTransform != null)
            {
                Rigidbody rb = pacmanTransform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
#pragma warning disable CS0618
                    rb.velocity = Vector3.zero;
#pragma warning restore CS0618
                }
            }

            var agents = FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None);
            if (agents != null)
            {
                foreach (var agent in agents)
                {
                    if (agent != null && agent.isOnNavMesh) agent.isStopped = pausar;
                }
            }
        }

        public void ExecutarResetColisao(float tempoEspera = 0.5f)
        {
            vidasRestantes--;
            ExibirAvisoColisao("Colisão com Fantasma! -1 Vida", 2.5f);

            if (vidasRestantes <= 0)
            {
                FinalizarJogoGameOver();
            }
            else
            {
                ResetarPosicaoPacman();
            }
        }

        public void VerificarColisaoPacmanBlinky()
        {
            Transform alvoFantasma = ghostBlinkyTransform != null ? ghostBlinkyTransform : ghostTransform;
            if (pacmanTransform == null || alvoFantasma == null) return;

            float distancia = Vector3.Distance(pacmanTransform.position, alvoFantasma.position);

            if (distancia <= distanciaColisaoSegura)
            {
                bool isVulnerable = (avaliadorRegras != null &&
                    avaliadorRegras.ultimoEstadoInferido == GameEntityState.Vulnerable) ||
                    (motorSemantico != null && motorSemantico.pacmanIsPowered);

                if (isVulnerable)
                {
                    alvoFantasma.position = new Vector3(0f, 0f, 0f);
                    pontuacaoAcumulada += 200;
                    ExibirAvisoColisao("Fantasma Capturado! +200 pts", 2.5f);
                }
                else
                {
                    ExecutarResetColisao(0.5f);
                }
            }
        }

        public void ExibirAvisoColisao(string msg, float tempo = 3.0f)
        {
            mensagemAvisoColisao = msg;
            temporizadorAvisoColisao = tempo;
        }

        public void ResetarPosicaoPacman()
        {
            if (pacmanTransform != null)
            {
                pacmanTransform.position = pontoSpawnInicial;
                Rigidbody rb = pacmanTransform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
#pragma warning disable CS0618
                    rb.velocity = Vector3.zero;
#pragma warning restore CS0618
                }
            }
        }

        public void RegistrarCapturaItem(string id, string nomeClasse, bool valida, int pontos, string dl = "")
        {
            ultimaInstanciaCapturada = id;
            ultimaClasseCapturada = nomeClasse;
            ultimoAxiomaDL = dl;
            ultimoItemValido = valida;
            totalItensCapturados++;
            pontuacaoAcumulada += pontos;
        }

        private void AoInteragirInstanciaSemantica(IInstanciaSemantica instancia, bool sucesso)
        {
            if (instancia == null) return;

            ultimaInstanciaCapturada = instancia.Id;
            ultimaClasseCapturada = instancia.NomeClasse;
            ultimoAxiomaDL = instancia.ExpressaoDL;
            ultimoItemValido = instancia.EhValida;
            totalItensCapturados++;

            if (instancia.ValorPontuacao > 0)
            {
                pontuacaoAcumulada += instancia.ValorPontuacao;
            }
            else
            {
                pontuacaoAcumulada += instancia.EhValida ? 50 : 10;
            }
        }

        public void AlternarPowerPellet()
        {
            if (motorSemantico != null)
            {
                bool novoEstado = !motorSemantico.pacmanIsPowered;
                if (novoEstado)
                {
                    motorSemantico.AtivarPowerPellet(duracaoPowerPelletSimulado);
                }
                else
                {
                    motorSemantico.pacmanIsPowered = false;
                }
            }
        }

        // =========================================================================
        // A renderização de interface agora é 100% gerenciada pelo SemanticHUDManager
        // (TextMeshPro + ScrollRect). O OnGUI legado do Arcade foi desativado.
        // =========================================================================
        private void OnGUI()
        {
            // Desativado para garantir que apenas o Canvas do SemanticHUDManager apareça na tela.
        }
    }
}

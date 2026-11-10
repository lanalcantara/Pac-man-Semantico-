using System;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;

namespace PacMan.XR
{
    /// <summary>
    /// Estrutura de dados de entrada por frame (Valor / Struct).
    /// Passada por valor para garantir ZERO alocação no managed heap (Zero-GC).
    /// Suporta Gamepads (Xbox/PlayStation), Meta Quest Touch Plus e Teclado/Mouse.
    /// </summary>
    public struct DadosEntradaJogador
    {
        public Vector2 Movimento2D;
        public float GiroCameraYaw;
        public float OlharCameraPitch;
        public float ZoomCamera;
        public float AceleradorGatilho;  // Gatilho indicador direito (0.0 a 1.0) para acelerar o Pac-Man para frente
        public bool AlternarModoCamera; // Botão B (Xbox) / Bola (PlayStation) / Botão B (Meta Quest) / Tecla C
        public bool Interagir;           // Botão A (Xbox) / Cruz (PlayStation) / Botão A (Meta Quest) / Gatilho / Enter / Espaço
        public bool AlternarMenuHub;     // Botão Start (Xbox) / Menu ou Y (Meta Quest) / Tecla ESC
        public bool ResetCheckpoint;     // Botão X (Xbox) / Quadrado (PlayStation) / Botão X (Meta Quest) / Tecla X
        public bool TemEntradaAtiva;
    }

    /// <summary>
    /// Interface abstrata para provedores de entrada desacoplados.
    /// Permite trocar ou adicionar novos dispositivos (VR, Gamepad, Teclado/Mouse)
    /// sem tocar na lógica de física, câmera ou regras ontológicas.
    /// </summary>
    public interface IProvedorEntradaXR
    {
        string NomeProvedor { get; }
        bool DispositivoConectado { get; }
        DadosEntradaJogador LerEntradas(float deadzoneMovimento);
        void EnviarPulsoHaptico(float amplitude, float duracao);
    }

    /// <summary>
    /// Provedor de entrada nativo para Meta Quest 3 e Quest 3S via OpenXR / Unity XR Subsystems.
    /// Lê analógicos dos controles Touch Plus (Thumbsticks), botões frontais (A/B, X/Y),
    /// gatilhos e emite feedback háptico por impulso físico.
    /// </summary>
    public class ProvedorEntradaVR : IProvedorEntradaXR
    {
        public string NomeProvedor => "Meta Quest Touch Plus (OpenXR)";

        private UnityEngine.XR.InputDevice m_MaoEsquerda;
        private UnityEngine.XR.InputDevice m_MaoDireita;
        private bool m_DispositivosInicializados = false;

        // Borda de subida (debounce) para alternâncias limpas em VR
        private bool m_PrevBtnB;
        private bool m_PrevBtnA;
        private bool m_PrevBtnX;
        private bool m_PrevBtnY;
        private bool m_PrevBtnMenu;
        private bool m_PrevTriggerDir;

        public bool DispositivoConectado => m_MaoEsquerda.isValid || m_MaoDireita.isValid;

        public void AtualizarDispositivos()
        {
            var leftList = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, leftList);
            if (leftList.Count > 0) m_MaoEsquerda = leftList[0];
            else m_MaoEsquerda = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

            var rightList = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, rightList);
            if (rightList.Count > 0) m_MaoDireita = rightList[0];
            else m_MaoDireita = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            m_DispositivosInicializados = true;
        }

        public DadosEntradaJogador LerEntradas(float deadzoneMovimento)
        {
            if (!m_DispositivosInicializados || !m_MaoEsquerda.isValid || !m_MaoDireita.isValid)
            {
                AtualizarDispositivos();
            }

            DadosEntradaJogador dados = default;

            // 1. Locomoção Contínua (Thumbstick Esquerdo do Meta Quest)
            if (m_MaoEsquerda.isValid && m_MaoEsquerda.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axisEsq))
            {
                if (axisEsq.sqrMagnitude > deadzoneMovimento * deadzoneMovimento)
                {
                    dados.Movimento2D = axisEsq;
                    dados.TemEntradaAtiva = true;
                }
            }

            // 2. Rotação Smooth Turn e Pitch (Thumbstick Direito do Meta Quest)
            if (m_MaoDireita.isValid && m_MaoDireita.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axisDir))
            {
                if (Mathf.Abs(axisDir.x) > 0.08f)
                {
                    dados.GiroCameraYaw = axisDir.x;
                    dados.TemEntradaAtiva = true;
                }
                if (Mathf.Abs(axisDir.y) > 0.10f)
                {
                    dados.ZoomCamera = axisDir.y;
                    dados.TemEntradaAtiva = true;
                }
            }

            // 3. Botão B (Secondary Button no controle direito do Meta Quest) -> ALTERNAR MODO DE CÂMERA
            bool btnBAtual = false;
            if (m_MaoDireita.isValid && m_MaoDireita.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool btnB))
            {
                btnBAtual = btnB;
            }
            if (btnBAtual && !m_PrevBtnB)
            {
                dados.AlternarModoCamera = true;
                dados.TemEntradaAtiva = true;
            }
            m_PrevBtnB = btnBAtual;

            // 4. Botão A (Primary Button no controle direito do Meta Quest) ou Gatilho Index -> INTERAGIR / PLAY NO HUB
            bool btnAAtual = false;
            if (m_MaoDireita.isValid && m_MaoDireita.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool btnA))
            {
                btnAAtual = btnA;
            }
            bool triggerDirAtual = false;
            if (m_MaoDireita.isValid && m_MaoDireita.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool trigDir))
            {
                triggerDirAtual = trigDir;
            }
            if ((btnAAtual && !m_PrevBtnA) || (triggerDirAtual && !m_PrevTriggerDir))
            {
                dados.Interagir = true;
                dados.TemEntradaAtiva = true;
            }
            m_PrevBtnA = btnAAtual;
            m_PrevTriggerDir = triggerDirAtual;

            // 4.1 Acelerador via Gatilho Indicador Direito (Index Trigger do Meta Quest)
            // Permite acelerar o Pac-Man para frente na direção em que a câmera/analógico direito está apontando
            if (m_MaoDireita.isValid && m_MaoDireita.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float trigFloatVal))
            {
                dados.AceleradorGatilho = trigFloatVal;
                if (trigFloatVal > 0.05f)
                {
                    dados.TemEntradaAtiva = true;
                }
            }

            // 5. Botão X (Primary Button no controle esquerdo do Meta Quest) -> Não reseta checkpoint acidentalmente
            bool btnXAtual = false;
            if (m_MaoEsquerda.isValid && m_MaoEsquerda.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool btnX))
            {
                btnXAtual = btnX;
            }
            if (btnXAtual && !m_PrevBtnX)
            {
                // Botão X atua como confirmação secundária
                dados.Interagir = true;
                dados.TemEntradaAtiva = true;
            }
            m_PrevBtnX = btnXAtual;

            // 6. Botão Y (Secondary Button no controle esquerdo) ou Botão Menu -> ABRIR / FECHAR HUB
            bool btnYAtual = false;
            if (m_MaoEsquerda.isValid && m_MaoEsquerda.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool btnY))
            {
                btnYAtual = btnY;
            }
            bool menuBtnAtual = false;
            if (m_MaoEsquerda.isValid && m_MaoEsquerda.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool btnMenu))
            {
                menuBtnAtual = btnMenu;
            }
            if ((btnYAtual && !m_PrevBtnY) || (menuBtnAtual && !m_PrevBtnMenu))
            {
                dados.AlternarMenuHub = true;
                dados.TemEntradaAtiva = true;
            }
            m_PrevBtnY = btnYAtual;
            m_PrevBtnMenu = menuBtnAtual;

            return dados;
        }

        public void EnviarPulsoHaptico(float amplitude, float duracao)
        {
            if (!m_MaoEsquerda.isValid || !m_MaoDireita.isValid)
            {
                AtualizarDispositivos();
            }

            if (m_MaoEsquerda.isValid && m_MaoEsquerda.TryGetHapticCapabilities(out var capEsq) && capEsq.supportsImpulse)
            {
                m_MaoEsquerda.SendHapticImpulse(0, amplitude, duracao);
            }

            if (m_MaoDireita.isValid && m_MaoDireita.TryGetHapticCapabilities(out var capDir) && capDir.supportsImpulse)
            {
                m_MaoDireita.SendHapticImpulse(0, amplitude, duracao);
            }
        }
    }

    /// <summary>
    /// Provedor universal de Gamepad (Xbox One / Xbox Series / PS4 / PS5 / Universal).
    /// Lê analógicos, sticks e botões via New Input System e Legacy Input Manager em paralelo.
    /// Mapeia Botão B (Xbox) / Bola (PlayStation) para alternar câmeras.
    /// </summary>
    public class ProvedorEntradaGamepad : IProvedorEntradaXR
    {
        public string NomeProvedor => "Gamepad / Xbox Controller";

        public bool DispositivoConectado
        {
            get
            {
                if (Gamepad.all.Count > 0 || UnityEngine.InputSystem.Joystick.all.Count > 0) return true;
                return Input.GetJoystickNames().Length > 0;
            }
        }

        public DadosEntradaJogador LerEntradas(float deadzoneMovimento)
        {
            DadosEntradaJogador dados = default;

            // 1. Processamento via Unity New Input System (Gamepad)
            if (Gamepad.all.Count > 0)
            {
                for (int i = 0; i < Gamepad.all.Count; i++)
                {
                    var gp = Gamepad.all[i];
                    if (gp == null) continue;

                    // Stick Esquerdo (Locomoção Contínua)
                    Vector2 stickEsq = gp.leftStick.ReadValue();
                    if (stickEsq.sqrMagnitude > deadzoneMovimento * deadzoneMovimento)
                    {
                        dados.Movimento2D = stickEsq;
                        dados.TemEntradaAtiva = true;
                    }

                    // D-Pad Direcional
                    Vector2 dpad = gp.dpad.ReadValue();
                    if (dpad.sqrMagnitude > 0.05f && dpad.sqrMagnitude > dados.Movimento2D.sqrMagnitude)
                    {
                        dados.Movimento2D = dpad;
                        dados.TemEntradaAtiva = true;
                    }

                    // Stick Direito (Câmera Yaw e Pitch)
                    Vector2 stickDir = gp.rightStick.ReadValue();
                    if (Mathf.Abs(stickDir.x) > 0.05f)
                    {
                        dados.GiroCameraYaw = stickDir.x;
                        dados.TemEntradaAtiva = true;
                    }
                    if (Mathf.Abs(stickDir.y) > 0.05f)
                    {
                        dados.OlharCameraPitch = -stickDir.y;
                        dados.TemEntradaAtiva = true;
                    }

                    // Gatilhos Triggers (RT afasta / LT aproxima) & Bumpers
                    float rt = gp.rightTrigger.ReadValue();
                    float lt = gp.leftTrigger.ReadValue();
                    if (rt > 0.12f)
                    {
                        dados.ZoomCamera += rt;
                        dados.TemEntradaAtiva = true;
                    }
                    if (lt > 0.12f)
                    {
                        dados.ZoomCamera -= lt;
                        dados.TemEntradaAtiva = true;
                    }

                    if (gp.rightShoulder.isPressed)
                    {
                        dados.ZoomCamera += 0.8f;
                        dados.TemEntradaAtiva = true;
                    }
                    if (gp.leftShoulder.isPressed)
                    {
                        dados.ZoomCamera -= 0.8f;
                        dados.TemEntradaAtiva = true;
                    }

                    // *** MUDAR CÂMERA: BOTÃO B (Xbox) OU BOLA (PlayStation) -> buttonEast ***
                    // Também aceita R3 (Right Stick Click)
                    if (gp.buttonEast.wasPressedThisFrame || gp.rightStickButton.wasPressedThisFrame)
                    {
                        dados.AlternarModoCamera = true;
                        dados.TemEntradaAtiva = true;
                    }

                    // BOTÃO A (Xbox) OU CRUZ (PlayStation) -> buttonSouth: Interagir / Play no Hub
                    if (gp.buttonSouth.wasPressedThisFrame)
                    {
                        dados.Interagir = true;
                        dados.TemEntradaAtiva = true;
                    }

                    // BOTÃO START / MENU OU SELECT -> Abrir/Fechar Hub Inicial
                    if (gp.startButton.wasPressedThisFrame || gp.selectButton.wasPressedThisFrame)
                    {
                        dados.AlternarMenuHub = true;
                        dados.TemEntradaAtiva = true;
                    }

                    // BOTÃO X (Xbox) OU QUADRADO (PlayStation) -> buttonWest: Reset Checkpoint (apenas tecla X ou botão oeste)
                    if (gp.buttonWest.wasPressedThisFrame)
                    {
                        dados.ResetCheckpoint = true;
                        dados.TemEntradaAtiva = true;
                    }

                    // GATILHO DIREITO (RT / R2): Acelerar para a frente
                    if (gp.rightTrigger != null && gp.rightTrigger.ReadValue() > 0.05f)
                    {
                        float trigVal = gp.rightTrigger.ReadValue();
                        if (trigVal > dados.AceleradorGatilho)
                        {
                            dados.AceleradorGatilho = trigVal;
                            dados.TemEntradaAtiva = true;
                        }
                    }
                }
            }

            // 2. Processamento de Joysticks Genéricos do New Input System
            if (UnityEngine.InputSystem.Joystick.all.Count > 0)
            {
                for (int i = 0; i < UnityEngine.InputSystem.Joystick.all.Count; i++)
                {
                    var joy = UnityEngine.InputSystem.Joystick.all[i];
                    if (joy == null) continue;

                    Vector2 joyStickVal = joy.stick.ReadValue();
                    if (joyStickVal.sqrMagnitude > deadzoneMovimento * deadzoneMovimento && joyStickVal.sqrMagnitude > dados.Movimento2D.sqrMagnitude)
                    {
                        dados.Movimento2D = joyStickVal;
                        dados.TemEntradaAtiva = true;
                    }

                    if (joy.trigger.wasPressedThisFrame)
                    {
                        dados.Interagir = true;
                        dados.TemEntradaAtiva = true;
                    }
                }
            }

            // 3. Fallback Direto para Unity Legacy Input Manager / DirectInput / XInput clássico
            try
            {
                float joyX = Input.GetAxisRaw("Horizontal");
                float joyY = Input.GetAxisRaw("Vertical");
                if (Mathf.Abs(joyX) > deadzoneMovimento || Mathf.Abs(joyY) > deadzoneMovimento)
                {
                    Vector2 joyMove = new Vector2(joyX, joyY);
                    if (joyMove.sqrMagnitude > dados.Movimento2D.sqrMagnitude)
                    {
                        dados.Movimento2D = joyMove.sqrMagnitude > 1f ? joyMove.normalized : joyMove;
                        dados.TemEntradaAtiva = true;
                    }
                }

                // D-Pad analógico/direcional em controladores USB/Bluetooth clássicos
                float dpadX = Input.GetAxisRaw("DPad_X");
                float dpadY = Input.GetAxisRaw("DPad_Y");
                if (Mathf.Abs(dpadX) > 0.2f || Mathf.Abs(dpadY) > 0.2f)
                {
                    Vector2 dpadMove = new Vector2(dpadX, dpadY);
                    if (dpadMove.sqrMagnitude > dados.Movimento2D.sqrMagnitude)
                    {
                        dados.Movimento2D = dpadMove.sqrMagnitude > 1f ? dpadMove.normalized : dpadMove;
                        dados.TemEntradaAtiva = true;
                    }
                }
            }
            catch { /* Ignora se eixos não estiverem no InputManager */ }

            // Botão B (Xbox) / Bola (PlayStation) no mapeamento nativo DirectInput: KeyCode.JoystickButton1 ou JoystickButton9
            if (Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.JoystickButton9))
            {
                dados.AlternarModoCamera = true;
                dados.TemEntradaAtiva = true;
            }

            // Botão A (Xbox) / Cruz (PlayStation): KeyCode.JoystickButton0
            if (Input.GetKeyDown(KeyCode.JoystickButton0))
            {
                dados.Interagir = true;
                dados.TemEntradaAtiva = true;
            }

            // Botão SELECT / BACK / VIEW (Xbox): KeyCode.JoystickButton6 ou START: JoystickButton7 -> AlternarMenuHub (Volta ao Menu)
            if (Input.GetKeyDown(KeyCode.JoystickButton6) || Input.GetKeyDown(KeyCode.JoystickButton7))
            {
                dados.AlternarMenuHub = true;
                dados.TemEntradaAtiva = true;
            }

            // Botão X (Xbox) / Quadrado (PlayStation): KeyCode.JoystickButton2 -> ResetCheckpoint
            if (Input.GetKeyDown(KeyCode.JoystickButton2))
            {
                dados.ResetCheckpoint = true;
                dados.TemEntradaAtiva = true;
            }

            return dados;
        }

        public void EnviarPulsoHaptico(float amplitude, float duracao)
        {
            if (Gamepad.current != null)
            {
                Gamepad.current.SetMotorSpeeds(amplitude, amplitude * 0.8f);
            }
        }
    }

    /// <summary>
    /// Provedor Desktop para Teclado e Mouse.
    /// Suporte total para desenvolvimento ágil no Editor sem necessidade de headset.
    /// </summary>
    public class ProvedorEntradaDesktop : IProvedorEntradaXR
    {
        public string NomeProvedor => "Teclado & Mouse (Simulador Desktop)";

        public bool DispositivoConectado => true;

        public DadosEntradaJogador LerEntradas(float deadzoneMovimento)
        {
            DadosEntradaJogador dados = default;

            // 1. Teclas WASD e Setas
            Vector2 teclado = Vector2.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) teclado.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) teclado.y -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) teclado.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) teclado.x += 1f;

            if (teclado.sqrMagnitude > 0.01f)
            {
                dados.Movimento2D = teclado.normalized;
                dados.TemEntradaAtiva = true;
            }

            // Shift acelera no simulador desktop
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                dados.AceleradorGatilho = 1.0f;
                dados.TemEntradaAtiva = true;
            }

            // 2. Mouse para Giro de Câmera (Yaw horizontal e Pitch vertical em 1ª pessoa)
            bool podeGirarMouse = (Cursor.lockState == CursorLockMode.Locked) || Input.GetMouseButton(0) || Input.GetMouseButton(1);
            if (podeGirarMouse)
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");
                if (Mathf.Abs(mouseX) > 0.002f)
                {
                    dados.GiroCameraYaw = mouseX;
                    dados.TemEntradaAtiva = true;
                }
                if (Mathf.Abs(mouseY) > 0.002f)
                {
                    dados.OlharCameraPitch = -mouseY;
                    dados.TemEntradaAtiva = true;
                }
            }

            // Teclas Q e E para giro manual alternativo
            if (Input.GetKey(KeyCode.Q))
            {
                dados.GiroCameraYaw -= 1f;
                dados.TemEntradaAtiva = true;
            }
            if (Input.GetKey(KeyCode.E))
            {
                dados.GiroCameraYaw += 1f;
                dados.TemEntradaAtiva = true;
            }

            // 3. Roda do Mouse e Teclas R/F para Zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                dados.ZoomCamera = -scroll * 4.0f;
                dados.TemEntradaAtiva = true;
            }

            if (Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.PageUp) || Input.GetKey(KeyCode.KeypadPlus))
            {
                dados.ZoomCamera += 1f;
                dados.TemEntradaAtiva = true;
            }
            if (Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.PageDown) || Input.GetKey(KeyCode.KeypadMinus))
            {
                dados.ZoomCamera -= 1f;
                dados.TemEntradaAtiva = true;
            }

            // 4. Teclas C ou V para alternar modo de câmera
            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.V))
            {
                dados.AlternarModoCamera = true;
                dados.TemEntradaAtiva = true;
            }

            // 5. Tecla X para Resetar Checkpoint
            if (Input.GetKeyDown(KeyCode.X))
            {
                dados.ResetCheckpoint = true;
                dados.TemEntradaAtiva = true;
            }

            // 6. Espaço ou Enter para interação / Play
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                dados.Interagir = true;
                dados.TemEntradaAtiva = true;
            }

            // 7. Tecla ESC para abrir/fechar Hub Inicial
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                dados.AlternarMenuHub = true;
                dados.TemEntradaAtiva = true;
            }

            return dados;
        }

        public void EnviarPulsoHaptico(float amplitude, float duracao)
        {
            // Sem haptics físicos em teclado/mouse de desktop
        }
    }

    /// <summary>
    /// Gerenciador unificado de entradas Zero-GC.
    /// Combina os provedores VR, Gamepad e Desktop sem gerar lixo no Heap.
    /// </summary>
    public class GerenciadorEntradaUnificada
    {
        private readonly ProvedorEntradaVR m_ProvedorVR = new ProvedorEntradaVR();
        private readonly ProvedorEntradaGamepad m_ProvedorGamepad = new ProvedorEntradaGamepad();
        private readonly ProvedorEntradaDesktop m_ProvedorDesktop = new ProvedorEntradaDesktop();

        public ProvedorEntradaVR ProvedorVR => m_ProvedorVR;
        public ProvedorEntradaGamepad ProvedorGamepad => m_ProvedorGamepad;
        public ProvedorEntradaDesktop ProvedorDesktop => m_ProvedorDesktop;

        /// <summary>
        /// Consolida as entradas de todos os dispositivos conectados em uma única estrutura de valor.
        /// Zero alocações por frame (0 B GC Alloc).
        /// </summary>
        public DadosEntradaJogador ObterEntradaConsolidada(float deadzoneMovimento)
        {
            DadosEntradaJogador consolidada = default;

            // 1. Meta Quest Touch Plus (VR)
            DadosEntradaJogador entradaVR = m_ProvedorVR.LerEntradas(deadzoneMovimento);
            if (entradaVR.TemEntradaAtiva)
            {
                consolidada.Movimento2D = entradaVR.Movimento2D;
                consolidada.GiroCameraYaw = entradaVR.GiroCameraYaw;
                consolidada.OlharCameraPitch = entradaVR.OlharCameraPitch;
                consolidada.ZoomCamera = entradaVR.ZoomCamera;
                consolidada.AceleradorGatilho = entradaVR.AceleradorGatilho;
                if (entradaVR.AlternarModoCamera) consolidada.AlternarModoCamera = true;
                if (entradaVR.Interagir) consolidada.Interagir = true;
                if (entradaVR.AlternarMenuHub) consolidada.AlternarMenuHub = true;
                if (entradaVR.ResetCheckpoint) consolidada.ResetCheckpoint = true;
                consolidada.TemEntradaAtiva = true;
            }

            // 2. Gamepad Xbox / PlayStation / Universal
            DadosEntradaJogador entradaGamepad = m_ProvedorGamepad.LerEntradas(deadzoneMovimento);
            if (entradaGamepad.TemEntradaAtiva)
            {
                if (entradaGamepad.Movimento2D.sqrMagnitude > consolidada.Movimento2D.sqrMagnitude)
                {
                    consolidada.Movimento2D = entradaGamepad.Movimento2D;
                }
                if (Mathf.Abs(entradaGamepad.GiroCameraYaw) > Mathf.Abs(consolidada.GiroCameraYaw))
                {
                    consolidada.GiroCameraYaw = entradaGamepad.GiroCameraYaw;
                }
                if (Mathf.Abs(entradaGamepad.OlharCameraPitch) > Mathf.Abs(consolidada.OlharCameraPitch))
                {
                    consolidada.OlharCameraPitch = entradaGamepad.OlharCameraPitch;
                }
                if (Mathf.Abs(entradaGamepad.ZoomCamera) > Mathf.Abs(consolidada.ZoomCamera))
                {
                    consolidada.ZoomCamera = entradaGamepad.ZoomCamera;
                }
                if (entradaGamepad.AceleradorGatilho > consolidada.AceleradorGatilho)
                {
                    consolidada.AceleradorGatilho = entradaGamepad.AceleradorGatilho;
                }
                if (entradaGamepad.AlternarModoCamera) consolidada.AlternarModoCamera = true;
                if (entradaGamepad.Interagir) consolidada.Interagir = true;
                if (entradaGamepad.AlternarMenuHub) consolidada.AlternarMenuHub = true;
                if (entradaGamepad.ResetCheckpoint) consolidada.ResetCheckpoint = true;
                consolidada.TemEntradaAtiva = true;
            }

            // 3. Desktop Teclado & Mouse
            DadosEntradaJogador entradaDesktop = m_ProvedorDesktop.LerEntradas(deadzoneMovimento);
            if (entradaDesktop.TemEntradaAtiva)
            {
                if (entradaDesktop.Movimento2D.sqrMagnitude > consolidada.Movimento2D.sqrMagnitude)
                {
                    consolidada.Movimento2D = entradaDesktop.Movimento2D;
                }
                if (Mathf.Abs(entradaDesktop.GiroCameraYaw) > Mathf.Abs(consolidada.GiroCameraYaw))
                {
                    consolidada.GiroCameraYaw = entradaDesktop.GiroCameraYaw;
                }
                if (Mathf.Abs(entradaDesktop.OlharCameraPitch) > Mathf.Abs(consolidada.OlharCameraPitch))
                {
                    consolidada.OlharCameraPitch = entradaDesktop.OlharCameraPitch;
                }
                if (Mathf.Abs(entradaDesktop.ZoomCamera) > Mathf.Abs(consolidada.ZoomCamera))
                {
                    consolidada.ZoomCamera = entradaDesktop.ZoomCamera;
                }
                if (entradaDesktop.AceleradorGatilho > consolidada.AceleradorGatilho)
                {
                    consolidada.AceleradorGatilho = entradaDesktop.AceleradorGatilho;
                }
                if (entradaDesktop.AlternarModoCamera) consolidada.AlternarModoCamera = true;
                if (entradaDesktop.Interagir) consolidada.Interagir = true;
                if (entradaDesktop.AlternarMenuHub) consolidada.AlternarMenuHub = true;
                if (entradaDesktop.ResetCheckpoint) consolidada.ResetCheckpoint = true;
                consolidada.TemEntradaAtiva = true;
            }

            return consolidada;
        }

        /// <summary>
        /// Dispara pulso háptico em todos os dispositivos táteis conectados.
        /// </summary>
        public void DispararHaptics(float amplitude, float duracao)
        {
            m_ProvedorVR.EnviarPulsoHaptico(amplitude, duracao);
            m_ProvedorGamepad.EnviarPulsoHaptico(amplitude, duracao);
        }
    }
}

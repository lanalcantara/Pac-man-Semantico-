# 🥽 MANUAL DE HANDOVER E MIGRAÇÃO: PAC-MAN SEMÂNTICO VR
## Transição Completa de Desenvolvimento para MacBook (macOS) com Antigravity

Este documento contém **todos os dados, credenciais, repositórios, configurações de engine, variáveis de ambiente e o prompt mestre turnkey** para permitir que outro assistente Antigravity em um **MacBook (Apple Silicon M1/M2/M3/M4 ou Intel)** configure o ambiente do zero, baixe o projeto e continue o desenvolvimento sem interrupções.

---

## 1. Dados Centrais do Projeto e Repositórios

| Atributo | Valor / Configuração |
| :--- | :--- |
| **Nome do Projeto** | Pac-Man Semântico VR |
| **Organização / Empresa** | Cin-UFPE (`com.cinufpe`) |
| **Package Identifier** | `com.cinufpe.pacmansemanticovr` |
| **Activity Principal** | `com.unity3d.player.UnityPlayerGameActivity` |
| **Repositório Git (GitHub)** | `https://github.com/lanalcantara/Pac-man-Semantico-.git` |
| **Redirecionamento Histórico**| `https://github.com/lanalcantara/Pac-man-Semantico-Privado.git` |
| **Branch Principal** | `main` |
| **Último Commit Base** | `397c880` (6 commits estruturados com histórico completo) |
| **Versão Exata do Unity** | **Unity 6 - `6000.6.7f1`** (Revision `b22767f04f04`) |
| **Pipeline Gráfico (SRP)** | Universal Render Pipeline (URP Mobile) |
| **Cena Principal** | `Assets/Scenes/DemoWeb/CenaDemoWeb.unity` |
| **Alvo Principal de Execução**| **Meta Quest Standalone (Quest 2, Quest 3, Quest 3S, Quest Pro)** |
| **Arquitetura Android** | `ARM64` (IL2CPP) |
| **Modo Estéreo XR** | `Multiview` (Single-Pass Instanced) |
| **Espaço de Cores** | `Linear` (Obrigatório para VR) |

---

## 2. Resumo da Arquitetura & O Que Foi Desenvolvido

1. **Menu Hub e Telas com Gaze-Tracking 360° em VR (Zero Enjoo)**:
   - Os Canvas (`HubInicialAmbiente.cs`, `HUDGameplayPeriferico.cs`, `PainelGrafoSemanticoProtege.cs`) rodam em `RenderMode.WorldSpace`, com escala `0.0018f` e profundidade de `1.90m`.
   - Utilizam interpolação contínua em `LateUpdate` (`Vector3.Lerp` e `Quaternion.Slerp` a 14f) alinhados à rotação da cabeça. Nunca ficam "colados na cara" (face-glued), respeitando o Reflexo Vestíbulo-Ocular (VOR).
   - HUD periférico de partida: vidas em corações (`♥♥♥`), pontuação, orbes capturadas (`0/12`) e indicador de perigo no canto superior esquerdo da visão.

2. **Zero Cache & Reinício Limpo de Partida**:
   - `GerenciadorCenarioSemantico.ReiniciarLabirintoEInstancias()` destrói qualquer orbe antiga da hierarquia, zera `m_IndiceOrbeAtual = 0` e contadores estáticos (`InstanciaSemanticaBase.totalColetadas = 0`).
   - Cada jogo começa rigorosamente da Orbe #1 de 12. Capturar orbes destrói o GameObject (`Destroy(gameObject, 0.15f)`), evitando que a vitória seja disparada antecipadamente.

3. **Locomoção por Controle Touch Plus (Combo da Mão Direita)**:
   - **Gatilho Indicador Direito (`CommonUsages.trigger`)**: Acelerador analógico contínuo. Puxar o gatilho projeta o Pac-Man diretamente para a frente na direção apontada pela cabeça/visão.
   - **Analógico Direito (`CommonUsages.primary2DAxis`)**: Controla a rotação (yaw) da câmera e do corpo.
   - **Analógico Esquerdo**: Movimentação omnidirecional / strafe.
   - **Física Anti-Snagging**: `CapsuleCollider` com `PhysicMaterial` sem atrito (`dynamicFriction: 0`, `staticFriction: 0`, combine: `Minimum`) e velocidade aplicada diretamente em `Rigidbody.linearVelocity` (FixedUpdate), permitindo deslizar livremente nas paredes do labirinto sem grudar.

4. **Otimizações Gráficas para Meta Quest (Adreno TBDR)**:
   - `Mobile_RPAsset.asset`: 4x MSAA acelerado via hardware no chip GMEM.
   - Desabilitados blits desnecessários (`supportsCameraDepthTexture = false`, `supportsCameraOpaqueTexture = false`).
   - Fixed Foveated Rendering (FFR) dinâmico nível 3 (High).
   - Texturas em formato ASTC com Mipmaps ativos.

5. **Skill Global `VRGAMESDEV`**:
   - Criada no diretório global `~/.gemini/config/skills/vrgamesdev/` contendo documentação completa, scripts e exemplos reutilizáveis para desenvolvimento de VR no Meta Quest.

---

## 3. Passo a Passo de Instalação e Preparação no MacBook (macOS)

### 3.1. Instalar Ferramentas de Linha de Comando (Terminal Mac)
Abra o Terminal no macOS e execute:

```bash
# 1. Instalar Homebrew (se ainda nao tiver)
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"

# 2. Instalar dependencias essenciais de desenvolvimento
brew install git git-lfs android-platform-tools dotnet-sdk

# 3. Inicializar Git LFS
git lfs install
```

### 3.2. Instalar Unity Hub e o Unity Editor 6
```bash
# Instalar Unity Hub
brew install --cask unity-hub
```
No **Unity Hub** no Mac:
1. Faça login na sua conta Unity (licença Unity Personal ou Pro).
2. Vá em **Installs** -> **Install Editor**.
3. Selecione ou baixe a versão **Unity 6 (6000.6.7f1)**.
4. **MÓDULOS OBRIGATÓRIOS PARA ADICIONAR**:
   - `Android Build Support`
   - `Android SDK & NDK Tools`
   - `OpenJDK`
   - `Mac Build Support (Apple Silicon)` (ou Intel, conforme seu Mac).

### 3.3. Configurar Variáveis de Ambiente no Mac (`~/.zshrc`)
Edite o arquivo `~/.zshrc` no Mac e adicione as seguintes linhas:

```bash
# Caminhos do Unity e Android SDK no macOS
export UNITY_PATH="/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity"
export ANDROID_HOME="$HOME/Library/Android/sdk"
export ANDROID_NDK_HOME="$ANDROID_HOME/ndk"

# Fallback para o SDK interno do Unity se o Android Studio nao estiver instalado
if [ ! -d "$ANDROID_HOME" ]; then
    export ANDROID_HOME="/Applications/Unity/Hub/Editor/6000.6.7f1/PlaybackEngines/AndroidPlayer/SDK"
fi

export PATH="$PATH:$ANDROID_HOME/platform-tools:$ANDROID_HOME/tools"
```
Recarregue o terminal:
```bash
source ~/.zshrc
```

### 3.4. Clonar o Repositório do Projeto no Mac
```bash
# Escolha o diretorio de projetos (ex: ~/Developer ou ~/Desktop)
cd ~/Developer
git clone https://github.com/lanalcantara/Pac-man-Semantico-.git
cd Pac-man-Semantico-
```

### 3.5. Abrir no Unity Editor no Mac
1. Abra o **Unity Hub**.
2. Clique em **Add** -> **Add project from disk**.
3. Selecione a pasta `Pac-man-Semantico-`.
4. Certifique-se de que a versão selecionada seja **6000.6.7f1**.
5. Abra o projeto e aguarde a primeira importação de assets (Package Manager e Asset Database).
6. Abra a cena: `Assets/Scenes/DemoWeb/CenaDemoWeb.unity`.

---

## 4. Conectar e Desenvolver com o Meta Quest no Mac

### 4.1. Habilitar Modo de Desenvolvedor no Quest
1. No smartphone (app Meta Horizon), conecte ao óculos -> **Configurações do Headset** -> **Modo Desenvolvedor** -> Ativar.
2. Plugue o cabo USB-C no MacBook e no Meta Quest.
3. Coloque o óculos e selecione **"Sempre permitir a partir deste computador"** na caixa de diálogo de depuração USB.

### 4.2. Testar Conexão ADB no Mac
```bash
adb devices -l
# Deve listar: <serial> device product:panther model:Quest_3 ou model:Quest_3S
```

### 4.3. Script de Build e Deploy Rápido no Mac (`build_quest_mac.sh`)
Crie ou execute no terminal do Mac na raiz do projeto:

```bash
#!/bin/bash
set -e

UNITY_APP="/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity"
PROJECT_DIR="$(pwd)"
OUTPUT_DIR="$PROJECT_DIR/Builds/Quest3"
APK_PATH="$OUTPUT_DIR/PacManSemantico_Quest3.apk"

mkdir -p "$OUTPUT_DIR"

echo "=== [VRGAMESDEV] Compilando APK Standalone para Meta Quest no Mac ==="
"$UNITY_APP" -batchmode -quit -projectPath "$PROJECT_DIR" -executeMethod BuildQuest3.BuildMetaQuest3APK -logFile "$HOME/Library/Logs/Unity/unity_build.log"

echo "=== [VRGAMESDEV] Compilacao concluida com sucesso! ==="

# Instalar via ADB se o Quest estiver conectado
if adb devices | grep -q "device$"; then
    echo "=== Instalando no Meta Quest via ADB ==="
    adb install -r -d "$APK_PATH"
    adb shell input keyevent 224 # Acorda telas do headset
    adb shell am start -S -n com.cinufpe.pacmansemanticovr/com.unity3d.player.UnityPlayerGameActivity
    echo "=== Jogo instalado e iniciado com sucesso no Meta Quest! ==="
else
    echo "Aviso: Meta Quest nao detectado via USB. APK disponivel em: $APK_PATH"
fi
```
Dê permissão de execução:
```bash
chmod +x build_quest_mac.sh
```

---

## 5. Instalar a Skill Global `VRGAMESDEV` no Mac

Para que o Antigravity no Mac tenha todas as capacidades de desenvolvimento para VR e Meta Quest, crie a pasta da skill global no Mac:

```bash
mkdir -p ~/.gemini/config/skills/vrgamesdev
```

Copie os arquivos da skill para o MacBook (ou clone/baixe da pasta `~/.gemini/config/skills/vrgamesdev/` da máquina Windows). A estrutura deve ser:
- `~/.gemini/config/skills/vrgamesdev/SKILL.md`
- `~/.gemini/config/skills/vrgamesdev/references/*.md`
- `~/.gemini/config/skills/vrgamesdev/examples/*.cs`
- `~/.gemini/config/skills/vrgamesdev/scripts/*.ps1` / `*.sh`

---

## 6. PROMPT MESTRE DE HANDOVER PARA O ANTIGRAVITY NO MACBOOK

Copie e cole o bloco abaixo integralmente para iniciar o Antigravity no seu MacBook:

```text
Olá, Antigravity! Você está assumindo o projeto "Pac-Man Semântico VR" para Meta Quest (Quest 2, 3, 3S e Pro), anteriormente desenvolvido em Windows e agora transferido integralmente para este MacBook.

Aqui estão os dados centrais do projeto e instruções para você assumir o controle imediatamente:

1. REPOSITÓRIO E CÓDIGO FONTE:
- Repositório Git: https://github.com/lanalcantara/Pac-man-Semantico-.git
- Branch: main (commit 397c880 ou superior)
- Pacote Android: com.cinufpe.pacmansemanticovr
- Activity Principal: com.unity3d.player.UnityPlayerGameActivity
- Versão do Unity Engine: Unity 6 (6000.6.7f1) com suporte a Android (ARM64, OpenXR, URP).
- Cena principal: Assets/Scenes/DemoWeb/CenaDemoWeb.unity

2. ARQUITETURA CONSTRUÍDA E REQUISITOS FUNDAMENTAIS:
- Locomoção XR: O controle da mão direita usa combo onde o Analógico Direito gira a câmera/corpo (Yaw) e o Gatilho do Indicador Direito (CommonUsages.trigger) funciona como acelerador contínuo na direção para onde a visão aponta. O analógico esquerdo faz movimentação/strafe omnidirecional. A física do jogador usa CapsuleCollider com PhysicMaterial de atrito 0.0 para deslizar suavemente nas paredes sem travar.
- Menus e Telas 360° em VR: Todos os menus (HubInicialAmbiente, modais de Vitória, Game Over, Contagem Regressiva e PainelGrafoSemanticoProtege) flutuam em WorldSpace a 1.9m com escala 0.0018f e usam amortecimento contínuo em LateUpdate (Vector3.Lerp/Quaternion.Slerp a 14f). Eles acompanham suavemente para onde o usuário olha, evitando "face-glued UI" (enjoo por falta de VOR).
- HUD Periférico: Telemetria (vidas em corações ♥♥♥, pontuação e orbes) flutua estavelmente no canto superior esquerdo da visão sem obstruir o jogo.
- Zero Cache / Reset Limpo: Cada nova partida destrói orbes antigas e zera contadores estáticos, iniciando rigorosamente da Orbe #1 de 12. Orbes capturadas são imediatamente destruídas do labirinto (Destroy com 0.15s).
- Gráficos Mobile: URP Mobile com 4x MSAA no GMEM, Multiview stereo (single-pass instanced), Fixed Foveated Rendering dinâmico nível 3 (High).

3. SUAS PRIMEIRAS TAREFAS NO MACBOOK:
a) Verifique se o Unity 6000.6.7f1, o Unity Hub, o .NET SDK e o ADB (Android platform-tools) estão instalados e configurados no PATH.
b) Verifique o status do repositório local em git status e confirme que todos os arquivos de Assets/, Packages/ e ProjectSettings/ estão intactos.
c) Se o Meta Quest estiver conectado via USB-C, verifique a conexão com "adb devices -l" e confirme se ele responde.
d) Certifique-se de que a skill global "vrgamesdev" esteja configurada em ~/.gemini/config/skills/vrgamesdev/ para apoiar o desenvolvimento contínuo de VR.

Leia o documento HANDOVER_MIGRACAO_MACBOOK.md presente na raiz do projeto para todos os detalhes e confirme quando o ambiente estiver validado e pronto para continuarmos codificando!
```

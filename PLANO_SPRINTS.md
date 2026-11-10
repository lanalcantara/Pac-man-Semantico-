# Plano de Sprints e Entregáveis
## Pac-Man Semântico (VirtOnto) - CIn-UFPE
**Documento Vivo de Gestão e Escopo**  
**Data de Criação:** 07 de Novembro de 2026  
**Ambiente:** Unity 6 LTS (`6000.6.7f1`), C# 12, Universal Render Pipeline (URP), OpenXR  
**Hardware Alvo Primário:** Meta Quest 3 e Meta Quest 3S (Standalone APK ARM64)  
**Hardware Alvo Secundário:** PC Desktop (Windows Standalone) e WebGL  

---

### Visão Geral da Metodologia

O desenvolvimento é estruturado em **5 Sprints incrementais**. A evolução prioriza a **robustez da fundação técnica** (física, perspectiva em primeira pessoa, proporcionalidade arquitetônica verossímil, performance Zero-GC e compatibilidade de hardware) antes de qualquer detalhamento de assets, design de arte ou regras complexas de interface.

---

### 🗺️ Visão Macro das Sprints

```
[ Sprint 1: Fundação & Mobilidade ] ──▶ [ Sprint 2: Grafo & Minimapa ] ──▶ [ Sprint 3: Entidades & Regras ] ──▶ [ Sprint 4: HUD & Áudio ] ──▶ [ Sprint 5: Deploy & Profiling ]
  - Perspectiva 1ª Pessoa Humana (1:1)    - Minimapa estilo Protégé              - Gemas simples ("só bolas")           - HUD imersivo e limpo         - Build APK Quest 3/3S
  - Proporcionalidade Verossímil          - Rastreio de rota espacial            - 4 Fantasmas sem poluição             - Áudio espacial 3D            - Profiling de 90 FPS
  - Física, Rigidbody, Wall Sliding       - Desatamento de nós conceituais       - 3 Vidas + Instâncias ABox            - Hápticos nos controles       - Testes de campo VR
  - Performance Zero-GC (Sem lixo)        - Conexão TBox / ABox                  - Inferência SWRL sem atrito           - Juice e polimento
  - Sensores & Controles Híbridos
```

---

### 📌 Detalhamento das Sprints

#### 🏃 Sprint 1 (Concluída e Pronta para Validação): Fundação, Mobilidade, Câmeras Híbridas, Hub Inicial e Arquitetura
**Foco:** Garantir que o ambiente base, a movimentação, os modos de câmera (1ª Pessoa 1:1, 3ª Pessoa e Tática), a tela inicial (Hub Inicial) e os controles rodem de forma impecável, com zero lixo de memória (*Zero Garbage Collection*), arquitetura desacoplada e suporte híbrido completo a acessórios e sensores (Meta Quest 3/3S Touch Plus, Gamepad Xbox e Teclado/Mouse).

* **Entregáveis Implementados:**
  1. **Hub Inicial e Gerenciador de Ambiente (`HubInicialAmbiente`):**
     - Tela inicial de configuração pré-jogo em *dark glassmorphism*.
     - Botões funcionais: `> INICIAR PARTIDA (PLAY) <`, `[ RESETAR CHECKPOINT ]`, `[ REINICIAR PARTIDA (RELOAD) ]`.
     - Seletor de Câmera dinâmico em tempo real: `1ª Pessoa 1:1`, `3ª Pessoa Dinâmica` e `Tática Aérea`.
     - Controle e ajuste fino de sensibilidade de rotação do mouse/stick.
     - Suporte a pausa e reabertura do menu a qualquer instante com a tecla `[ESC]`.
  2. **Perspectiva em Primeira Pessoa Imersiva (Escala Humana 1:1):**
     - Altura dos olhos calibrada em $1.68\text{m}$, posicionando o jogador com percepção autêntica de presença física.
     - Proporcionalidade arquitetônica: Paredes de $2.60\text{m}$ erguem-se quase $1\text{m}$ acima da visão do usuário, com espessura maciça de $1.20\text{m}$ e corredores de $\approx 2.20\text{m}$ de largura confortável.
     - *Near Clip Plane* fixado em $0.02\text{m}$ ($2\text{cm}$) eliminando qualquer corte geométrico de paredes ao se aproximar ou encostar.
     - Campo de Visão (FoV) de $75^\circ$ no Desktop e $110^\circ$ nativo binocular no Meta Quest 3/3S.
     - Desativação automática dos renderizadores de malha do avatar `Visual_PacMan` no modo 1ª pessoa, reativando-os em 3ª pessoa.
  3. **Mobilidade Física Fluida com Atrito Zero (Zero Snagging / Deslizamento Perfeito):**
     - Resolução definitiva de travamentos com desacoplamento dos scripts concorrentes.
     - Aplicação de `PhysicsMaterial` de **Atrito Zero** (`dynamicFriction = 0`, `staticFriction = 0`) no `CapsuleCollider`, garantindo deslizamento suave e contínuo ao esbarrar ou contornar qualquer parede ou esquina.
     - Aplicação direta de velocidade no `Rigidbody.linearVelocity` nos eixos X e Z, proporcionando resposta ágil e árcade sem lentidão ou inércia arrastada.
     - Ponto de spawn seguro calibrado no centro do corredor aberto $(0, 0, -5.0)$.
  4. **Arquitetura de Entrada Desacoplada (`IProvedorEntradaXR`):**
     - Criação do módulo `PacMan.XR.IProvedorEntradaXR` com struct de valor `DadosEntradaJogador` (Zero-GC, 0 bytes alocados no heap).
     - Provedores modulares implementados para VR (Quest Touch Plus), Gamepad Xbox e Teclado/Mouse de Desktop.
     - `GerenciadorEntradaUnificada`: Multiplexa e consolida entradas de forma automática e sem alocação.
  5. **Performance & Zero-GC (Anti-Lixo de Memória):**
     - Substituição de todas as varreduras de cena por buffers estáticos pré-alocados (`OverlapSphereNonAlloc`).
     - Compilação com **0 Erros** no Unity 6 (`6000.6.7f1`).

* **Critério de Aceite para Transição à Sprint 2:**
  - Validação da jogabilidade e sensação de escala e presença pelo usuário no Editor.
  - Aprovação do usuário para avançar à Sprint 2 (Grafo Semântico e Minimapa Lateral).

---

#### 🏃 Sprint 2 (Concluída e Validada): Grafo Semântico, Wayfinding e Minimapa Lateral
**Foco:** Substituir o chat legado por uma visualização ontológica viva no canto da tela, inspirada no visualizador de grafos do Protégé.

* **Entregáveis Implementados:**
  1. **Minimapa do Grafo Ontológico (`PainelGrafoSemanticoProtege`):** Grafo 2D em tempo real com nós conceituais hierárquicos e arestas conexas, iluminando progressivamente os conceitos conforme as orbes são coletadas.
  2. **Spawning Sequencial de Orbes (1 a 1):** Orbes surgem no labirinto de forma sequencial com radar tático no minimapa apontando sua localização exata.
  3. **Banner Semântico Superior:** Exibição da descrição lógica do conceito ontológico correspondente à orbe capturada.
  4. **Tela de Vitória com Grafo Iluminado Completo:** Exibição triunfante do fluxograma ontológico integral e estatísticas ao coletar todos os 12 axiomas.

---

#### 🏃 Sprint 3 (Concluída e Validada): Entidades Ontológicas, Contato Estrito e Mecânicas de Poder
**Foco:** Reintroduzir os agentes e itens de forma limpa, funcional e semanticamente correta.

* **Entregáveis Implementados:**
  1. **Contato Físico Estrito (Hitbox Calibrada $\le 0.40\text{m}$):** Orbes só são consumidas mediante encosto físico real do jogador, sem raio de atração ou coleta à distância.
  2. **Mecânica de Power Pellets (1 a cada 3 Orbes):** A cada 3 orbes coletadas, o jogador recebe o poder de devorar os fantasmas por 8 segundos.
  3. **Modo Frightened dos Fantasmas:** Transição para azul, velocidade de fuga reduzida e reversão de papéis (o Pac-Man caça os fantasmas).
  4. **Efeito Visual de Explosão e Flash:** Partículas e brilho visual ao devorar os fantasmas.
  5. **Temporizador de Liberação da Gaiola (0s, 4s, 8s, 12s):** Saída escalonada dos 4 fantasmas.
  6. **Trava de Movimento no Game Over:** Parada total imediata do jogador ao esgotar as 3 vidas.

---

#### 🏃 Sprint 4 (Concluída e Validada): Interface (HUD), Áudio Espacial e Juice
**Foco:** Interface ergonômica periférica para VR e Desktop, áudio 3D imersivo posicional e feedback sensorial.

* **Entregáveis Implementados:**
  1. **HUD Holográfico de Gameplay Periférico (`HUDGameplayPeriferico`):**
     - Painel discreto no canto superior direito da visão periférica, sem ocluir o campo visual central.
     - Indicador dinâmico de 3 Vidas com ícones visuais do Pac-Man (amarelo ouro quando ativas, cinza translúcido ao perder).
     - Contador digital de Pontuação (`PONTOS: XXXXX`) e Progresso de Orbes (`ORBES: X / 12`).
     - Barra de Poder (Power Pellet Mode): contagem regressiva em tempo real com preenchimento horizontal e destaque para fantasmas azuis/vulneráveis.
     - Sensor de Proximidade: badge de alerta pulsante quando fantasmas hostis estão a menos de 3.5m nos corredores.
  2. **Áudio Espacial 3D e Sons Procedurais:**
     - Sons posicionais 3D nos 4 Fantasmas (`ObstaculoInvalidoSemantico`): loop espectral contínuo com atenuação logarítmica (1.5m a 12m), permitindo rastrear a aproximação dos fantasmas pela direção do som nos fones de ouvido.
     - Modulação do áudio dos fantasmas em tempo real: tom grave lento quando vulneráveis (azul), e tom agudo acelerado quando devorados e retornando à gaiola como olhos.
     - Sons procedurais de síntese na coleta de orbes ontológicas (chime harmônico) e coleta de Power Pellet (fanfarra dourada ascendente).
     - Alerta sonoro de dano do reasoner e som crocante de devorar fantasma com popup 3D (`+200`, `+400`).
  3. **Feedback Háptico Refinado:**
     - Vibração diferenciada nos controles Quest Touch Plus e Gamepad:
       - Coleta de orbe: pulso ágil suave (0.30f, 0.08s).
       - Coleta de Power Pellet: pulso duplo triunfante (0.75f, 0.25s).
       - Devorar fantasma: impacto crocante e potente (0.85f, 0.30s).
       - Colisão/dano sofrido: tremor pesado (1.0f, 0.45s).
       - Batimento cardíaco de proximidade (0.18f, 0.06s a cada 0.85s) quando um fantasma hostil está nos corredores a < 3.2m.
  4. **Juice e Polimento:**
     - Zero-GC em frame updates.

---

#### 🏃 Sprint 5 (Concluída e Entregue): Build Standalone APK Meta Quest 3/3S & OpenXR
**Foco:** Empacotamento para Meta Quest 3 e 3S, pipeline IL2CPP ARM64 nativo, OpenXR e disponibilização do executável.

* **Entregáveis Implementados:**
  1. **Configuração de Plataforma Android/OpenXR:**
     - Target Architecture: ARM64 (`AndroidArchitecture.ARM64`).
     - Scripting Backend: IL2CPP nativo (`ScriptingImplementation.IL2CPP`).
     - Min Android SDK: API Level 29 (Android 10), Target SDK: API Level 36 (Android 14/15).
     - API Gráfica Primária: Vulkan de alta performance com Color Space Linear.
     - OpenXR Feature Set: `MetaQuestFeature`, `OculusTouchControllerProfile`, `MetaQuestTouchPlusControllerProfile` e `MetaQuestTouchProControllerProfile` ativados.
     - 6DoF Head Tracking: `TrackedPoseDriver` configurado para a Câmera Principal (HMD Center Eye) e controladores esquerdo/direito.
  2. **Compilação e Executável Gerado:**
     - **Arquivo gerado:** `PacManSemantico_Quest3.apk` (49.57 MB).
     - **Localização 1 (Área de Trabalho):** `C:\Users\testcb\Desktop\PacManSemantico_Quest3.apk`
     - **Localização 2 (Projeto):** `C:\Users\testcb\Pac-man-Semantico-Privado\Builds\Quest3\PacManSemantico_Quest3.apk`
  3. **Documentação de Instalação e Teste:**
     - Criado o artefato `GUIA_INSTALACAO_META_QUEST.md` com instruções passo a passo para SideQuest, Meta Quest Developer Hub e ADB.

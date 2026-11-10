# Documento de Requisitos do Produto (PRD)
## Pac-Man Semântico (VirtOnto) - CIn-UFPE
**Versão:** 2.1 (Sprint 3 - Movimentação, Câmera e VR Nativo Quest 3 / 3S)  
**Data:** Novembro de 2026  
**Ambiente:** Unity 6 LTS (`6000.6.7f1`), Universal Render Pipeline (URP), OpenXR  

---

### 1. Visão Geral e Objetivos do Projeto
O **Pac-Man Semântico (VirtOnto)** é um ambiente interativo acadêmico e lúdico desenvolvido no Centro de Informática da Universidade Federal de Pernambuco (CIn-UFPE) no contexto de **Realidade Virtual e Aumentada (RVA)**.

O objetivo do projeto é unir a jogabilidade arcade clássica do **Pac-Man** com raciocínio e modelagem semântica baseada em **Ontologias OWL** e regras **SWRL**, oferecendo:
1. Ensino interativo de conceitos de Parasitologia médica (classes taxonômicas, ciclos de vida, hospedeiros definitivos e intermediários).
2. Validação ontológica em tempo real: o consumo de instâncias e a interação com agentes hostis correspondem à satisfatibilidade de axiomas na Lógica de Descrição $\mathcal{ALCQ}(\mathcal{D})$.
3. Suporte multiplataforma com foco principal na **execução nativa em Realidade Virtual (VR 360° Standalone)** no **Meta Quest 3** e **Meta Quest 3S**, mantendo compatibilidade de testes em Desktop Windows e WebGL.

---

### 2. Identidade Visual e Cenário Contemporâneo

#### 2.1 Cenário e Iluminação
- **Superfície do Labirinto:** Piso fosco verde vibrante inspirado em grama de campo de futebol (`Mat_Chao_GramaVerde`).
- **Paredes:** Estruturas modulares em cinza metálico/arquitetônico claro com acabamento industrial suave (`Mat_Parede_CinzaMetalico`), com altura máxima padronizada em $2.60\text{m}$.
- **Iluminação Diurna (Tarde / Golden Hour):** Luz solar direcional quente (intensidade $1.65$, ângulo diagonal suave a $34^\circ / 48^\circ$), com sombras suaves atenuadas em $0.32$ para garantir alta visibilidade dentro dos corredores sem pontos cegos escuros.
- **Ambiente e Céu:** Céu limpo azul diurno suave, com iluminação de ambiente no modo Trilight (luz celeste azulada, horizonte quente dourado e reflexo verde do gramado).

#### 2.2 Pac-Man (Protagonista / Jogador)
- **Modelagem Procedural 3D:** Esfera articulada amarela com mandíbulas superior e inferior móveis e olhos expressivos.
- **Escala e Física:** Proporção física $0.80$, controlado por `Rigidbody` dinâmico contínuo travado no solo ($y = 0.0\text{m}$) com restrições estritas de rotação e do eixo vertical (`FreezePositionY`).
- **Orientação de Movimento:** Rotação visual suave voltada para o vetor de deslocamento.

#### 2.3 Fantasmas e Gemas Ontológicas
- **Fantasmas Canônicos:** 4 agentes com inteligência de patrulha autônoma via `NavMeshSurface` (Blinky, Pinky, Inky, Clyde), com estados hostil e vulnerável temporariamente desacoplados para validação de movimentação.
- **Gemas e Power Pellets:** Representações espaciais de instâncias taxonômicas consumíveis instantaneamente pela boca do avatar.

---

### 3. Sistema de Câmera e Perspectivas (Zero Tremedeira)

| Modo / Nível de Zoom | Parâmetros de Câmera | Experiência de Jogabilidade |
|---|---|---|
| **Visão 3ª Pessoa Próxima (Zoom 0.0)** | Altura $3.50\text{m}$, Distância $2.20\text{m}$, Pitch $\approx 57^\circ$ | Foco próximo e imersivo no Pac-Man, com visão limpa do corredor à frente. Câmera sobrevoa acima das paredes ($2.60\text{m}$), garantindo zero oclusão e zero tremedeira. |
| **Visão Tática Panorâmica (Zoom 1.0)** | Altura $6.50\text{m}$, Distância $4.50\text{m}$, Pitch $\approx 57^\circ$ | Visão ampla dos corredores adjacentes e bifurcações, permitindo planejamento tático de rotas. |
| **Transição Contínua (Zoom Fluido)** | Interpolação via `Mathf.Lerp` / `SmoothDamp` | Zoom contínuo sem cortes de tela através dos gatilhos (RT/LT) do joystick Xbox, bumpers (RB/LB), roda do mouse ou thumbstick VR. |
| **Imersão VR 360° (Meta Quest 3/3S)** | Rastreamento Roomscale 6DoF via OpenXR | Liberdade para movimentar e girar a cabeça naturalmente em 360° enquanto acompanha o labirinto. |

#### Regras de Rotação e Controles
- **Rotação Soberana (Yaw Livre):** O ângulo da câmera é determinado exclusivamente pelo jogador (analógico direito, mouse ou teclas Q/E). Não há auto-alinhamento forçado que dispute o controle com o jogador ao caminhar para os lados.
- **Controles Unificados:** O direcional de movimento (WASD ou stick esquerdo) responde sempre de forma relativa ao que é exibido na tela (empurrar para cima move para frente na visão do jogador), independente do nível de zoom.

---

### 4. Plataformas de Hardware Homologadas

1. **Meta Quest 3 (VR Nativo Standalone):**
   - **Chipset:** Qualcomm Snapdragon XR2 Gen 2, 8 GB RAM, GPU Adreno 740.
   - **Display / Óptica:** Lentes Pancake, dual displays 4K+ ($2064 \times 2208$ px por olho), IPD contínuo.
   - **Formato:** Arquivo executável local `.apk` (Android 12L+, ARM64, IL2CPP).
2. **Meta Quest 3S (VR Nativo Standalone):**
   - **Chipset:** Qualcomm Snapdragon XR2 Gen 2, 8 GB RAM, GPU Adreno 740.
   - **Display / Óptica:** Lentes Fresnel, display único ($1832 \times 1920$ px por olho).
   - **Formato:** Executa **o mesmo arquivo `.apk`** do Quest 3 sem necessidade de compilação separada.
3. **PC Desktop / WebGL:**
   - Ambiente de desenvolvimento, depuração rápida e validação de regras ontológicas via browser.

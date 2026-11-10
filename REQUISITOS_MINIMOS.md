# Requisitos Mínimos do Sistema e Guia de Instalação
## Pac-Man Semântico (VirtOnto) - CIn-UFPE
**Atualização:** Novembro de 2026  
**Ambiente de Desenvolvimento:** Unity 6 LTS (`6000.6.7f1`)  

Este documento descreve os requisitos de hardware, software e o procedimento para preparar o ambiente de desenvolvimento e compilação do executável local (APK) para os óculos **Meta Quest 3** e **Meta Quest 3S**.

---

### 1. Requisitos de Hardware

#### 1.1 Ambiente de Desenvolvimento (Desktop / Editor Unity)
| Componente | Requisito Mínimo | Requisito Recomendado |
|---|---|---|
| **Processador (CPU)** | Intel Core i5 8ª Geração / AMD Ryzen 5 2600 (4 núcleos / 8 threads) | Intel Core i7 12ª Geração / AMD Ryzen 7 5700X ou superior |
| **Memória RAM** | 16 GB DDR4 | 32 GB DDR4/DDR5 |
| **Placa de Vídeo (GPU)** | NVIDIA GeForce GTX 1060 (6 GB) / AMD Radeon RX 580 (DirectX 11 / 12) | NVIDIA GeForce RTX 3060 (12 GB) ou superior |
| **Armazenamento** | 25 GB livres em SSD | 50 GB livres em SSD NVMe M.2 rápido |
| **Sistema Operacional** | Windows 10 (64-bit, versão 21H2+) ou Windows 11 | Windows 11 (64-bit) |

#### 1.2 Dispositivos de Realidade Virtual (Execução Nativa Standalone)
- **Headsets Homologados:**
  - **Meta Quest 3:** 8 GB RAM, Snapdragon XR2 Gen 2, lentes Pancake, displays 4K+.
  - **Meta Quest 3S:** 8 GB RAM, Snapdragon XR2 Gen 2, lentes Fresnel.
- **Conectividade:**
  - Cabo USB-C para transferência inicial do arquivo `.apk` e depuração via `adb`.
  - Rede local Wi-Fi 5 GHz / Wi-Fi 6 para comunicação opcional com a ontologia no servidor Protégé/Java do PC.

---

### 2. Requisitos de Software e Módulos do Unity

1. **Unity Editor:**
   - **Versão:** `Unity 6000.6.7f1` (Unity 6).
   - **Módulos Obrigatórios no Unity Hub:**
     - ✅ **Android Build Support**
     - ✅ **Android SDK & NDK Tools**
     - ✅ **OpenJDK**
     - ✅ **Windows Build Support**
     - ✅ **WebGL Build Support**
2. **Ambiente .NET / C#:**
   - .NET 8.0 SDK ou .NET 9.0 SDK configurado nas variáveis de ambiente do sistema (`PATH`).
3. **Ferramentas de Depuração Android:**
   - `adb` (Android Debug Bridge), instalado automaticamente junto ao Android SDK do Unity ou via Android Platform-Tools.
4. **Modo Desenvolvedor no Meta Quest:**
   - Modo de desenvolvedor ativado no aplicativo móvel da Meta para permitir carregamento de aplicativos via USB (*sideloading* de APK).

---

### 3. Procedimento de Preparação do Módulo Android no Unity Hub

1. Abra o **Unity Hub**.
2. Na aba lateral esquerda, clique em **Installs**.
3. Localize o card do **Unity 6000.6.7f1**, clique no ícone de engrenagem ⚙️ e selecione **Add modules**.
4. Marque a caixa de seleção **Android Build Support** e confirme que os componentes filhos estão selecionados:
   - *Android SDK & NDK Tools*
   - *OpenJDK*
5. Clique em **Install** e aguarde a conclusão da instalação.

---

### 4. Executando o Projeto no Unity

1. No Unity Hub, abra o projeto selecionando a pasta raiz clonada.
2. Na janela de pastas do Unity (`Project`), abra a cena:
   `Assets` > `Scenes` > `DemoWeb` > **`CenaDemoWeb.unity`**
3. Para testar no PC, clique no botão **`Play`** (`▶`):
   - Controle a câmera com o mouse ou analógico direito do Xbox.
   - Movimente o avatar com **WASD** ou analógico esquerdo.
   - Ajuste o zoom através dos gatilhos (RT/LT), botões RB/LB ou roda do mouse.

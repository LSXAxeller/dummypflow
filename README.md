
<div align="center">
    <img src="./ProseFlow.UI/Assets/logo.svg" alt="Project Logo" width="256" height="256">

# ProseFlow

**Your Universal AI Text Processor, Powered by Local and Cloud LLMs.**

[![Build Status](https://github.com/LSXPrime/ProseFlow/actions/workflows/build.yml/badge.svg)](https://github.com/LSXPrime/SoundFlow/actions/workflows/build.yml) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT) [![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0) [![Release](https://img.shields.io/github/v/release/LSXPrime/ProseFlow?color=black)](https://github.com/LSXPrime/ProseFlow/releases)

ProseFlow is a cross-platform desktop application that integrates powerful AI text processing into your daily workflow. With a simple hotkey, you can access a menu of customizable AI actions to proofread, summarize, refactor, or transform text in *any* application—be it your code editor, browser, or word processor.

Its unique hybrid engine allows you to seamlessly switch between the world's best cloud-based LLMs and private, offline-capable models running directly on your own hardware.

---

[![Stand With Palestine](https://raw.githubusercontent.com/TheBSD/StandWithPalestine/main/banner-no-action.svg)](https://thebsd.github.io/StandWithPalestine)
  <p><strong>This project stands in solidarity with the people of Palestine and condemns the ongoing violence and ethnic cleansing by Israel. We believe developers have a responsibility to be aware of such injustices.</strong></p>

</div>

---

### Screenshots

<table>
  <tr>
    <td align="center"><strong>Floating Action Menu</strong></td>
    <td align="center"><strong>Comprehensive Dashboard</strong></td>
  </tr>
  <tr>
    <td><img src="https://raw.githubusercontent.com/LSXPrime/ProseFlow/master/assets/screenshot-menu.png" alt="Floating Action Menu"></td>
    <td><img src="https://raw.githubusercontent.com/LSXPrime/ProseFlow/master/assets/screenshot-dashboard.png" alt="Dashboard"></td>
  </tr>
  <tr>
    <td align="center"><strong>Action Management</strong></td>
    <td align="center"><strong>Local Model Library</strong></td>
  </tr>
  <tr>
    <td><img src="https://raw.githubusercontent.com/LSXPrime/ProseFlow/master/assets/screenshot-actions.png" alt="Action Management"></td>
    <td><img src="https://raw.githubusercontent.com/LSXPrime/ProseFlow/master/assets/screenshot-models.png" alt="Local Model Library"></td>
  </tr>
</table>

---

### ✨ Features

ProseFlow is packed with features designed for power, privacy, and productivity.

#### 🚀 Core Workflow
*   **Global Hotkey Activation:** Access ProseFlow from any application with a customizable system-wide hotkey.
*   **Floating Action Menu:** An elegant, searchable menu of your AI actions appears right where you need it.
*   **Smart Paste:** Assign a dedicated hotkey to your most frequent action for one-press text transformation.
*   **Flexible Output:** Choose to have results instantly replace your text or open in an interactive window for review.
*   **Iterative Refinement:** Conversationally refine AI output in the result window until it's perfect.
*   **Context-Aware Actions:** Configure actions to only appear when you're in specific applications.

#### 🧠 Hybrid AI Engine
*   **Run 100% Locally & Offline:** Use GGUF-compatible models on your own hardware for maximum privacy and offline access.
*   **Connect to Cloud APIs:** Integrates with OpenAI, Groq, Anthropic, Google, and any OpenAI-compatible endpoint.
*   **Intelligent Fallback Chain:** Configure multiple cloud providers. If one fails, ProseFlow automatically tries the next.
*   **Secure Credential Storage:** API keys are always encrypted and stored securely on your local machine.

#### 🛠️ Customization & Management
*   **Custom AI Actions:** Create reusable AI instructions with unique names, icons, and system prompts.
*   **Action Groups:** Organize your actions into logical groups with a drag-and-drop interface.
*   **Import & Export:** Share your action sets with others or back up your configuration to a JSON file.
*   **Action Presets:** Get started quickly by importing curated sets of actions for common tasks like writing, coding, and more.

#### 📊 Dashboard & Analytics
*   **Usage Dashboard:** Visualize your token usage over time for both cloud and local models.
*   **Performance Monitoring:** Track provider latency and tokens/second to optimize your setup.
*   **Live Hardware Monitor:** See real-time CPU, GPU, RAM, and VRAM usage when running local models.
*   **Interaction History:** Review a detailed log of all your past AI operations.

#### 💻 Platform Integration
*   **Cross-Platform:** Native support for **Windows, macOS, and Linux**.
*   **System Tray Control:** Runs quietly in the background with a tray icon for quick access to key functions.
*   **Launch at Login:** Configure ProseFlow to start automatically with your system.
*   **Guided Onboarding:** A smooth setup process for new users to get configured in minutes.

---

### 🚀 Getting Started

Pre-built binaries for Windows, macOS, and Linux are available on the **[Releases Page](https://github.com/LSXPrime/ProseFlow/releases)**.

1.  Download the appropriate package for your operating system.
2.  Install and run the application.
3.  The first time you run ProseFlow, a guided onboarding window will help you configure your first AI provider and set your global hotkey.

---

### 📖 How to Use

The core workflow is designed to be fast and intuitive:

1.  **Select Text:** Highlight any text in any application.
2.  **Press Hotkey:** Press your configured Action Menu hotkey (default is `Ctrl+J`).
3.  **Choose an Action:** The floating menu will appear. Use your mouse or arrow keys to select an action and press `Enter`.
4.  **Get Results:**
    *   For quick edits (like "Proofread"), your selected text will be replaced instantly.
    *   For longer content (like "Explain Code"), a result window will appear with the generated text.

---

### 🏗️ Architecture Overview

ProseFlow is built using a modern, layered architecture inspired by **Clean Architecture**, promoting separation of concerns, testability, and maintainability.

*   **`ProseFlow.Core`**: The domain layer. Contains the core business models, enums, and interfaces for repositories and services. It has zero dependencies on other layers.
*   **`ProseFlow.Application`**: The application layer. It orchestrates the business logic using services, DTOs, and application-specific events. It depends only on `Core`.
*   **`ProseFlow.Infrastructure`**: The infrastructure layer. Contains all implementations of external concerns, including:
    *   **Data Access:** Entity Framework Core with SQLite using the Repository & Unit of Work patterns.
    *   **AI Providers:** Implementations for Cloud (`LlmTornado`) and Local (`LLamaSharp`) providers.
    *   **OS Services:** Cross-platform hotkeys (`SharpHook`), clipboard access, and active window tracking.
*   **`ProseFlow.UI`**: The presentation layer. A cross-platform desktop application built with **Avalonia** and the **ShadUI** component library, following the **MVVM** pattern.

---

### 🔧 Building from Source

#### Prerequisites
*   .NET 8 SDK
*   Git

#### Steps
1.  Clone the repository:
    ```bash
    git clone https://github.com/LSXPrime/ProseFlow.git
    cd ProseFlow
    ```
2.  Navigate to the UI project:
    ```bash
    cd ProseFlow.UI
    ```
3.  Run the application:
    ```bash
    dotnet run
    ```

---

### 🛠️ Technology Stack

*   **UI Framework:** [Avalonia UI](https://avaloniaui.net/)
*   **UI Components:** [ShadUI.Avalonia](https://github.com/shadcn-ui/avalonia)
*   **MVVM Framework:** [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
*   **Database:** [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/) with SQLite
*   **Local LLM Engine:** [LLamaSharp](https://github.com/SciSharp/LLamaSharp)
*   **Cloud LLM Library:** [LlmTornado](https://github.com/lofcz/LlmTornado)
*   **Global Hotkeys:** [SharpHook](https://github.com/TolikPylypchuk/SharpHook)
*   **Hardware Monitoring:** [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
*   **Dependency Injection:** [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime/tree/main/src/libraries/Microsoft.Extensions.DependencyInjection)
*   **Logging:** [Serilog](https://serilog.net/)

---

### 📜 License

ProseFlow is free and open-source software licensed under the **GNU Affero General Public License v3.0 (AGPLv3)**. See the [LICENSE](LICENSE.md) file for details.

---

### 🙏 Acknowledgements

This project would not be possible without the incredible open-source libraries it is built upon. Special thanks to the teams and contributors behind Avalonia, LLamaSharp, LlmTornado, and all the other fantastic projects listed in the technology stack.
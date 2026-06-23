# CodeSnip (Avalonia)

**CodeSnip** — a snippet manager & local code runner with multi-language interpreter support and Compiler Explorer integration.

This project began as a port of the original **[CodeSnip (WPF) application](https://github.com/mx7b7/codesnip-wpf)** to **[Avalonia](https://github.com/AvaloniaUI/Avalonia)**, but has since grown beyond a direct port with new features and improvements.

![.NET](https://img.shields.io/badge/.NET-10.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)
![License: MIT](https://img.shields.io/badge/License-MIT-green)
![Status](https://img.shields.io/badge/Status-Active-success)

## ⬇️ Download

[![GitHub Release](https://img.shields.io/github/v/release/mx7b7/codesnip-avalonia?sort=semver&display_name=tag)](https://github.com/mx7b7/codesnip-avalonia/releases/latest)

---

![Slideshow GIF](images/slideshow.gif)

---

## ✨ Features

- **Local storage with SQLite**  
  All snippets are stored privately on your device.

- **Snippet organization**:
  - Hierarchical: *Language → Category → Snippet* (TreeView)
  - Filter by name or tags
  - Instant search

- **AvaloniaEdit integration**:
  - **Dual‑Mode syntax highlighting**: Support for both native XSHD and TextMateSharp tokenizers.
  - Syntax highlighting (light/dark mode)
  - Toggle single-line and multi-line comments

- **Highlighting Editor**:
  - **Dual-Mode Editing**: Tabbed interface for both visual tweaks (colors, font styles) and advanced source code editing.
  - **Direct XSHD Source Editing**: Edit raw `.xshd` XML for full control over rules, spans, and keywords.
  - **Preview on Demand**: Apply changes from the XSHD source to the main editor before saving.
  - **Validation**: Integrated validation engine checks for XML errors and XSHD schema compliance.
  - > **Note:** The Highlighting Editor is designed exclusively for editing XSHD syntax definition files.

- **Compiler Explorer (Godbolt) integration**:
  - Compile snippets without installing compilers locally
  - Support for 30+ languages
  - Add or edit available compilers
  - Select compiler and flags
  - View stdout/stderr output
  - **View assembly output** with syntax highlighting for supported languages
  - Generate shareable shortlinks to Compiler Explorer
  
 - **Local Code Execution**:
   - Run scripts using built‑in support for Shell scripts (.sh), C#, F#, PowerShell, Python, PHP, Perl, Lua, Ruby, Node.js, and Java (via `JShell`) directly using local interpreters.
   - **Dual-mode runner**: Internal execution or external native terminal.
   - **Internal Mode**: Quiet execution inside the app.
   - **External mode**: Native terminal, full interactivity, `sudo`/password input, and script arguments from the app.
     - Supported languages: C#, F#, Python, JavaScript, Lua, PowerShell, Bash, and Java.
   - > **Note:** If an interpreter is not in your system's `PATH`, you can place its portable executable (e.g., `lua`, `node`, `csrunner`, `fsrunner`) in the `Tools/Interpreters` directory within the application's installation folder.
   - For C# and F# execution, you can use these custom wrappers:  
     - **C#**: [`csrunner`](https://gist.github.com/mx7b7/90013b77c1d0bcfb6b9e77399f62e409)  
     - **F#**: [`fsrunner`](https://gist.github.com/mx7b7/3d6ee8179ba435c2c1e1e19ee38dced9) or [`fsrunner-alt`](https://gist.github.com/mx7b7/1ca60b7e4f29b4220eeccda06f5ffc57)  
     - > These wrappers are provided as Gists for convenience, as building and testing for all platforms is not feasible.

- **UI/UX**:
  - Responsive interface using Avalonia's SimpleTheme
  - Flyout panels for additional windows (settings, editors, actions, etc.)
  - Automatic loading of theme and syntax definitions
  
- **Export & Sharing**:
  - **Copy As**: Copy selected code as Markdown, HTML, BBCode, Base64, or JSON string.
  - **Export to File**: Save the entire snippet or a specific selection as a **PNG Images** or in its original language format.

---

## 🧩 Supported Syntax Engines

| Engine | Status | Language Coverage |
|--------|--------|-------------------|
| **XSHD (AvaloniaEdit)** | Built‑in | 36+ bundled definitions (light/dark variants) • **Unlimited** via custom `.xshd` files |
| **TextMateSharp** | Supported | 60+ bundled grammars from TextMateSharp.Grammars • **Unlimited** via local `.tmLanguage.json` files  |

---

## 📦 Libraries

This project uses the following open-source libraries:

- **[Avalonia](https://github.com/AvaloniaUI/Avalonia)**
- **[AvaloniaEdit](https://github.com/avaloniaui/avaloniaedit)**
- **[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)**
- **[Dapper](https://github.com/DapperLib/Dapper)**
- **[MessageBox.Avalonia](https://github.com/AvaloniaCommunity/MessageBox.Avalonia)**
- **[System.Data.SQLite](https://system.data.sqlite.org/)**
- **[Xaml.Behaviors.Interactivity](https://github.com/wieslawsoltes/Xaml.Behaviors)**
---

## 🧹 Code Formatters

CodeSnip integrates various code formatters. The application first looks for the required executable in the `Tools` directory within its installation folder. If not found, it falls back to searching the system's PATH.

### Supported Formatters

- [asmfmt](https://github.com/Mi-AIoT/asmfmt) – Assembly
- [autopep8](https://pypi.org/project/autopep8) – Python
- [black](https://black.readthedocs.io/en/stable) – Python
- [clang-format](https://clang.llvm.org/docs/ClangFormat.html) – C, C++, C#, Java, and more
- [csharpier](https://csharpier.com/) – C#, XML
- [dfmt](https://github.com/dlang-community/dfmt) – D
- [gofmt](https://pkg.go.dev/cmd/gofmt) – Go
- [fantomas](https://github.com/fsprojects/fantomas) – F#
- [pasfmt](https://github.com/integrated-application-development/pasfmt) – Pascal/Delphi
- [prettier](https://prettier.io/) – JavaScript, TypeScript, JSX, HTML, CSS, JSON, Markdown, and more
- [rustfmt](https://github.com/rust-lang/rustfmt) – Rust
- [ruff](https://github.com/astral-sh/ruff) – Python
- [shfmt](https://github.com/mvdan/sh) – Shell scripts
- [sqlfmt](https://github.com/GrantFBarnes/sqlfmt) – SQL
- [stylua](https://github.com/JohnnyMorganz/StyLua) – Lua
- [zig fmt](https://ziglang.org/) – Zig (Included with Zig installation)


---

## ⚙️ Build

To build and run CodeSnip, you need the [**.NET 10 SDK**](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
> **Note:** The application has been tested on Windows 10 and Linux Mint 22.3, macOS support is untested.

### 🚀 Quick Build (Recommended)
Open your terminal in the `src/CodeSnip` directory and run the interactive script for your platform:

* **Windows:** Run `build.bat`
* **Linux / macOS:** Run `./build.sh` (make sure it has execute permissions: `chmod +x build.sh`)

### 🛠️ Manual Build & Run

If you prefer using the standard .NET CLI inside the `src/CodeSnip` directory, use the following commands:

#### Run the application:
```bash
dotnet run
```
#### Create a Release Build:

##### Windows (x64)
```bash
dotnet build -c Release
```

##### Linux (x64)
```bash
dotnet build -c Release-Linux
```

##### macOS (ARM64)
```bash
dotnet build -c Release-Mac-ARM
```

After building, the complete application will be available in the `bin/<configuration>/net10.0/<runtime>` directory.

---
 
## 📜 License

This project is licensed under the MIT License.  
See the [LICENSE](LICENSE.txt) file for details.

---
**Tags**: snippet manager, code runner, Avalonia UI, cross-platform, C#, SQLite, AvaloniaEdit, syntax highlighting, Godbolt, xshd, developer tools, open source, code snippets, code execution, script runner, code playground


# CodeSnip (Avalonia)

**CodeSnip** — a cross-platform snippet manager and local code runner with multi-language interpreter support and Compiler Explorer integration.

This project is a port of the original **[CodeSnip (WPF)](https://github.com/mx7b7/codesnip-wpf)** application to the modern, cross-platform UI framework **Avalonia**.

![.NET](https://img.shields.io/badge/.NET-10.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)
![License: MIT](https://img.shields.io/badge/License-MIT-green)
![Status](https://img.shields.io/badge/Status-In%20Development-orange)

---

## 🌟 About the Project

**CodeSnip for Avalonia** aims to bring the full feature set and architecture of the original WPF version to all major desktop platforms: Windows, macOS, and Linux.

By migrating to **Avalonia UI**, the application becomes truly cross-platform while preserving the same clean architecture, services, and overall philosophy.

---

## ✨ Features (In Development)

All core features from the original application are planned, including:

- **Local storage** using an SQLite database.
- **Snippet organization**: Hierarchy (Language → Category → Snippet), filtering, and searching.
- **AvaloniaEdit integration**: Syntax highlighting, code folding and more.
- **Highlighting editor** A built-in editor for creating and customizing `.xshd` syntax definitions. The native `.xshd` format is prioritized for its significantly faster rendering, with optional TextMate grammar support planned for the future.
- **Compiler Explorer (Godbolt) integration**: Compile and analyze snippets remotely without a local compiler installation.
- **Local code execution**: Run scripts using locally installed interpreters.
- **Code formatters integration**: Support for various external code formatters.
- **Modern UI**: Utilizes **Avalonia SimpleTheme** for a fast and responsive user interface and enabling a UI design consistent with the WPF version. Supports both light and dark modes.

---

## 🧰 Technology Stack

- **UI Framework**: Avalonia UI
- **Language**: C#
- **Runtime**: .NET 10.0

---

## ⚙️ Build & Usage

Detailed instructions for building and running the project will be provided as the development progresses.

---

## 📦 Third-Party Libraries

This project makes use of the following open-source libraries:

### UI & Editor
- **[AvaloniaEdit](https://github.com/avaloniaui/avaloniaedit)**  
  Text editor component used for code editing and syntax highlighting.

### Core & Infrastructure
- **[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)**  
  MVVM toolkit used for commands, observable properties, and messaging.

- **[Dapper](https://github.com/DapperLib/Dapper)**  
  Lightweight ORM used for database access.

- **[System.Data.SQLite](https://system.data.sqlite.org/)**  
  SQLite provider used for local data storage.
  
---

## 📜 License

This project is licensed under the MIT License.  
See the [LICENSE](LICENSE.txt) file for details.

using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Reflection;

namespace CodeSnip.Views.AboutView
{

    public partial class AboutWindowModel : ObservableObject
    {
        [ObservableProperty]
        private string? title;

        [ObservableProperty]
        private string? description;

        [ObservableProperty]
        private string? company;

        [ObservableProperty]
        private string? version;

        [ObservableProperty]
        private string? copyright;

        public ObservableCollection<LibraryInfo> Libraries { get; } =
        [
            new() { Name = "Avalonia", Url = new Uri("https://github.com/AvaloniaUI/Avalonia"), LicenseName = "MIT License", LicenseUrl = new Uri("https://github.com/AvaloniaUI/Avalonia/blob/master/licence.md") },
            new() { Name = "AvaloniaEdit", Url = new Uri("https://github.com/avaloniaui/avaloniaedit"), LicenseName = "MIT License", LicenseUrl = new Uri("https://github.com/AvaloniaUI/AvaloniaEdit/blob/master/LICENSE") },
            new() { Name = "CommunityToolkit.Mvvm", Url = new Uri("https://github.com/CommunityToolkit/dotnet"),LicenseName = "MIT License", LicenseUrl = new Uri("https://github.com/CommunityToolkit/dotnet/blob/main/License.md") },
            new() { Name = "CSharpier.Core", Url = new Uri("https://csharpier.com/"), LicenseName = "MIT License", LicenseUrl = new Uri("https://github.com/belav/csharpier/blob/main/LICENSE") },
            new() { Name = "Dapper", Url = new Uri("https://github.com/DapperLib/Dapper"),LicenseName = "Apache 2.0 License", LicenseUrl = new Uri("https://github.com/DapperLib/Dapper/blob/main/License.txt") },
            new() { Name = "System.Data.SQLite.Core", Url = new Uri("https://system.data.sqlite.org/"),LicenseName = "Public Domain License", LicenseUrl = new Uri("https://system.data.sqlite.org/home/doc/trunk/www/copyright.wiki") }
        ];

        public ObservableCollection<ServicesInfo> Services { get; } =
        [
            new() { Name = "Compiler Explorer", Url = new Uri("https://godbolt.org/") }
        ];

        public ObservableCollection<ToolsInfo> Tools { get; } =
        [
            new() { Name = "autopep8", Url = new Uri("https://pypi.org/project/autopep8")},
            new() { Name = "black", Url = new Uri("https://black.readthedocs.io/en/stable/")},
            new() { Name = "clang-format", Url = new Uri("https://clang.llvm.org/docs/ClangFormat.html")},
            new() { Name = "dfmt", Url = new Uri("https://github.com/dlang-community/dfmt")},
            new() { Name = "gofmt", Url = new Uri("https://pkg.go.dev/cmd/gofmt")},
            new() { Name = "fantomas", Url = new Uri("https://github.com/fsprojects/fantomas")},

    ];

        public ObservableCollection<ToolsInfo> Tools2 { get; } =
        [
            new() { Name = "pasfmt", Url = new Uri("https://github.com/integrated-application-development/pasfmt")},
            new() { Name = "prettier", Url = new Uri("https://prettier.io/")},
            new() { Name = "rustfmt", Url = new Uri("https://github.com/rust-lang/rustfmt")},
            new() { Name = "ruff", Url = new Uri("https://github.com/astral-sh/ruff")},
            new() { Name = "stylua", Url = new Uri("https://github.com/JohnnyMorganz/StyLua" ) }
         ];

        public AboutWindowModel()
        {
            var assembly = Assembly.GetExecutingAssembly();
            Title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "Unknown";
            Description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;
            Company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;
            var version = assembly.GetName().Version;
            Version = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
            Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
        }

    }

    public partial class LibraryInfo : ObservableObject
    {
        [ObservableProperty] public string? name = string.Empty;
        [ObservableProperty] public Uri? url = new("https://example.com");
        [ObservableProperty] public string? licenseName = string.Empty;
        [ObservableProperty] public Uri? licenseUrl = new("https://opensource.org/licenses/MIT");
    }

    public partial class ToolsInfo : ObservableObject
    {
        [ObservableProperty] public string? name = string.Empty;
        [ObservableProperty] public Uri? url = new("https://example.com");
    }

    public partial class ServicesInfo : ObservableObject
    {
        [ObservableProperty] public string? name = string.Empty;
        [ObservableProperty] public Uri? url = new("https://example.com");
    }
}
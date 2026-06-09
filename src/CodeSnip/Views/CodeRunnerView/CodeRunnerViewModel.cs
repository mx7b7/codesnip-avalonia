using Avalonia.Threading;
using CodeSnip.Interfaces;
using CodeSnip.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CodeSnip.Views.CodeRunnerView;

public partial class CodeRunnerViewModel : ObservableObject, IOverlayViewModel
{
    public Func<Task>? CloseOverlayAsync { get; set; }

    public string? HeaderText { get; private set; }

    private readonly CompilerSettingsService _compilersSettings = new();

    private readonly HttpClient _httpClient = new();

    private readonly Func<string> _getLatestCode;

    private readonly GodboltService _godboltService;

    [ObservableProperty]
    private List<CompilerInfo>? _compilers = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    [NotifyCanExecuteChangedFor(nameof(GetLinkCommand))]
    private CompilerInfo? _selectedCompiler;

    [ObservableProperty] private string _flags = "";

    [ObservableProperty] private string _stdOut = "";

    [ObservableProperty] private string _errorText = "";

    [ObservableProperty] private string _code = "";

    [ObservableProperty] private string _extension = "";

    [ObservableProperty] private string _shortLink = "";

    [ObservableProperty] private bool _isRunning;

    [ObservableProperty] private string _asmCode = "";

    [ObservableProperty] private string _asmHighlightingName = "asm"; // Default to "asm"

    [ObservableProperty] private bool _showAsm = false;

    [ObservableProperty] private bool _hasError = false;

    [ObservableProperty] private bool _hasOut = true;

    private Process? RunningProcess { get; set; }

    private static readonly Dictionary<string, (string win, string linux, string mac, string args)> Interpreters = new()
    {
        ["py"] = ("python.exe", "python3", "python3", "-u -"),
        ["lua"] = ("lua.exe", "lua", "lua", "-"),
        ["js"] = ("node.exe", "node", "node", "-"),
        ["rb"] = ("ruby.exe", "ruby", "ruby", "-"),
        ["pl"] = ("perl.exe", "perl", "perl", "-"),
        ["php"] = ("php.exe", "php", "php", ""),
        ["java"] = ("jshell.exe", "jshell", "jshell", "-s -"),
        ["ps1"] = ("powershell.exe", "pwsh", "pwsh", "-NoProfile -ExecutionPolicy Bypass -EncodedCommand "),
        ["fs"] = ("fsrunner.exe", "fsrunner", "fsrunner", ""),
        ["cs"] = ("csrunner.exe", "csrunner", "csrunner", ""),
        ["sh"] = ("bash.exe", "bash", "bash", "")
    };

    private readonly StringBuilder _outputBuffer = new();
    private readonly StringBuilder _errorBuffer = new();
    private readonly Lock _bufferLock = new();

    public CodeRunnerViewModel(string languageExtension, string code, Func<string> getLatestCode)
    {
        Extension = languageExtension;// // Must set before triggering OnSelectedCompilerChanged (it depends on Extension)
        Compilers = _compilersSettings.GetCompilersByExtension(languageExtension);
        var defaultCompilerId = _compilersSettings.GetDefaultCompilerIdByExtension(languageExtension);
        SelectedCompiler = Compilers.FirstOrDefault(c => c.Id == defaultCompilerId) ?? Compilers.FirstOrDefault();
        Code = code;
        _getLatestCode = getLatestCode;
        _godboltService = new GodboltService(_httpClient);
        HeaderText = $"Code Runner - .{languageExtension} snippet";

    }

    // This method maps the source language extension to the appropriate assembly highlighting definition name.
    private static string MapLanguageExtensionToAsmHighlighting(string languageExtension, CompilerInfo? compiler)
    {
        if (compiler?.Id?.Contains("ildasm", StringComparison.OrdinalIgnoreCase) == true &&
            (languageExtension.Equals("cs", StringComparison.OrdinalIgnoreCase) ||
             languageExtension.Equals("fs", StringComparison.OrdinalIgnoreCase)))
        {
            return "il";
        }

        return languageExtension.ToLowerInvariant() switch
        {

            "java" => "javaopc",
            _ => "asm", // Default for C++, Rust, D, etc.
        };
    }

    partial void OnSelectedCompilerChanged(CompilerInfo? oldValue, CompilerInfo? newValue)
    {
        Flags = newValue?.Flags ?? "";
        AsmHighlightingName = MapLanguageExtensionToAsmHighlighting(Extension, newValue);
        // clear previous outputs
        AsmCode = "";
        StdOut = "";
        ErrorText = "";
    }

    private bool CanExecuteCompilerActions()
    {
        return SelectedCompiler != null;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCompilerActions))]
    private async Task Run()
    {
        try
        {
            IsRunning = true;
            await CompileSnippetAsync();
        }
        finally
        {
            IsRunning = false;
        }
    }

    partial void OnErrorTextChanged(string value)
    {
        HasError = !string.IsNullOrEmpty(value);
    }

    partial void OnStdOutChanged(string value)
    {
        HasOut = !string.IsNullOrEmpty(value);
    }

    private async Task CompileSnippetAsync()
    {
        string? langId = _compilersSettings.GetLanguageIdByExtension(Extension); // godbolt languageId (c++, csharp ...)
                                                                                 // Set skipAsm to false to get both execution output and assembler
        var (stdout, stderr, asm, error) = await _godboltService.CompileAndRunAsync(
            Code, SelectedCompiler!.Id ?? "", langId ?? "", Flags, !ShowAsm); // if ShowAsm is true, then SkipAsm must be false

        StdOut = string.IsNullOrEmpty(stdout) ? "" : stdout;
        ErrorText = RemoveAnsiCodes(stderr);
        AsmCode = asm ?? ""; // Store raw asm

        if (!string.IsNullOrEmpty(error))
        {
            ErrorText = error;
            StdOut = "";
            AsmCode = "";
            return;
        }
        // The AsmCode property is already set. The UI will bind to this directly.SS
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCompilerActions))]
    private async Task GetLink()
    {
        try
        {
            IsRunning = true;
            await GetShortenerLinkAsync();
        }
        finally
        {
            IsRunning = false;
        }
    }

    private async Task GetShortenerLinkAsync()
    {
        string? langId = _compilersSettings.GetLanguageIdByExtension(Extension); // godbolt languageId (c++, csharp ...)
        var (link, error) = await _godboltService.GetShortLinkAsync(langId ?? "", Code, SelectedCompiler!.Id ?? "", Flags);
        ShortLink = string.IsNullOrEmpty(link) ? "" : link;
        ErrorText = string.IsNullOrEmpty(error) ? "" : error;
    }

    private static string RemoveAnsiCodes(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var ansiRegex = new Regex(@"\x1B\[[0-9;]*[mK]");
        return ansiRegex.Replace(input, "");
    }

    [RelayCommand]
    private async Task Reload()
    {
        Code = _getLatestCode();
    }

    private bool CanNavigateToLink()
    {
        return !string.IsNullOrWhiteSpace(ShortLink);
    }

    [RelayCommand(CanExecute = nameof(CanNavigateToLink))]
    private void NavigateToLink()
    {
        if (!string.IsNullOrWhiteSpace(ShortLink))
        {
            if (Uri.TryCreate(ShortLink, UriKind.Absolute, out var uri))
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunLocal))]
    private async Task RunLocal()
    {
        try
        {
            IsRunning = true;
            StdOut = "";
            ErrorText = "";

            var (interpreterPath, arguments) = GetLocalInterpreter(Extension);

            if (string.IsNullOrWhiteSpace(interpreterPath))
            {
                ErrorText = $"No local interpreter configured for extension '{Extension}'.";
                return;
            }

            int timeout = Timeout.Infinite; // No timeout, rely on manual kill, beacuse some script may run long tasks

            // -- BUFFERED THROTTLING: Refresh the UI every 100 milliseconds to prevent log flooding --
            using var flushTimer = new System.Timers.Timer(100);
            flushTimer.Elapsed += (s, e) =>
            {
                string? newOutput = null;
                string? newError = null;

                // Acquire lock, capture accumulated text chunks, and immediately clear the buffers
                lock (_bufferLock)
                {
                    if (_outputBuffer.Length > 0)
                    {
                        newOutput = _outputBuffer.ToString();
                        _outputBuffer.Clear();
                    }
                    if (_errorBuffer.Length > 0)
                    {
                        newError = _errorBuffer.ToString();
                        _errorBuffer.Clear();
                    }
                }

                // Dispatch the text block to the UI thread only if there is new data
                if (newOutput != null)
                    Dispatcher.UIThread.Post(() => StdOut += newOutput);

                if (newError != null)
                    Dispatcher.UIThread.Post(() => ErrorText += newError);
            };

            flushTimer.Start(); // Start the throttling timer before launching the process

            // -- LOCAL FUNCTIONS: Append incoming lines to background buffers without blocking the UI thread --
            void onOutput(string line)
            {
                lock (_bufferLock) { _outputBuffer.AppendLine(line); }
            }

            void onError(string line)
            {
                lock (_bufferLock) { _errorBuffer.AppendLine(line); }
            }

            switch (Extension.ToLowerInvariant())
            {
                case "ps1":
                    string base64Script = Convert.ToBase64String(Encoding.Unicode.GetBytes(Code));
                    arguments += base64Script;
                    // For PS, the code is in the arguments, so the 'input' parameter is empty.
                    await RunProcessAsync_Dynamic_Reading(interpreterPath, arguments, "", timeout, onOutput, onError);
                    break;

                default:
                    await RunProcessAsync_Dynamic_Reading(interpreterPath, arguments, Code, timeout, onOutput, onError);
                    break;
            }

            // Stop the timer once the process execution is complete
            flushTimer.Stop();

            // Flush any remaining data left in the buffers (accumulated within the last <100ms)
            lock (_bufferLock)
            {
                if (_outputBuffer.Length > 0) StdOut += _outputBuffer.ToString();
                if (_errorBuffer.Length > 0) ErrorText += _errorBuffer.ToString();
                _outputBuffer.Clear();
                _errorBuffer.Clear();
            }
        }
        catch (Exception ex)
        {
            ErrorText = $"Error running local interpreter:\n{ex.Message}";
            StdOut = "";
        }
        finally
        {
            IsRunning = false;
            RunningProcess = null;
        }
    }

    /// <summary>
    /// Attempts to locate a local interpreter executable and its default arguments for the specified compiler file
    /// extension.
    /// </summary>
    /// <remarks>The method searches for interpreter executables in the "Tools\Interpreters" directory
    /// under the application's base directory. For C# scripts ("cs"), it looks for "csrunner.exe". If the
    /// interpreter executable is not found locally, the method may return the executable name as a fallback, which
    /// may require the interpreter to be available in the system PATH.</remarks>
    /// <param name="compilerExtension">The file extension of the compiler or script language, with or without a leading period (e.g., ".cs" or
    /// "cs"). Case-insensitive. If null or whitespace, no interpreter is returned.</param>
    /// <returns>A tuple containing the full path to the interpreter executable and its default arguments, or (null, null) if
    /// no suitable interpreter is found.</returns>
    private static (string? path, string? args) GetLocalInterpreter(string? compilerExtension)
    {
        if (string.IsNullOrWhiteSpace(compilerExtension))
            return (null, null);

        compilerExtension = compilerExtension.TrimStart('.').ToLowerInvariant();

        if (!Interpreters.TryGetValue(compilerExtension, out var info))
            return (null, null);

        string exe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? info.win :
                     RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? info.linux :
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? info.mac :
                     info.win; // fallback

        string appDir = Path.GetDirectoryName(Environment.ProcessPath)!;
        string toolsDir = Path.Combine(appDir, "Tools", "Interpreters");
        string interpreterPath = Path.Combine(toolsDir, exe);

        if (!Directory.Exists(toolsDir))
            Directory.CreateDirectory(toolsDir);

        return File.Exists(interpreterPath)
            ? (interpreterPath, info.args)
            : (exe, info.args);
    }


    private bool CanRunLocal()
    {
        return !string.IsNullOrWhiteSpace(GetLocalInterpreter(Extension).path);
    }

    /// <summary>
    /// Runs an external process asynchronously with the specified input and captures its standard output and error
    /// streams in real time.
    /// </summary>
    /// <remarks>If the process does not complete within the specified timeout, it is terminated and
    /// an error message is sent to the error callback. Both output and error callbacks are invoked for each line of
    /// output or error received from the process. This method does not throw exceptions for process errors;
    /// instead, error information is provided via the error callback.</remarks>
    /// <param name="fileName">The path to the executable file to run. Must not be null or empty.</param>
    /// <param name="arguments">The command-line arguments to pass to the process, or null to run the process without arguments.</param>
    /// <param name="input">The input string to write to the process's standard input. If empty, no input is sent.</param>
    /// <param name="timeoutMs">The maximum time, in milliseconds, to wait for the process to complete before terminating it. Must be
    /// greater than zero.</param>
    /// <param name="onOutputReceived">A callback invoked each time a line of text is received from the process's standard output. Cannot be null.</param>
    /// <param name="onErrorReceived">A callback invoked each time a line of text is received from the process's standard error, or when an error
    /// or timeout occurs. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task completes when the process exits and all output
    /// has been read.</returns>
    private async Task RunProcessAsync_Dynamic_Reading(
               string fileName, string? arguments, string input, int timeoutMs,
               Action<string> onOutputReceived, Action<string> onErrorReceived)
    {
        string appDir = Path.GetDirectoryName(Environment.ProcessPath)!;
        string workingDirectory = Path.Combine(appDir, "Tools", "Interpreters");

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? "",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        ApplyEncodingProfile(psi, Extension);

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        RunningProcess = process;

        var processCompletion = new TaskCompletionSource<bool>();
        int streamReadersFinished = 0;

        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data is null)
            {
                if (Interlocked.Increment(ref streamReadersFinished) == 2)
                    processCompletion.TrySetResult(true);
            }
            else
            {
                onOutputReceived?.Invoke(e.Data);
            }
        };

        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data is null)
            {
                if (Interlocked.Increment(ref streamReadersFinished) == 2)
                    processCompletion.TrySetResult(true);
            }
            else
            {
                onErrorReceived?.Invoke(e.Data);
            }
        };

        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            if (!process.Start())
            {
                onErrorReceived?.Invoke($"Failed to start process: {fileName}");
                return;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!string.IsNullOrEmpty(input))
            {
                await process.StandardInput.WriteAsync(input);
            }
            process.StandardInput.Close();

            var completedTask = await Task.WhenAny(processCompletion.Task, Task.Delay(timeoutMs, cts.Token));

            if (completedTask != processCompletion.Task)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch { /* Ignore exceptions during kill */ }

                onErrorReceived?.Invoke($"Timeout: The process '{psi.FileName}' took too long and was terminated.");
            }
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                try { process.Kill(entireProcessTree: true); } catch { }

            onErrorReceived?.Invoke("Timeout: The process took too long and was terminated.");
        }
        catch (Exception ex)
        {
            onErrorReceived?.Invoke(ex.Message);
        }
        finally
        {
            process.Dispose();
        }
    }

    private void ApplyEncodingProfile(ProcessStartInfo psi, string ext)
    {
        // Use UTF-8 without BOM to prevent corrupting script shebangs in POSIX/BusyBox shells
        Encoding utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        switch (ext)
        {
            case "py":
                psi.StandardInputEncoding = utf8WithoutBom;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                psi.EnvironmentVariables["PYTHONUTF8"] = "1";
                break;

            case "lua":
            case "js":
            case "rb":
            case "pl":
            case "php":
            case "sh":
                psi.StandardInputEncoding = utf8WithoutBom;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                break;
            case "cs":
            case "fs":
                break;
            default:
                psi.StandardInputEncoding = Encoding.Default;
                psi.StandardOutputEncoding = Encoding.Default;
                psi.StandardErrorEncoding = Encoding.Default;
                break;
        }
    }

    [RelayCommand]
    private void KillRunningProcess()
    {
        try
        {
            if (RunningProcess != null && !RunningProcess.HasExited)
            {
                RunningProcess.Kill(entireProcessTree: true);
                ErrorText += $"Process '{RunningProcess?.ProcessName}' (ID: {RunningProcess?.Id}) was terminated by the user.\n";
            }
        }
        catch (Exception ex)
        {
            ErrorText += $"\nError terminating process: {RunningProcess?.ProcessName} (ID: {RunningProcess?.Id})\n{ex.Message}";
        }
        finally
        {
            RunningProcess = null;
        }
    }

    [RelayCommand]
    private async Task Cancel()
    {
        if (RunningProcess != null && !RunningProcess.HasExited)
        {
            KillRunningProcess();
        }
        if (CloseOverlayAsync != null)
            await CloseOverlayAsync();
    }
}

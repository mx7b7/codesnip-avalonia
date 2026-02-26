using CSharpier.Core.CSharp;
using CSharpier.Core.Xml;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CodeSnip.Services
{
    /// <summary>
    /// Provides services for formatting code snippets using various external and internal formatters.
    /// </summary>
    public static class FormattingService
    {
        /// <summary>
		/// Formats C# code using the built-in CSharpier library.
		/// </summary>
		/// <param name="code">The C# code to format.</param>
		/// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool isSuccess, string? formattedCode, string? errorMessage)> TryFormatCodeWithCSharpierAsync(string code)
        {
            try
            {
                var result = await CSharpFormatter.FormatAsync(code);
                return (true, result.Code, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Formats XML code using the built-in CSharpier library.
        /// </summary>
        /// <param name="code">The XML code to format.</param>
        /// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool isSuccess, string? formattedCode, string? errorMessage)> TryFormatXmlWithCSharpierAsync(string code)
        {
            try
            {
                var result = await Task.Run(() => XmlFormatter.Format(code));
                return (true, result.Code, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
		/// Formats code using the 'clang-format.exe' tool from the 'Tools' directory or system's PATH.
		/// </summary>
		/// <param name="code">The source code to format.</param>
		/// <param name="timeoutMs">The timeout in milliseconds for the process.</param>
		/// <param name="assumeFilename">An optional filename to help clang-format determine the language and style.</param>
		/// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithClangAsync(string code, int timeoutMs = 5000, string? assumeFilename = null)
        {
            string arguments = "";
            if (!string.IsNullOrEmpty(assumeFilename))
            {
                arguments = $"--assume-filename=\"{assumeFilename}\"";
            }

            return await TryFormatWithExternalProcessAsync("clang-format", arguments, code, timeoutMs);
        }

        /// <summary>
		/// Formats D code using the 'dfmt.exe' tool from the 'Tools' directory or system's PATH.
		/// </summary>
		/// <param name="code">The D code to format.</param>
		/// <param name="timeoutMs">The timeout in milliseconds for the process.</param>
		/// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithDfmtAsync(string code, int timeoutMs = 5000)
        {
            return await TryFormatWithExternalProcessAsync("dfmt", "", code, timeoutMs);
        }

        /// <summary>
        /// Formats Rust code using the `rustfmt` command-line tool from the 'Tools' directory or system's PATH.
        /// </summary>
        /// <param name="code">The Rust code to format.</param>
        /// <param name="timeoutMs">The timeout in milliseconds for the process.</param>
        /// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithRustFmtAsync(string code, int timeoutMs = 5000)
        {
            return await TryFormatWithExternalProcessAsync("rustfmt", "", code, timeoutMs);
        }

        /// <summary>
		/// Formats Python code using the 'ruff.exe' tool from the 'Tools' directory or system's PATH.
		/// </summary>
		/// <param name="code">The Python code to format.</param>
		/// <param name="timeoutMs">The timeout in milliseconds for the process.</param>
		/// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithRuffAsync(string code, int timeoutMs = 5000)
        {
            string arguments = "format --no-cache --stdin-filename=temp.py -";
            return await TryFormatWithExternalProcessAsync("ruff", arguments, code, timeoutMs);
        }

        /// <summary>
        /// Formats Go code using the 'gofmt.exe' tool from the 'Tools' directory or system's PATH.
        /// </summary>
        /// <param name="code">The go code to format</param>
        /// <param name="timeoutMs">The timeout in milliseconds for the process.</param>
        /// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithGofmtAsync(string code, int timeoutMs = 5000)
        {
            return await TryFormatWithExternalProcessAsync("gofmt", "", code, timeoutMs);
        }

        /// <summary>
        /// Formats Lua code using the 'stylua.exe' tool from the 'Tools' directory or system's PATH.
        /// </summary>
        /// <param name="code">The Lua code to format.</param>
        /// <param name="timeoutMs">The timeout in milliseconds for the process.</param>
        /// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithStyluaAsync(string code, int timeoutMs = 5000)
        {
            return await TryFormatWithExternalProcessAsync("stylua", "-", code, timeoutMs);
        }

        /// <summary>
        /// Attempts to format the provided code using the 'shfmt' external process asynchronously.
        /// </summary>
        /// <remarks>If the formatting operation exceeds the specified timeout, it will return false and
        /// an appropriate error message.</remarks>
        /// <param name="code">The code to be formatted. This parameter cannot be null or empty.</param>
        /// <param name="timeoutMs">The maximum time, in milliseconds, to wait for the formatting operation to complete. The default value is
        /// 5000 milliseconds.</param>
        /// <returns>A tuple containing a boolean indicating success, the formatted code as a string, and an error message if the
        /// operation fails.</returns>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithShFmtAsync(string code, int timeoutMs = 5000)
        {
            return await TryFormatWithExternalProcessAsync("shfmt", "-", code, timeoutMs);
        }

        /// <summary>
        /// Attempts to format the provided SQL code using the external 'sqlfmt' formatter tool asynchronously.
        /// </summary>
        /// <remarks>If the formatting operation exceeds the specified timeout, it is aborted and the
        /// method returns <see langword="false"/> with an appropriate error message.</remarks>
        /// <param name="code">The SQL code to be formatted. This parameter must not be null or empty.</param>
        /// <param name="timeoutMs">The maximum time, in milliseconds, to wait for the formatting operation to complete. Must be a positive
        /// value. The default is 5000 milliseconds.</param>
        /// <returns>A tuple containing a value indicating whether the formatting was successful, the formatted SQL code if
        /// successful, or an error message if the operation fails.</returns>

        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithSqlFmtAsync(string code, int timeoutMs = 5000)
        {
            return await TryFormatWithExternalProcessAsync("sqlfmt", "", code, timeoutMs);
        }

        /// <summary>
        ///Format Pascal source code using the external 'pasfmt.exe' formatter from the 'Tools' directory or system's PATH.
        /// </summary>
        /// <param name="code">The Pascal source code to format.</param>
        /// <param name="timeoutMs">The maximum time, in milliseconds, to wait for the formatting process to complete.</param>
        /// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithPasFmtAsync(string code, int timeoutMs = 5000)
        {
            return await TryFormatWithExternalProcessAsync("pasfmt", "", code, timeoutMs);
        }

        /// <summary>
        /// Formats code using the 'prettier' tool from the 'Tools' directory or system's PATH.
        /// </summary>
        /// <param name="code">The JavaScript source code to format. Cannot be null.</param>
        /// <param name="timeoutMs">The maximum time, in milliseconds, to wait for the Prettier process to complete. Must be greater than zero.
        /// The default timeout is 10000 milliseconds.</param>
        /// <returns>A tuple containing a success flag, the formatted code if successful, and an error message if formatting fails.
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithPrettierAsync(string code, int timeoutMs = 10000, string? assumeFilename = null)
        {
            string arguments = "";
            if (!string.IsNullOrEmpty(assumeFilename))
            {
                arguments = $"--stdin-filepath {assumeFilename} --stdin ";
            }
            // run prettier from system's node installation, prettier must be installed globally
            string prettierName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "prettier.cmd" : "prettier";
            return await TryFormatWithExternalProcessAsync(prettierName, arguments, code, timeoutMs);
            // same as above
            //return await TryFormatWithExternalProcessAsync("npx.cmd", $"prettier {arguments}", code, timeoutMs);
        }

        /// <summary>
        /// Formats F# source code using the Fantomas formatter via an external process.
        /// </summary>
        /// <remarks>
        /// Writes the code to a temporary file in the 'Tools' directory, formats it with Fantomas, and returns the result.
        /// The temporary file is deleted after formatting.
        /// </remarks>
        /// <param name="code">The F# source code to format. Cannot be null.</param>
        /// <param name="timeoutMs">The maximum time, in milliseconds, to wait for the Fantomas formatting process to complete. Defaults to 15,000 milliseconds.</param>
        /// <returns>A tuple containing a success flag, the formatted code if successful, and an error message if formatting fails.
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithFantomasAsync(string code, int timeoutMs = 15000)
        {
            string toolsDirectory = Path.Combine(AppContext.BaseDirectory, "Tools");
            string tempFilePath = Path.Combine(toolsDirectory, "temp.fs");

            try
            {
                // Save the code to a temporary file
                await File.WriteAllTextAsync(tempFilePath, code);

                // Call fantomas to format the file
                var result = await TryFormatWithExternalProcessAsync(
                    "dotnet",
                    $"fantomas \"{tempFilePath}\"",
                    "",
                    timeoutMs
                );

                if (!result.Success)
                {
                    return result;
                }

                // Read the formatted code back from the temporary file
                string formattedCode = await File.ReadAllTextAsync(tempFilePath);
                return (true, formattedCode, null);
            }
            catch (Exception ex)
            {
                return (false, null, $"Fantomas formatting exception: {ex.Message}");
            }
            // Don't delete temporary file beacause WriteAllTextAsync alows overwriting it next time

        }

        /// <summary>
        /// Formats Python code using black/autopep8 via python(3) -m.
        /// </summary>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)>
            TryFormatCodeWithPythonFormatterAsync(string code, string formatterName, int timeoutMs = 10000)
        {
            string pythonExe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python" : "python3";
            string arguments = $"-m {formatterName} -";

            return await TryFormatWithExternalProcessAsync(pythonExe, arguments, code, timeoutMs);
        }

        /// <summary>
        /// A generic helper method to run an external formatting tool from the 'Tools' directory.
        /// </summary>
        /// <param name="executableName">The name of the executable file (e.g., 'dfmt.exe').</param>
        /// <param name="arguments">The command-line arguments to pass to the executable.</param>
        /// <param name="code">The source code to pass to the process's standard input.</param>
        /// <param name="timeoutMs">The timeout in milliseconds for the process.</param>
        /// <returns>A tuple indicating success, the formatted code from standard output, and any error message from standard error.</returns>
        private static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatWithExternalProcessAsync(
            string toolName,
            string arguments,
            string code,
            int timeoutMs = 5000)
        {
            string baseDirectory = AppContext.BaseDirectory;
            string toolsDirectory = Path.Combine(baseDirectory, "Tools");

            try
            {
                if (!Directory.Exists(toolsDirectory))
                {
                    Directory.CreateDirectory(toolsDirectory);
                }
            }
            catch (Exception ex)
            {
                return (false, null, $"Failed to create Tools directory: {ex.Message}");
            }

            string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? toolName + ".exe" : toolName;

            string localToolPath = Path.Combine(toolsDirectory, exeName);

            string executableToRun = localToolPath;
            if (!File.Exists(localToolPath))
            {
                executableToRun = toolName; // Fallback na PATH
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executableToRun,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = toolsDirectory // Set the working directory to the formatter's folder to ensure it can locate its config files
            };

            try
            {
                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();

                    // Asynchronously write to the process's standard input and then close it
                    await process.StandardInput.WriteAsync(code);
                    process.StandardInput.Close();

                    // Start reading output and error streams asynchronously
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    // Asynchronously wait for the process to exit with a timeout
                    using var cts = new CancellationTokenSource(timeoutMs);
                    try
                    {
                        await process.WaitForExitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        try { process.Kill(entireProcessTree: true); } catch { /* Ignore errors if the process is already gone */ }
                        return (false, null, $"Timeout: The '{exeName}' process took too long to respond.");
                    }

                    // Await the results of the read operations
                    string output = await outputTask;
                    string error = await errorTask;

                    if (process.ExitCode != 0)
                    {
                        return (false, null, $"{exeName} error (exit code {process.ExitCode}): {error.Trim()}");
                    }

                    return (true, output, null);
                }
            }
            catch (Exception ex)
            {
                return (false, null, $"Formatting exception for '{exeName}':\n{ex.Message}");
            }
        }


    }
}

using System;
using System.IO;
using System.Threading;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace SigmaTau.Unity.NeovimCodeEditor.Editor
{
    public static class ProcessHelper
    {
        public static readonly bool SupportsFindExecutablePath =
            Application.platform is RuntimePlatform.WindowsEditor or RuntimePlatform.LinuxEditor;
        public static readonly bool SupportsFindNamedPipe =
            Application.platform is RuntimePlatform.WindowsEditor or RuntimePlatform.LinuxEditor;

        public static void LaunchNeovim(
            string editorPath,
            string neovimLaunchPath,
            string neovimLaunchArguments,
            string namedPipePath,
            CancellationToken cancellationToken)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = neovimLaunchPath,
                Arguments = neovimLaunchArguments,
                ErrorDialog = false,
                UseShellExecute = true,
            });

            // Wait for the server to be available.
            do
            {
                if (process.WaitForExit(100))
                {
                    throw new InvalidOperationException($"'{neovimLaunchPath}' exited with code {process.ExitCode}");
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            while (!NvimServerExists(editorPath, namedPipePath, cancellationToken));
        }

        public static bool TryNvimRemoteSend(
            string editorPath,
            string namedPipePath,
            string keys,
            CancellationToken cancellationToken)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = editorPath,
                CreateNoWindow = true,
                ErrorDialog = false,
                UseShellExecute = false,
                ArgumentList = { "--headless", "--server", namedPipePath, "--remote-send", keys },
                RedirectStandardOutput = false,
            });

            while (!process.WaitForExit(10))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (process.ExitCode is not 0)
            {
                Debug.LogErrorFormat(
                    "<noparse>'{0}' with arguments '{1}' returned a non-zero exit code of {2}</noparse>",
                    editorPath, string.Join(' ', process.StartInfo.ArgumentList), process.ExitCode);
                return false;
            }

            return true;
        }

        public static bool NvimServerExists(
            string editorPath,
            string namedPipePath,
            CancellationToken cancellationToken)
        {
            string result = GetNvimExprOutput(editorPath, namedPipePath, "version", cancellationToken);
            return !string.IsNullOrWhiteSpace(result);
        }

        private static string GetNvimExprOutput(
            string editorPath,
            string namedPipePath,
            string expression,
            CancellationToken cancellationToken)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = editorPath,
                    CreateNoWindow = true,
                    ErrorDialog = false,
                    UseShellExecute = false,
                    ArgumentList = { "--headless", "--server", namedPipePath, "--remote-expr", expression },
                    RedirectStandardOutput = true,
                },
                EnableRaisingEvents = true,
            };
            using var mre = new ManualResetEventSlim();
            process.Exited += (_, _) => mre.Set();
            process.Start();

            try
            {
                mre.Wait(cancellationToken);
                return process.ExitCode == 0 ? process.StandardOutput.ReadToEnd() : string.Empty;
            }
            catch
            {
                process.Kill();
                throw;
            }
        }

        public static string GetRemoteNvimWorkingDirectory(
            string editorPath,
            string namedPipePath,
            CancellationToken cancellationToken)
        {
            return GetNvimExprOutput(editorPath, namedPipePath, "getcwd()", cancellationToken).Trim();
        }

        public static string FindExecutablePath(string name)
        {
            const int timeoutMs = 5000;

            string findExecutableTool = Application.platform switch
            {
                RuntimePlatform.WindowsEditor => "where",
                RuntimePlatform.LinuxEditor => "which",
                _ => throw new PlatformNotSupportedException(
                    "Platform {Application.platform} does not support searching for executables currently"),
            };

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = findExecutableTool,
                CreateNoWindow = true,
                ErrorDialog = false,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                ArgumentList = { name },
            });

            if (!process.WaitForExit(timeoutMs))
            {
                Debug.LogErrorFormat("Timed out ({0} ms) searching for executable '{1}'", timeoutMs, name);
                process.Kill();
                return string.Empty;
            }
            if (process.ExitCode != 0)
            {
                Debug.LogErrorFormat("Exit code of {0} while searching for executable '{1}'", process.ExitCode, name);
                return string.Empty;
            }

            string path = process.StandardOutput.ReadToEnd();
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogErrorFormat("No path returned while searching for executable '{0}'", name);
                return string.Empty;
            }

            return path.Trim();
        }

        public static string GetPipeRootPath()
        {
            return Application.platform switch
            {
                RuntimePlatform.WindowsEditor => "\\\\.\\pipe",
                RuntimePlatform.LinuxEditor => Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? string.Empty,
                _ => string.Empty,
            };
        }

        public static string SearchForNamedPipe(string editorPath, CancellationToken cancellationToken)
        {
            string pipeFolderPath = GetPipeRootPath();

            if (string.IsNullOrWhiteSpace(pipeFolderPath))
            {
                Debug.LogWarning("Could not find pipe folder to search");
                return null;
            }

            foreach (string namedPipePath in Directory.EnumerateFiles(pipeFolderPath, "*nvim*"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string workingDirectory = GetRemoteNvimWorkingDirectory(editorPath, namedPipePath, cancellationToken);

                if (!string.IsNullOrWhiteSpace(workingDirectory)
                    && PathUtils.IsSameFile(workingDirectory, PathUtils.ProjectFullPath))
                {
                    Debug.LogFormat("Found existing named pipe: {0}", namedPipePath);
                    return namedPipePath;
                }
            }

            Debug.LogWarning("Could not find name pipe");
            return null;
        }
    }
}

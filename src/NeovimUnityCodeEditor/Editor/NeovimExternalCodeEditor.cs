using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

namespace SigmaTau.Unity.NeovimCodeEditor.Editor
{
    [InitializeOnLoad]
    public class NeovimExternalCodeEditor : IExternalCodeEditor
    {
        private static readonly Regex _argumentsRegex = new(
            @"\$\(.*?\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly ManualResetEvent _isInitializedMre;

        private CodeEditor.Installation[] _installations;
        private string _namedPipePath;
        private bool _namedPipeConnected;

        public static readonly NeovimExternalCodeEditor Instance;

        public bool IsCurrent => CodeEditor.Editor.CurrentCodeEditor == this;
        public bool HasNamedPipe => !string.IsNullOrWhiteSpace(_namedPipePath);

        static NeovimExternalCodeEditor()
        {
            Instance = new NeovimExternalCodeEditor();
            CodeEditor.Register(Instance);
        }

        private NeovimExternalCodeEditor()
        {
            string neovimBinPath = NeovimCodeEditorSettings.NeovimBinPath;
            // If an executable is already configured, use it and don't search.
            if (!string.IsNullOrWhiteSpace(neovimBinPath))
            {
                _installations = new CodeEditor.Installation[] { new() { Name = "Neovim", Path = neovimBinPath } };
                return;
            }
            // Linux/macOS don't support searching from executables yet.
            if (!ProcessHelper.SupportsFindExecutablePath)
            {
                _installations = Array.Empty<CodeEditor.Installation>();
                return;
            }

            _isInitializedMre = new ManualResetEvent(false);
            // This instance is created in the static constructor. Starting a thread in the static constructor seems to
            // cause a deadlock. So execute on the next editor tick.
            EditorApplication.delayCall += () =>
            {
                Task.Run(() =>
                {
                    string neovimBinPath = ProcessHelper.FindExecutablePath("nvim");
                    // Cannot set EditorPrefs on a background thread, so execute on the next editor tick.
                    EditorApplication.delayCall += () =>
                    {
                        SetInstallationInternal(neovimBinPath);
                        if (_installations.Length > 0)
                        {
                            NeovimCodeEditorSettings.NeovimBinPath = _installations[0].Path;
                        }
                        _isInitializedMre.Set();
                    };
                });
            };
        }

        public void ClearNamedPipe()
        {
            _namedPipePath = null;
            _namedPipeConnected = false;
        }

        public CodeEditor.Installation[] Installations
        {
            get
            {
                // If still searching, wait for it to finish.
                _isInitializedMre?.WaitOne();
                // Update installations if Neovim BIN Path is configured, in case it was updated by the user.
                string editorPath = NeovimCodeEditorSettings.NeovimBinPath;
                if (!string.IsNullOrWhiteSpace(editorPath))
                {
                    SetInstallationInternal(editorPath);
                }
                return _installations;
            }
        }

        public void SetInstallation(string editorPath)
        {
            _isInitializedMre?.WaitOne();
            SetInstallationInternal(editorPath);
        }

        private void SetInstallationInternal(string editorPath)
        {
            if (string.IsNullOrWhiteSpace(editorPath))
            {
                _installations = Array.Empty<CodeEditor.Installation>();
            }
            else
            {
                if (_installations is null || _installations.Length != 1)
                {
                    _installations = new CodeEditor.Installation[1];
                }
                _installations[0] = new CodeEditor.Installation { Name = "Neovim", Path = editorPath };
            }
        }

        public void Initialize(string editorInstallationPath)
        {
        }

        public void OnGUI()
        {
        }

        private bool OpenProjectInternal(
            string editorPath,
            string filePath,
            int line,
            int column,
            string postOpenCommand,
            bool searchForNamedPipe,
            string namedPipePath,
            string neovimLaunchPath,
            string neovimLaunchArguments,
            Action<string> updateProgress,
            CancellationToken cancellationToken)
        {
            updateProgress($"Opening file {filePath}...");

            if (!HasNamedPipe)
            {
                _namedPipePath = namedPipePath;
                _namedPipeConnected = false;

                // If no named pipe is configured, try to search for a named pipe.
                if (!HasNamedPipe)
                {
                    if (!ProcessHelper.SupportsFindNamedPipe)
                    {
                        Debug.LogWarningFormat(
                            "No named pipe path configured, and searching is not supported your platform: {0}",
                            Application.platform);
                        return false;
                    }
                    if (!searchForNamedPipe)
                    {
                        Debug.LogWarning("No named pipe path configured, and searching is disabled");
                        return false;
                    }

                    updateProgress("Searching for named pipe...");
                    _namedPipePath = ProcessHelper.SearchForNamedPipe(editorPath, cancellationToken);
                    _namedPipeConnected = HasNamedPipe;
                    if (!_namedPipeConnected)
                    {
                        return false;
                    }
                }
            }

            if (!_namedPipeConnected)
            {
                updateProgress($"Checking connection to '{_namedPipePath}'...");
                _namedPipeConnected = ProcessHelper.NvimServerExists(editorPath, _namedPipePath, cancellationToken);

                if (!_namedPipeConnected && searchForNamedPipe && ProcessHelper.SupportsFindNamedPipe)
                {
                    updateProgress("Searching for named pipe...");
                    _namedPipePath = ProcessHelper.SearchForNamedPipe(editorPath, cancellationToken);
                    _namedPipeConnected = HasNamedPipe;
                }
                if (!_namedPipeConnected && !string.IsNullOrWhiteSpace(neovimLaunchPath))
                {
                    updateProgress($"Launching Neovim '{editorPath}'...");
                    ProcessHelper.LaunchNeovim(
                        editorPath: editorPath,
                        neovimLaunchPath: neovimLaunchPath,
                        neovimLaunchArguments: neovimLaunchArguments,
                        namedPipePath: namedPipePath,
                        cancellationToken: cancellationToken);
                    _namedPipePath = namedPipePath;
                    _namedPipeConnected = true;
                }
            }

            if (!_namedPipeConnected)
            {
                Debug.LogError("Could not connect to a named pipe");
                return false;
            }

            var commandStringBuilder = new StringBuilder("<c-\\><c-N><cmd>n ");
            commandStringBuilder.Append(filePath);
            commandStringBuilder.Append("<cr>");

            if (line >= 0)
            {
                commandStringBuilder.Append("<cmd>");
                commandStringBuilder.Append(line);
                commandStringBuilder.Append("<cr>");
            }
            if (column >= 0)
            {
                commandStringBuilder.Append(column);
                commandStringBuilder.Append("|");
            }

            if (!string.IsNullOrWhiteSpace(postOpenCommand))
            {
                commandStringBuilder.Append(postOpenCommand);
            }

            bool remoteSendSuccess = ProcessHelper.TryNvimRemoteSend(
                editorPath, _namedPipePath, commandStringBuilder.ToString(), cancellationToken);

            if (!remoteSendSuccess)
            {
                _namedPipeConnected = false;
            }

            return true;
        }

        public bool OpenProject(string filePath = "", int line = -1, int column = -1)
        {
            if (CodeEditor.Editor.CurrentCodeEditor != this)
            {
                return false;
            }

            bool failReturn = !NeovimCodeEditorSettings.AllowCodeEditorFallback;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return failReturn;
            }

            string editorPath = _installations.FirstOrDefault().Path;
            if (string.IsNullOrWhiteSpace(editorPath))
            {
                Debug.LogError("Neovim BIN path not set");
                return failReturn;
            }

            // OpenProjectInternal() runs in a background thread and cannot access EditorPrefs, so fetch them now.
            bool searchForNamedPipe = NeovimCodeEditorSettings.SearchForNamedPipe;
            string namedPipePath = NeovimCodeEditorSettings.NamedPipePath;
            string postOpenKeys = NeovimCodeEditorSettings.PostOpenKeys;
            string neovimLaunchPath = string.Empty;
            string neovimLaunchArguments = string.Empty;
            if (NeovimCodeEditorSettings.LaunchNeovim)
            {
                neovimLaunchPath = NeovimCodeEditorSettings.NeovimLaunchPath;
                if (string.IsNullOrWhiteSpace(neovimLaunchPath))
                {
                    neovimLaunchPath = editorPath;
                }

                bool parseError = false;
                neovimLaunchArguments = _argumentsRegex.Replace(
                    NeovimCodeEditorSettings.NeovimLaunchArguments,
                    (match) =>
                    {
                        if (match.Value.Equals("$(pipe)", StringComparison.OrdinalIgnoreCase))
                        {
                            return $"\"{namedPipePath}\"";
                        }
                        else
                        {
                            Debug.LogErrorFormat("Unknown launch argument variable: {0}", match.Value);
                            parseError = true;
                            return string.Empty;
                        }
                    });

                if (parseError)
                {
                    return failReturn;
                }
            }

            try
            {
                bool success = TaskHelper.ExecuteCancelable(
                    (updateProgress, cancellationToken) => OpenProjectInternal(
                        editorPath, filePath, line, column,
                        postOpenCommand: postOpenKeys,
                        searchForNamedPipe: searchForNamedPipe,
                        namedPipePath: namedPipePath,
                        neovimLaunchPath: neovimLaunchPath,
                        neovimLaunchArguments: neovimLaunchArguments,
                        updateProgress: updateProgress,
                        cancellationToken));
                return success || failReturn;
            }
            catch (OperationCanceledException)
            {
                return failReturn;
            }
            catch (Exception exc)
            {
                Debug.LogErrorFormat("Unexpected error while opening file: {0}", filePath);
                Debug.LogException(exc);
                return failReturn;
            }
        }

        public void SyncAll()
        {
            using var projectGeneration = new NeovimProjectGeneration();
            projectGeneration.GenerateProjectFiles();
        }

        public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
        {
            SyncAll();
        }

        public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
        {
            ReadOnlySpan<char> filename = Path.GetFileNameWithoutExtension(editorPath.AsSpan());
            if (filename.Equals("nvim", StringComparison.OrdinalIgnoreCase))
            {
                installation = new() { Name = "Neovim", Path = editorPath };
                return true;
            }

            installation = default;
            return false;
        }
    }
}

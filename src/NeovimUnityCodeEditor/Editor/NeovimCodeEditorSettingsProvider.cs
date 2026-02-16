using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SigmaTau.Unity.NeovimCodeEditor.Editor
{
    public class NeovimCodeEditorSettingsProvider : SettingsProvider
    {
        private const int LabelWidth = 180;

        private TextField _binPathTextField;
        private TextField _namedPipePathTextField;
        private TextField _neovimLaunchPathTextField;
        private TextField _neovimLaunchArgumentsTextField;
        private TextField _postOpenKeysTextField;

        public NeovimCodeEditorSettingsProvider()
            : base("Preferences/External Tools/Neovim Code Editor", SettingsScope.User)
        {
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new NeovimCodeEditorSettingsProvider();
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            rootElement.Add(new Label("Neovim Code Editor")
            {
                style = {
                    fontSize = 20,
                    unityFontStyleAndWeight = FontStyle.Bold,
                },
            });
            rootElement.Add(new Label("Project Generation Settings")
            {
                style =
                {
                    marginTop = 16,
                    marginBottom = 8,
                    fontSize = 16,
                    unityFontStyleAndWeight = FontStyle.Bold,
                },
            });
            rootElement.Add(CreateProjectGenerationSettingsPanel());
            rootElement.Add(new Label("Open File Settings")
            {
                style =
                {
                    marginTop = 16,
                    marginBottom = 8,
                    fontSize = 16,
                    unityFontStyleAndWeight = FontStyle.Bold,
                },
            });
            rootElement.Add(CreatOpenFileSettingsPanel());
            rootElement.Add(new Label("Project-Specific Settings")
            {
                style =
                {
                    marginTop = 16,
                    marginBottom = 8,
                    unityFontStyleAndWeight = FontStyle.Bold,
                },
            });
            rootElement.Add(CreateProjectSpecificSettingsPanel());
        }

        public override void OnDeactivate()
        {
            NeovimCodeEditorSettings.NeovimBinPath = _binPathTextField.value.Trim();
            NeovimCodeEditorSettings.NamedPipePath = _namedPipePathTextField.value.Trim();
            NeovimCodeEditorSettings.NeovimLaunchPath = _neovimLaunchPathTextField.value.Trim();
            NeovimCodeEditorSettings.NeovimLaunchArguments = _neovimLaunchArgumentsTextField.value.Trim();
            NeovimCodeEditorSettings.PostOpenKeys = _postOpenKeysTextField.value.Trim();
        }

        private VisualElement CreatOpenFileSettingsPanel()
        {
            var panel = new VisualElement();

            _binPathTextField = new TextField("Neovim Bin Path")
            {
                value = NeovimCodeEditorSettings.NeovimBinPath,
                labelElement = { style = { minWidth = LabelWidth } },
                tooltip = "Neovim executable to use to open files.",
            };
            _binPathTextField.RegisterCallback<BlurEvent>(
                (_) => NeovimCodeEditorSettings.NeovimBinPath = _binPathTextField.value.Trim());
            panel.Add(_binPathTextField);

            panel.Add(CreateButtonsPanel(
                new Button(OnBrowseNeovimBinPath) { text = "Browse..." },
                new Button(OnSearchNeovimBinPath)
                {
                    text = "Search",
                    enabledSelf = Application.platform is RuntimePlatform.WindowsEditor,
                    tooltip = "* Windows only currently\n\nSearch for an nvim executable in PATH.",
                }
            ));

            var launchNeovimToggle = new Toggle("Launch Neovim If Not Open")
            {
                value = NeovimCodeEditorSettings.LaunchNeovim,
                labelElement = { style = { minWidth = LabelWidth } },
                style = { marginTop = 16 },
                tooltip = "Whether or not to launch an instance of Neovim when opening a file if an instance with the "
                    + "configured project named pipe path is not running or an instance cannot be found (if searching "
                    + "is enabled.",
            };
            panel.Add(launchNeovimToggle);

            var launchSettingsPanel = CreateLaunchSettingsPanel();
            launchSettingsPanel.enabledSelf = launchNeovimToggle.value;
            panel.Add(launchSettingsPanel);

            launchNeovimToggle.RegisterCallback<ChangeEvent<bool>>(
                (evt) => launchSettingsPanel.enabledSelf = evt.newValue);

            _postOpenKeysTextField = new TextField("Post-Open Keys")
            {
                labelElement = { style = { minWidth = LabelWidth } },
                value = NeovimCodeEditorSettings.PostOpenKeys,
                tooltip = "<noparse>Keys to send to Neovim after opening a file. Could be used to bring the Neovim window into "
                    + "focus if a command is available (e.g. '<cmd>NeovideFocus<cr>' for Neovide).</noparse>",
            };
            _postOpenKeysTextField.RegisterCallback<BlurEvent>(
                (_) => NeovimCodeEditorSettings.PostOpenKeys = _postOpenKeysTextField.value.Trim());
            panel.Add(_postOpenKeysTextField);

            var searchForNamedPipeToggle = new Toggle("Search for Named Pipe")
            {
                value = NeovimCodeEditorSettings.SearchForNamedPipe,
                labelElement = { style = { minWidth = LabelWidth } },
                enabledSelf = Application.platform is RuntimePlatform.WindowsEditor,
                tooltip = "* Windows only currently\n\nSearch for a Neovim named pipe (search filter of "
                    + "'\\\\.\\pipe\\*nvim*') with the current directory set to the project folder directory. Tries to "
                    + "open a file with the default named pipe path first.",
            };
            searchForNamedPipeToggle.RegisterCallback<ChangeEvent<bool>>(
                (changeEvent) => NeovimCodeEditorSettings.SearchForNamedPipe = changeEvent.newValue);
            panel.Add(searchForNamedPipeToggle);

            var allowFallbackToggle = new Toggle("Allow Fallback")
            {
                value = NeovimCodeEditorSettings.AllowCodeEditorFallback,
                labelElement = { style = { minWidth = LabelWidth } },
                enabledSelf = Application.platform is RuntimePlatform.WindowsEditor,
                tooltip = "Whether or not to fallback to another code editor if Neovim fails to open the file.",
            };
            allowFallbackToggle.RegisterCallback<ChangeEvent<bool>>(
                (changeEvent) => NeovimCodeEditorSettings.AllowCodeEditorFallback = changeEvent.newValue);
            panel.Add(allowFallbackToggle);

            return panel;
        }

        private VisualElement CreateProjectGenerationSettingsPanel()
        {
            var panel = new VisualElement();

            var deleteProjectFilesToggle = new Toggle("Delete Unused Project Files")
            {
                value = NeovimCodeEditorSettings.DeleteProjectFiles,
                labelElement = { style = { minWidth = LabelWidth } },
                tooltip = "Deletes unused .csproj and .csproj.meta files in the Assets folder and subfolders, and "
                    + "deletes unsued .sln and .slnx files in the root project folder",
            };
            deleteProjectFilesToggle.RegisterCallback<ChangeEvent<bool>>(
                    (changeEvent) => NeovimCodeEditorSettings.DeleteProjectFiles = changeEvent.newValue);
            panel.Add(deleteProjectFilesToggle);
            var useXmlSolutionToggle = new Toggle("Use SLNX Format")
            {
                value = NeovimCodeEditorSettings.UseXmlSolution,
                labelElement = { style = { minWidth = LabelWidth } },
                tooltip = "Whether or not to use SLN or SLNX format for the solution file.",
            };
            useXmlSolutionToggle.RegisterCallback<ChangeEvent<bool>>(
                (changeEvent) => NeovimCodeEditorSettings.UseXmlSolution = changeEvent.newValue);
            panel.Add(useXmlSolutionToggle);

            return panel;
        }

        private VisualElement CreateProjectSpecificSettingsPanel()
        {
            var panel = new VisualElement();
            _namedPipePathTextField = new TextField("Named Pipe Path")
            {
                value = NeovimCodeEditorSettings.NamedPipePath,
                labelElement = { style = { minWidth = LabelWidth } },
            };
            _namedPipePathTextField.RegisterCallback<BlurEvent>(
                (_) => NeovimCodeEditorSettings.NamedPipePath = _namedPipePathTextField.value.Trim());
            panel.Add(_namedPipePathTextField);

            var namedPipePathButtonPanel = new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Row,
                    marginLeft = 120,
                },
            };
            panel.Add(CreateButtonsPanel(new Button(OnDefaultNamedPipePath) { text = "Default" }));

            return panel;
        }

        private VisualElement CreateLaunchSettingsPanel()
        {
            var panel = new VisualElement { style = { marginBottom = 16 } };

            _neovimLaunchPathTextField = new TextField("Neovim Launch Path")
            {
                labelElement = { style = { minWidth = LabelWidth } },
                value = NeovimCodeEditorSettings.NeovimLaunchPath,
                tooltip = "Executable to launch. If blank, uses the Neovim Bin Path.",
            };
            _neovimLaunchPathTextField.RegisterCallback<BlurEvent>(
                (_) => NeovimCodeEditorSettings.NeovimLaunchPath = _neovimLaunchPathTextField.value.Trim());
            panel.Add(_neovimLaunchPathTextField);
            panel.Add(CreateButtonsPanel(new Button(OnBrowseNeovimLaunchPath) { text = "Browse..." }));

            string launchArguments = NeovimCodeEditorSettings.NeovimLaunchArguments;
            var launchArgumentOptions = new List<string> { "--listen $(pipe)", "-- --listen $(pipe)", null };
            int launchArgumentsIndex = launchArgumentOptions.IndexOf(
                launchArguments, 0, launchArgumentOptions.Count - 1);

            var neovimLaunchArgumentsPopup = new PopupField<string>(
                "Neovim Launch Arguments",
                launchArgumentOptions,
                launchArgumentsIndex < 0 ? launchArgumentOptions.Count - 1 : launchArgumentsIndex,
                (value) => value ?? "<custom>",
                (value) => value ?? "<custom>")
            {
                labelElement = { style = { minWidth = LabelWidth } },
                tooltip = "Arguments to pass when launching an instance of Neovim. $pipe is replaced by the current "
                    + "named pipe. $$ is replaced by a single $.",
            };
            neovimLaunchArgumentsPopup.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                _neovimLaunchArgumentsTextField.enabledSelf = string.IsNullOrWhiteSpace(evt.newValue);
                if (!_neovimLaunchArgumentsTextField.enabledSelf)
                {
                    _neovimLaunchArgumentsTextField.value = evt.newValue;
                    NeovimCodeEditorSettings.NeovimLaunchArguments = evt.newValue;
                }
            });
            panel.Add(neovimLaunchArgumentsPopup);

            _neovimLaunchArgumentsTextField = new TextField(" ")
            {
                labelElement = { style = { minWidth = LabelWidth } },
                enabledSelf = launchArgumentsIndex < 0,
                value = launchArguments,
            };
            _neovimLaunchArgumentsTextField.RegisterCallback<BlurEvent>(
                (_) => NeovimCodeEditorSettings.NeovimLaunchArguments = _neovimLaunchArgumentsTextField.value.Trim());
            panel.Add(_neovimLaunchArgumentsTextField);

            return panel;
        }

        private static VisualElement CreateButtonsPanel(params Button[] buttons)
        {
            var panel = new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Row,
                    marginLeft = LabelWidth + 1,
                },
            };
            foreach (var button in buttons)
            {
                button.style.width = 100;
                panel.Add(button);
            }

            return panel;
        }

        private void OnBrowseNeovimBinPath()
        {
            string path = EditorUtility.OpenFilePanel("Neovim Bin Path", null, null);
            if (!string.IsNullOrWhiteSpace(path))
            {
                _binPathTextField.value = path.Trim();
                NeovimCodeEditorSettings.NeovimBinPath = _binPathTextField.value;
            }
        }

        private void OnBrowseNeovimLaunchPath()
        {
            string path = EditorUtility.OpenFilePanel("Neovim Launch Path", null, null);
            if (!string.IsNullOrWhiteSpace(path))
            {
                _neovimLaunchPathTextField.value = path.Trim();
                NeovimCodeEditorSettings.NeovimLaunchPath = _neovimLaunchPathTextField.value;
            }
        }

        private void OnSearchNeovimBinPath()
        {
            string path = ProcessHelper.FindExecutablePath("nvim");
            if (!string.IsNullOrWhiteSpace(path))
            {
                _binPathTextField.value = path.Trim();
                NeovimCodeEditorSettings.NeovimBinPath = _binPathTextField.value;
            }
        }

        private void OnDefaultNamedPipePath()
        {
            string path = $"\\\\.\\pipe\\nvim.unity.{PathUtils.ProjectName}";
            _namedPipePathTextField.value = path;
            NeovimCodeEditorSettings.NamedPipePath = path;
        }
    }
}

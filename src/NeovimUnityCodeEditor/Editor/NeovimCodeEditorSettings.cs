using System;
using UnityEditor;

namespace SigmaTau.Unity.NeovimCodeEditor.Editor
{
    public static class NeovimCodeEditorSettings
    {
        private const string _keyPrefix = "SigmaTau.";

        private const string _searchForNamedPipeKey = _keyPrefix + "SearchForNamedPipe";
        private const string _deleteProjectFilesKey = _keyPrefix + "DeleteProjectFiles";
        private const string _neovimBinPathKey = _keyPrefix + "NeovimPath";
        private const string _useXmlSolutionKey = _keyPrefix + "UseXmlSolution";
        private const string _launchNeovimKey = _keyPrefix + "LaunchNeovim";
        private const string _neovimLaunchPathKey = _keyPrefix + "NeovimLaunchPath";
        private const string _neovimLaunchArgumentsKey = _keyPrefix + "NeovimLaunchArguments";
        private const string _allowCodeEditorFallbackKey = _keyPrefix + "AllowCodeEditorFallback";
        private const string _postOpenKeysKey = _keyPrefix + "PostOpenKeys";

        private static string NamedPipePathKey => GetProjectKey("NamedPipePath");

        public static string NeovimBinPath
        {
            get => EditorPrefs.GetString(_neovimBinPathKey, string.Empty);
            set => EditorPrefs.SetString(_neovimBinPathKey, value);
        }

        public static bool SearchForNamedPipe
        {
            get => EditorPrefs.GetBool(_searchForNamedPipeKey, false);
            set => EditorPrefs.SetBool(_searchForNamedPipeKey, value);
        }

        public static bool DeleteProjectFiles
        {
            get => EditorPrefs.GetBool(_deleteProjectFilesKey, false);
            set => EditorPrefs.SetBool(_deleteProjectFilesKey, value);
        }

        public static string NamedPipePath
        {
            get => EditorPrefs.GetString(NamedPipePathKey, PathUtils.DefaultNamedPipePath);
            set => EditorPrefs.SetString(NamedPipePathKey, value);
        }

        public static bool UseXmlSolution
        {
            get => EditorPrefs.GetBool(_useXmlSolutionKey, true);
            set => EditorPrefs.SetBool(_useXmlSolutionKey, value);
        }

        public static bool LaunchNeovim
        {
            get => EditorPrefs.GetBool(_launchNeovimKey, true);
            set => EditorPrefs.SetBool(_launchNeovimKey, value);
        }

        public static string NeovimLaunchPath
        {
            get => EditorPrefs.GetString(_neovimLaunchPathKey, string.Empty);
            set => EditorPrefs.SetString(_neovimLaunchPathKey, value);
        }

        public static string NeovimLaunchArguments
        {
            get => EditorPrefs.GetString(_neovimLaunchArgumentsKey, "--listen $(pipe)");
            set => EditorPrefs.SetString(_neovimLaunchArgumentsKey, value);
        }

        public static bool AllowCodeEditorFallback
        {
            get => EditorPrefs.GetBool(_allowCodeEditorFallbackKey, false);
            set => EditorPrefs.SetBool(_allowCodeEditorFallbackKey, value);
        }

        public static string PostOpenKeys
        {
            get => EditorPrefs.GetString(_postOpenKeysKey, string.Empty);
            set => EditorPrefs.SetString(_postOpenKeysKey, value);
        }

        private static string GetProjectKey(string key)
        {
            Guid projectGuid = PlayerSettings.productGUID;
            if (projectGuid == Guid.Empty)
            {
                throw new InvalidOperationException("Project GUID is not set");
            }
            return $"{_keyPrefix}Projects.{projectGuid}.{key}";
        }
    }
}

using UnityEditor;

namespace SigmaTau.Unity.NeovimCodeEditor.Editor
{
    public static class NeovimCodeEditorMenuItems
    {
        [MenuItem("Neovim/Generate Project Files", true)]
        public static bool IsGenerateProjectFilesValid()
        {
            return NeovimExternalCodeEditor.Instance.IsCurrent;
        }

        [MenuItem("Neovim/Generate Project Files", false, 0)]
        public static void GenerateProjectFiles()
        {
            NeovimExternalCodeEditor.Instance.SyncAll();
        }

        [MenuItem("Neovim/Clear Named Pipe", true)]
        public static bool IsClearNamedPipeValid()
        {
            return NeovimExternalCodeEditor.Instance.HasNamedPipe;
        }

        [MenuItem("Neovim/Clear Named Pipe", false, 100)]
        public static void ClearNamedPipe()
        {
            NeovimExternalCodeEditor.Instance.ClearNamedPipe();
        }

        [MenuItem("Neovim/Preferences", false, 200)]
        public static void Preferences()
        {
            SettingsService.OpenUserPreferences(NeovimCodeEditorSettingsProvider.SettingsPath);
        }
    }
}

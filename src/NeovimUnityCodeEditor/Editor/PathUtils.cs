using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SigmaTau.Unity.NeovimCodeEditor.Editor
{
    public static class PathUtils
    {
        public static readonly StringComparison PathComparison =
            Application.platform is RuntimePlatform.WindowsEditor or RuntimePlatform.OSXEditor
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        public static readonly string AssetFolderFullPath =
            Path.GetFullPath(TrimTrailingDirectorySeparators(Application.dataPath));

        public static readonly string ProjectFullPath = Path.GetDirectoryName(AssetFolderFullPath);

        public static readonly string ProjectName = Path.GetFileName(ProjectFullPath);

        public static readonly string DefaultNamedPipePath =
            Application.platform is RuntimePlatform.WindowsEditor
                ? $"\\\\.\\pipe\\nvim.unity.{ProjectName}"
                : string.Empty;

        public static bool IsDirectorySeparatorChar(char character)
        {
            return character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar;
        }

        public static string TrimTrailingDirectorySeparators(string path)
        {
            return path is null
                ? string.Empty
                : TrimTrailingDirectorySeparators(path.AsSpan()).ToString();
        }

        public static ReadOnlySpan<char> TrimTrailingDirectorySeparators(ReadOnlySpan<char> path)
        {
            while (path.Length > 0 && IsDirectorySeparatorChar(path[^1]))
            {
                path = path[..^1];
            }
            return path;
        }

        public static bool IsSameFile(string path1, string path2)
        {
            if (!Path.GetFileName(path1.AsSpan()).Equals(Path.GetFileName(path2.AsSpan()), PathComparison))
            {
                return false;
            }
            path1 = Path.GetFullPath(path1, ProjectFullPath);
            path2 = Path.GetFullPath(path1, ProjectFullPath);
            string relativePath = Path.GetRelativePath(Path.GetDirectoryName(path1), Path.GetDirectoryName(path2));
            return relativePath == ".";
        }

        /// <summary>
        /// Checks if the given path is nested in the <b>Asset</b> folder.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IsInAssetsFolder(string path)
        {
            return IsNestedInternal(AssetFolderFullPath, Path.GetFullPath(path, ProjectFullPath));
        }

        /// <summary>
        /// Check if the given nested path is actually nested in the given root path. If they point to the same folder,
        /// they are considered <b>not</b> nested.
        /// </summary>
        /// <param name="rootPath">The root path to check.</param>
        /// <param name="nestedPath">The potentially nested path to check against the root path.</param>
        /// <returns>
        /// <c>true</c> if the nested path is nested; otherwise, <c>false</c> if the path is not nested or points to the
        /// same folder.
        /// </returns>
        public static bool IsNested(string rootPath, string nestedPath)
        {
            // Convert to full paths to ensure we are using the project folder as the working directory.
            return IsNestedInternal(
                Path.GetFullPath(rootPath, ProjectFullPath),
                Path.GetFullPath(nestedPath, ProjectFullPath));
        }

        private static bool IsNestedInternal(string rootFullPath, string nestedFullPath)
        {
            string relativePath = Path.GetRelativePath(rootFullPath, nestedFullPath);

            if (
                // I don't believe this should ever happen. If pointing to the same file, it should return '.'.
                string.IsNullOrWhiteSpace(relativePath)
                // This should happen if the nested path being tested is pointing to a folder on a different root.
                || Path.IsPathFullyQualified(relativePath))
            {
                return false;
            }
            // Not using '..' to traverse up a directory and not pointing to the same file.
            if (relativePath[0] is not '.')
            {
                return true;
            }
            // '.' indicates its pointing the same folder (i.e. not nested).
            if (relativePath.Length == 1)
            {
                return false;
            }
            // Not using '..' to traverse up a directory (e.g. dotfile).
            if (relativePath[1] is not '.')
            {
                return true;
            }
            return relativePath.Length > 2 && !IsDirectorySeparatorChar(relativePath[2]);
        }

        public static string GetAbsoluteOrRelativePath(string relativeTo, string path)
        {
            return Path.IsPathFullyQualified(path) ? path : Path.GetRelativePath(relativeTo, path);
        }

        public static string TryFindRootPathOfAllFiles(IList<string> files)
        {
            if (files.Count == 0)
            {
                return null;
            }

            ReadOnlySpan<char> rootSourcePathSpan = files[0].AsSpan();
            foreach (string sourceFile in files.Skip(1))
            {
                ReadOnlySpan<char> folderPathSpan = Path.GetDirectoryName(sourceFile.AsSpan());

                while (
                    rootSourcePathSpan.Length > 0
                    && folderPathSpan.Length > 0
                    && !rootSourcePathSpan.Equals(folderPathSpan, PathComparison))
                {
                    if (rootSourcePathSpan.Length > folderPathSpan.Length)
                    {
                        rootSourcePathSpan = Path.GetDirectoryName(rootSourcePathSpan);
                    }
                    else
                    {
                        folderPathSpan = Path.GetDirectoryName(folderPathSpan);
                    }
                }
            }

            string rootSourcePath = rootSourcePathSpan.ToString();
            return IsInAssetsFolder(rootSourcePath) ? rootSourcePath : null;
        }

    }
}

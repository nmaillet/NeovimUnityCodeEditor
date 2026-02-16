# Neovim Unity Code Editor

## Installation

Install via the **Package Manager** in Unity (Window > Package Management > Package Manager).

1. Click `+` and select `Install package from git URL...`
2. Copy and paste the following URL and click `Install`:

    `https://github.com/nmaillet/NeovimUnityCodeEditor.git?path=src/NeovimUnityCodeEditor`

## Platform Support

This plugin has only been tested on Windows currently. It _should_ work on Linux and macOS as well (aside from some
missing functionality listed below), but may have issues.

## Configuration

Configuration changes can be made in the **Preferences** window (Edit > Preferences...) under External Tools/Neovim Code
Editor. On Windows, it should find `nvim.exe` automatically and be selectable from from **External Tools**.

The following options are available:

- **Delete Unused Project Files**: Automatically deletes project and solution files that weren't generated. It searches
  for `.csproj` and `.csproj.meta` files in the `Assets` folder and subfolders. `.sln` and `.slnx` files are searched
  for in the top project folder only.
- **Use SLNX Format**: Whether to generate a `.sln` or `.slnx` solution file for the project.
- **Neovim Bin Path**: The executable used to open files. Must be specified to have Neovim be selectable as the
  **External Script Editor**.
- **Launch Neovim If Not Open**: Whether or not to launch an instance of Neovim if one cannot be found when opening a
  file.
- **Neovim Launch Path**: The executable to launch, if launching is enabled. Will use the **Neovim Bin Path** if not
  provided.
- **Neovim Launch Arguments**: The arguments to pass when launching an instance of Neovim. `$(pipe)` will be substituted
  for the **Named Pipe Path** for the project.
- **Post-Open Keys**: A key sequence to execute in Neovim after opening a file (e.g. `<cmd>NeovideFocus<cr>` to focus
  the Neovide window).
- **Search for Named Pipe**: _Windows only_. Searches `\\.\pipe\` for any pipe that matches the name `*nvim*` and tries
  to get the current directory. If the current directory matches the project path, uses that named pipe.
- **Allow Fallback**: Whether or not to allow Unity to use another code editor to open a file if this plugin fails to
  open a file (e.g. VS Code).
- **Named Pipe Path**: A project-specific named pipe path to use when opening files or launching Neovim.

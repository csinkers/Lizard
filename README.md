# Lizard
A debugger targeting DosBox with a focus on reverse engineering

## Attributions

This project has dependencies on:
- [zeroc/ice](https://github.com/zeroc-ice/ice) for RPC between debugger UI and hosts, licensed under [GPL v2.0](https://github.com/zeroc-ice/ice/blob/3.7/LICENSE) © ZeroC
- [feliwir/SharpFileDialog](https://github.com/feliwir/SharpFileDialog) for cross-platform 'open file' dialogs, licensed under [MIT](https://github.com/feliwir/SharpFileDialog/blob/master/LICENSE) © Stephan Vedder 
- [csinkers/GhidraProgramData](https://github.com/csinkers/GhidraProgramData) for reading Ghidra XML export data, licensed under [MIT](https://github.com/csinkers/GhidraProgramData/blob/master/LICENSE)
- [csinkers/ImGuiColorTextEditNet](https://github.com/csinkers/ImGuiColorTextEditNet) for code and text editors, licensed under [MIT](https://github.com/csinkers/ImGuiColorTextEditNet/blob/master/LICENSE)
- [microsoft/vscode-codicons](https://github.com/microsoft/vscode-codicons) is used for toolbar icons and is licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) © Microsoft
- [veldrid](https://github.com/veldrid/veldrid) is used for access to graphics APIs, licensed under [MIT](https://github.com/veldrid/veldrid/blob/master/LICENSE) © Eric Mellino and Veldrid contributors
- [ImGUI.NET](https://github.com/ImGuiNET/ImGui.NET) is used for the GUI, licensed under [MIT](https://github.com/ImGuiNET/ImGui.NET/blob/master/LICENSE) © Eric Mellino and ImGui.NET contributors
- [Tooll 3](https://github.com/tooll3/t3) for some ImGui controls, licensed under [MIT](https://github.com/tooll3/t3/blob/master/LICENSE.txt) © Thomas Mann, Daniel Szymanski, Andreas Rose, Framefield GmbH

## Environment setup

### Machine install:
Install csharpier VS 2022 plugin
Run `pip install pre-commit`

### Project Install:
dotnet tool install csharpier
Create .csharpierrc:
{
    "printWidth": 120,
    "useTabs": false,
    "tabWidth": 4,
    "endOfLine": "auto"
}

Create .pre-commit-config.yaml:
repos:
  - repo: local
    hooks:
      - id: dotnet-tool-restore
        name: Install .NET tools
        entry: dotnet tool restore
        language: system
        always_run: true
        pass_filenames: false
        stages:
          - commit
          - push
          - post-checkout
          - post-rewrite
        description: Install the .NET tools listed at .config/dotnet-tools.json.
      - id: csharpier
        name: Run CSharpier on C# files
        entry: dotnet tool run dotnet-csharpier
        language: system
        types:
          - c#
        description: CSharpier is an opinionated C# formatter inspired by Prettier.


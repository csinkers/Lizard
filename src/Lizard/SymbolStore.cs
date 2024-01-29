using GhidraProgramData;
using Lizard.Config;
using Lizard.Config.Properties;

namespace Lizard;

public class SymbolStore
{
    public static readonly StringProperty ProgramDataPathProperty = new(nameof(SymbolStore), "ProgramDataPath");
    public static readonly StringProperty CodePathProperty = new(nameof(SymbolStore), "CCodePath");

    public ProgramData? Data { get; private set; }
    public DecompilationResults? Code { get; private set; }

    public string? DataPath { get; private set; } // XML program data
    public string? CodePath { get; private set; } // Decompiled C code w/ line offsets

    public event Action? DataLoading;
    public event Action<ProgramData?>? DataLoaded;

    public Symbol? LookupSymbol(string name) => Data?.LookupSymbol(name); 

    public void Load(string? path, string? codePath)
    {
        DataLoading?.Invoke();

        DataPath = path;
        CodePath = codePath;
        Data = !string.IsNullOrEmpty(path) && File.Exists(path) ? ProgramData.Load(path) : null;

        Code?.Dispose();
        Code = LoadCode(codePath);

        DataLoaded?.Invoke(Data);
    }

    public void Load(Stream dataStream, Stream codeStream, string dataName, string codeName)
    {
        DataLoading?.Invoke();

        DataPath = dataName;
        CodePath = codeName;
        Data = ProgramData.Load(dataStream);

        Code?.Dispose();
        Code = DecompilationResults.Load(codeStream);

        DataLoaded?.Invoke(Data);
    }

    public void LoadProject(ProjectConfig project)
    {
        string? path = project.GetProperty(ProgramDataPathProperty);
        string? codePath = project.GetProperty(CodePathProperty);
        Load(path, codePath);
    }

    public void SaveProject(ProjectConfig project)
    {
        project.SetProperty(ProgramDataPathProperty, DataPath);
        project.SetProperty(CodePathProperty, CodePath);
    }

    static DecompilationResults? LoadCode(string? codePath)
    {
        if (string.IsNullOrEmpty(codePath) || !File.Exists(codePath))
            return null;

        return DecompilationResults.Load(codePath);
    }
}
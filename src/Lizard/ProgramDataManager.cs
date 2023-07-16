using GhidraProgramData;
using Lizard.Config.Properties;

namespace Lizard;

public class ProgramDataManager : ISymbolStore
{
    readonly ProjectManager _projectManager;
    static readonly StringProperty ProgramDataPath = new(nameof(ProgramDataManager), "ProgramDataPath");
    static readonly IntProperty OffsetProperty = new(nameof(ProgramDataManager), "Offset", 0);
    public ProgramData? Data { get; private set; }
    public string? DataPath { get; private set; }
    public int Offset { get; private set; }

    public event Action? DataLoading;
    public event Action<ProgramData?>? DataLoaded;

    public ProgramDataManager(ProjectManager projectManager)
    {
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        projectManager.ProjectLoaded += _ => LoadFromProject();
        projectManager.ProjectSaving += project =>
        {
            project.SetProperty(ProgramDataPath, DataPath);
            project.SetProperty(OffsetProperty, Offset);
        };

        LoadFromProject();
    }

    void LoadFromProject()
    {
        var project = _projectManager.Project;
        var path = project.GetProperty(ProgramDataPath);
        var offset = project.GetProperty(OffsetProperty);
        Load(path, offset);
    }

    public void Load(string? path, int offset)
    {
        DataLoading?.Invoke();

        Data = null;
        DataPath = path;
        Offset = offset;

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            Data = ProgramData.Load(path);

        DataLoaded?.Invoke(Data);
    }

    public SymbolInfo? Lookup(uint address)
    {
        if (Data == null)
            return null;

        var (symAddress, name, context) = Data.Lookup(address);
        var symbolType = context switch
        {
            GFunction _ => SymbolType.Function,
            GGlobal _ => SymbolType.Global,
            _ => SymbolType.Unknown
        };

        return new(symAddress, name, symbolType, context);
    }
}
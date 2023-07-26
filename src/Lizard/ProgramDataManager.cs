using System.Globalization;
using GhidraProgramData;
using Lizard.Config.Properties;

namespace Lizard;

public class ProgramDataManager : ISymbolStore
{
    static readonly StringProperty ProgramDataPath = new(nameof(ProgramDataManager), "ProgramDataPath");
    static readonly StringListProperty MappingProperty = new(nameof(ProgramDataManager), "MemoryMapping");

    readonly ProjectManager _projectManager;
    MemoryMapping _mapping = new();

    public ProgramData? Data { get; private set; }

    public MemoryMapping Mapping
    {
        get => _mapping;
        set
        {
            _mapping = value;
            MappingChanged?.Invoke();
        }
    }

    public string? DataPath { get; private set; }

    public event Action? DataLoading;
    public event Action<ProgramData?>? DataLoaded;
    public event Action? MappingChanged;

    public ProgramDataManager(ProjectManager projectManager)
    {
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        projectManager.ProjectLoaded += _ => LoadFromProject();
        projectManager.ProjectSaving += project =>
        {
            project.SetProperty(ProgramDataPath, DataPath);
            project.SetProperty(MappingProperty, SaveMapping(Mapping));
        };

        LoadFromProject();
    }

    static MemoryMapping LoadMapping(List<string> list)
    {
        var mapping = new MemoryMapping();
        foreach (var region in list)
        {
            var parts = region.Split(' ');
            if (parts.Length != 4)
                continue;

            var fileStart = ParseHex(parts[0]);
            var memoryStart = ParseHex(parts[1]);
            var length = ParseHex(parts[2]);
            var type = Enum.Parse<MemoryType>(parts[3]);
            mapping.Add(memoryStart, fileStart, length, type);
        }

        return mapping;
    }

    static uint ParseHex(string s) => uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    static List<string> SaveMapping(MemoryMapping mapping) 
        => mapping.Regions
            .Select(region => $"{region.FileStart:x} {region.MemoryStart:x} {region.Length:x} {region.Type}")
            .ToList();

    public Symbol? LookupSymbol(uint memoryAddress) =>
        Mapping.ToFile(memoryAddress) is var (fileOffset, _) 
            ? Data?.LookupSymbol(fileOffset) 
            : null;

    public void Load(string? path)
    {
        DataLoading?.Invoke();

        Data = null;
        DataPath = path;

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            Data = ProgramData.Load(path);

        DataLoaded?.Invoke(Data);
    }

    void LoadFromProject()
    {
        var project = _projectManager.Project;
        var path = project.GetProperty(ProgramDataPath);
        var list = project.GetProperty(MappingProperty);
        Load(path);
        Mapping = LoadMapping(list);
    }
}
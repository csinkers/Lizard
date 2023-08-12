using System.Globalization;
using GhidraProgramData;
using Lizard.Config.Properties;

namespace Lizard;

public class ProgramDataManager : ISymbolStore
{
    static readonly StringProperty ProgramDataPathProperty = new(nameof(ProgramDataManager), "ProgramDataPath");
    static readonly StringProperty CodePathProperty = new(nameof(ProgramDataManager), "CCodePath");
    static readonly StringListProperty MappingProperty = new(nameof(ProgramDataManager), "MemoryMapping");

    readonly ProjectManager _projectManager;
    MemoryMapping _mapping = new();

    public ProgramData? Data { get; private set; }
    public DecompilationResults? Code { get; private set; }

    public MemoryMapping Mapping
    {
        get => _mapping;
        set
        {
            _mapping = value;
            MappingChanged?.Invoke();
        }
    }

    public string? DataPath { get; private set; } // XML program data
    public string? CodePath { get; private set; } // Decompiled C code w/ line offsets

    public event Action? DataLoading;
    public event Action<ProgramData?>? DataLoaded;
    public event Action? MappingChanged;

    public ProgramDataManager(ProjectManager projectManager)
    {
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        projectManager.ProjectLoaded += _ => LoadFromProject();
        projectManager.ProjectSaving += project =>
        {
            project.SetProperty(ProgramDataPathProperty, DataPath);
            project.SetProperty(CodePathProperty, CodePath);
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

    public Symbol? LookupSymbol(string name) => Data?.LookupSymbol(name); 

    public void Load(string? path, string? codePath)
    {
        DataLoading?.Invoke();

        DataPath = path;
        CodePath = codePath;
        Data = !string.IsNullOrEmpty(path) && File.Exists(path) ? ProgramData.Load(path) : null;
        Code = LoadCode(codePath);

        DataLoaded?.Invoke(Data);
    }

    void LoadFromProject()
    {
        var project = _projectManager.Project;
        var path = project.GetProperty(ProgramDataPathProperty);
        var codePath = project.GetProperty(CodePathProperty);
        var list = project.GetProperty(MappingProperty);
        Load(path, codePath);
        Mapping = LoadMapping(list);
    }

    static DecompilationResults? LoadCode(string? codePath)
    {
        if (string.IsNullOrEmpty(codePath) || !File.Exists(codePath))
            return null;

        return DecompilationResults.Load(codePath);
    }
}
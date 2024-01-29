using Lizard.Config;

namespace Lizard;

public class ProjectManager : IProjectManager
{
    public ProjectConfig Project { get; private set; }
    public event Action<ProjectConfig>? ProjectLoading;
    public event Action<ProjectConfig>? ProjectSaving;
    public event Action? ProjectLoaded;
    public event Action? ProjectSaved;

    public ProjectManager(ProjectConfig project)
        => Project = project ?? throw new ArgumentNullException(nameof(project));

    public void Load(string path)
    {
        var project = ProjectConfig.Load(path);
        project.Path = path;
        Load(project);
    }

    public void Load(ProjectConfig config)
    {
        Project = config;
        ProjectLoading?.Invoke(Project);
        ProjectLoaded?.Invoke();
    }

    public void Save(string path)
    {
        ProjectSaving?.Invoke(Project);
        Project.Path = path;
        Project.Save(path);
        ProjectSaved?.Invoke();
    }

    public void Save(ProjectConfig project) => ProjectSaving?.Invoke(project);
}
using Lizard.Config;

namespace Lizard;

public class ProjectManager : IProjectManager
{
    public ProjectConfig Project { get; private set; }
    public event Action? ProjectLoading;
    public event Action<ProjectConfig>? ProjectLoaded;
    public event Action<ProjectConfig>? ProjectSaving;
    public event Action? ProjectSaved;

    public ProjectManager(ProjectConfig project) => Project = project ?? throw new ArgumentNullException(nameof(project));

    public void Load(string path)
    {
        ProjectLoading?.Invoke();
        Project = ProjectConfig.Load(path);
        ProjectLoaded?.Invoke(Project);
    }

    public void Save(string path)
    {
        ProjectSaving?.Invoke(Project);
        Project.Path = path;
        Project.Save(path);
        ProjectSaved?.Invoke();
    }
}
namespace Lizard.Config;

public interface IProjectManager
{
    ProjectConfig Project { get; }
    event Action<ProjectConfig>? ProjectLoading;
    event Action? ProjectLoaded;
    event Action<ProjectConfig>? ProjectSaving;
    event Action? ProjectSaved;
}

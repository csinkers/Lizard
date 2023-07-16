namespace Lizard.Config;

public interface IProjectManager
{
    ProjectConfig Project { get; }
    event Action? ProjectLoading;
    event Action<ProjectConfig>? ProjectLoaded;
    event Action<ProjectConfig>? ProjectSaving;
    event Action? ProjectSaved;
}
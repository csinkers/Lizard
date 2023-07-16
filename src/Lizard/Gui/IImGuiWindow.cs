using Lizard.Config;

namespace Lizard.Gui;

public interface IImGuiWindow // Note: Instances of IImGuiWindow represent all windows of a given type.
{
    string Prefix { get; }
    void Draw();
    void ClearState();
    void Load(WindowId id, WindowConfig config);
    void Save(Dictionary<string, WindowConfig> configs);
}

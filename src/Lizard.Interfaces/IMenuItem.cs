namespace Lizard.Interfaces;

public interface IMenuItem
{
    string Path { get; } // '/' separated path segments, e.g. Windows/Debug/Watch
    void OnClicked();
}
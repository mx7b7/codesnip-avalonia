namespace CodeSnip.Interfaces;

public interface ITreeNode
{
    bool IsExpanded { get; set; }
    bool IsSelected { get; set; }
    bool IsVisible { get; set; }
}
using System;
using System.Threading.Tasks;

namespace CodeSnip.Interfaces;

public interface IOverlayViewModel
{
    Func<Task>? CloseOverlayAsync { get; set; }
}
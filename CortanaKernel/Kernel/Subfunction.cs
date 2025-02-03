using System.Diagnostics;
using CortanaLib.Structures;

namespace CortanaKernel.Kernel;

public class Subfunction : Process
{
    public ESubFunctionType Type { get; set; }
    public bool ShuttingDown { get; set; }
}
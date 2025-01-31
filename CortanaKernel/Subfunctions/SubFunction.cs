using System.Diagnostics;
using CortanaKernel.Hardware.Structures;
using CortanaLib.Structures;

namespace CortanaKernel.Subfunctions;

public class SubFunction : Process
{
    public ESubFunctionType Type { get; set; }
    public bool ShuttingDown { get; set; }
}
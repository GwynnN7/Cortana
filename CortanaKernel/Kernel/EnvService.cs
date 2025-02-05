using System.Diagnostics;
using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;

namespace CortanaKernel.Kernel;

public static class EnvService
{
    private static readonly Dictionary<ESubFunctionType, Dictionary<string, string>> SubfunctionsEnv = new();
    public static void Load()
    {
        string filePath = DataHandler.CortanaPath(EDirType.Config, ".env");
        if (!File.Exists(filePath))
        {
            throw new CortanaException("Cannot load environment file, quitting...");
        }

        var currentSubfunction = ESubFunctionType.CortanaKernel;
        foreach (var line in File.ReadAllLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith('#'))
            {
                string subfunctionText = line.Replace('#', ' ').Trim();
                currentSubfunction = subfunctionText.ToEnum<ESubFunctionType>().Match(
                    value => value,
                    () => throw new CortanaException("Failed to read environment file")
                    );
                SubfunctionsEnv.Add(currentSubfunction, new Dictionary<string, string>());
            }
            
            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();
            SubfunctionsEnv[currentSubfunction].Add(key, value);
        }
    }

    public static void SetEnv(Process subfunction)
    {
        foreach ((ESubFunctionType subfunctionType, Dictionary<string, string> env) in SubfunctionsEnv)
        {
            foreach ((string key, string value) in env)
            {
                if (subfunction is Subfunction subfunctionProcess)
                {
                    if (subfunctionType != subfunctionProcess.Type) continue;
                    subfunctionProcess.StartInfo.Environment.Add(key, value);
                    continue;
                }
                if (subfunctionType != ESubFunctionType.CortanaKernel) continue;
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
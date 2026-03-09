using CortanaLib;
using CortanaLib.Structures;

namespace CortanaKernel.Kernel;

public static class EnvService
{
    public static void Load()
    {
        string filePath = DataHandler.CortanaPath(EDirType.Config, ".env");
        if (!File.Exists(filePath))
        {
            throw new CortanaException("Cannot load environment file, quitting...");
        }

        foreach (var line in File.ReadAllLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;

            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
using CortanaDesktop;
using CortanaLib.Runtime;

CortanaEnvironment.Load(required: false);

return args.Length > 0 ? await Cli.Run(args) : await Agent.Run();

using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Common;

public sealed record CommandOrigin(CommandActor Actor, CommandSurface Surface, bool ViaAi = false, string Reason = "")
{
	public static readonly CommandOrigin Startup = new(CommandActor.System, CommandSurface.Startup);
	public static readonly CommandOrigin Automation = new(CommandActor.System, CommandSurface.Automation);
	public static readonly CommandOrigin Scheduler = new(CommandActor.System, CommandSurface.Scheduler);
	public static readonly CommandOrigin Internal = new(CommandActor.System, CommandSurface.Internal);

	public static CommandOrigin User(CommandSurface surface, bool viaAi = false) => new(CommandActor.User, surface, viaAi);

	public bool IsUser => Actor == CommandActor.User;

	public override string ToString() => $"{Actor}/{Surface}{(ViaAi ? "/AI" : "")}";
}

using System.Diagnostics;
using CortanaKernel.Domain.Services;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Infrastructure.Process;

/// systemd user units
public sealed class SystemdSupervisor : IServiceSupervisor
{
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

	private static string Unit(ServiceId service) => $"cortana-{service.ToString().ToLowerInvariant()}";

	public async Task<Result<string>> Control(ServiceId service, ServiceAction action, CancellationToken token = default)
	{
		string unit = Unit(service);

		if (action == ServiceAction.Update)
		{
			await Shell.Run("cortana git", TimeSpan.FromMinutes(2));
			return await Control(service, ServiceAction.Restart, token);
		}

		string verb = action.ToString().ToLowerInvariant();

		// The Kernel cannot wait for systemctl to stop the Kernel, so it detaches the call
		if (service == ServiceId.Kernel)
		{
			Shell.StartDetached($"systemctl --user {verb} {unit}", TimeSpan.FromSeconds(1));
			return Result.Ok(Log.Write("Services", $"{unit} {verb}ing"));
		}

		int exit = await Run(verb, unit, token);
		return exit == 0
			? Result.Ok(Log.Write("Services", $"{unit} {verb} succeeded"))
			: Result.Fail<string>(Log.Write("Services", $"Could not {verb} {unit}"));
	}

	public async Task<bool> IsRunning(ServiceId service, CancellationToken token = default) =>
		await Run("is-active", Unit(service), token) == 0;

	public async Task<Result<string>> Journal(ServiceId service, int lines, CancellationToken token = default)
	{
		int wanted = Math.Clamp(lines, 10, 500);

		try
		{
			string output = await Shell.Run(
				$"journalctl --user -u {Unit(service)} -n {wanted} --no-pager --reverse --output=short-iso", TimeSpan.FromSeconds(20));

			return Result.Ok(string.IsNullOrWhiteSpace(output) ? "No journal entries" : output.TrimEnd());
		}
		catch (Exception ex)
		{
			return Result.Fail<string>($"Could not read the journal: {ex.Message}");
		}
	}

	private static async Task<int> Run(string action, string unit, CancellationToken token)
	{
		using var process = new System.Diagnostics.Process();
		process.StartInfo = new ProcessStartInfo
		{
			FileName = "systemctl",
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};
		process.StartInfo.ArgumentList.Add("--user");
		process.StartInfo.ArgumentList.Add(action);
		process.StartInfo.ArgumentList.Add(unit);

		process.Start();

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
		cts.CancelAfter(Timeout);

		try
		{
			await process.WaitForExitAsync(cts.Token);
			return process.ExitCode;
		}
		catch (OperationCanceledException)
		{
			try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
			Log.Error("Services", $"systemctl {action} {unit} timed out");
			return -1;
		}
	}
}

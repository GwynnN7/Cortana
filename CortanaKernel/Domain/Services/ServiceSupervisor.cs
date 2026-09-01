using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Services;

/// Process supervision only: start, stop, restart, update, and whether something is running
public interface IServiceSupervisor
{
	Task<Result<string>> Control(ServiceId service, ServiceAction action, CancellationToken token = default);
	Task<bool> IsRunning(ServiceId service, CancellationToken token = default);
	Task<Result<string>> Journal(ServiceId service, int lines, CancellationToken token = default);
}

/// The Raspberry the Kernel runs on
public interface IHostMachine
{
	Location Location { get; }
	string Gateway { get; }
	double CpuTemperature { get; }
	Task<string> PublicIpAddress(CancellationToken token = default);
	Result<string> PowerOff();
	Result<string> Reboot();
	Task<Result<string>> RunShellCommand(string command, CancellationToken token = default);
	Result<string> WakeComputer(string macAddress);
}

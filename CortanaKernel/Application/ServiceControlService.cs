using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Services;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Application;

/// Process supervision
public sealed class ServiceControlService(IServiceSupervisor supervisor, IEventBus bus)
{
	private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(5);
	private readonly SemaphoreSlim _gate = new(1, 1);

	private IReadOnlyList<ServiceView>? _cached;
	private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

	public async Task<IReadOnlyList<ServiceView>> All(CancellationToken token = default)
	{
		if (Fresh(out IReadOnlyList<ServiceView>? cached)) return cached!;

		await _gate.WaitAsync(token);
		try
		{
			if (Fresh(out cached)) return cached!;

			ServiceId[] services = Enum.GetValues<ServiceId>();
			bool[] running = await Task.WhenAll(services.Select(service => supervisor.IsRunning(service, token)));

			_cached = [.. services.Select((service, index) => new ServiceView(service, running[index]))];
			_cachedAt = DateTimeOffset.Now;
			return _cached;
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task<bool> IsRunning(ServiceId service, CancellationToken token = default) => await supervisor.IsRunning(service, token);

	public async Task<Result<string>> Control(ServiceId service, ServiceAction action, CancellationToken token = default)
	{
		_cachedAt = DateTimeOffset.MinValue;

		Result<string> result = await supervisor.Control(service, action, token);
		if (result.IsOk) bus.Publish(new ServiceStateChanged(service, action != ServiceAction.Stop, DateTimeOffset.Now));

		return result;
	}

	public Task<Result<string>> Journal(ServiceId service, int lines, CancellationToken token = default) =>
		supervisor.Journal(service, lines, token);

	private bool Fresh(out IReadOnlyList<ServiceView>? cached)
	{
		cached = _cached;
		return cached != null && DateTimeOffset.Now - _cachedAt < CacheFor;
	}
}

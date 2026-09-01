using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Settings;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Application;

public sealed class SettingsService
{
	private readonly SettingsStore _store;
	private readonly IEventBus _bus;

	public SettingsService(SettingsStore store, IEventBus bus)
	{
		_store = store;
		_bus = bus;
		_store.Changed += (key, value) => _bus.Publish(new SettingChanged(key, value, DateTimeOffset.Now));
	}

	public IReadOnlyList<SettingView> All() => _store.All();

	public SettingView Read(SettingKey key) => _store.View(key);

	public Result<string> Write(SettingKey key, string value) => _store.Write(key, value);
}

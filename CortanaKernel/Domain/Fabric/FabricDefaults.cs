using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Fabric;

public static class FabricDefaults
{
	public static readonly IReadOnlyList<SourceDescriptor> Sources =
	[
		new(SourceIds.Raspberry, SourceKind.Host, ["pin23", "pin24", "pin25"], ["cpu", "cpu_temp", "ram", "disk"]),
		new(SourceIds.Station, SourceKind.Station, [],
			[SensorIds.Motion, SensorIds.Light, SensorIds.Temperature, SensorIds.Humidity, SensorIds.Co2, SensorIds.Tvoc, "air_temperature"]),
		new(SourceIds.Kernel, SourceKind.Host, [], [SensorIds.Presence, SensorIds.Night, SensorIds.Sleep]),
		new(SourceIds.Computer, SourceKind.Computer, [DeviceIds.Computer],
			["cpu", "cpu_temp", "gpu", "gpu_temp", "gpu_power", "ram", "disk", "at_desk", "locked"])
	];

	public static readonly Registrations Registered = new(
	[
		new VirtualDevice(DeviceIds.Lamp, "Lamp", [new ChannelRef(SourceIds.Raspberry, "pin25")], "💡", "🕯", InStatus: true),
		new VirtualDevice(DeviceIds.Power, "Power", [new ChannelRef(SourceIds.Raspberry, "pin23")], "⚡", "🔌"),
		new VirtualDevice(DeviceIds.Generic, "Generic", [new ChannelRef(SourceIds.Raspberry, "pin24")], "🔊", "🔇"),
		new VirtualDevice(DeviceIds.Computer, "Computer", [new ChannelRef(SourceIds.Computer, DeviceIds.Computer)], "🖥", "💤",
			PoweredBy: DeviceIds.Power, InStatus: true),
		new VirtualDevice(DeviceIds.Room, "Room",
		[
			new ChannelRef(SourceIds.Raspberry, "pin25"),
			new ChannelRef(SourceIds.Raspberry, "pin24"),
			new ChannelRef(SourceIds.Raspberry, "pin23")
		], "🏠", "🌑")
	],
	[
		new VirtualSensor(SensorIds.Motion, "Motion", SourceIds.Station, SensorIds.Motion, "", ReadingKind.Boolean, "💠", "🔮",
			Presence: PresenceRole.Reports, InStatus: true),
		new VirtualSensor(SensorIds.Light, "Light", SourceIds.Station, SensorIds.Light, " lux", ReadingKind.Number, "🔆", "🌑", Min: 0, Max: 1000),
		new VirtualSensor(SensorIds.Temperature, "Temperature", SourceIds.Station, SensorIds.Temperature, "°C", ReadingKind.Number, "🔥", "🌡",
			Min: 10, Max: 40, InStatus: true),
		new VirtualSensor(SensorIds.Humidity, "Humidity", SourceIds.Station, SensorIds.Humidity, " %", ReadingKind.Number, "💦", "💧", Min: 0, Max: 100),
		new VirtualSensor(SensorIds.Co2, "CO₂", SourceIds.Station, SensorIds.Co2, " ppm", ReadingKind.Number, "☁️", "🧪", Min: 400, Max: 2000),
		new VirtualSensor(SensorIds.Tvoc, "TVOC", SourceIds.Station, SensorIds.Tvoc, " ppb", ReadingKind.Number, "☣️", "🦠", Min: 0, Max: 1000),
		new VirtualSensor(SensorIds.Presence, "Presence", SourceIds.Kernel, SensorIds.Presence, "", ReadingKind.Boolean, "🚶", "🫥"),
		new VirtualSensor(SensorIds.Night, "Night", SourceIds.Kernel, SensorIds.Night, "", ReadingKind.Boolean, "🌙", "☀️"),
		new VirtualSensor(SensorIds.Sleep, "Sleep", SourceIds.Kernel, SensorIds.Sleep, "", ReadingKind.Boolean, "🛌", "👁"),
		new VirtualSensor("pc_cpu", "CPU", SourceIds.Computer, "cpu", " %", ReadingKind.Number, "🔥", "🖥", Min: 0, Max: 100),
		new VirtualSensor("pc_cpu_temp", "CPU Temp", SourceIds.Computer, "cpu_temp", "°C", ReadingKind.Number, "🔥", "🌡", Min: 30, Max: 100),
		new VirtualSensor("pc_gpu", "GPU", SourceIds.Computer, "gpu", " %", ReadingKind.Number, "🔥", "🎮", Min: 0, Max: 100),
		new VirtualSensor("pc_gpu_temp", "GPU Temp", SourceIds.Computer, "gpu_temp", "°C", ReadingKind.Number, "🔥", "🌡", Min: 30, Max: 100),
		new VirtualSensor("pc_gpu_power", "GPU Power", SourceIds.Computer, "gpu_power", " W", ReadingKind.Number, "⚡", "🔌", Min: 0, Max: 180),
		new VirtualSensor("pc_ram", "RAM", SourceIds.Computer, "ram", " %", ReadingKind.Number, "🔥", "🧠", Min: 0, Max: 100),
		new VirtualSensor("pi_cpu", "CPU", SourceIds.Raspberry, "cpu", " %", ReadingKind.Number, "🔥", "🍓", Min: 0, Max: 100),
		new VirtualSensor("pi_cpu_temp", "CPU Temp", SourceIds.Raspberry, "cpu_temp", "°C", ReadingKind.Number, "🔥", "🌡", Min: 25, Max: 100),
		new VirtualSensor("pi_ram", "RAM", SourceIds.Raspberry, "ram", " %", ReadingKind.Number, "🔥", "🧠", Min: 0, Max: 100),
		new VirtualSensor("pi_disk", "Disk", SourceIds.Raspberry, "disk", " %", ReadingKind.Number, "🔥", "💾", Min: 0, Max: 100),
		// The desk keeps presence alive while it is in use, but a machine can be woken from anywhere,
		// so it never gets to announce that somebody is here
		new VirtualSensor("at_desk", "Active", SourceIds.Computer, "at_desk", "", ReadingKind.Boolean, "🪑", "🚪",
			Presence: PresenceRole.Sustains),
		new VirtualSensor("locked", "LockScreen", SourceIds.Computer, "locked", "", ReadingKind.Boolean, "🔒", "🔓")
	]);

	public static readonly IReadOnlyList<Warning> Warnings =
	[
		new("air-quality", "Air quality", "Air quality low, open the window",
		[
			new Trigger(SensorIds.Presence, TriggerKind.IsTrue),
			new Trigger(SensorIds.Co2, TriggerKind.Above, High: 1000, Sustains: false),
			new Trigger(SensorIds.Tvoc, TriggerKind.Above, High: 600, Sustains: false)
		], Icon: "💨")
	];

	public static readonly IReadOnlyList<Bind> Binds =
	[
		new("lamp-on-motion", DeviceIds.Lamp,
		[
			new Trigger(SensorIds.Presence, TriggerKind.IsTrue),
			new Trigger(SensorIds.Light, TriggerKind.Below, Low: 60, Sustains: false),
			new Trigger(SensorIds.Sleep, TriggerKind.IsFalse)
		], Name: "Lamp follows the room")
	];
}

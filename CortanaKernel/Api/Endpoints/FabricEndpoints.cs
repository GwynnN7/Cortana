using CortanaLib.Runtime;
using CortanaKernel.Infrastructure.Persistence;
using CortanaKernel.Application;
using CortanaKernel.Domain.Fabric;
using CortanaLib.Primitives;
using CortanaLib.Contracts;

namespace CortanaKernel.Api.Endpoints;

public static class FabricEndpoints
{
	public static void MapFabric(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder sources = app.MapGroup("/sources").WithTags("Fabric");

		sources.MapGet("", (Fabric fabric, HttpRequest request) =>
			{
				IReadOnlyList<SourceView> all = fabric.Views();
				string text = string.Join("\n", all.Select(view =>
					$"{view.Name} ({view.Kind}): {view.State}, {view.Outputs} outputs, {view.Inputs} inputs"));

				return ApiResults.Ok(request, text, new SourceListResponse(all));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Every hardware source and what it offers").Produces<SourceListResponse>();

		app.MapGet("/channels", (Fabric fabric, HttpRequest request) =>
			{
				IReadOnlyList<ChannelView> all = fabric.Channels();
				string text = string.Join("\n", all.Select(view =>
					$"{view.Source}/{view.Channel} ({(view.IsOutput ? "output" : "input")}): {(view.Registered ? view.RegisteredAs : "free")}"));

				return ApiResults.Ok(request, text, new ChannelListResponse(all));
			})
			.Access(ApiAccess.ReadOnly).WithTags("Fabric").WithSummary("Every channel the hardware exposes, and whether something is registered on it")
			.Produces<ChannelListResponse>();

		RouteGroupBuilder registrations = app.MapGroup("/registrations").WithTags("Fabric");

		registrations.MapGet("", (Fabric fabric, HttpRequest request) =>
			{
				var all = new Registrations(
					[.. fabric.Devices(_ => null).Select(view => fabric.Device(view.Device)!)],
					[.. fabric.Sensors().Select(view => fabric.Sensor(view.Sensor)!)]);

				return ApiResults.Ok(request, $"{all.Devices.Count} devices, {all.Sensors.Count} sensors", all);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Every virtual device and sensor you have registered").Produces<Registrations>();

		registrations.MapPost("/devices", (VirtualDevice body, Fabric fabric, HttpRequest request) =>
				ApiResults.From(request, fabric.Register(body), device => $"{device.Name} is on {string.Join(", ", device.Channels.Select(channel => $"{channel.Source}/{channel.Channel}"))}"))
			.Access(ApiAccess.Sensitive).WithSummary("Create a virtual device on a hardware channel");

		registrations.MapPost("/sensors", (VirtualSensor body, Fabric fabric, HttpRequest request) =>
				ApiResults.From(request, fabric.Register(body), sensor => $"{sensor.Name} reads {sensor.Source}/{sensor.Channel}"))
			.Access(ApiAccess.Sensitive).WithSummary("Create a virtual sensor on a hardware channel");

		registrations.MapDelete("/{id}", (string id, Fabric fabric, BindStore binds, WarningStore alerts,
				AutomationService automation, HttpRequest request) =>
			{
				Result<string> removed = fabric.Unregister(id);
				if (!removed.IsOk) return ApiResults.From(request, removed);

				string[] cascade = [.. binds.Purge(id, id), .. alerts.Purge(id)];
				automation.Engine.Evaluate();

				return ApiResults.From(request, Result.Ok(cascade.Length == 0
					? removed.Value
					: $"{removed.Value}, and {string.Join("; ", cascade)}"));
			})
			.Access(ApiAccess.Sensitive).WithSummary("Remove a virtual device or sensor, and anything bound to it");

		app.MapGet("/layout", (JsonLayoutRepository layout, HttpRequest request) =>
			{
				DashboardLayout current = layout.Load();
				return ApiResults.Ok(request, $"{current.Sensors.Count} sensors, {current.Devices.Count} devices on the dashboard", current);
			})
			.Access(ApiAccess.ReadOnly).WithTags("Fabric").WithSummary("What the dashboard shows").Produces<DashboardLayout>();

		app.MapPost("/layout", (DashboardLayout body, JsonLayoutRepository layout, HttpRequest request) =>
			{
				layout.Save(body);
				return ApiResults.Message(request, "Dashboard updated");
			})
			.Access(ApiAccess.Sensitive).WithTags("Fabric").WithSummary("Choose what the dashboard shows");

		RouteGroupBuilder warnings = app.MapGroup("/warnings").WithTags("Fabric");

		warnings.MapGet("", (WarningStore store, WarningState state, HttpRequest request) =>
			{
				IReadOnlyList<WarningView> all =
					[.. store.All().Select(warning => new WarningView(warning, state.IsActive(warning.Id), state.Since(warning.Id)))];

				string text = all.Count == 0
					? "Nothing is watched"
					: string.Join("\n", all.Select(view =>
						$"[{view.Warning.Id}] {view.Warning.Name}: {(view.Active ? "active" : "quiet")} " +
						$"({string.Join(", ", view.Warning.Triggers.Select(trigger => TriggerText.Describe(trigger)))})"));

				return ApiResults.Ok(request, text, new WarningListResponse(all, store.Adrift(FabricDefaults.Warnings)));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("What Cortana watches for, and whether it is firing").Produces<WarningListResponse>();

		warnings.MapPost("", (Warning body, WarningStore store, HttpRequest request) =>
				ApiResults.From(request, store.Save(body), warning => $"Watching {warning.Triggers.Count} sensor(s) for {warning.Name}"))
			.Access(ApiAccess.Sensitive).WithSummary("Create or replace a warning");

		warnings.MapPost("/{id}/restore", (string id, WarningStore store, HttpRequest request) =>
				ApiResults.From(request, store.Restore(id, FabricDefaults.Warnings),
					warning => $"{warning.Id} is back to what ships with Cortana"))
			.Access(ApiAccess.Sensitive).WithSummary("Puts one shipped warning back to its default triggers");

		warnings.MapDelete("/{id}", (string id, WarningStore store, HttpRequest request) =>
				ApiResults.From(request, store.Delete(id)))
			.Access(ApiAccess.Sensitive).WithSummary("Remove a warning");

		RouteGroupBuilder binds = app.MapGroup("/binds").WithTags("Fabric");

		binds.MapGet("", (BindStore store, AutomationService automation, HttpRequest request) =>
			{
				IReadOnlyList<Bind> all = store.All();
				IReadOnlyList<BindStatusView> status = automation.Engine.BindStatus;

				string text = all.Count == 0
					? "Nothing is bound"
					: string.Join("\n", all.Select(bind =>
						$"[{(bind.Name.Length > 0 ? bind.Name : bind.Id)}] {bind.Device} <- " +
						$"{string.Join($" {bind.Mode} ", bind.Triggers.Select(trigger => trigger.Sensor))}" +
						$"{(status.FirstOrDefault(entry => entry.Bind == bind.Id) is { } current ? $", {current.Outcome}: {current.Reason}" : "")}"));

				return ApiResults.Ok(request, text, new BindListResponse(all, status, store.Adrift(FabricDefaults.Binds)));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Which sensors drive which devices").Produces<BindListResponse>();

		binds.MapPost("", (Bind body, BindStore store, AutomationService automation, HttpRequest request) =>
			{
				Result<Bind> saved = store.Save(body);
				if (saved.IsOk) automation.Engine.Evaluate();

				return ApiResults.From(request, saved, bind => $"Bound {bind.Device} to {bind.Triggers.Count} trigger(s)" +
					(bind.Triggers.Any(trigger => trigger.Sustains)
						? ""
						: ", though nothing keeps it on so the bind will never switch it off"));
			})
			.Access(ApiAccess.Sensitive).WithSummary("Create or replace a binding");

		binds.MapPost("/{id}/restore", (string id, BindStore store, AutomationService automation, HttpRequest request) =>
			{
				Result<Bind> restored = store.Restore(id, FabricDefaults.Binds);
				if (restored.IsOk) automation.Engine.Evaluate();

				return ApiResults.From(request, restored, bind => $"{bind.Id} is back to what ships with Cortana");
			})
			.Access(ApiAccess.Sensitive).WithSummary("Puts one shipped bind back to its default triggers");

		binds.MapDelete("/{id}", (string id, BindStore store, AutomationService automation, HttpRequest request) =>
			{
				Result<string> removed = store.Delete(id);
				if (removed.IsOk) automation.Engine.Evaluate();

				return ApiResults.From(request, removed);
			})
			.Access(ApiAccess.Sensitive).WithSummary("Remove a binding");
	}
}

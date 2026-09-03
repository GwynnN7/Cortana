using Microsoft.AspNetCore.Components;

namespace CortanaWeb.Components.Shared;

/// Every page that calls the Kernel and shows what came back does the same three things
public abstract class ActingPage : ComponentBase
{
	protected string? Message;
	protected bool Busy;

	protected async Task Run(Func<Task<string>> action)
	{
		Busy = true;
		try
		{
			Message = await action();
			await AfterRun();
		}
		finally
		{
			Busy = false;
		}
	}

	protected virtual Task AfterRun() => Task.CompletedTask;
}

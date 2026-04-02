using CortanaLib;
using CortanaLib.Structures;
using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Timer = CortanaLib.Structures.Timer;

namespace CortanaTelegram.Modules;

internal sealed class SensorModule : IModuleInterface
{
	private static int TabIndex = 0;
	private static Timer? UpdateTimer = null;

	public static async Task CreateMenu(ITelegramBotClient cortana, CallbackQuery? query = null)
	{
		await cortana.SendChatAction(Utils.HomeId, ChatAction.Typing);
		Utils.ChatArgs.TryRemove(Utils.Topics.Sensors, out _);

		string messageText = await GetSensorDashboard();

		if (query != null && query.Message != null)
		{
			try
			{
				await cortana.EditMessageText(query.Message.Chat.Id, query.Message.MessageId, messageText, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
			}
			catch
			{
				await cortana.AnswerCallbackQuery(query.Id);
			}
			ResetUpdateTimer(cortana, query);
		}
		else
		{
			await Utils.SendToTopic(messageText, Utils.Topics.Sensors, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
		}
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery query, string command)
	{
		int messageId = query.Message!.MessageId;
		long chatId = query.Message.Chat.Id;

		var task = command switch
		{
			ActionTag.Refresh => CreateMenu(cortana, query),
			ActionTag.Tab => Task.Run(async () =>
			{
				TabIndex = (TabIndex + 1) % 2;
				await CreateMenu(cortana, query);
			}),
			_ => null

		};

		if (task != null)
		{
			await task;
			return;
		}

		var chatArg = command switch
		{
			ActionTag.SetLightThreshold => new ChatArgs(EArgsType.SetLightThreshold, query, query.Message),
			ActionTag.SetCO2Threshold => new ChatArgs(EArgsType.SetCO2Threshold, query, query.Message),
			ActionTag.SetTvocThreshold => new ChatArgs(EArgsType.SetTvocThreshold, query, query.Message),
			ActionTag.SetMorningHour => new ChatArgs(EArgsType.SetMorningHour, query, query.Message),
			ActionTag.SetMotionOffMax => new ChatArgs(EArgsType.SetMotionOffMax, query, query.Message),
			ActionTag.SetMotionOffMin => new ChatArgs(EArgsType.SetMotionOffMin, query, query.Message),
			_ => null
		};

		if (chatArg != null)
		{
			if (Utils.AddChatArg(Utils.Topics.Sensors, chatArg, query))
			{
				ResetUpdateTimer(cortana, query);
				string prompt = command switch
				{
					ActionTag.SetLightThreshold => "Set Light Threshold",
					ActionTag.SetCO2Threshold => "Set CO2 Threshold",
					ActionTag.SetTvocThreshold => "Set TVOC Threshold",
					ActionTag.SetMorningHour => "Set Morning Hour (0~23)",
					ActionTag.SetMotionOffMax => "Set Light Off Maximum Time",
					ActionTag.SetMotionOffMin => "Set Light Off Minimum Time",
					_ => "Sensors Settings"
				};
				await cortana.EditMessageText(chatId, messageId, prompt, replyMarkup: CreateCancelButton());
			}
		}
		else
		{
			switch (command)
			{
				case ActionTag.EnableMotionDetection:
					await ApiHandler.Post($"{ERoute.Settings}/{ESettings.AutomaticMode}", new PostValue((int)EMotionDetection.On));
					break;
				case ActionTag.DisableMotionDetection:
					await ApiHandler.Post($"{ERoute.Settings}/{ESettings.AutomaticMode}", new PostValue((int)EMotionDetection.Off));
					break;
				case ActionTag.Cancel:
					break;
			}
			await CreateMenu(cortana, query);
		}
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageData msgData, ChatArgs chatArg)
	{
		await cortana.SendChatAction(Utils.HomeId, ChatAction.Typing);

		if (int.TryParse(msgData.Message, out int sensorValue))
		{
			_ = chatArg.Type switch
			{
				EArgsType.SetLightThreshold => await ApiHandler.Post($"{ERoute.Settings}/{ESettings.LightThreshold}", new PostValue(sensorValue)),
				EArgsType.SetMorningHour => await ApiHandler.Post($"{ERoute.Settings}/{ESettings.MorningHour}", new PostValue(sensorValue)),
				EArgsType.SetMotionOffMax => await ApiHandler.Post($"{ERoute.Settings}/{ESettings.MotionOffMax}", new PostValue(sensorValue)),
				EArgsType.SetMotionOffMin => await ApiHandler.Post($"{ERoute.Settings}/{ESettings.MotionOffMin}", new PostValue(sensorValue)),
				EArgsType.SetCO2Threshold => await ApiHandler.Post($"{ERoute.Settings}/{ESettings.CO2Threshold}", new PostValue(sensorValue)),
				EArgsType.SetTvocThreshold => await ApiHandler.Post($"{ERoute.Settings}/{ESettings.TvocThreshold}", new PostValue(sensorValue)),
				_ => null
			};
		}

		await cortana.DeleteMessage(Utils.HomeId, msgData.MessageId);
		await CreateMenu(cortana, chatArg.Query);
	}

	private static async Task<string> GetSensorDashboard()
	{
		string temperature = (await ApiHandler.Get<SensorResponse>($"{ERoute.Sensors}/{ESensor.Temperature}")).Match(temp => $"{temp.Value}{temp.Unit}", () => "Unknown");
		string light = (await ApiHandler.Get<SensorResponse>($"{ERoute.Sensors}/{ESensor.Light}")).Match(light => $"{light.Value}{light.Unit}", () => "Unknown");
		string humidity = (await ApiHandler.Get<SensorResponse>($"{ERoute.Sensors}/{ESensor.Humidity}")).Match(humidity => $"{humidity.Value}{humidity.Unit}", () => "Unknown");
		string co2 = (await ApiHandler.Get<SensorResponse>($"{ERoute.Sensors}/{ESensor.CO2}")).Match(co2 => $"{co2.Value}{co2.Unit}", () => "Unknown");
		string tvoc = (await ApiHandler.Get<SensorResponse>($"{ERoute.Sensors}/{ESensor.Tvoc}")).Match(tvoc => $"{tvoc.Value}{tvoc.Unit}", () => "Unknown");
		string motion = (await ApiHandler.Get<SensorResponse>($"{ERoute.Sensors}/{ESensor.Motion}")).Match(motion => bool.Parse(motion.Value) ? "🟢" : "🔴", () => "Unknown");

		string autoMode = (await ApiHandler.Get<SettingsResponse>($"{ERoute.Settings}/{ESettings.AutomaticMode}")).Match(autoMode => autoMode.Value.Contains("On") ? "🟢" : "🔴", () => "Unknown");
		string lightThreshold = (await ApiHandler.Get<SettingsResponse>($"{ERoute.Settings}/{ESettings.LightThreshold}")).Match(light => light.Value, () => "Unknown");
		string co2Threshold = (await ApiHandler.Get<SettingsResponse>($"{ERoute.Settings}/{ESettings.CO2Threshold}")).Match(co2 => co2.Value, () => "Unknown");
		string tvocThreshold = (await ApiHandler.Get<SettingsResponse>($"{ERoute.Settings}/{ESettings.TvocThreshold}")).Match(tvoc => tvoc.Value, () => "Unknown");
		string morningHour = (await ApiHandler.Get<SettingsResponse>($"{ERoute.Settings}/{ESettings.MorningHour}")).Match(morningHour => morningHour.Value, () => "Unknown");
		string motionOffMax = (await ApiHandler.Get<SettingsResponse>($"{ERoute.Settings}/{ESettings.MotionOffMax}")).Match(motionOffMax => motionOffMax.Value, () => "Unknown");
		string motionOffMin = (await ApiHandler.Get<SettingsResponse>($"{ERoute.Settings}/{ESettings.MotionOffMin}")).Match(motionOffMin => motionOffMin.Value, () => "Unknown");

		return $"\n📡 <b>Sensors Dashboard</b>\n====================\n💡 • <b>Light:</b> {light}\n🌡 • <b>Temperature:</b> {temperature}\n🌡 • <b>Humidity:</b> {humidity}\n🖲 • <b>CO2:</b> {co2}\n🖲 • <b>TVOC:</b> {tvoc}\n🖲 • <b>Motion Detected:</b> {motion}\n\n⚙️ <b>Sensor Settings</b>\n=================\n🖲 • <b>Automatic Mode:</b> {autoMode}\n💡 • <b>Light Threshold</b>: {lightThreshold}\n🖲 • <b>CO2 Threshold</b>: {co2Threshold}\n🖲 • <b>TVOC Threshold</b>: {tvocThreshold}\n🕒 • <b>Morning Hour</b>: {morningHour}\n⏳ • <b>Timer Min/Max</b>: {motionOffMin}/{motionOffMax}\n";
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		InlineKeyboardMarkup inlineKeyboard = new();

		switch (TabIndex)
		{
			case 0:
				inlineKeyboard
					.AddButton("Automatic Mode 🟢", ActionTag.EnableMotionDetection)
					.AddButton("Manual Mode 🔴", ActionTag.DisableMotionDetection);
				break;
			case 1:
				inlineKeyboard
					.AddButton("Motion Min ⏳", ActionTag.SetMotionOffMin)
					.AddButton("Motion Max ⏳", ActionTag.SetMotionOffMax)
					.AddNewRow()
					.AddButton("CO2 Threshold 🖲", ActionTag.SetCO2Threshold)
					.AddButton("TVOC Threshold 🖲", ActionTag.SetTvocThreshold)
					.AddNewRow()
					.AddButton("Light Threshold 💡", ActionTag.SetLightThreshold)
					.AddNewRow()
					.AddButton("Morning Hour 🕒", ActionTag.SetMorningHour);
				break;
		}

		return inlineKeyboard
			.AddNewRow()
			.AddButton("Refresh 🔄", ActionTag.Refresh)
			.AddButton("Tab ↔️", ActionTag.Tab);
	}

	private static InlineKeyboardMarkup CreateCancelButton()
	{
		return new InlineKeyboardMarkup().AddButton("<<", ActionTag.Cancel);
	}

	private struct ActionTag
	{
		public const string Refresh = "sensor-refresh";
		public const string Settings = "sensor-settings";
		public const string SetLightThreshold = "sensor-set_light";
		public const string SetCO2Threshold = "sensor-set_co2";
		public const string SetTvocThreshold = "sensor-set_tvoc";
		public const string EnableMotionDetection = "sensor-enable_automaticmode";
		public const string DisableMotionDetection = "sensor-disable_automaticmode";
		public const string SetMorningHour = "sensor-set_morninghour";
		public const string SetMotionOffMax = "sensor-set_motionoffmax";
		public const string SetMotionOffMin = "sensor-set_motionoffmin";
		public const string Tab = "sensor-tab";
		public const string Cancel = "sensor-cancel";
	}

	private static void ResetUpdateTimer(ITelegramBotClient cortana, CallbackQuery? query = null)
	{
		UpdateTimer?.Destroy();
		UpdateTimer = new Timer("sensor-updater", new TelegramTimerPayload<(ITelegramBotClient, CallbackQuery?)>(Utils.HomeId, (cortana, query)), async Task (object? sender) =>
		{
			if (sender is not Timer { TimerType: ETimerType.Telegram } timer) return;

			try
			{
				if (timer.Payload is not TelegramTimerPayload<(ITelegramBotClient cortana, CallbackQuery? query)> payload) return;
				await CreateMenu(payload.Arg.cortana, payload.Arg.query);
			}
			catch { }
		}, ETimerType.Telegram).Set((10, 0, 0));
	}
}
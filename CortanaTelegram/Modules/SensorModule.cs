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

	public static async Task CreateMenu(ITelegramBotClient cortana, CallbackQuery? query = null)
	{
		await cortana.SendChatAction(Utils.Data.HomeGroup, ChatAction.Typing);

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
			IModuleInterface.ResetUpdateTimer<SensorModule>("sensor-updater", cortana, query);
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
			ActionTag.Settings => Task.Run(async () =>
			{
				IModuleInterface.ResetUpdateTimer<SensorModule>("sensor-updater", cortana, query);
				await cortana.EditMessageReplyMarkup(chatId, messageId, replyMarkup: CreateSettingsButtons());
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
			ActionTag.SetMorningHour => new ChatArgs(EArgsType.SetMorningHour, query, query.Message),
			ActionTag.SetMotionOffMax => new ChatArgs(EArgsType.SetMotionOffMax, query, query.Message),
			ActionTag.SetMotionOffMin => new ChatArgs(EArgsType.SetMotionOffMin, query, query.Message),
			_ => null
		};

		if (chatArg != null)
		{
			if (Utils.AddChatArg(chatId, chatArg, query))
			{
				IModuleInterface.DestroyUpdateTimer();
				string prompt = command switch
				{
					ActionTag.SetLightThreshold => "Set Light Threshold (0~4096)",
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
					Utils.ChatArgs.TryRemove(chatId, out _);
					break;
			}
			await CreateMenu(cortana, query);
		}

	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageData msgData)
	{
		await cortana.SendChatAction(msgData.ChatId, ChatAction.Typing);

		if (int.TryParse(msgData.Message, out int sensorValue))
		{
			_ = Utils.ChatArgs[msgData.ChatId].Type switch
			{
				EArgsType.SetLightThreshold => await ApiHandler.Post($"{ERoute.Settings}/{ESettings.LightThreshold}", new PostValue(sensorValue)),
				EArgsType.SetMorningHour => await ApiHandler.Post($"{ERoute.Settings}/{ESettings.MorningHour}", new PostValue(sensorValue)),
				EArgsType.SetMotionOffMax => await ApiHandler.Post($"{ERoute.Settings}/{ESettings.MotionOffMax}", new PostValue(sensorValue)),
				EArgsType.SetMotionOffMin => await ApiHandler.Post($"{ERoute.Settings}/{ESettings.MotionOffMin}", new PostValue(sensorValue)),
				_ => null
			};
		}

		await cortana.DeleteMessage(msgData.ChatId, msgData.MessageId);
		await CreateMenu(cortana, Utils.ChatArgs[msgData.ChatId].Query);
		Utils.ChatArgs.TryRemove(msgData.ChatId, out _);
	}

	private static async Task<string> GetSensorDashboard()
	{
		string temperature = (await ApiHandler.Get<SensorResponse>($"{ERoute.Sensors}/{ESensor.Temperature}")).Match(temp => $"{temp.Value}{temp.Unit}", () => "Unknown");
		string light = (await ApiHandler.Get<SensorResponse>($"{ERoute.Sensors}/{ESensor.Light}")).Match(light => light.Value, () => "Unknown");
		string motion = (await ApiHandler.Get<SensorResponse>($"{ERoute.Sensors}/{ESensor.Motion}")).Match(motion => bool.Parse(motion.Value) ? "🟢" : "🔴", () => "Unknown");

		string autoMode = (await ApiHandler.Get<SettingsResponse>($"{ERoute.Settings}/{ESettings.AutomaticMode}")).Match(autoMode => autoMode.Value.Contains("On") ? "🟢" : "🔴", () => "Unknown");
		string lightThreshold = (await ApiHandler.Get<SettingsResponse>($"{ERoute.Settings}/{ESettings.LightThreshold}")).Match(light => light.Value, () => "Unknown");
		string morningHour = (await ApiHandler.Get<SettingsResponse>($"{ERoute.Settings}/{ESettings.MorningHour}")).Match(morningHour => morningHour.Value, () => "Unknown");
		string motionOffMax = (await ApiHandler.Get<SettingsResponse>($"{ERoute.Settings}/{ESettings.MotionOffMax}")).Match(motionOffMax => motionOffMax.Value, () => "Unknown");
		string motionOffMin = (await ApiHandler.Get<SettingsResponse>($"{ERoute.Settings}/{ESettings.MotionOffMin}")).Match(motionOffMin => motionOffMin.Value, () => "Unknown");

		return $"\n📡 <b>Sensors Dashboard</b>\n\n• 💡 <b>Light</b>: {light}\n• 🌡 <b>Temperature</b>: {temperature}\n• 🖲 <b>Motion Detected</b>: {motion}\n\n\n⚙️ <b>Current Sensor Settings</b>\n\n• 🖲 <b>Automatic Mode</b>: {autoMode}\n• 💡 <b>Light Threshold</b>: {lightThreshold}\n• 🕒 <b>Morning Hour</b>: {morningHour}\n• ⏳ <b>Time Off Min/Max</b>: {motionOffMin}/{motionOffMax}\n";
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Refresh 🔄", ActionTag.Refresh)
			.AddNewRow()
			.AddButton("Settings ⚙️", ActionTag.Settings);
	}

	private static InlineKeyboardMarkup CreateSettingsButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Automatic Mode 🟢", ActionTag.EnableMotionDetection)
			.AddButton("Manual Mode 🔴", ActionTag.DisableMotionDetection)
			.AddNewRow()
			.AddButton("Motion Max ⏳", ActionTag.SetMotionOffMax)
			.AddButton("Motion Min ⏳", ActionTag.SetMotionOffMin)
			.AddNewRow()
			.AddButton("Light Threshold 💡", ActionTag.SetLightThreshold)
			.AddNewRow()
			.AddButton("Morning Hour 🕒", ActionTag.SetMorningHour)
			.AddNewRow()
			.AddButton("<<", ActionTag.Cancel);
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
		public const string EnableMotionDetection = "sensor-enable_automaticmode";
		public const string DisableMotionDetection = "sensor-disable_automaticmode";
		public const string SetMorningHour = "sensor-set_morninghour";
		public const string SetMotionOffMax = "sensor-set_motionoffmax";
		public const string SetMotionOffMin = "sensor-set_motionoffmin";
		public const string Cancel = "sensor-cancel";
	}

}
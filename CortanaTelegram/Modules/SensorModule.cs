using CortanaLib;
using CortanaLib.Structures;
using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Modules;

internal sealed class SensorModule : IModuleInterface
{
	public static async Task CreateMenu(ITelegramBotClient cortana, Message? message = null)
	{
		await cortana.SendChatAction(Utils.Data.HomeGroup, ChatAction.Typing);

		string messageText = await GetSensorDashboard();

		if (message != null)
		{
			await cortana.EditMessageText(message.Chat.Id, message.MessageId, messageText, replyMarkup: CreateButtons());
		}
		else
		{
			await Utils.SendToTopic(messageText, Utils.Topics.Sensors, replyMarkup: CreateButtons());
		}
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery query, string command)
	{
		int messageId = query.Message!.MessageId;
		long chatId = query.Message.Chat.Id;

		var task = command switch
		{
			ActionTag.Refresh => CreateMenu(cortana, query.Message),
			ActionTag.Settings => cortana.EditMessageText(chatId, messageId, await GetSettingsText(), replyMarkup: CreateSettingsButtons()),
			ActionTag.Delete => cortana.DeleteMessage(chatId, messageId),
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
				string prompt = command switch
				{
					ActionTag.SetLightThreshold => "Set Light Threshold (0~4096)",
					ActionTag.SetMorningHour => "Set Morning Hour (0~23)",
					ActionTag.SetMotionOffMax => "Set Motion-Off Maximum Time",
					ActionTag.SetMotionOffMin => "Set Motion-Off Minimum Time",
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
					await ApiHandler.Post($"{ERoute.Settings}/{ESettings.MotionDetection}", new PostValue((int)EMotionDetection.On));
					break;
				case ActionTag.DisableMotionDetection:
					await ApiHandler.Post($"{ERoute.Settings}/{ESettings.MotionDetection}", new PostValue((int)EMotionDetection.Off));
					break;
				case ActionTag.Cancel:
					Utils.ChatArgs.TryRemove(chatId, out _);
					break;
			}
			await CreateMenu(cortana, query.Message);
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
		await cortana.EditMessageText(msgData.ChatId, Utils.ChatArgs[msgData.ChatId].Message.MessageId, await GetSettingsText(), replyMarkup: CreateSettingsButtons());
		Utils.ChatArgs.TryRemove(msgData.ChatId, out _);
	}

	private static async Task<string> GetSensorDashboard()
	{
		string light = await ApiHandler.Get($"{ERoute.Sensors}/{ESensor.Light}");
		string temp = await ApiHandler.Get($"{ERoute.Sensors}/{ESensor.Temperature}");
		string motion = await ApiHandler.Get($"{ERoute.Sensors}/{ESensor.Motion}");
		return $"Sensors Dashboard\n\n💡 Light: {light}\n🌡 Temperature: {temp}\n🖲 Motion Detected: {motion}";
	}

	private static async Task<string> GetSettingsText()
	{
		string motionDetection = await ApiHandler.Get($"{ERoute.Settings}/{ESettings.MotionDetection}");
		string lightThreshold = await ApiHandler.Get($"{ERoute.Settings}/{ESettings.LightThreshold}");
		string morningHour = await ApiHandler.Get($"{ERoute.Settings}/{ESettings.MorningHour}");
		string motionOffMax = await ApiHandler.Get($"{ERoute.Settings}/{ESettings.MotionOffMax}");
		string motionOffMin = await ApiHandler.Get($"{ERoute.Settings}/{ESettings.MotionOffMin}");

		return $"Current Settings:\n\n🖲 {motionDetection}\n💡 {lightThreshold}\n🕒 {morningHour}\n⏳ {motionOffMax}\n⏳ {motionOffMin}";
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Refresh 🔄", ActionTag.Refresh)
			.AddNewRow()
			.AddButton("Settings ⚙️", ActionTag.Settings)
			.AddNewRow()
			.AddButton("❌", ActionTag.Delete);
	}

	private static InlineKeyboardMarkup CreateSettingsButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Motion On 🟢", ActionTag.EnableMotionDetection)
			.AddButton("Motion Off 🔴", ActionTag.DisableMotionDetection)
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
		public const string EnableMotionDetection = "sensor-enable_motiondetection";
		public const string DisableMotionDetection = "sensor-disable_motiondetection";
		public const string SetMorningHour = "sensor-set_morninghour";
		public const string SetMotionOffMax = "sensor-set_motionoffmax";
		public const string SetMotionOffMin = "sensor-set_motionoffmin";
		public const string Delete = "sensor-delete";
		public const string Cancel = "sensor-cancel";
	}

}
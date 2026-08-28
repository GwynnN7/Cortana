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
	private static int _tabIndex;
	private const int TabCount = 2;

	public static async Task CreateMenu(ITelegramBotClient cortana, CallbackQuery? query = null)
	{
		await cortana.SendChatAction(Utils.HomeId, ChatAction.Typing);
		Utils.ChatArgs.TryRemove(Utils.Topics.Sensors, out _);

		string messageText = await GetSensorDashboard();

		if (query?.Message != null)
		{
			try
			{
				await cortana.EditMessageText(query.Message.Chat.Id, query.Message.MessageId, messageText, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
			}
			catch
			{
				await cortana.AnswerCallbackQuery(query.Id);
			}
			LiveMenu.Track(Utils.Topics.Sensors, query.Message.MessageId, GetSensorDashboard, CreateButtons);
		}
		else
		{
			Message sent = await Utils.SendToTopic(messageText, Utils.Topics.Sensors, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
			LiveMenu.Track(Utils.Topics.Sensors, sent.MessageId, GetSensorDashboard, CreateButtons);
		}
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery query, string command)
	{
		int messageId = query.Message!.MessageId;
		long chatId = query.Message.Chat.Id;

		switch (command)
		{
			case ActionTag.Refresh:
				await CreateMenu(cortana, query);
				return;
			case ActionTag.Tab:
				_tabIndex = (_tabIndex + 1) % TabCount;
				await CreateMenu(cortana, query);
				return;
		}

		if (SettingPrompts.TryGetValue(command, out (ESettings Setting, string Prompt) entry))
		{
			if (Utils.AddChatArg(Utils.Topics.Sensors, new ChatArgs<ESettings>(EArgsType.SetSetting, query, query.Message, entry.Setting), query))
				await cortana.EditMessageText(chatId, messageId, entry.Prompt, replyMarkup: CreateCancelButton());
			return;
		}

		switch (command)
		{
			case ActionTag.EnableAutomatic:
				await ApiHandler.Post($"{ERoute.Sensors}/settings/{ESettings.AutomaticMode}", new PostValue((int)EStatus.On));
				break;
			case ActionTag.DisableAutomatic:
				await ApiHandler.Post($"{ERoute.Sensors}/settings/{ESettings.AutomaticMode}", new PostValue((int)EStatus.Off));
				break;
			case ActionTag.ToggleLampRelay:
				await ApiHandler.Post($"{ERoute.Sensors}/settings/{ESettings.LampToggle}", new PostValue(-1));
				break;
		}

		await CreateMenu(cortana, query);
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageData msgData, ChatArgs chatArg)
	{
		await cortana.SendChatAction(Utils.HomeId, ChatAction.Typing);

		if (chatArg is ChatArgs<ESettings> setting && int.TryParse(msgData.Message, out int value))
			await ApiHandler.Post($"{ERoute.Sensors}/settings/{setting.Arg}", new PostValue(value));

		await cortana.DeleteMessage(Utils.HomeId, msgData.MessageId);
		await CreateMenu(cortana, chatArg.Query);
	}

	private static async Task<string> GetSensorDashboard()
	{
		IOption<SensorListResponse> sensorsOption = await ApiHandler.Get<SensorListResponse>($"{ERoute.Sensors}");
		IOption<SettingsListResponse> settingsOption = await ApiHandler.Get<SettingsListResponse>($"{ERoute.Sensors}/settings");

		string Sensor(ESensor sensor) => sensorsOption.Match(
			list =>
			{
				SensorResponse? found = list.Sensors.FirstOrDefault(s => s.Sensor == sensor.ToString());
				if (found == null || string.IsNullOrEmpty(found.Value)) return "Unknown";
				if (sensor != ESensor.Motion) return $"{found.Value}{found.Unit}";
				return found.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ? "🟢" : "🔴";
			},
			() => "Unknown");

		string Setting(ESettings setting) => settingsOption.Match(
			list => list.Settings.FirstOrDefault(s => s.Setting == setting.ToString())?.Value ?? "Unknown",
			() => "Unknown");

		string autoMode = Setting(ESettings.AutomaticMode) == nameof(EStatus.On) ? "🟢" : "🔴";

		return $"\n📡 <b>Sensors Dashboard</b>\n====================\n" +
			$"💡 • <b>Light:</b> {Sensor(ESensor.Light)}\n" +
			$"🌡 • <b>Temperature:</b> {Sensor(ESensor.Temperature)}\n" +
			$"💧 • <b>Humidity:</b> {Sensor(ESensor.Humidity)}\n" +
			$"🧪 • <b>CO2:</b> {Sensor(ESensor.CO2)}\n" +
			$"🦠 • <b>TVOC:</b> {Sensor(ESensor.Tvoc)}\n" +
			$"🖲 • <b>Motion Detected:</b> {Sensor(ESensor.Motion)}\n\n" +
			$"⚙️ <b>Sensor Settings</b>\n=================\n" +
			$"🖲 • <b>Automatic Mode:</b> {autoMode}\n" +
			$"💡 • <b>Light Threshold</b>: {Setting(ESettings.LightThreshold)}\n" +
			$"🧪 • <b>CO2 Threshold</b>: {Setting(ESettings.CO2Threshold)}\n" +
			$"🦠 • <b>TVOC Threshold</b>: {Setting(ESettings.TvocThreshold)}\n" +
			$"⚖️ • <b>Lamp Toggle</b>: {Setting(ESettings.LampToggle)}\n" +
			$"🌅 • <b>Morning Hour</b>: {Setting(ESettings.MorningHour)}\n" +
			$"🌇 • <b>Night Hour</b>: {Setting(ESettings.NightHour)}\n" +
			$"✋ • <b>Manual Minutes</b>: {Setting(ESettings.ManualModeMinutes)}\n" +
			$"⏳ • <b>Motion Min/Max</b>: {Setting(ESettings.MotionOffMin)}/{Setting(ESettings.MotionOffMax)}\n";
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		InlineKeyboardMarkup inlineKeyboard = new();

		switch (_tabIndex)
		{
			case 0:
				inlineKeyboard
					.AddButton("Automatic Mode 🟢", ActionTag.EnableAutomatic)
					.AddButton("Manual Mode 🔴", ActionTag.DisableAutomatic)
					.AddNewRow()
					.AddButton("Lamp Relay ⚖️", ActionTag.ToggleLampRelay);
				break;

			case 1:
				inlineKeyboard
					.AddButton("Motion Min ⏳", ActionTag.SetMotionOffMin)
					.AddButton("Motion Max ⏳", ActionTag.SetMotionOffMax)
					.AddNewRow()
					.AddButton("CO2 🧪", ActionTag.SetCO2Threshold)
					.AddButton("TVOC 🦠", ActionTag.SetTvocThreshold)
					.AddNewRow()
					.AddButton("Light 💡", ActionTag.SetLightThreshold)
					.AddButton("Manual ✋", ActionTag.SetManualMinutes)
					.AddNewRow()
					.AddButton("Morning 🌅", ActionTag.SetMorningHour)
					.AddButton("Night 🌇", ActionTag.SetNightHour);
				break;
		}

		return inlineKeyboard
			.AddNewRow()
			.AddButton("Refresh 🔄", ActionTag.Refresh)
			.AddButton("Tab ↔️", ActionTag.Tab);
	}

	private static InlineKeyboardMarkup CreateCancelButton() => new InlineKeyboardMarkup().AddButton("<<", ActionTag.Cancel);

	private static readonly Dictionary<string, (ESettings Setting, string Prompt)> SettingPrompts = new()
	{
		{ ActionTag.SetLightThreshold, (ESettings.LightThreshold, "Set Light Threshold (lux)") },
		{ ActionTag.SetCO2Threshold, (ESettings.CO2Threshold, "Set CO2 Threshold (ppm)") },
		{ ActionTag.SetTvocThreshold, (ESettings.TvocThreshold, "Set TVOC Threshold (ppb)") },
		{ ActionTag.SetMorningHour, (ESettings.MorningHour, "Set Morning Hour (0~23)") },
		{ ActionTag.SetNightHour, (ESettings.NightHour, "Set Night Hour (0~23)") },
		{ ActionTag.SetMotionOffMax, (ESettings.MotionOffMax, "Seconds before lamp off, computer on") },
		{ ActionTag.SetMotionOffMin, (ESettings.MotionOffMin, "Seconds before lamp off, computer off") },
		{ ActionTag.SetManualMinutes, (ESettings.ManualModeMinutes, "Minutes a manual touch holds automation off") }
	};

	private struct ActionTag
	{
		public const string Refresh = "sensor-refresh";
		public const string Tab = "sensor-tab";
		public const string Cancel = "sensor-cancel";

		public const string EnableAutomatic = "sensor-enable_automaticmode";
		public const string DisableAutomatic = "sensor-disable_automaticmode";
		public const string ToggleLampRelay = "sensor-toggle_lamp_relay";

		public const string SetLightThreshold = "sensor-set_light";
		public const string SetCO2Threshold = "sensor-set_co2";
		public const string SetTvocThreshold = "sensor-set_tvoc";
		public const string SetMorningHour = "sensor-set_morninghour";
		public const string SetNightHour = "sensor-set_nighthour";
		public const string SetMotionOffMax = "sensor-set_motionoffmax";
		public const string SetMotionOffMin = "sensor-set_motionoffmin";
		public const string SetManualMinutes = "sensor-set_manualminutes";
	}
}

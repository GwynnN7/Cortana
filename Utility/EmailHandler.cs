using EAGetMail;
using Newtonsoft.Json;

namespace Utility
{
    public static class EmailHandler
    {
        public static List<Func<UnreadEmailStructure, Task>> Callbacks;

        public static void Init()
        {
            Callbacks = new();

            List<EmailStructure>? emailData = null;
            if (File.Exists("Data/Email.json"))
            {
                var file = File.ReadAllText("Data/Email.json");

                emailData = JsonConvert.DeserializeObject<List<EmailStructure>>(file);
            }
            if (emailData == null) emailData = new List<EmailStructure>();

            foreach(var email in emailData)
            {
                Task.Run(() =>
                {
                    MailClient? oClient = null;
                    while (true)
                    {
                        try
                        {
                            if (oClient == null) throw new Exception();

                            oClient.WaitNewEmail(-1);
                            MailInfo mailInfo = oClient.GetMailInfos().First();
                            Mail mailData = oClient.GetMail(mailInfo);
                            oClient.MarkAsRead(mailInfo, true);

                            foreach (var callback in Callbacks)
                            {
                                var result = new UnreadEmailStructure();
                                result.Email = email;
                                result.FromName = mailData.From.Name;
                                result.FromAddress = mailData.From.Address;
                                result.Subject = mailData.Subject;
                                result.Content = mailData.TextBody.Length >= 2048 ? mailData.TextBody.Substring(0, 2048) :  mailData.TextBody;
                                callback(result);
                            }
                        }
                        catch
                        {
                            MailServer oServer = new MailServer("imap.gmail.com", email.Email, email.Password, ServerProtocol.Imap4);
                            oServer.SSLConnection = true;
                            oServer.Port = 993;

                            oClient = new MailClient("TryIt");
                            oClient.Connect(oServer);
                            oClient.GetMailInfosParam.Reset();
                            oClient.GetMailInfosParam.GetMailInfosOptions = GetMailInfosOptionType.NewOnly;
                        }
                    }
                });
            }
        }
    }

    public class EmailStructure
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public int Id { get; set; }
        public bool Log { get; set; }
    }

    public class UnreadEmailStructure
    {
        public EmailStructure Email { get; set; }
        public string FromName { get; set; }
        public string FromAddress { get; set; }
        public string Subject { get; set; }

        public string Content { get; set; }
    }
}

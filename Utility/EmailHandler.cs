using EAGetMail;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Data;


namespace Utility
{
    public static class EmailHandler
    {
        static Dictionary<MailServer, MailClient> Emails;
        static List<EmailStructure> EmailStructures;
        public static List<Func<string, string, string, Task>> Callbacks;

        static EmailHandler()
        {
            Callbacks = new();
            Emails = new Dictionary<MailServer, MailClient>();

            List<EmailStructure>? emailData = null;
            if (File.Exists("Data/Email.json"))
            {
                var file = File.ReadAllText("Data/Email.json");

                emailData = JsonConvert.DeserializeObject<List<EmailStructure>>(file);
            }
            if (emailData == null) emailData = new List<EmailStructure>();

            foreach(var email in emailData)
            {
                MailServer oServer = new MailServer("imap.gmail.com", email.Email, email.Password, ServerProtocol.Imap4);
                oServer.SSLConnection = true;
                oServer.Port = 993;

                MailClient oClient = new MailClient(email.Name);
                oClient.Connect(oServer);
                oClient.GetMailInfosParam.Reset();
                oClient.GetMailInfosParam.GetMailInfosOptions = GetMailInfosOptionType.NewOnly;

                Emails.Add(oServer, oClient);
            }

            EmailStructures = emailData;

            StartWaitingThreads();
        }

        public static List<UnreadEmailStructure> GetEmails()
        {
            var UnreadEmails = new List<UnreadEmailStructure>();
            foreach(var email in Emails)
            {
                UnreadEmailStructure newUnread = new UnreadEmailStructure();
                newUnread.Email = EmailStructures.Find(x => x.Email == email.Key.User) ?? EmailStructures.First();

                MailInfo[] infos = email.Value.GetMailInfos();
                newUnread.Number = infos.Length;

                foreach (var info in infos)
                {
                    Mail oMail = email.Value.GetMail(info);
                    newUnread.From = oMail.From.Address;
                    newUnread.Subject = oMail.Subject;

                    

                    if (!info.Read) email.Value.MarkAsRead(info, true);
                }
                UnreadEmails.Add(newUnread);
            }
            return UnreadEmails;
        }

        private static void StartWaitingThreads()
        {
            foreach(var email in Emails)
            {
                new Thread(() =>
                {
                    while (true)
                    {
                        email.Value.WaitNewEmail(-1);
                        Mail oMail = email.Value.GetMail(email.Value.GetMailInfos().First());
                        foreach(var callback in Callbacks)
                        {
                            callback(email.Key.User, oMail.From.Address, oMail.Subject);
                        }
                    }
                }).Start();
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
        public int Number { get; set; }
        public string From { get; set; }
        public string Subject { get; set; }
    }
}

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
                MailServer oServer = new MailServer("imap.gmail.com", email.Email, email.Password, ServerProtocol.Imap4);
                oServer.SSLConnection = true;
                oServer.Port = 993;

                new Thread(() =>
                {
                    MailClient? oClient = null;
                    while (true)
                    {
                        try
                        {
                            if (oClient == null) throw new Exception();

                            oClient.WaitNewEmail(-1);

                            Mail oMail = oClient.GetMail(oClient.GetMailInfos().First());
                            foreach (var callback in Callbacks)
                            {
                                var result = new UnreadEmailStructure();
                                result.Email = email;
                                result.FromName = oMail.From.Name;
                                result.FromAddress = oMail.From.Address;
                                result.Subject = oMail.Subject;
                                callback(result);
                            }
                        }
                        catch
                        {
                            oClient = new MailClient("TryIt");
                            oClient.Connect(oServer);
                            oClient.GetMailInfosParam.Reset();
                            oClient.GetMailInfosParam.GetMailInfosOptions = GetMailInfosOptionType.NewOnly;
                        }
                    }
                }).Start();
            }
        }

        /*
        public static List<UnreadEmailStructure> GetEmails()
        {
            var UnreadEmails = new List<UnreadEmailStructure>();
            foreach(var email in Emails)
            {
                UnreadEmailStructure newUnread = new UnreadEmailStructure();
                newUnread.Email = Emails.Find(x => x.Email == email.Key.User) ?? EmailStructures.First();

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
            
        }*/

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
    }
}

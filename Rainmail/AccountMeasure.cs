using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Rainmeter;

using MailKit;
using MailKit.Net.Imap;

namespace Rainmail
{
    public class AccountMeasure : Measure
    {
        private static List<AccountMeasure> list = new List<AccountMeasure>();

        private object locker = new object();
        private bool running = false;

        private string host = null;
        private int port = 993;
        private bool useSSL = true;
        private string username = null;
        private string password = null;
        private int limit = 100;
        private TemplateOption[] readTemplate = null;
        private TemplateOption[] unreadTemplate = null;

        private int totalMessages = -1;
        private int totalUnread = -1;
        private Email[] emails = null;
        private string output = "";

        public override void Reload(API api, ref double maxValue)
        {
            base.Reload(api, ref maxValue);

            host = api.ReadString("Host", null);
            port = api.ReadInt("Port", 993);
            useSSL = api.ReadString("UseSSL", "true") == "true";
            username = api.ReadString("Username", null);
            password = api.ReadString("Password", null);
            limit = api.ReadInt("Limit", 100);
            string readTemplate = api.ReadString("ReadTemplate", null);
            string unreadTemplate = api.ReadString("UnreadTemplate", null);

            if (string.IsNullOrWhiteSpace(host))
                API.Log(API.LogType.Error, $"Missing Host parameter for Measure: {Name}.");
            else if (port <= 0)
                API.Log(API.LogType.Error, $"Invalid Port parameter for Measure: {Name}.");
            else if (string.IsNullOrWhiteSpace(username))
                API.Log(API.LogType.Error, $"Missing Username parameter for Measure: {Name}.");
            else if (limit < -1)
                API.Log(API.LogType.Error, $"Invalid Limit parameter for Measure: {Name}.");
            else if (string.IsNullOrWhiteSpace(readTemplate) && string.IsNullOrWhiteSpace(unreadTemplate))
                API.Log(API.LogType.Error, $"Must provide a template parameter for Measure: {Name}.");
            else
            {
                if (string.IsNullOrWhiteSpace(readTemplate))
                    readTemplate = unreadTemplate;
                else if (string.IsNullOrWhiteSpace(unreadTemplate))
                    unreadTemplate = readTemplate;

                this.readTemplate = TemplateOption.ParseTemplate(readTemplate);
                this.unreadTemplate = TemplateOption.ParseTemplate(unreadTemplate);

                DoUpdate();
            }
        }

        public void DoUpdate()
        {
            UpdateEmails();

            UpdateOutput();

            lock (locker)
            {
                running = false;
            }
        }

        public override double Update()
        {
            if (!running)
            {
                lock (locker)
                {
                    running = true;
                    Task.Run(() => DoUpdate());
                }
            }

            return base.Update();
        }

        private void UpdateEmails()
        {
            if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password) && port > 0 && limit >= -1)
            {
                using (ImapClient client = new ImapClient())
                {
                    // For demo-purposes, accept all SSL certificates
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    client.Connect(host, port, useSSL);

                    client.AuthenticationMechanisms.Remove("XOAUTH2");

                    client.Authenticate(username, password);

                    IMailFolder inbox = client.Inbox;
                    inbox.Open(FolderAccess.ReadOnly);

                    totalMessages = inbox.Count;
                    totalUnread = inbox.Unread;

                    int index = Math.Max(inbox.Count - limit, 0);

                    emails = inbox
                        .Fetch(index, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId)
                        .Select(x => new Email()
                        {
                            From = x.Envelope.From.FirstOrDefault().ToString(),
                            Subject = x.Envelope.Subject,
                            Recieved = x.Date.UtcDateTime,
                            Read = x.Flags.HasValue && (x.Flags.Value & MessageFlags.Seen) == MessageFlags.Seen
                        })
                        .Reverse()
                        .ToArray();

                    client.Disconnect(true);
                }
            }
        }

        public void UpdateOutput()
        {
            output = "";

            if (emails?.Length > 0)
                output = string.Join("\n", emails.Select(x => FormatEmail(x, x.Read ? readTemplate : unreadTemplate)));
        }

        private string FormatEmail(Email email, TemplateOption[] options)
        {
            string output = "";

            foreach (TemplateOption option in options)
            {
                switch (option.Type)
                {
                    case TemplateOptionType.Literal:
                        output += option.Data;
                        break;
                    case TemplateOptionType.Recieved:
                        if (option.Data == null)
                            output += email.Recieved.ToString();
                        else
                            output += email.Recieved.ToString(option.Data);
                        break;
                    case TemplateOptionType.Sender:
                        if (option.Data == null)
                            output += email.From;
                        else if (int.TryParse(option.Data, out int length))
                            output += PadOrTrim(email.From, length);
                        break;
                    case TemplateOptionType.Subject:
                        if (option.Data == null)
                            output += email.Subject;
                        else if (int.TryParse(option.Data, out int length))
                            output += PadOrTrim(email.Subject, length);
                        break;
                }
            }

            return output;
        }

        private string PadOrTrim(string value, int length)
        {
            string output = "";

            if (value.Length > length)
                output = value.Substring(0, length - 3) + "...";
            else if (value.Length == length)
                output = value;
            else
            {
                int y;
                if (string.IsNullOrWhiteSpace(value))
                    y = length;
                else
                {
                    output = value;
                    y = (length - value.Length);
                }

                for (int i = 0; i < y; i++)
                    output += " ";
            }

            return output;
        }

        public override string GetString()
        {
            return output;
        }

        public static AccountMeasure Find(IntPtr skin, string name)
        {
            return list
                .Where(x => x.Skin == skin && x.Name == name)
                .FirstOrDefault();
        }
    }
}

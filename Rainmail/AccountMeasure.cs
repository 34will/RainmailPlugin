using System;
using System.Collections.Generic;
using System.Linq;

using Rainmeter;

using MailKit;
using MailKit.Net.Imap;

namespace Rainmail
{
    public class AccountMeasure : Measure
    {
        private static List<AccountMeasure> list = new List<AccountMeasure>();

        private string host = null;
        private int port = 993;
        private bool useSSL = true;
        private string username = null;
        private string password = null;
        private int limit = 100;

        public override void Reload(API api, ref double maxValue)
        {
            base.Reload(api, ref maxValue);

            host = api.ReadString("Host", null);
            port = api.ReadInt("Port", 993);
            useSSL = api.ReadString("UseSSL", "true") == "true";
            username = api.ReadString("Username", null);
            password = api.ReadString("Password", null);
            limit = api.ReadInt("Limit", 100);

            if (string.IsNullOrWhiteSpace(host))
                API.Log(API.LogType.Error, $"Missing Host parameter for Measure: {Name}.");
            else if (port <= 0)
                API.Log(API.LogType.Error, $"Invalid Port parameter for Measure: {Name}.");
            else if (string.IsNullOrWhiteSpace(username))
                API.Log(API.LogType.Error, $"Missing Username parameter for Measure: {Name}.");
            else if (limit < -1)
                API.Log(API.LogType.Error, $"Invalid Limit parameter for Measure: {Name}.");
            else
                UpdateEmails();
        }

        public override double Update()
        {
            UpdateEmails();

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

                    TotalMessages = inbox.Count;
                    TotalUnread = inbox.Unread;

                    Emails = inbox
                        .Fetch(0, limit, MessageSummaryItems.Full | MessageSummaryItems.UniqueId)
                        .Select(x => new Email()
                        {
                            From = x.Envelope.From.FirstOrDefault().ToString(),
                            Subject = x.Envelope.Subject,
                            Recieved = x.Date.UtcDateTime,
                            Read = !(x.Flags.HasValue && (x.Flags.Value & MessageFlags.Seen) == MessageFlags.Seen)
                        })
                         .ToArray();

                    client.Disconnect(true);
                }
            }
        }

        public static AccountMeasure Find(IntPtr skin, string name)
        {
            return list
                .Where(x => x.Skin == skin && x.Name == name)
                .FirstOrDefault();
        }

        // ----- Properties ----- //

        public int TotalMessages { private set; get; }

        public int TotalUnread { private set; get; }

        public Email[] Emails { private set; get; }
    }
}

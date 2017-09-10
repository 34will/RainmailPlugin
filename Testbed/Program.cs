using System;
using System.Linq;

using MailKit;
using MailKit.Net.Imap;

namespace Testbed
{
    public class Program
    {
        private static string host = "";
        private static int port = 993;
        private static bool useSSL = true;
        private static string username = "";
        private static int limit = 10;

        public static void Main(string[] args)
        {
            Console.Write("Enter Password: ");
            string password = ReadPassword();

            Console.WriteLine("");

            using (ImapClient client = new ImapClient())
            {
                // For demo-purposes, accept all SSL certificates
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                client.Connect(host, port, useSSL);

                client.AuthenticationMechanisms.Remove("XOAUTH2");

                client.Authenticate(username, password);

                IMailFolder inbox = client.Inbox;
                inbox.Open(FolderAccess.ReadOnly);

                Console.WriteLine($"Total: {inbox.Count}");
                Console.WriteLine($"Unread: {inbox.Unread}");

                int index = Math.Max(inbox.Count - limit, 0);

                foreach (IMessageSummary x in inbox.Fetch(index, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId).ToArray().Reverse())
                {
                    Console.WriteLine($"From: {PadOrTrim(x.Envelope.From.FirstOrDefault().ToString(), 50)}, Subject: {x.Envelope.Subject}, Recieved: {x.Date.UtcDateTime}, Read: {x.Flags.HasValue && (x.Flags.Value & MessageFlags.Seen) == MessageFlags.Seen}.");
                }

                client.Disconnect(true);
            }

            Console.ReadLine();
        }

        private static string PadOrTrim(string value, int length)
        {
            string output = "";

            if (value.Length > length)
                output = value.Substring(0, length);
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

        public static string ReadPassword()
        {
            string output = "";
            while (true)
            {
                ConsoleKeyInfo i = Console.ReadKey(true);
                if (i.Key == ConsoleKey.Enter)
                    break;
                else if (i.Key == ConsoleKey.Backspace)
                {
                    if (output.Length > 0)
                    {
                        output.Remove(0, output.Length - 1);
                        Console.Write("\b \b");
                    }
                }
                else
                {
                    output += i.KeyChar;
                    Console.Write("*");
                }
            }
            return output;
        }
    }
}

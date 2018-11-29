using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using Rainmeter;

using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using System.Drawing;
using System.Windows.Forms;

namespace Rainmail
{
    public class AccountMeasure : Measure
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private static List<AccountMeasure> list = new List<AccountMeasure>();
        private static Regex icons = new Regex("\uD83C[\uDF00-\uDFFF]|\uD83D[\uDC00-\uDEFF]|[\u2600-\u26FF]");

        private readonly object locker = new object();
        private bool running = false;
        private bool inactive = false;
        private IntPtr hwnd = IntPtr.Zero;

        private string host = null;
        private int port = 993;
        private bool useSSL = true;
        private string username = null;
        private SecureString password = null;
        private int limit = 100;
        private TemplateOption[] readTemplate = null;
        private TemplateOption[] unreadTemplate = null;
        private string folderName = null;
        private bool removeIcons = true;

        private int totalMessages = -1;
        private int totalUnread = -1;
        private Email[] emails = null;
        private string output = "Loading...";

        public override void Reload(API api, ref double maxValue)
        {
            base.Reload(api, ref maxValue);

            hwnd = api.GetSkinWindow();

            host = api.ReadString("Host", null);
            port = api.ReadInt("Port", 993);
            useSSL = api.ReadInt("UseSSL", 1) == 1;
            username = api.ReadString("Username", null);

            string password = api.ReadString("Password", null);
            if (string.IsNullOrEmpty(password) && api.ReadInt("EmptyPassword", 0) == 0)
                password = null;

            limit = api.ReadInt("Limit", 100);

            folderName = api.ReadString("FolderName", "Inbox");
            if (string.IsNullOrWhiteSpace(folderName) || folderName.ToLower() == "inbox")
                folderName = null;

            removeIcons = api.ReadInt("RemoveIcons", 0) == 1;

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

                if (password != null)
                    AssignPassword(password);
            }
        }

        public override void Finished()
        {
            if (password != null)
                password.Dispose();
        }

        private void AssignPassword(string value)
        {
            if (value == null)
                inactive = true;
            else
            {
                if (password != null)
                    password.Dispose();

                password = new SecureString();
                foreach (char c in value)
                    password.AppendChar(c);

                password.MakeReadOnly();
            }
        }

        private async Task DoUpdate()
        {
            if (!inactive && password == null)
            {
                RECT skinWindow = new RECT();
                GetWindowRect(hwnd, ref skinWindow);
                AssignPassword(await InputForm.QueryString(new Point(skinWindow.Left + 5, skinWindow.Top + 5)));
            }

            emails = null;
            if (inactive)
                output = "Cancelled. Reload to try again.";
            else
            {
                UpdateEmails();

                if (emails?.Length > 0)
                    output = string.Join("\n", emails.Select(x => FormatEmail(x, x.Read ? readTemplate : unreadTemplate, removeIcons)));
            }

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
                }
                Task.Run(async () => await DoUpdate());
            }

            return base.Update();
        }

        private void UpdateEmails()
        {
            if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(username) && password != null && port > 0 && limit >= -1)
            {
                using (ImapClient client = new ImapClient())
                {
                    // For demo-purposes, accept all SSL certificates
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    client.Connect(host, port, useSSL);

                    client.AuthenticationMechanisms.Remove("XOAUTH2");

                    try
                    {
                        client.Authenticate(username, GetString(password));

                        IMailFolder folder = null;
                        if (folderName == null)
                            folder = client.Inbox;
                        else
                            folder = client.GetFolder(folderName);

                        folder.Open(FolderAccess.ReadOnly);

                        totalMessages = folder.Count;
                        totalUnread = folder.Unread;

                        int index = Math.Max(folder.Count - limit, 0);

                        emails = folder
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
                    }
                    catch (AuthenticationException)
                    {
                        password = null;
                        output = "Invalid username and password combination.";
                    }
                    catch (FolderNotFoundException)
                    {
                        output = $"A folder with the name: \"{folderName}\" could not be found.";
                        inactive = true;
                    }
                    catch (Exception e)
                    {
                        output = "Something went wrong.";
                        API.Log(API.LogType.Error, e.Message);
                    }
                    finally
                    {
                        client.Disconnect(true);
                    }
                }
            }
        }

        public override string GetString()
        {
            return output;
        }

        private static string FormatEmail(Email email, TemplateOption[] options, bool removeIcons)
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

            if (output.Length > 0 && removeIcons)
                output = icons.Replace(output, string.Empty);

            return output;
        }

        private static string PadOrTrim(string value, int length)
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

        public static string GetString(SecureString value)
        {
            IntPtr valuePtr = IntPtr.Zero;
            try
            {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(value);
                return Marshal.PtrToStringUni(valuePtr);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
            }
        }

        public static AccountMeasure Find(IntPtr skin, string name)
        {
            return list
                .Where(x => x.Skin == skin && x.Name == name)
                .FirstOrDefault();
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);
    }
}

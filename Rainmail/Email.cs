using System;

namespace Rainmail
{
    public class Email
    {
        public string From { set; get; }
        public string Subject { set; get; }
        public DateTime Recieved { set; get; }
        public bool Read { set; get; }
    }
}

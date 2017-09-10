using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Rainmail
{
    public enum TemplateOptionType
    {
        Literal = 0,
        Recieved,
        Sender,
        Subject
    }

    public class TemplateOption
    {
        private static Regex templateRegex = new Regex(@"\{(.*?)\}", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        public TemplateOptionType Type;
        public string Data;

        public static TemplateOption[] ParseTemplate(string template)
        {
            List<TemplateOption> options = new List<TemplateOption>();
            MatchCollection matches = templateRegex.Matches(template);
            int start = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                if (match.Index > start)
                {
                    options.Add(new TemplateOption()
                    {
                        Type = TemplateOptionType.Literal,
                        Data = template.Substring(start, match.Index - start)
                    });
                }

                string[] values = match.Value.Substring(1, match.Value.Length - 2).Split(',');
                string typeString = values[0].ToLower();
                TemplateOptionType type = TemplateOptionType.Literal;
                if (typeString == "date")
                    type = TemplateOptionType.Recieved;
                else if (typeString == "sender")
                    type = TemplateOptionType.Sender;
                else if (typeString == "subject")
                    type = TemplateOptionType.Subject;

                string data = null;
                if (type == TemplateOptionType.Literal)
                    data = "{" + match.Value + "}";
                else if (values.Length > 1)
                    data = values[1];

                options.Add(new TemplateOption()
                {
                    Type = type,
                    Data = data
                });

                start = match.Index + match.Length;
            }

            if (template.Length > start)
            {
                options.Add(new TemplateOption()
                {
                    Type = TemplateOptionType.Literal,
                    Data = template.Substring(start, template.Length - start)
                });
            }

            return options.ToArray();
        }
    }
}

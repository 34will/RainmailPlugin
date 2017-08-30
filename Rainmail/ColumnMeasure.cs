using System;
using System.Linq;

using Rainmeter;

namespace Rainmail
{
    public enum ColumnType
    {
        None = 0,
        From,
        Subject,
        Recieved
    }

    public class ColumnMeasure : Measure
    {
        private AccountMeasure parent = null;
        private ColumnType column = ColumnType.None;
        private string output = "";

        public override void Reload(API api, ref double maxValue)
        {
            base.Reload(api, ref maxValue);

            string parentName = api.ReadString("AccountMeasure", "");
            IntPtr skin = api.GetSkin();
            parent = AccountMeasure.Find(skin, parentName);

            string column = api.ReadString("Column", "");
            switch (column.ToLowerInvariant())
            {
                case "from":
                    this.column = ColumnType.From;
                    break;
                case "subject":
                    this.column = ColumnType.Subject;
                    break;
                case "recieved":
                    this.column = ColumnType.Recieved;
                    break;
                default:
                    this.column = ColumnType.None;
                    API.Log(API.LogType.Error, $"Invalid Column type: {column} for Measure: {Name}.");
                    break;
            }

            if (parent == null)
                API.Log(API.LogType.Error, $"Invalid AccountMeasure: {parentName} for Measure: {Name}.");
        }

        public override double Update()
        {
            output = "";

            if (parent != null && parent.Emails.Length > 0)
            {
                switch (column)
                {
                    case ColumnType.From:
                        output = string.Join("\n", parent.Emails.Select(x => x.From));
                        break;
                    case ColumnType.Subject:
                        output = string.Join("\n", parent.Emails.Select(x => x.Subject));
                        break;
                    case ColumnType.Recieved:
                        output = string.Join("\n", parent.Emails.Select(x => x.Recieved.ToString("dd/MM/yyyy")));
                        break;
                }
            }

            return base.Update();
        }

        public override string ToString()
        {
            return output;
        }
    }
}

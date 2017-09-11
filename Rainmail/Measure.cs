using System;

using Rainmeter;

namespace Rainmail
{
    public class Measure
    {
        public Measure() { }

        public virtual void Reload(API api, ref double maxValue)
        {
            Name = api.GetMeasureName();
            Skin = api.GetSkin();
        }

        public virtual double Update()
        {
            return 0.0;
        }

        public virtual void Finished()
        { }

        public virtual string GetString()
        {
            return "";
        }

        // ----- Properties ----- //

        public string Name { private set; get; }
        public IntPtr Skin { private set; get; }
    }
}

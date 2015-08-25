using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Corsair_Effects_Engine
{
    public interface ILight
    {
        Color LightColor { get; set; }
    }

    class LightSolid : ILight
    {
        public Color LightColor {
            get 
            {
                TimeSpan Difference = DateTime.Now - StartTime;
                if ((Difference.Seconds * 1000 + Difference.Milliseconds) < Duration) 
                { return StartColor; }
                else 
                { return Color.FromRgb(0, 255, 0); };
            }
            set { }
        }

        private DateTime StartTime;
        private double Duration;
        private Color StartColor;

        public LightSolid(Color startColor, double duration)
        {
            StartTime = DateTime.Now;
            Duration = duration;
            StartColor = startColor;
        }
    }

    class LightFade
    {
        public LightFade(Color startColor, Color endColor, double timeToFade, double duration)
        {

        }
    }
}

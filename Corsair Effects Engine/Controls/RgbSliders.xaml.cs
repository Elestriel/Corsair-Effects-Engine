using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Corsair_Effects_Engine.Controls
{
    /// <summary>
    /// Interaction logic for RgbSliders.xaml
    /// </summary>
    public  partial class RgbSliders : UserControl
    {
        public RgbSliders()
        {
            InitializeComponent();
            (this.Content as FrameworkElement).DataContext = this;

            // Make the sliders and thumbs update even when 0
            RangeSliderR_LowerValueChanged(null, null);
            RangeSliderG_LowerValueChanged(null, null);
            RangeSliderB_LowerValueChanged(null, null);
            RangeSliderR_HigherValueChanged(null, null);
            RangeSliderG_HigherValueChanged(null, null);
            RangeSliderB_HigherValueChanged(null, null);
        }

        public Color UpperColor
        {
            get { return Color.FromRgb((byte)this.RangeSliderR.HigherValue, (byte)this.RangeSliderG.HigherValue, (byte)this.RangeSliderB.HigherValue); }
            set
            { 
                this.RangeSliderR.HigherValue = value.R;
                this.RangeSliderG.HigherValue = value.G;
                this.RangeSliderB.HigherValue = value.B;
            }
        }

        public Color LowerColor
        {
            get { return Color.FromRgb((byte)this.RangeSliderR.LowerValue, (byte)this.RangeSliderG.LowerValue, (byte)this.RangeSliderB.LowerValue); }
            set
            {
                this.RangeSliderR.LowerValue = value.R;
                this.RangeSliderG.LowerValue = value.G;
                this.RangeSliderB.LowerValue = value.B;
            }
        }

        public static readonly DependencyProperty UpperProperty = DependencyProperty.Register("UpperColor", typeof(Color), typeof(RgbSliders), null);
        public static readonly DependencyProperty LowerProperty = DependencyProperty.Register("LowerColor", typeof(Color), typeof(RgbSliders), null);

        public static event PropertyChangedEventHandler PropertyChanged;
        void SetValueDp(DependencyProperty property, object value, [System.Runtime.CompilerServices.CallerMemberName] string p = null)
        {
            SetValue(property, value);
            if (PropertyChanged != null)
            { PropertyChanged(this, new PropertyChangedEventArgs(p)); };
        }

        private void RangeSliderR_LowerValueChanged(object sender, RoutedEventArgs e)
        {
            // Colour the slider and thumbs
            RangeSliderR.LowerThumbBackground = new SolidColorBrush(Color.FromRgb((byte)RangeSliderR.LowerValue, 0, 0));
            RangeSliderR.HigherThumbBackground = new SolidColorBrush(Color.FromRgb((byte)RangeSliderR.HigherValue, 0, 0));
            RangeSliderR.RangeBackground = new LinearGradientBrush(Color.FromRgb((byte)RangeSliderR.LowerValue, 0, 0), Color.FromRgb((byte)RangeSliderR.HigherValue, 0, 0), new Point(0,0), new Point(1,1));
            RangeSliderR.LowerValue = (int)RangeSliderR.LowerValue;
            SetValueDp(LowerProperty, this.LowerColor);
        }

        private void RangeSliderR_HigherValueChanged(object sender, RoutedEventArgs e)
        {
            // Colour the slider and thumbs
            RangeSliderR.LowerThumbBackground = new SolidColorBrush(Color.FromRgb((byte)RangeSliderR.LowerValue, 0, 0));
            RangeSliderR.HigherThumbBackground = new SolidColorBrush(Color.FromRgb((byte)RangeSliderR.HigherValue, 0, 0));
            RangeSliderR.RangeBackground = new LinearGradientBrush(Color.FromRgb((byte)RangeSliderR.LowerValue, 0, 0), Color.FromRgb((byte)RangeSliderR.HigherValue, 0, 0), new Point(0, 0), new Point(1, 1));
            RangeSliderR.HigherValue = (int)RangeSliderR.HigherValue;
            SetValueDp(UpperProperty, this.UpperColor);
        }

        private void RangeSliderG_LowerValueChanged(object sender, RoutedEventArgs e)
        {
            // Colour the slider and thumbs
            RangeSliderG.LowerThumbBackground = new SolidColorBrush(Color.FromRgb(0, (byte)RangeSliderG.LowerValue, 0));
            RangeSliderG.HigherThumbBackground = new SolidColorBrush(Color.FromRgb(0, (byte)RangeSliderG.HigherValue, 0));
            RangeSliderG.RangeBackground = new LinearGradientBrush(Color.FromRgb(0, (byte)RangeSliderG.LowerValue, 0), Color.FromRgb(0, (byte)RangeSliderG.HigherValue, 0), new Point(0, 0), new Point(1, 1));
            RangeSliderG.LowerValue = (int)RangeSliderG.LowerValue;
            SetValueDp(LowerProperty, this.LowerColor);
        }

        private void RangeSliderG_HigherValueChanged(object sender, RoutedEventArgs e)
        {
            // Colour the slider and thumbs
            RangeSliderG.LowerThumbBackground = new SolidColorBrush(Color.FromRgb(0, (byte)RangeSliderG.LowerValue, 0));
            RangeSliderG.HigherThumbBackground = new SolidColorBrush(Color.FromRgb(0, (byte)RangeSliderG.HigherValue, 0));
            RangeSliderG.RangeBackground = new LinearGradientBrush(Color.FromRgb(0, (byte)RangeSliderG.LowerValue, 0), Color.FromRgb(0, (byte)RangeSliderG.HigherValue, 0), new Point(0, 0), new Point(1, 1));
            RangeSliderG.HigherValue = (int)RangeSliderG.HigherValue;
            SetValueDp(UpperProperty, this.UpperColor);
        }

        private void RangeSliderB_LowerValueChanged(object sender, RoutedEventArgs e)
        {
            // Colour the slider and thumbs
            RangeSliderB.LowerThumbBackground = new SolidColorBrush(Color.FromRgb(0, 0, (byte)RangeSliderB.LowerValue));
            RangeSliderB.HigherThumbBackground = new SolidColorBrush(Color.FromRgb(0, 0, (byte)RangeSliderB.HigherValue));
            RangeSliderB.RangeBackground = new LinearGradientBrush(Color.FromRgb(0, 0, (byte)RangeSliderB.LowerValue), Color.FromRgb(0, 0, (byte)RangeSliderB.HigherValue), new Point(0, 0), new Point(1, 1));
            RangeSliderB.LowerValue = (int)RangeSliderB.LowerValue;
            SetValueDp(LowerProperty, this.LowerColor);
        }

        private void RangeSliderB_HigherValueChanged(object sender, RoutedEventArgs e)
        {
            // Colour the slider and thumbs
            RangeSliderB.LowerThumbBackground = new SolidColorBrush(Color.FromRgb(0, 0, (byte)RangeSliderB.LowerValue));
            RangeSliderB.HigherThumbBackground = new SolidColorBrush(Color.FromRgb(0, 0, (byte)RangeSliderB.HigherValue));
            RangeSliderB.RangeBackground = new LinearGradientBrush(Color.FromRgb(0, 0, (byte)RangeSliderB.LowerValue), Color.FromRgb(0, 0, (byte)RangeSliderB.HigherValue), new Point(0, 0), new Point(1, 1));
            RangeSliderB.HigherValue = (int)RangeSliderB.HigherValue;
            SetValueDp(UpperProperty, this.UpperColor);
        }
    }
}

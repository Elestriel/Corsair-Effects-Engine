using System;
using System.Collections.Generic;
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
    /// Interaction logic for ColorRangeCollection.xaml
    /// </summary>
    public partial class ColorRangeCollection : UserControl
    {
        public ColorRangeCollection()
        {
            InitializeComponent();
        }

        private void RangeSliderR_LowerValueChanged(object sender, RoutedEventArgs e)
        {
            RangeSliderR.LowerThumbBackground = new SolidColorBrush(Color.FromRgb((byte)RangeSliderR.LowerValue, 0, 0));
            RangeSliderR.HigherThumbBackground = new SolidColorBrush(Color.FromRgb((byte)RangeSliderR.HigherValue, 0, 0));
            RangeSliderR.RangeBackground = new LinearGradientBrush(Color.FromRgb((byte)RangeSliderR.LowerValue, 0, 0), Color.FromRgb((byte)RangeSliderR.HigherValue, 0, 0), new Point(0,0), new Point(1,1));
        }

        private void RangeSliderR_HigherValueChanged(object sender, RoutedEventArgs e)
        {
            RangeSliderR.LowerThumbBackground = new SolidColorBrush(Color.FromRgb((byte)RangeSliderR.LowerValue, 0, 0));
            RangeSliderR.HigherThumbBackground = new SolidColorBrush(Color.FromRgb((byte)RangeSliderR.HigherValue, 0, 0));
            RangeSliderR.RangeBackground = new LinearGradientBrush(Color.FromRgb((byte)RangeSliderR.LowerValue, 0, 0), Color.FromRgb((byte)RangeSliderR.HigherValue, 0, 0), new Point(0, 0), new Point(1, 1));
        }

        private void RangeSliderG_LowerValueChanged(object sender, RoutedEventArgs e)
        {
            RangeSliderG.LowerThumbBackground = new SolidColorBrush(Color.FromRgb(0, (byte)RangeSliderG.LowerValue, 0));
            RangeSliderG.HigherThumbBackground = new SolidColorBrush(Color.FromRgb(0, (byte)RangeSliderG.HigherValue, 0));
            RangeSliderG.RangeBackground = new LinearGradientBrush(Color.FromRgb(0, (byte)RangeSliderG.LowerValue, 0), Color.FromRgb(0, (byte)RangeSliderG.HigherValue, 0), new Point(0, 0), new Point(1, 1));
        }

        private void RangeSliderG_HigherValueChanged(object sender, RoutedEventArgs e)
        {
            RangeSliderG.LowerThumbBackground = new SolidColorBrush(Color.FromRgb(0, (byte)RangeSliderG.LowerValue, 0));
            RangeSliderG.HigherThumbBackground = new SolidColorBrush(Color.FromRgb(0, (byte)RangeSliderG.HigherValue, 0));
            RangeSliderG.RangeBackground = new LinearGradientBrush(Color.FromRgb(0, (byte)RangeSliderG.LowerValue, 0), Color.FromRgb(0, (byte)RangeSliderG.HigherValue, 0), new Point(0, 0), new Point(1, 1));
        }

        private void RangeSliderB_LowerValueChanged(object sender, RoutedEventArgs e)
        {
            RangeSliderB.LowerThumbBackground = new SolidColorBrush(Color.FromRgb(0, 0, (byte)RangeSliderB.LowerValue));
            RangeSliderB.HigherThumbBackground = new SolidColorBrush(Color.FromRgb(0, 0, (byte)RangeSliderB.HigherValue));
            RangeSliderB.RangeBackground = new LinearGradientBrush(Color.FromRgb(0, 0, (byte)RangeSliderB.LowerValue), Color.FromRgb(0, 0, (byte)RangeSliderB.HigherValue), new Point(0, 0), new Point(1, 1));
        }

        private void RangeSliderB_HigherValueChanged(object sender, RoutedEventArgs e)
        {
            RangeSliderB.LowerThumbBackground = new SolidColorBrush(Color.FromRgb(0, 0, (byte)RangeSliderB.LowerValue));
            RangeSliderB.HigherThumbBackground = new SolidColorBrush(Color.FromRgb(0, 0, (byte)RangeSliderB.HigherValue));
            RangeSliderB.RangeBackground = new LinearGradientBrush(Color.FromRgb(0, 0, (byte)RangeSliderB.LowerValue), Color.FromRgb(0, 0, (byte)RangeSliderB.HigherValue), new Point(0, 0), new Point(1, 1));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio;
using NAudio.CoreAudioApi;

namespace Corsair_Effects_Engine
{
    public class CeeDataContext
    {
        // Audio Devices
        public ObservableCollection<MMDevice> AudioOutputDeviceList { get; set; }
        public ObservableCollection<MMDevice> AudioInputDeviceList { get; set; }
                
        public CeeDataContext()
        {
            // Audio Devices
            MMDeviceEnumerator deviceEnum = new MMDeviceEnumerator();
            AudioOutputDeviceList = new ObservableCollection<MMDevice>(deviceEnum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToArray());
            AudioInputDeviceList = new ObservableCollection<MMDevice>(deviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToArray());
        }
    }
}

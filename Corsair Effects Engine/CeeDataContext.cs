using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CSCore;
using CSCore.CoreAudioAPI;

namespace Corsair_Effects_Engine
{
    public class CeeDataContext
    {
        // Audio Devices
        public ObservableCollection<MMDevice> AudioOutputDeviceList { get; set; }
        public ObservableCollection<MMDevice> AudioInputDeviceList { get; set; }

        // Key Colour Collection
        
        public CeeDataContext()
        {
            // Audio Devices
            MMDeviceEnumerator deviceEnum = new MMDeviceEnumerator();
            AudioOutputDeviceList = new ObservableCollection<MMDevice>(deviceEnum.EnumAudioEndpoints(DataFlow.Render, DeviceState.Active).ToArray());
            AudioInputDeviceList = new ObservableCollection<MMDevice>(deviceEnum.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active).ToArray());
        }
    }
}

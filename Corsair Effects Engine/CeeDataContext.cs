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
        public CeeDataContext()
        {
            MMDeviceEnumerator deviceEnum = new MMDeviceEnumerator();
            AudioOutputDeviceList = new List<MMDevice>(deviceEnum.EnumAudioEndpoints(DataFlow.Render, DeviceState.Active).ToList());
            //AudioInputDeviceList = new List<MMDevice>(deviceEnum.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active).ToList());
            AudioInputDeviceList = new ObservableCollection<MMDevice>(deviceEnum.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active).ToArray());
        }
        public List<MMDevice> AudioOutputDeviceList { get; set; }
        //public List<MMDevice> AudioInputDeviceList { get; set; }
        public ObservableCollection<MMDevice> AudioInputDeviceList { get; set; }
    }
}

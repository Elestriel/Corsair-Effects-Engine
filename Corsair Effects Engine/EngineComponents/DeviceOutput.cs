using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Corsair_Effects_Engine.EngineComponents
{
    public class DeviceOutput
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static public extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, ref uint lpNumberOfBytesWritten, IntPtr ipOverlapped);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_SetFeature(IntPtr HidDeviceObject, ref Byte lpReportBuffer, int ReportBufferLength);

        private byte[][] keyboardPacket = new byte[5][];
        private byte[] mousePacket = new byte[64];

        public DeviceOutput()
        {
            for (int i = 0; i < keyboardPacket.Length; i++)
            {
                this.keyboardPacket[i] = new byte[64];
            }
        }

        public void UpdateKeyboard(IntPtr outputDevice, KeyData[] Keys)
        {
            byte[] redValues = new byte[144];
            byte[] greenValues = new byte[144];
            byte[] blueValues = new byte[144];

            for (int i = 0; i < 144; i++)
            {
                redValues[i] = (byte)(7 - (Keys[i].KeyColor.LightColor.R / 32));
                greenValues[i] = (byte)(7 - (Keys[i].KeyColor.LightColor.G / 32));
                blueValues[i] = (byte)(7 - (Keys[i].KeyColor.LightColor.B / 32));
            }

            // Perform USB control message to keyboard
            //
            // Request Type:  0x21
            // Request:       0x09
            // Value          0x0300
            // Index:         0x03
            // Size:          64

            keyboardPacket[0][0] = 0x7F;
            keyboardPacket[0][1] = 0x01;
            keyboardPacket[0][2] = 0x3C;

            keyboardPacket[1][0] = 0x7F;
            keyboardPacket[1][1] = 0x02;
            keyboardPacket[1][2] = 0x3C;

            keyboardPacket[2][0] = 0x7F;
            keyboardPacket[2][1] = 0x03;
            keyboardPacket[2][2] = 0x3C;

            keyboardPacket[3][0] = 0x7F;
            keyboardPacket[3][1] = 0x04;
            keyboardPacket[3][2] = 0x24;

            keyboardPacket[4][0] = 0x07;
            keyboardPacket[4][1] = 0x27;
            keyboardPacket[4][4] = 0xD8;

            for (int i = 0; i < 60; i++)
            {
                keyboardPacket[0][i + 4] = (byte)(redValues[i * 2 + 1] << 4 | redValues[i * 2]);
            }

            for (int i = 0; i < 12; i++)
            {
                keyboardPacket[1][i + 4] = (byte)(redValues[i * 2 + 121] << 4 | redValues[i * 2 + 120]);
            }

            for (int i = 0; i < 48; i++)
            {
                keyboardPacket[1][i + 16] = (byte)(greenValues[i * 2 + 1] << 4 | greenValues[i * 2]);
            }

            for (int i = 0; i < 24; i++)
            {
                keyboardPacket[2][i + 4] = (byte)(greenValues[i * 2 + 97] << 4 | greenValues[i * 2 + 96]);
            }

            for (int i = 0; i < 36; i++)
            {
                keyboardPacket[2][i + 28] = (byte)(blueValues[i * 2 + 1] << 4 | blueValues[i * 2]);
            }

            for (int i = 0; i < 36; i++)
            {
                keyboardPacket[3][i + 4] = (byte)(blueValues[i * 2 + 73] << 4 | blueValues[i * 2 + 72]);
            }
            for (int i = 36; i < 60; i++)
            {
                this.keyboardPacket[3][i + 4] = (byte)0;
            }
            for (int i = 0; i < 60; i++)
            {
                this.keyboardPacket[4][i + 4] = (byte)0;
            }

            for (int p = 0; p < 5; p++)
            {
                this.SendUsbMessage(keyboardPacket[p], outputDevice, "Keyboard");
            }
        }

        public void UpdateMouse(IntPtr outputDevice, KeyData[] Keys)
        {
            byte[] redValues = new byte[5];
            byte[] greenValues = new byte[5];
            byte[] blueValues = new byte[5];

            for (int i = 0; i < 5; i++)
            {
                redValues[i] = (byte)Keys[i + 143].KeyColor.LightColor.R;
                greenValues[i] = (byte)Keys[i + 143].KeyColor.LightColor.G;
                blueValues[i] = (byte)Keys[i + 143].KeyColor.LightColor.B;
            }

            // Perform USB control message to keyboard
            //
            // Request Type:  0x21
            // Request:       0x09
            // Value          0x0300
            // Index:         0x03
            // Size:          64
            this.mousePacket[0] = 0x07;
            this.mousePacket[1] = 0x22;
            if (Properties.Settings.Default.MouseModel != "Scimitar") { this.mousePacket[2] = 0x04; }
            else { this.mousePacket[2] = 0x05; };
            this.mousePacket[3] = 0x01;
            
                // Light 1
                this.mousePacket[4] = 0x01;
            this.mousePacket[5] = redValues[0];
            this.mousePacket[6] = greenValues[0];
            this.mousePacket[7] = blueValues[0];

            // Light 2
            this.mousePacket[8] = 0x02;
            this.mousePacket[9] = redValues[1];
            this.mousePacket[10] = greenValues[1];
            this.mousePacket[11] = blueValues[1];

            // Light 3
            this.mousePacket[12] = 0x03;
            this.mousePacket[13] = redValues[2];
            this.mousePacket[14] = greenValues[2];
            this.mousePacket[15] = blueValues[2];

            // Light 4
            this.mousePacket[16] = 0x04;
            this.mousePacket[17] = redValues[3];
            this.mousePacket[18] = greenValues[3];
            this.mousePacket[19] = blueValues[3];

            if (Properties.Settings.Default.MouseModel == "Scimitar")
            {
                // Light 5
                this.mousePacket[20] = 0x05;
                this.mousePacket[21] = redValues[4];
                this.mousePacket[22] = greenValues[4];
                this.mousePacket[23] = blueValues[4];
            }
            else
            {
                this.mousePacket[20] = 0x0;
                this.mousePacket[21] = 0x0;
                this.mousePacket[22] = 0x0;
                this.mousePacket[23] = 0x0;
            }

            this.SendUsbMessage(mousePacket, outputDevice, "Mouse");
        }

        public void RestoreKeyboard(IntPtr outputDevice)
        {
            byte[] RestoreKeyboardPacket1 = { 0x07, 0x05, 0x02, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                              0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                              0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                              0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
            byte[] RestoreKeyboardPacket2 = { 0x07, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                              0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                              0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                              0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
            SendUsbMessage(RestoreKeyboardPacket1, outputDevice, "Keyboard");
            SendUsbMessage(RestoreKeyboardPacket2, outputDevice, "Keyboard");
        }

        public void RestoreMouse(IntPtr outputDevice)
        {
            byte[] RestoreMousePacket = { 0x07, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                          0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                          0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                          0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
            SendUsbMessage(RestoreMousePacket, outputDevice, "Mouse");
        }

        private bool SendUsbMessage(byte[] data_pkt, IntPtr outputDevice, string deviceType)
        {
            byte[] usb_pkt = new byte[65];
            for (int i = 1; i < 65; i++)
            {
                usb_pkt[i] = data_pkt[i - 1];
            }

            if (deviceType == "Keyboard")
            {
                uint written = 0;
                return WriteFile(outputDevice, usb_pkt, 65, ref written, IntPtr.Zero);
            }
            else if (deviceType == "Mouse")
            {
                return HidD_SetFeature(outputDevice, ref usb_pkt[0], 65);
            }
            else return false;
        }
        
    }
}

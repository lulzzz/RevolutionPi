﻿using System.Diagnostics;
using System.Text;
using IctBaden.RevolutionPi.Model;
// ReSharper disable UnusedMember.Global

namespace IctBaden.RevolutionPi
{
    /// <summary>
    /// Interface to piControl driver process.
    /// </summary>
    public class PiControl
    {
        /// <summary>
        /// Linux device name full path
        /// </summary>
        public string PiControlDeviceName = "/dev/piControl0";

        private int _piControlHandle = -1;

        /// <summary>
        /// Opens the driver connection.
        /// </summary>
        /// <returns>True if connection successfully opened</returns>
        public bool Open()
        {
            if (!IsOpen)
            {
                _piControlHandle = Interop.open(PiControlDeviceName, Interop.O_RDWR);
            }
            return IsOpen;
        }

        /// <summary>
        /// True if connection to the device driver established
        /// </summary>
        public bool IsOpen => _piControlHandle >= 0;

        /// <summary>
        /// Closes the driver connection.
        /// </summary>
        public void Close()
        {
            if (!IsOpen) return;

            Interop.close(_piControlHandle);
            _piControlHandle = -1;
        }

        /// <summary>
        /// Resets the piControl driver process.
        /// </summary>
        /// <returns>True if reset is successful</returns>
        public bool Reset()
        {
            if (!Open()) return false;

            return Interop.ioctl_void(_piControlHandle, Interop.KB_RESET) >= 0;
        }

        /// <summary>
        /// Read data from the process image.
        /// </summary>
        /// <param name="offset">Position to read from</param>
        /// <param name="length">Byte count to read</param>
        /// <returns>Data read or null in case of failure</returns>
        public byte[] Read(int offset, int length)
        {
            if (!IsOpen) return null;

            if (Interop.lseek(_piControlHandle, offset, Interop.SEEK_SET) < 0)
            {
                return null;
            }

            var data = new byte[length];
            var bytesRead = Interop.read(_piControlHandle, data, length);
            return bytesRead != length ? null : data;
        }

        /// <summary>
        /// Write data to the process image.
        /// </summary>
        /// <param name="offset">Position to write to</param>
        /// <param name="data">Data to be written</param>
        /// <returns>Bytes written</returns>
        public int Write(int offset, byte[] data)
        {
            if (!IsOpen) return 0;

            if (Interop.lseek(_piControlHandle, offset, Interop.SEEK_SET) < 0)
            {
                return 0;
            }

            var bytesWritten = Interop.write(_piControlHandle, data, data.Length);
            return bytesWritten;
        }

        /// <summary>
        /// Get the value of one bit in the process image.
        /// </summary>
        /// <param name="address">Address of the byte in the process image</param>
        /// <param name="bit">bit position (0-7)</param>
        /// <returns>Bit value</returns>
        public bool GetBitValue(ushort address, byte bit)
        {
            var bitValue = new SpiValue
            {
                Address = address,
                Bit = bit
            };

            if (!IsOpen) return false;

            if (Interop.ioctl_value(_piControlHandle, Interop.KB_GET_VALUE, bitValue) < 0)
            {
                Trace.TraceError("PiControl.SetBitValue: Failed to read bit value.");
                return false;
            }

            return bitValue.Value != 0;
        }

        /// <summary>
        /// Set the value of one bit in the process image.
        /// </summary>
        /// <param name="address">Address of the byte in the process image</param>
        /// <param name="bit">bit position (0-7)</param>
        /// <param name="value"></param>
        public void SetBitValue(ushort address, byte bit, bool value)
        {
            var bitValue = new SpiValue
            {
                Address = address,
                Bit = bit,
                Value = (byte)(value ? 1 : 0)
            };

            if (!IsOpen) return;

            if (Interop.ioctl_value(_piControlHandle, Interop.KB_SET_VALUE, bitValue) < 0)
            {
                Trace.TraceError("PiControl.SetBitValue: Failed to write bit value.");
            }
        }

        /// <summary>
        /// Converts given data to value
        /// </summary>
        /// <param name="data">Source data</param>
        /// <returns>Value of data</returns>
        public object ConvertDataToValue(byte[] data)
        {
            switch (data.Length)
            {
                case 1:
                    return data[0];
                case 2:
                    return (ushort)(data[0] + (data[1] * 0x100));
                case 3:
                    return data[0] +
                           (ulong)(data[1] * 0x100) +
                           (ulong)(data[2] * 0x10000) +
                           (ulong)(data[3] * 0x1000000);
                default:
                    return Encoding.ASCII.GetString(data);
            }
        }

        public VarData ReadVariable(VariableInfo varInfo)
        {
            var deviceOffset = varInfo.Device.Offset;
            int byteLen;

            switch (varInfo.Length)
            {
                case 1: byteLen = 0; break;        // Bit
                case 8: byteLen = 1; break;
                case 16: byteLen = 2; break;
                case 32: byteLen = 4; break;
                default:                            // strings, z.B. IP-Adresse
                    byteLen = -varInfo.Length / 8;
                    break;
            }

            var varData = new VarData();

            if (byteLen > 0)
            {
                varData.Raw = Read(deviceOffset + varInfo.Address, byteLen);
            }
            else if (byteLen == 0)
            {
                var address = (ushort)(deviceOffset + varInfo.Address);
                varData.Raw = new[]
                {
                    (byte) (GetBitValue(address, varInfo.BitOffset) ? 1 : 0)
                };
            }
            else  // iByteLen < 0
            {
                varData.Raw = Read(deviceOffset + varInfo.Address, -byteLen);
            }

            if (varData.Raw == null) return null;

            varData.Value = ConvertDataToValue(varData.Raw);
            return varData;
        }
    }
}

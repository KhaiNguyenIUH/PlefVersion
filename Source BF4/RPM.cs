using SharpDX;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PlefVersion
{
    class RPM
    {
        private static IntPtr pHandle = IntPtr.Zero;

        public static IntPtr OpenProcess(int pId)
        {
            pHandle = Manager.OpenProcess(Manager.PROCESS_VM_READ | Manager.PROCESS_VM_WRITE | Manager.PROCESS_VM_OPERATION, false, pId);
            return pHandle;
        }

        public static IntPtr GetHandle()
        {
            return pHandle;
        }

        public static void CloseProcess()
        {
            Manager.CloseHandle(pHandle);
        }

        public static T Read<T>(Int64 address)
        {
            byte[] Buffer = new byte[Marshal.SizeOf(typeof(T))];
            IntPtr ByteRead;
            Manager.ReadProcessMemory(pHandle, address, Buffer, (uint)Buffer.Length, out ByteRead);

            // Get Struct from Buffer
            GCHandle handle = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
            T stuff = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            // Return
            return stuff;
        }

        public static void Read<T>(Int64 address, out T value)
        {
            value = Read<T>(address);
        }

        public static bool Write<T>(Int64 address, T t)
        {
            Byte[] Buffer = new Byte[Marshal.SizeOf(typeof(T))];
            GCHandle handle = GCHandle.Alloc(t, GCHandleType.Pinned);
            Marshal.Copy(handle.AddrOfPinnedObject(), Buffer, 0, Buffer.Length);
            handle.Free();

            uint oldProtect;
            Manager.VirtualProtectEx(pHandle, (IntPtr)address, (uint)Buffer.Length, Manager.PAGE_READWRITE, out oldProtect);
            IntPtr ptrBytesWritten;
            return Manager.WriteProcessMemory(pHandle, address, Buffer, (uint)Buffer.Length, out ptrBytesWritten);
        }

        public static void WriteAngle(float _Yaw, float _Pitch)
        {
            Int64 pBase = Read<Int64>(Offsets.OFFSET_ANGLES);
            Int64 m_authorativeAiming = Read<Int64>(pBase + Offsets.MKO_ClientSoldierWeapon.m_authorativeAiming);
            Int64 m_fpsAimer = Read<Int64>(m_authorativeAiming + Offsets.MKO_ClientSoldierAimingSimulation.m_fpsAimer);
            
            Write<float>(m_fpsAimer + Offsets.MKO_AimAssist.m_yaw, _Yaw);
            Write<float>(m_fpsAimer + Offsets.MKO_AimAssist.m_pitch, _Pitch);
        }

        public static void ReadAngle(out System.Drawing.PointF point)
        {
            float Yaw = 0.0f;
            float Pitch = 0.0f;
            Int64 pViewAngles = RPM.Read<Int64>(Offsets.OFFSET_ANGLES);
            if (RPM.IsValid(pViewAngles))
            {
                Int64 pAuthorativeAiming = RPM.Read<Int64>(pViewAngles + Offsets.MKO_ClientSoldierWeapon.m_authorativeAiming); //(pViewAngles + 0x4988);
                if (RPM.IsValid(pAuthorativeAiming))
                {
                    Int64 pFpsAimer = RPM.Read<Int64>(pAuthorativeAiming + Offsets.MKO_ClientSoldierAimingSimulation.m_fpsAimer); //(pAuthorativeAiming + 0x0010);
                    if (RPM.IsValid(pFpsAimer))
                    {
                        Yaw = RPM.Read<float>(pFpsAimer + Offsets.MKO_AimAssist.m_yaw);
                        Pitch = RPM.Read<float>(pFpsAimer + Offsets.MKO_AimAssist.m_pitch);
                    }
                }
            }
            point = new System.Drawing.PointF(Yaw, Pitch);
        }

        public static System.Drawing.PointF ReadAngle()
        {
            float Yaw = 0.0f;
            float Pitch = 0.0f;
            Int64 pViewAngles = RPM.Read<Int64>(Offsets.OFFSET_ANGLES);
            if (RPM.IsValid(pViewAngles))
            {
                Int64 pAuthorativeAiming = RPM.Read<Int64>(pViewAngles + Offsets.MKO_ClientSoldierWeapon.m_authorativeAiming); //(pViewAngles + 0x4988);
                if (RPM.IsValid(pAuthorativeAiming))
                {
                    Int64 pFpsAimer = RPM.Read<Int64>(pAuthorativeAiming + Offsets.MKO_ClientSoldierAimingSimulation.m_fpsAimer); //(pAuthorativeAiming + 0x0010);
                    if (RPM.IsValid(pFpsAimer))
                    {
                        Yaw = RPM.Read<float>(pFpsAimer + Offsets.MKO_AimAssist.m_yaw);
                        Pitch = RPM.Read<float>(pFpsAimer + Offsets.MKO_AimAssist.m_pitch);
                    }
                }
            }

            return new System.Drawing.PointF(Yaw, Pitch);
        }

        public static string ReadName(Int64 address, UInt64 _Size)
        {
            byte[] buffer = new byte[_Size];
            IntPtr BytesRead;

            Manager.ReadProcessMemory(pHandle, address, buffer, _Size, out BytesRead);

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == 0)
                {
                    byte[] _buffer = new byte[i];
                    Buffer.BlockCopy(buffer, 0, _buffer, 0, i);
                    return Encoding.ASCII.GetString(_buffer);
                }
            }
            return Encoding.ASCII.GetString(buffer);
        }

        public static string ReadString(Int64 address, UInt64 _Size)
        {
            byte[] buffer = new byte[_Size];
            IntPtr BytesRead;

            Manager.ReadProcessMemory(pHandle, address, buffer, _Size, out BytesRead);
            return Encoding.ASCII.GetString(buffer);
        }

        public static bool IsValid(Int64 Address)
        {
            return (Address >= 0x10000 && Address < 0x000F000000000000);
        } 
    }
}

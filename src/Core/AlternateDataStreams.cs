using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace NuGet
{
    public static class AlternateDataStreams
    {
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        private const uint GENERIC_ALL = 0x10000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint OPEN_EXISTING = 3;
        private const uint OPEN_ALWAYS = 4;
        public static SafeFileHandle GetHandle(string path, string name)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Invalid path", "path");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Invalid name", "name");
            string streamPath = path + ":" + name;
            SafeFileHandle handle = CreateFile(streamPath,
            GENERIC_ALL, FILE_SHARE_READ, IntPtr.Zero, OPEN_ALWAYS,
            FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);
            if (handle.IsInvalid)
                throw new Win32Exception(Marshal.GetLastWin32Error(),string.Format("Can't get handle for file {0} - alternate data stream {1}",path, name ));
            //    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            return handle;
        }

        public static bool DeleteAlternateStream(string path, string name)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Invalid path", "path");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Invalid name", "name");
            string streamPath = path + ":" + name;
            return DeleteFile(streamPath);
        }
        public static bool AlternateStreamExist(string path, string name)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Invalid path", "path");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Invalid name", "name");
            string streamPath = path + ":" + name;

            SafeFileHandle handle = CreateFile(streamPath,
                GENERIC_ALL, FILE_SHARE_READ, IntPtr.Zero, OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);
            handle.Close();
            if (handle.IsInvalid) return false;
            return true;

        }
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess,
        uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool DeleteFile(string lpFileName);
    }
}

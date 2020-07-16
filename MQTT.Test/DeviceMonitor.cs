using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text;

namespace MQTT.Test
{
    public class DeviceMonitor
    {
        static readonly PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        static readonly PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");
        static readonly PerformanceCounter uptime = new PerformanceCounter("System", "System Up Time");


        public static bool GetInternetAvilable()
        {
            bool networkUp = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
            return networkUp;
        }

        public static TimeSpan GetSystemUpTime()
        {
            uptime.NextValue();
            TimeSpan ts = TimeSpan.FromSeconds(uptime.NextValue());
            return ts;
        }

        public static string GetPhysicalMemory()
        {
            string str = null;
            ManagementObjectSearcher objCS = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            foreach (ManagementObject objMgmt in objCS.Get())
            {
                str = objMgmt["totalphysicalmemory"].ToString();
            }
            return str;
        }

        public static string getCurrentCpuUsage()
        {
            return cpuCounter.NextValue() + "%";
        }

        public static string getAvailableRAM()
        {
            return ramCounter.NextValue() + "MB";
        }

        public static IEnumerable<HardDiskInfo> GetAllHardDiskInfo()
        {
            List<HardDiskInfo> list = new List<HardDiskInfo>();
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                if (d.IsReady)
                {
                    list.Add(new HardDiskInfo { Name = d.Name, FreeSpace = GetDriveData(d.AvailableFreeSpace), TotalSpace = GetDriveData(d.TotalSize) });
                }
            }
            return list;
        }

        private static string GetDriveData(long data)//将磁盘大小的单位由byte转化为G
        {
            return (data / Convert.ToDouble(1024) / Convert.ToDouble(1024) / Convert.ToDouble(1024)).ToString("0.00");
        }
    }
    public class HardDiskInfo//自定义类
    {
        public string Name { get; set; }
        public string FreeSpace { get; set; }
        public string TotalSpace { get; set; }
    }
}

using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace pc_system_monitor_app.Services
{
    public class CpuDetails
    {
        public string Name { get; set; } = "—";
        public int Cores { get; set; }
        public int Threads { get; set; }
        public int MaxClockMHz { get; set; }
        public int L2CacheKB { get; set; }
        public int L3CacheKB { get; set; }
    }

    public class RamModuleInfo
    {
        public string Bank { get; set; } = "—";
        public ulong CapacityMB { get; set; }
        public uint SpeedMHz { get; set; }
        public string Manufacturer { get; set; } = "—";
        public string PartNumber { get; set; } = "—";
        public string MemoryType { get; set; } = "—";
    }

    public class GpuDetails
    {
        public string Name { get; set; } = "—";
        public ulong AdapterRamMB { get; set; }
        public string VideoProcessor { get; set; } = "—";
        public string DriverVersion { get; set; } = "—";
        public double? LoadPercent { get; set; }
        public double? TempC { get; set; }
    }

    public class HardwareReader : IDisposable
    {
        private Computer _pc;
        private PerformanceCounter _cpuPerf;
        private PerformanceCounter _availMemPerf;

        public HardwareReader()
        {
            _pc = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true
            };
            _pc.Open();
            try
            {
                _cpuPerf = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _availMemPerf = new PerformanceCounter("Memory", "Available MBytes");
                _ = _cpuPerf.NextValue();
            }
            catch
            {
                _cpuPerf = null;
                _availMemPerf = null;
            }
        }

        public double? GetCpuLoad()
        {
            try
            {
                foreach (var h in _pc.Hardware.Where(h => h.HardwareType == HardwareType.Cpu))
                {
                    h.Update();
                    var s = h.Sensors.FirstOrDefault(x => x.SensorType == SensorType.Load && x.Name.ToLower().Contains("total"));
                    if (s?.Value != null) return Math.Round((double)s.Value, 1);
                }
            }
            catch { }
            try
            {
                if (_cpuPerf != null) return Math.Round(_cpuPerf.NextValue(), 1);
            }
            catch { }
            return null;
        }

        public (string name, List<(string name, double? temp)> perCoreTemps, double? avgTemp) GetCpuTemperatures()
        {
            try
            {
                string cpuName = "CPU";
                List<(string name, double? temp)> temps = new List<(string, double?)>();
                foreach (var h in _pc.Hardware)
                {
                    try
                    {
                        h.Update();
                        string lname = (h.Name ?? "").ToLower();
                        bool likelyCpu = h.HardwareType == HardwareType.Cpu || lname.Contains("cpu") || lname.Contains("processor");
                        if (!likelyCpu) continue;
                        if (!string.IsNullOrEmpty(h.Name)) cpuName = h.Name;
                        foreach (var s in h.Sensors.Where(s => s.SensorType == SensorType.Temperature))
                        {
                            double? v = s.Value == null ? (double?)null : Math.Round((double)s.Value, 1);
                            temps.Add((s.Name ?? "Temp", v));
                        }
                    }
                    catch { }
                }

                if (temps.Count == 0)
                {
                    var patterns = new[] { "core", "package", "cpu", "celsius", "tctl" };
                    foreach (var h in _pc.Hardware)
                    {
                        try
                        {
                            h.Update();
                            foreach (var s in h.Sensors.Where(s => s.SensorType == SensorType.Temperature))
                            {
                                string sname = (s.Name ?? "").ToLower();
                                if (patterns.Any(p => sname.Contains(p)))
                                {
                                    double? v = s.Value == null ? (double?)null : Math.Round((double)s.Value, 1);
                                    temps.Add((s.Name ?? "Temp", v));
                                    if (string.IsNullOrEmpty(cpuName) && !string.IsNullOrEmpty(h.Name)) cpuName = h.Name;
                                }
                            }
                        }
                        catch { }
                    }
                }

                if (temps.Count == 0)
                {
                    double? wmi = GetCpuTempFromWmi();
                    if (wmi.HasValue) temps.Add(("WMI", wmi));
                }

                double sum = 0;
                int cnt = 0;
                foreach (var t in temps)
                {
                    if (t.temp.HasValue) { sum += t.temp.Value; cnt++; }
                }
                double? avg = cnt > 0 ? (double?)Math.Round(sum / cnt, 1) : null;
                return (cpuName, temps, avg);
            }
            catch
            {
                double? wmi = GetCpuTempFromWmi();
                var list = new List<(string, double?)>();
                if (wmi.HasValue) list.Add(("WMI", wmi));
                return ("CPU", list, wmi);
            }
        }

        public CpuDetails GetCpuDetails()
        {
            CpuDetails res = new CpuDetails();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed,L2CacheSize,L3CacheSize FROM Win32_Processor"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        res.Name = (mo["Name"] as string) ?? res.Name;
                        res.Cores = mo["NumberOfCores"] != null ? Convert.ToInt32(mo["NumberOfCores"]) : res.Cores;
                        res.Threads = mo["NumberOfLogicalProcessors"] != null ? Convert.ToInt32(mo["NumberOfLogicalProcessors"]) : res.Threads;
                        res.MaxClockMHz = mo["MaxClockSpeed"] != null ? Convert.ToInt32(mo["MaxClockSpeed"]) : res.MaxClockMHz;
                        res.L2CacheKB = mo["L2CacheSize"] != null ? Convert.ToInt32(mo["L2CacheSize"]) : res.L2CacheKB;
                        res.L3CacheKB = mo["L3CacheSize"] != null ? Convert.ToInt32(mo["L3CacheSize"]) : res.L3CacheKB;
                        break;
                    }
                }
            }
            catch { }
            if (string.IsNullOrEmpty(res.Name) || res.Name == "—")
            {
                foreach (var h in _pc.Hardware.Where(h => h.HardwareType == HardwareType.Cpu))
                {
                    try { h.Update(); res.Name = h.Name ?? res.Name; break; } catch { }
                }
            }
            return res;
        }

        public (double? usedPercent, double totalMB, double freeMB) GetRamInfo()
        {
            try
            {
                var mem = GetMemoryStatus();
                double totalMB = Math.Round(mem.ullTotalPhys / 1024.0 / 1024.0, 0);
                double freeMB = Math.Round(mem.ullAvailPhys / 1024.0 / 1024.0, 0);
                double usedMB = totalMB - freeMB;
                double percent = totalMB > 0 ? Math.Round((usedMB / totalMB) * 100.0, 1) : 0;
                return (percent, totalMB, freeMB);
            }
            catch
            {
                try
                {
                    if (_availMemPerf != null)
                    {
                        var avail = _availMemPerf.NextValue();
                        var total = GetMemoryStatus().ullTotalPhys;
                        double totalMB = Math.Round(total / 1024.0 / 1024.0, 0);
                        double freeMB = Math.Round(avail, 0);
                        double usedMB = totalMB - freeMB;
                        double percent = totalMB > 0 ? Math.Round((usedMB / totalMB) * 100.0, 1) : 0;
                        return (percent, totalMB, freeMB);
                    }
                }
                catch { }
            }
            foreach (var h in _pc.Hardware.Where(h => h.HardwareType == HardwareType.Memory))
            {
                try
                {
                    h.Update();
                    var total = h.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.ToLower().Contains("total"));
                    var used = h.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.ToLower().Contains("used"));
                    if (total?.Value != null && used?.Value != null)
                    {
                        double t = (double)total.Value;
                        double u = (double)used.Value;
                        double percent = t > 0 ? Math.Round((u / t) * 100.0, 1) : 0;
                        return (percent, t, t - u);
                    }
                }
                catch { }
            }
            return (null, 0, 0);
        }

        public List<RamModuleInfo> GetRamModules()
        {
            List<RamModuleInfo> list = new List<RamModuleInfo>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT BankLabel, Capacity, Speed, Manufacturer, PartNumber, MemoryType FROM Win32_PhysicalMemory"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        try
                        {
                            var cap = mo["Capacity"] != null ? (ulong)Convert.ToUInt64(mo["Capacity"]) / 1024UL / 1024UL : 0UL;
                            var speed = mo["Speed"] != null ? Convert.ToUInt32(mo["Speed"]) : 0U;
                            var bank = mo["BankLabel"] as string ?? "—";
                            var man = mo["Manufacturer"] as string ?? "—";
                            var part = mo["PartNumber"] as string ?? "—";
                            var mt = mo["MemoryType"] != null ? MemoryTypeToString(Convert.ToInt32(mo["MemoryType"])) : "—";
                            list.Add(new RamModuleInfo { Bank = bank, CapacityMB = cap, SpeedMHz = speed, Manufacturer = man, PartNumber = part, MemoryType = mt });
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return list;
        }

        private string MemoryTypeToString(int code)
        {
            switch (code)
            {
                case 20: return "DDR";
                case 21: return "DDR2";
                case 24: return "DDR3";
                case 26: return "DDR4";
                default: return $"Тип {code}";
            }
        }

        public GpuDetails GetGpuDetails()
        {
            GpuDetails d = new GpuDetails();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name,AdapterRAM,VideoProcessor,DriverVersion FROM Win32_VideoController"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        try
                        {
                            var name = mo["Name"] as string ?? d.Name;
                            var ram = mo["AdapterRAM"] != null ? Convert.ToUInt64(mo["AdapterRAM"]) / 1024UL / 1024UL : 0UL;
                            var proc = mo["VideoProcessor"] as string ?? d.VideoProcessor;
                            var drv = mo["DriverVersion"] as string ?? d.DriverVersion;
                            d.Name = name;
                            d.AdapterRamMB = ram;
                            d.VideoProcessor = proc;
                            d.DriverVersion = drv;
                            break;
                        }
                        catch { }
                    }
                }
            }
            catch { }

            try
            {
                var gpuKeywords = new[] { "gpu", "graphics", "intel", "iris", "uhd", "hd graphics", "radeon", "nvidia", "vga" };
                foreach (var h in _pc.Hardware)
                {
                    try
                    {
                        h.Update();
                        string lname = (h.Name ?? "").ToLower();
                        bool likelyGpu = h.HardwareType.ToString().ToLower().Contains("gpu") || gpuKeywords.Any(k => lname.Contains(k));
                        if (!likelyGpu) continue;
                        if (string.IsNullOrEmpty(d.Name) || d.Name == "—") d.Name = h.Name ?? d.Name;
                        var load = h.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load);
                        if (load?.Value != null) d.LoadPercent = Math.Round((double)load.Value, 1);
                        var temp = h.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
                        if (temp?.Value != null) d.TempC = Math.Round((double)temp.Value, 1);
                        break;
                    }
                    catch { }
                }

                if (!d.TempC.HasValue)
                {
                    foreach (var h in _pc.Hardware)
                    {
                        try
                        {
                            h.Update();
                            foreach (var s in h.Sensors.Where(s => s.SensorType == SensorType.Temperature))
                            {
                                string sname = (s.Name ?? "").ToLower();
                                if (gpuKeywords.Any(k => sname.Contains(k)))
                                {
                                    if (s.Value != null) { d.TempC = Math.Round((double)s.Value, 1); }
                                    if (string.IsNullOrEmpty(d.Name) || d.Name == "—") d.Name = h.Name ?? d.Name;
                                    break;
                                }
                            }
                            if (d.TempC.HasValue) break;
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return d;
        }

        private double? GetCpuTempFromWmi()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var cur = obj["CurrentTemperature"];
                        if (cur != null)
                        {
                            double tempKelvinTenth = Convert.ToDouble(cur);
                            double c = (tempKelvinTenth / 10.0) - 273.15;
                            return Math.Round(c, 1);
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public List<(string hardware, string sensor, double? value)> GetAllTemperatureSensors()
        {
            var res = new List<(string, string, double?)>();
            try
            {
                foreach (var h in _pc.Hardware)
                {
                    try
                    {
                        h.Update();
                        foreach (var s in h.Sensors.Where(s => s.SensorType == SensorType.Temperature))
                        {
                            res.Add((h.Name ?? "—", s.Name ?? "—", s.Value == null ? (double?)null : Math.Round((double)s.Value, 1)));
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return res;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        private MEMORYSTATUSEX GetMemoryStatus()
        {
            MEMORYSTATUSEX ms = new MEMORYSTATUSEX();
            ms.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (GlobalMemoryStatusEx(ref ms)) return ms;
            throw new Exception("GlobalMemoryStatusEx failed");
        }

        public void Dispose()
        {
            try { _pc.Close(); } catch { }
            try { _cpuPerf?.Dispose(); } catch { }
            try { _availMemPerf?.Dispose(); } catch { }
        }
    }
}
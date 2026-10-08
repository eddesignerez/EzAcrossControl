using System;
using WindowsHost.Input;
using System.Collections.Generic;

namespace WindowsHost.V2.Edge
{
    public class MonitorInfo
    {
        public IntPtr Handle { get; set; }
        public NativeMethods.RECT Bounds { get; set; }
        public NativeMethods.RECT WorkingArea { get; set; }
        public bool IsPrimary { get; set; }
    }

    public class MonitorGeometry
    {
        public virtual IEnumerable<MonitorInfo> GetAllMonitors()
        {
            var monitors = new List<MonitorInfo>();
            NativeMethods.MonitorEnumDelegate callback = (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData) =>
            {
                var mi = new NativeMethods.MONITORINFO();
                if (NativeMethods.GetMonitorInfo(hMonitor, mi))
                {
                    monitors.Add(new MonitorInfo
                    {
                        Handle = hMonitor,
                        Bounds = mi.rcMonitor,
                        WorkingArea = mi.rcWork,
                        IsPrimary = (mi.dwFlags & 1) != 0
                    });
                }
                return true;
            };

            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            return monitors;
        }

        public virtual MonitorInfo GetMonitorFromPoint(int x, int y)
        {
            var pt = new NativeMethods.POINT { x = x, y = y };
            IntPtr hMonitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONULL);
            
            if (hMonitor != IntPtr.Zero)
            {
                var mi = new NativeMethods.MONITORINFO();
                if (NativeMethods.GetMonitorInfo(hMonitor, mi))
                {
                    return new MonitorInfo
                    {
                        Handle = hMonitor,
                        Bounds = mi.rcMonitor,
                        WorkingArea = mi.rcWork,
                        IsPrimary = (mi.dwFlags & 1) != 0
                    };
                }
            }
            return null;
        }

        public virtual bool IsInternalEdge(ScreenEdge edge, MonitorInfo currentMonitor)
        {
            if (currentMonitor == null) return false;

            var allMonitors = GetAllMonitors();
            
            foreach (var monitor in allMonitors)
            {
                if (monitor.Handle == currentMonitor.Handle) continue;

                switch (edge)
                {
                    case ScreenEdge.Left:
                        if (monitor.Bounds.right == currentMonitor.Bounds.left && 
                            RangesOverlap(monitor.Bounds.top, monitor.Bounds.bottom, currentMonitor.Bounds.top, currentMonitor.Bounds.bottom))
                            return true;
                        break;
                    case ScreenEdge.Right:
                        if (monitor.Bounds.left == currentMonitor.Bounds.right && 
                            RangesOverlap(monitor.Bounds.top, monitor.Bounds.bottom, currentMonitor.Bounds.top, currentMonitor.Bounds.bottom))
                            return true;
                        break;
                    case ScreenEdge.Top:
                        if (monitor.Bounds.bottom == currentMonitor.Bounds.top && 
                            RangesOverlap(monitor.Bounds.left, monitor.Bounds.right, currentMonitor.Bounds.left, currentMonitor.Bounds.right))
                            return true;
                        break;
                    case ScreenEdge.Bottom:
                        if (monitor.Bounds.top == currentMonitor.Bounds.bottom && 
                            RangesOverlap(monitor.Bounds.left, monitor.Bounds.right, currentMonitor.Bounds.left, currentMonitor.Bounds.right))
                            return true;
                        break;
                }
            }

            return false;
        }

        private bool RangesOverlap(int start1, int end1, int start2, int end2)
        {
            return Math.Max(start1, start2) < Math.Min(end1, end2);
        }
    }
}

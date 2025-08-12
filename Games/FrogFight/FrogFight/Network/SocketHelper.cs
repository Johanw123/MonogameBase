using System.Runtime.InteropServices;

namespace FrogFight.Network
{
  public static class SocketHelper
  {
    [StructLayout(LayoutKind.Sequential)]
    internal struct WSAData
    {
      public short wVersion;
      public short wHighVersion;

      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
      public string szDescription;

      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
      public string szSystemStatus;

      public short iMaxSockets;
      public short iMaxUdpDg;
      public int lpVendorInfo;
    }

    [DllImport("wsock32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern int WSAStartup(
      [In] short wVersionRequested,
      [Out] out WSAData lpWSAData
    );

    [DllImport("wsock32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern int WSACleanup();

    private const int IP_SUCCESS = 0;
    private const short VERSION = 2;

    public static bool SocketInit()
    {
      //if (Platform.ID != PlatformID.Windows)
      //  return true;

      return WSAStartup(VERSION, out _) == IP_SUCCESS;
    }

    public static void SocketFinish()
    {
      //if (Platform.ID != PlatformID.Windows)
      //  return;

      WSACleanup();
    }
  }
}

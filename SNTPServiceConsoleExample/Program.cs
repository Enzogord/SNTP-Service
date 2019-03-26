using System;
using System.Net;
using SNTPService;

namespace SNTPServiceConsoleExample
{
    class Program
    {
        static void Main(string[] args)
        {
            SntpService service = new SntpService(123);
            service.InterfaceAddress = IPAddress.Parse("192.168.1.217");
            service.Start();

            Console.ReadKey();
            service.Stop();
        }
    }
}

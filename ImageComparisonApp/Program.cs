using System;

namespace ImageComparisonServer
{
    internal class Program
    {
        public static void Main()
        {
            var server = new HttpServer(8080);
            server.Start();
        }
    }
}

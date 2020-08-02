using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace FtlIngest
{
    class Program
    {
        private const int INGEST_LISTEN_PORT = 8084;

        private TcpListener tcpListener;

        static void Main(string[] args)
        {
            new Program().Listen();
        }

        public void Listen()
        {
            var ipEndpoint = new IPEndPoint(IPAddress.Any, INGEST_LISTEN_PORT);
            tcpListener = new TcpListener(ipEndpoint);

            tcpListener.Start();

            Byte[] bytes = new byte[512];
            String data = null;

            Console.WriteLine("Waiting for connections...");
            while (true)
            {
                TcpClient client = tcpListener.AcceptTcpClient();

                Task.Run(() => {
                    new FtlConnection(client).Start();
                });
            }
        }
    }
}

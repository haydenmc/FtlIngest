using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace FtlIngest
{
    public class FtlConnection
    {
        private TcpClient tcpClient;
        private NetworkStream clientStream;

        private string streamKey = "aBcDeFgHiJkLmNoPqRsTuVwXyZ123456";

        private byte[] hmacPayload = {
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
            0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
            0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,
            0x18, 0x19, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F,
        };

        private HMACSHA512 hmac;

        private Dictionary<string, string> keyValues;

        public FtlConnection(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient;
            this.clientStream = tcpClient.GetStream();
            this.hmac = new HMACSHA512(Encoding.ASCII.GetBytes(streamKey));
            this.keyValues = new Dictionary<string, string>();
        }

        public void Start()
        {
            byte[] bytes = new byte[512];
            StringBuilder currentRequest = new StringBuilder();
            int i;
            while ((i = clientStream.Read(bytes, 0, bytes.Length)) != 0)
            {
                for (int j = 0; j < i; ++j)
                {
                    currentRequest.Append((char)bytes[j]);
                    if ((currentRequest.Length >= 4) &&
                        (currentRequest[currentRequest.Length - 4] == '\r') &&
                        (currentRequest[currentRequest.Length - 3] == '\n') &&
                        (currentRequest[currentRequest.Length - 2] == '\r') &&
                        (currentRequest[currentRequest.Length - 1] == '\n'))
                    {
                        var requestString = currentRequest.ToString();
                        processRequest(requestString.Substring(0, requestString.Length - 4));
                        currentRequest.Clear();
                    }
                }
            }
        }

        private void processRequest(string request)
        {
            Console.WriteLine($"Processing request '{request}'");
            if (request == "HMAC")
            {
                processHmacRequest();
            }
            else if (request.StartsWith("CONNECT"))
            {
                processConnectRequest(request);
            }
            else if (request.Contains(':'))
            {
                processKeyValue(request);
            }
            else if (request.StartsWith("."))
            {
                processEndIngest();
            }
            else if (request.StartsWith("PING"))
            {
                processPing();
            }
        }

        private void processHmacRequest()
        {
            Console.WriteLine("Processing HMAC request...");

            // Send back 32 characters followed by our payload
            clientStream.Write(Encoding.ASCII.GetBytes("200 "));
            Console.WriteLine("Sending payload:");
            foreach (var payloadByte in this.hmacPayload)
            {
                var hexString = payloadByte.ToString("x2");
                clientStream.Write(Encoding.ASCII.GetBytes(hexString));
                Console.Write(hexString);
            }
            Console.WriteLine();
            clientStream.Write(Encoding.ASCII.GetBytes("\n"));
            clientStream.Flush();
        }

        private void processConnectRequest(string request)
        {
            request = request.TrimEnd();
            var requestSplit = request.Split(' ');

            var channel = requestSplit[1];
            var remoteHashStr = requestSplit[2].Substring(1); // Remove leading $
            if (remoteHashStr.Length % 2 != 0)
            {
                throw new Exception("Unexpected hash length received from FTL client.");
            }

            // Parse remote hash
            byte[] remoteHash = new byte[remoteHashStr.Length / 2];
            for (int i = 0; i < (remoteHashStr.Length / 2); ++i)
            {
                remoteHash[i] = Convert.ToByte(remoteHashStr.Substring((i * 2), 2), 16);
            }

            // Compute local hash
            var hashValue = hmac.ComputeHash(hmacPayload);

            // Print and compare
            Console.WriteLine("Local hash:");
            foreach (var hashByte in hashValue)
            {
                var hexString = hashByte.ToString("x2");
                Console.Write(hexString);
            }
            Console.WriteLine();
            Console.WriteLine("Remote hash:");
            foreach (var hashByte in remoteHash)
            {
                var hexString = hashByte.ToString("x2");
                Console.Write(hexString);
            }
            Console.WriteLine();

            if (Enumerable.SequenceEqual(hashValue, remoteHash))
            {
                Console.WriteLine("Hashes match!");
                clientStream.Write(Encoding.ASCII.GetBytes("200\n"));
            }
            else
            {
                Console.WriteLine("Hashes don't match. >:(");
                clientStream.Close();
                tcpClient.Close();
            }
        }

        private void processKeyValue(string data)
        {
            var trimmed = data.TrimEnd();
            var separated = trimmed.Split(": ");

            keyValues[separated[0]] = separated[1];

            Console.WriteLine($"Set '{separated[0]}' to '{separated[1]}'");
        }

        private void processEndIngest()
        {
            clientStream.Write(Encoding.ASCII.GetBytes("200 kthx. Use UDP port 8004\n"));
        }

        private void processPing()
        {
            // pong
            clientStream.Write(Encoding.ASCII.GetBytes("201\n"));
        }
    }
}
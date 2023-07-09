﻿using NetworkLibrary.TCP.SSL.Custom;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleTest
{

    internal class Program
    {
        static void Main(string[] args)
        {
            CustomSslTest();

        }
        //----------TCP --------------------------------------------------------
        private static void CustomSslTest()
        {
            MiniLogger.AllLog += (string log) => Console.WriteLine(log);

            int totMsgClient = 0;
            int totMsgServer = 0;
            int lastTimeStamp = 1;
            int clientAmount = 100;
            const int numMsg = 10000;
            var message = new byte[3200];
            var response = new byte[3200];


            var scert = new X509Certificate2("server.pfx", "greenpass");
            var ccert = new X509Certificate2("client.pfx", "greenpass");

            CustomSslServer server = new CustomSslServer(2008, scert);
            List<CustomSslClient> clients = new List<CustomSslClient>();

            Stopwatch sw2 = new Stopwatch();
            AutoResetEvent testCompletionEvent = new AutoResetEvent(false);

            server.MaxIndexedMemoryPerClient = 1280000000;
            server.ClientSendBufsize = 128000;
            server.ClientReceiveBufsize = 128000;
            server.DropOnBackPressure = false;
            server.OnBytesReceived += OnServerReceviedMessage;
            server.StartServer();

            Task[] toWait = new Task[clientAmount];
            for (int i = 0; i < clientAmount; i++)
            {
                var client = new CustomSslClient(ccert);
                client.OnBytesReceived += (byte[] arg2, int offset, int count) => OnClientReceivedMessage(client, arg2, offset, count);
                client.MaxIndexedMemory = server.MaxIndexedMemoryPerClient;
                client.ConnectAsyncAwaitable("127.0.0.1", 2008).Wait();
                clients.Add(client);
            }
            Thread.Sleep(100);

            // -----------------------  Bechmark ---------------------------
            Console.WriteLine("Press any key to start");
            Console.Read();
            sw2.Start();

            Parallel.ForEach(clients, client =>
            {
                for (int i = 0; i < numMsg; i++)
                {
                    client.SendAsync(message);

                }

            });
            // final msg to get the time elapsed.
            foreach (var cl in clients)
            {
                cl.SendAsync(new byte[502]);
            }


            Console.WriteLine("All messages are dispatched in :" + sw2.ElapsedMilliseconds +
                "ms. Press enter to see status");
            Console.ReadLine();

            Console.WriteLine("Press E to Exit");
            while (Console.ReadLine() != "e")
            {
                ShowStatus();
            }
            void ShowStatus()
            {
                Console.WriteLine("Press E to Exit");

                Console.WriteLine("Total Messages on server: " + totMsgServer);
                Console.WriteLine("Total Messages on clients: " + totMsgClient);
                Console.WriteLine("Last Timestamp " + lastTimeStamp);
                Console.WriteLine("Elapsed " + sw2.ElapsedMilliseconds);
                var elapsedSeconds = ((float)lastTimeStamp / 1000);
                var messagePerSecond = totMsgClient / elapsedSeconds;

                Console.WriteLine(" Request-Response Per second " + totMsgClient / elapsedSeconds);
                Console.WriteLine("Data transmissıon rate Inbound " + (message.Length * messagePerSecond) / 1000000 + " Megabytes/s");
                Console.WriteLine("Data transmissıon rate Outbound " + (response.Length * messagePerSecond) / 1000000 + " Megabytes/s");
            }

            void OnClientReceivedMessage(CustomSslClient client, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgClient);
                //client.SendAsync(response);
                if (count == 502)
                {
                    lastTimeStamp = (int)sw2.ElapsedMilliseconds;
                    if (Volatile.Read(ref totMsgClient) == numMsg * clientAmount + clientAmount)
                    {
                        Console.WriteLine("--- All Clients are finished receiving response --- \n");
                        ShowStatus();
                        sw2.Stop();
                        Console.WriteLine("\n--- All Clients are finished receiving response --- \n");
                    }
                }
            }

            void OnServerReceviedMessage(Guid id, byte[] arg2, int offset, int count)
            {
                Interlocked.Increment(ref totMsgServer);
                if (count == 502)
                {
                    server.SendBytesToClient(id, new byte[502]);
                    return;
                }

                server.SendBytesToClient(id, response);
            }

        }




    }
}


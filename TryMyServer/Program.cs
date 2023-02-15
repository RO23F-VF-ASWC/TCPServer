// See https://aka.ms/new-console-template for more information
using TryMyServer;

Console.WriteLine("Hello, World!");
EchoServer server = new EchoServer(7);
server.Start();
// See https://aka.ms/new-console-template for more information
using TryMyServer;

Console.WriteLine("Hello, World!");
EchoServer server = new EchoServer("C:\\Users\\PELE\\source\\repos\\TCPServer\\TCPServerLib");
server.Start();
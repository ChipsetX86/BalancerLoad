using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;

namespace BalancerServer
{
    public static class Program
    {

        private static readonly ushort serverPort = 5000; 

        private static TcpServerBalancer balancer;

        private static List<IPEndPoint> balancingTargets = new List<IPEndPoint>();

        static void Main(string[] args)
        {

            balancingTargets.Add(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6482));
            balancingTargets.Add(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6483));
            balancingTargets.Add(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6484));
            balancer = new TcpServerBalancer(serverPort, balancingTargets);

            try
            {
                balancer.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error start balancer: {e}");
            }
            Console.ReadLine();
            balancer.Stop();
        }
    }
}

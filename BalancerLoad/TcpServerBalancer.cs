using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace BalancerServer
{
    internal class TcpServerBalancer
    {
        struct InfoServer
        {
            internal bool Enable { set; get; }
            internal int CountConnection { set; get; }
            internal DateTime LastConnection { set; get; }  
        }
        private readonly int port;
        private TcpListener tcpListener;
        private int flagStopServer = 0;
        private Task tcpListenTask;

        private readonly IReadOnlyCollection<IPEndPoint> balancingTargets;
        private static ConcurrentDictionary<IPEndPoint, InfoServer> countConnectionTarget = new ConcurrentDictionary<IPEndPoint, InfoServer>();

        public TcpServerBalancer(ushort port, IReadOnlyCollection<IPEndPoint> targets)
        {
            balancingTargets = targets ?? throw new ArgumentNullException(nameof(targets), $"{nameof(targets)} cannot be null");
            foreach (var target in balancingTargets)
                countConnectionTarget.GetOrAdd(target, new InfoServer{Enable = true, CountConnection = 0, LastConnection = DateTime.Now});
            this.port = port;
        }

        internal void Start()
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, port);
                tcpListener.Start();
                tcpListenTask = ListenConnection();
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Socket exception: {e}");
            }

            Console.WriteLine($"Load balancer started port: {port}");
        }

        public void Stop()
        {
            if (Interlocked.CompareExchange(ref flagStopServer, 1, 0) == 0)
            {
                tcpListener.Stop();
                tcpListenTask.Wait();
            }
            Console.WriteLine("Load balancer stoped");
        }

        private async Task ListenConnection()
        {
            while (flagStopServer == 0)
            {
                    try
                    {
                        var client = await tcpListener.AcceptTcpClientAsync();
                        IPEndPoint ipEndPoint;
                        do 
                        {
                            ipEndPoint = TargetServer();
                            try
                            {
                                var connect = new TcpProxyConnection(client, ClosedProxyConnection, ipEndPoint);
                                TargetIncrease(ipEndPoint);
                                break;
                            } 
                            catch (SocketException e) 
                            {
                                lock (countConnectionTarget)
                                {
                                    var t = countConnectionTarget[ipEndPoint];
                                    t.Enable = false;
                                    countConnectionTarget[ipEndPoint] = t;
                                }
                            break;
                            }
                        } while (ipEndPoint != null);
                    }
                    catch (SocketException)
                    {
                    }
             }
         }

        private IPEndPoint TargetServer() {
            lock (countConnectionTarget) 
            {
                IPEndPoint target = balancingTargets.Where(p => countConnectionTarget[p].Enable).OrderBy(p => countConnectionTarget[p].CountConnection).FirstOrDefault();
                if (target == null)
                {
                    throw new ArgumentNullException("Cannot found target server");
                }
                return target;
            }
        }

        private void TargetDecrease(IPEndPoint target)
        {
            lock (countConnectionTarget)
            {
                if (countConnectionTarget[target].CountConnection > 0)
                {
                    var t = countConnectionTarget[target];
                    t.CountConnection -= 1;
                    countConnectionTarget[target] = t;
                }
                else
                {
                    Console.WriteLine($"Error decrease target server {target}");
                }
            }
        }

        private void TargetIncrease(IPEndPoint target)
        {
            lock (countConnectionTarget)
            {
                var t = countConnectionTarget[target];
                t.CountConnection += 1;
                countConnectionTarget[target] = t;
            }
        }

        private void ClosedProxyConnection(TcpProxyConnection tcpConnect, IPEndPoint target)
        {
            TargetDecrease(target);
            tcpConnect.Dispose();
            Console.WriteLine($"Client disconnected form {target}, count connection target {countConnectionTarget[target].CountConnection}");
        }
    }
}

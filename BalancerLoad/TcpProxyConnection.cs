using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace BalancerServer
{
    using ActionClose = Action<TcpProxyConnection, IPEndPoint>;
    internal class TcpProxyConnection: IDisposable
    {
        private readonly TcpClient clientConnection;

        private readonly IPEndPoint selectedIPEndPointServer;
        private TcpClient serverConnection;

        private readonly ActionClose actionCloseConnection;
        private int flagClosed = 0;
        private const int bufferSize = 16 * 1024;

        internal TcpProxyConnection(TcpClient clientProxyConnection, ActionClose actionProxyCloseConnection, IPEndPoint serverIPEndPoint)
        {
            actionCloseConnection = actionProxyCloseConnection ?? throw new ArgumentNullException(nameof(actionProxyCloseConnection), $"{nameof(actionCloseConnection)} cannot be null"); ;
            clientConnection = clientProxyConnection ?? throw new ArgumentNullException(nameof(clientProxyConnection), $"{nameof(clientConnection)} cannot be null");
            selectedIPEndPointServer = serverIPEndPoint;

            try
            {
                serverConnection = new TcpClient(selectedIPEndPointServer.Address.ToString(), selectedIPEndPointServer.Port);
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Socket exception: {e}");
                throw e;
            }

            NetworkStream clientStream = clientConnection.GetStream();
            NetworkStream serverStream = serverConnection.GetStream();
            TrafficClientToServer(clientStream, serverStream);
            TrafficServerToClient(clientStream, serverStream);
        }

        internal bool IsConnected()
        {
            return serverConnection.Connected && clientConnection.Connected;
        }

        private async void TrafficClientToServer(Stream clientStream, Stream serverStream)
        {
            while (true)
            {
                var bufferData = new byte[bufferSize];
                int countBytesRead;
                try
                {
                    countBytesRead = await clientStream.ReadAsync(bufferData, 0, bufferSize);
                    if (countBytesRead == 0)
                    {
                        break;
                    }
                }
                catch {
                    break;
                }

                try
                {
                    await serverStream.WriteAsync(bufferData, 0, countBytesRead);
                } catch
                {
                    break;
                }
            }

            CloseConnection();
        }

        private async void TrafficServerToClient(Stream clientStream, Stream serverStream)
        {
            while (true)
            {
                var bufferData = new byte[bufferSize];
                int countBytesRead;
                try
                {
                    countBytesRead = await serverStream.ReadAsync(bufferData, 0, bufferSize);
                    if (countBytesRead == 0) break;
                }
                catch
                {
                    break;
                }

                try
                {
                    await clientStream.WriteAsync(bufferData, 0, countBytesRead);
                }
                catch
                {
                    break;
                }
            }

            CloseConnection();
        }

        private void CloseConnection()
        {
            if (Interlocked.CompareExchange(ref flagClosed, 1, 0) == 0)
            {
                actionCloseConnection(this, selectedIPEndPointServer);
            }
        }

        public void Dispose()
        {
            try
            {
                if (clientConnection.Connected)
                {
                    clientConnection.Close();
                }

                if (serverConnection.Connected)
                {
                    serverConnection.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Socket exception in dispose: {e}");
            }
        }
    }
}

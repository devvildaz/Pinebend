using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Pinebend
{
    // Bounded, ordered reliable frames. Sending never blocks the Unity simulation thread.
    public sealed class PeerLink : IDisposable
    {
        public const int MaxFrameBytes=16384, MaxPendingBytes=524288;
        readonly TcpClient socket;
        readonly NetworkStream stream;
        readonly byte[] chunk=new byte[8192];
        readonly MemoryStream frame=new();
        readonly ConcurrentQueue<byte[]> pending=new();
        readonly SemaphoreSlim available=new(0);
        readonly CancellationTokenSource stopped=new();
        readonly Task writer;
        volatile Exception sendFailure;
        int pendingBytes,disposed;
        public PeerLink(TcpClient client)
        {
            socket=client;socket.NoDelay=true;stream=socket.GetStream();writer=WriteLoop();
        }
        public void Send(string value)
        {
            if(Volatile.Read(ref disposed)!=0)throw new IOException("The connection is closed.");
            if(sendFailure!=null)throw new IOException("Unable to send to a peer.",sendFailure);
            byte[] bytes=Encoding.UTF8.GetBytes(value+"\n");
            if(bytes.Length>MaxFrameBytes+1)throw new IOException("Oversized outbound packet.");
            if(Interlocked.Add(ref pendingBytes,bytes.Length)>MaxPendingBytes)
            {
                Interlocked.Add(ref pendingBytes,-bytes.Length);
                throw new IOException("A peer cannot keep up with the race. Check connection latency.");
            }
            pending.Enqueue(bytes);available.Release();
        }
        async Task WriteLoop()
        {
            try
            {
                while(true)
                {
                    await available.WaitAsync(stopped.Token).ConfigureAwait(false);
                    if(!pending.TryDequeue(out var bytes))continue;
                    await stream.WriteAsync(bytes,0,bytes.Length,stopped.Token).ConfigureAwait(false);
                    Interlocked.Add(ref pendingBytes,-bytes.Length);
                }
            }
            catch(Exception error) when(error is OperationCanceledException || Volatile.Read(ref disposed)!=0) { }
            catch(Exception error){sendFailure=error;}
        }
        public void Read(Action<string> receive)
        {
            if(sendFailure!=null)throw new IOException("The peer connection failed.",sendFailure);
            if(socket.Client.Poll(0,SelectMode.SelectRead) && socket.Available==0)throw new IOException("A peer disconnected.");
            for(int batch=0;batch<8 && socket.Available>0;batch++)
            {
                int count=stream.Read(chunk,0,Math.Min(chunk.Length,socket.Available));
                for(int i=0;i<count;i++)
                {
                    if(chunk[i]==10)
                    {
                        string line=Encoding.UTF8.GetString(frame.GetBuffer(),0,(int)frame.Length);
                        frame.SetLength(0);receive(line);
                    }
                    else
                    {
                        if(frame.Length>=MaxFrameBytes)throw new IOException("Oversized peer packet.");
                        frame.WriteByte(chunk[i]);
                    }
                }
            }
        }
        public void Dispose()
        {
            if(Interlocked.Exchange(ref disposed,1)!=0)return;
            stopped.Cancel();socket.Close();frame.Dispose();
            // Dispose synchronization primitives only after the asynchronous writer exits.
            _=writer.ContinueWith(_=>{available.Dispose();stopped.Dispose();},TaskScheduler.Default);
        }
    }
}

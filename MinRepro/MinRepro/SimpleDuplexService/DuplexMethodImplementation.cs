using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MinRepro.SimpleDuplexService
{
    public class DuplexMethodImplementation
    {
        Func<IAsyncStreamReader<ClientToServerMessage>, IServerStreamWriter<ServerToClientMessage>, ServerCallContext, Task> Impl { get; }

        public DuplexMethodImplementation(Func<IAsyncStreamReader<ClientToServerMessage>, IServerStreamWriter<ServerToClientMessage>, ServerCallContext, Task> impl)
        {
            Impl = impl;
        }

        public async Task Start(IAsyncStreamReader<ClientToServerMessage> requestStream, IServerStreamWriter<ServerToClientMessage> responseStream, ServerCallContext context)
        {
            await Impl(requestStream, responseStream, context);
        }
    }
}

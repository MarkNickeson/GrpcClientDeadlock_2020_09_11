using Grpc.Core;
using MinRepro;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MinRepro.SimpleDuplexService
{
    public class SimpleDuplexServiceImpl : MinRepro.SimpleDuplex.SimpleDuplexBase
    {
        DuplexMethodImplementation ConfiguredDuplexMethodImplementation { get; }

        public SimpleDuplexServiceImpl(DuplexMethodImplementation duplexMethodImplementation)
        {
            ConfiguredDuplexMethodImplementation = duplexMethodImplementation;
        }

        public async override Task Start(
            IAsyncStreamReader<ClientToServerMessage> requestStream, 
            IServerStreamWriter<ServerToClientMessage> responseStream, 
            ServerCallContext context)
        {
            // forward call to delegate
            await ConfiguredDuplexMethodImplementation.Start(requestStream, responseStream, context);
        }
    }
}

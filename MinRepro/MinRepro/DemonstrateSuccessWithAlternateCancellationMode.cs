using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MinRepro.SimpleDuplexService;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MinRepro
{
    public class DemonstrateSuccessWithAlternateCancellationMode
    {
        [Fact]
        public async Task SucceedsIfCancellationViaClientCallOptions()
        {           
            // setup server side scenario
            // - server block on req.MoveNext and verify expectations on client cancellation 

            bool serverCancellationCallbackInvoked = false;
            var serverMoveNextOperationCancelled = false;
            bool? serverMoveNextResult = null;
            var serverMoveNextTrigger = new ManualResetEventSlim();

            var serviceTestFixture = new GrpcTestFixture<Startup>(true, (sc) =>
            {
                // test-specific dependency injection
                sc.TryAddSingleton<DuplexMethodImplementation>(new DuplexMethodImplementation(async (req, res, ctx) =>
                {
                    ctx.CancellationToken.Register(() =>
                    {
                        serverCancellationCallbackInvoked = true;
                    });

                    try
                    {
                        serverMoveNextTrigger.Set();
                        serverMoveNextResult = await req.MoveNext(CancellationToken.None);
                    }
                    catch (OperationCanceledException)
                    {
                        serverMoveNextOperationCancelled = true;
                    }

                    // keep alive?
                }));
            });

            // setup client side scenario 
            // - client starts streaming call which invokes serviceTestFixture
            // - client block on ResponseStream.MoveNext w/ cancellation token
            bool? clientMoveNextResult = null;

            CancellationTokenSource clientCallOptionsCts = new CancellationTokenSource();

            var channel = GrpcChannel.ForAddress(serviceTestFixture.Client.BaseAddress, new GrpcChannelOptions
            {
                LoggerFactory = serviceTestFixture.LoggerFactory,
                HttpClient = serviceTestFixture.Client
            });

            var client = new SimpleDuplex.SimpleDuplexClient(channel);

            var clientStreamingCall = client.Start(cancellationToken: clientCallOptionsCts.Token); //  <---- added cancellation token here via CallOptions

            // setup complete, execute test

            // start clientReadTask that blocks awaiting client.MoveNext
            var clientReadTask = Task.Run(async () =>
            {
                clientMoveNextResult = await clientStreamingCall.ResponseStream.MoveNext(CancellationToken.None); //  <---- switched to CancellationToken.None
            });

            // pause to ensure server has reached req.MoveNext and to allow background task to reach MoveNext
            serverMoveNextTrigger.Wait();
            await Task.Delay(200);

            // cancel the client
            clientCallOptionsCts.Cancel();

            // await clientReadTask, with expectation of RpcException with StatusCode.Cancelled           
            var exception = await Assert.ThrowsAsync<RpcException>(async () => await clientReadTask);
            Assert.True(exception.StatusCode == StatusCode.Cancelled);

            // expect server cancellation callback to have been invoked
            Assert.True(serverCancellationCallbackInvoked);
            // expect server MoveNext to have exited with OperationCanceledException
            Assert.True(serverMoveNextOperationCancelled);
            // DO NOT expect server MoveNext to have return value
            Assert.Null(serverMoveNextResult);
            // DO NOT expect client MoveeNext to have return value
            Assert.Null(clientMoveNextResult);
        }
    }
}

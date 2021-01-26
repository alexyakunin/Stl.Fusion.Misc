using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Stl.Async;
using Stl.Fusion;
using static System.Console;

namespace UpdateExamples
{
    public class Program
    {
        private static async Task Main()
        {
            var s = new ServiceCollection();
            s.AddFusion().AddComputeService<MyService>().AddComputeService<MyAggregateService>();
            var sb = s.BuildServiceProvider();

            var stateFactory = sb.GetRequiredService<IStateFactory>();
            var myAggregateService = sb.GetRequiredService<MyAggregateService>();

            async Task TestAsync(string testId)
            {
                using var live = stateFactory.NewLive<string>((_, ct) => myAggregateService.AggregateAsync());
                live.Updated += (s, _) => WriteLine($"{testId}: {s.Value}");
                await Task.Delay(5000);
                myAggregateService.UiInput = "New Value";
                await Task.Delay(5000);
            }

            var tasks = new List<Task>();
            for (var i = 0; i < 5; i++)
                tasks.Add(Task.Run(() => TestAsync(i.ToString())));
            await Task.WhenAll(tasks);
        }
    }

    public class MyAggregateService
    {
        private readonly MyService _myService;
        private string _uiInput = "Initial Value";

        public MyAggregateService(MyService myService)
        {
            _myService = myService;
        }

        public string UiInput
        {
            get => _uiInput;
            set
            {
                _uiInput = value;
                using var _ = Computed.Invalidate();
                AggregateAsync().Ignore();
            }
        }

        [ComputeMethod]
        public virtual async Task<string> AggregateAsync()
        {
            var serviceResult = await _myService.GetWebApiResultAsync();
            return $"{serviceResult} - {_uiInput}";
        }
    }

    public class MyService
    {
        private Stopwatch _stopwatch = Stopwatch.StartNew();

        [ComputeMethod]
        public virtual async Task<string> GetWebApiResultAsync()
        {
            var computed = Computed.GetCurrent();
            using var _ = Computed.SuspendDependencyCapture();
            var result = await GetWebApiResultAsyncImpl();

            async Task MaybeInvalidate()
            {
                await Task.Delay(2000); // The delay here should be >= AutoInvalidateTime value above
                var mustInvalidate = true;

                try {
                    var newValue = await GetWebApiResultAsyncImpl();
                    mustInvalidate = IsChanged((string) computed!.Value!, newValue);
                }
                catch (Exception) {
                    // Intended
                }
                finally {
                    // One of actions below should be called no matter what,
                    // otherwise GetWebApiResultAsync result will stay the same forever
                    if (mustInvalidate)
                        computed!.Invalidate();
                    else
                        Task.Run(MaybeInvalidate).Ignore();
                }
            }

            Task.Run(MaybeInvalidate).Ignore();
            return result;
        }

        [ComputeMethod(KeepAliveTime = 1, AutoInvalidateTime = 1)] // Caches WebAPI call result for 1 second
        protected virtual Task<string> GetWebApiResultAsyncImpl()
        {
            WriteLine("Executing GetWebApiResultAsyncImpl");
            var result = Math.Floor(_stopwatch.Elapsed.TotalSeconds / 3).ToString(CultureInfo.InvariantCulture);
            return Task.FromResult(result);
        }

        private bool IsChanged(string a, string b)
            => a != b;
    }
}

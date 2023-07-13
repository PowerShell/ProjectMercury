using Azure.Core;
using Azure.Core.Pipeline;

#nullable enable

namespace Shell
{
    internal sealed class ApimRetryPolicy : RetryPolicy
    {
        private const string RetryAfterHeaderName = "Retry-After";
        private const string RetryAfterMsHeaderName = "retry-after-ms";
        private const string XRetryAfterMsHeaderName = "x-ms-retry-after-ms";

        /// <summary>
        /// Initializes a new instance of the <see cref="ApimRetryPolicy"/> class.
        /// </summary>
        /// <param name="maxRetries">The maximum number of retries to attempt.</param>
        /// <param name="delayStrategy">The delay to use for computing the interval between retry attempts.</param>
        public ApimRetryPolicy(int maxRetries = 2, DelayStrategy? delayStrategy = default) : base(
            maxRetries,
            delayStrategy ?? DelayStrategy.CreateExponentialDelayStrategy(
                initialDelay: TimeSpan.FromSeconds(0.8),
                maxDelay: TimeSpan.FromSeconds(5)))
        {
            // By default, we retry 2 times at most, and use a delay strategy that waits 5 seconds at most between retries.
        }

        protected override bool ShouldRetry(HttpMessage message, Exception? exception) => ShouldRetryImpl(message, exception);

        protected override ValueTask<bool> ShouldRetryAsync(HttpMessage message, Exception? exception) => new(ShouldRetryImpl(message, exception));

        private bool ShouldRetryImpl(HttpMessage message, Exception? exception)
        {
            bool result = base.ShouldRetry(message, exception);

            if (result && message.HasResponse)
            {
                TimeSpan? retryAfter = GetRetryAfterHeaderValue(message.Response.Headers);
                if (retryAfter > TimeSpan.FromSeconds(5))
                {
                    // Do not retry if the required interval is longer than 5 seconds.
                    return false;
                }
            }

            return result;
        }

        private TimeSpan? GetRetryAfterHeaderValue(ResponseHeaders headers)
        {
            if (headers.TryGetValue(RetryAfterMsHeaderName, out var retryAfterValue) ||
                headers.TryGetValue(XRetryAfterMsHeaderName, out retryAfterValue))
            {
                if (int.TryParse(retryAfterValue, out var delaySeconds))
                {
                    return TimeSpan.FromMilliseconds(delaySeconds);
                }
            }

            if (headers.TryGetValue(RetryAfterHeaderName, out retryAfterValue))
            {
                if (int.TryParse(retryAfterValue, out var delaySeconds))
                {
                    return TimeSpan.FromSeconds(delaySeconds);
                }

                if (DateTimeOffset.TryParse(retryAfterValue, out DateTimeOffset delayTime))
                {
                    return delayTime - DateTimeOffset.Now;
                }
            }

            return default;
        }
    }
}

using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Polly;
using Polly.Retry;

namespace NotificationService.Infrastructure.Mail;

public static class ResiliencePolicies
{
    public static ResiliencePipeline SmtpSendPipeline() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<SmtpCommandException>(ex => !EmailFailureClassifier.IsPermanent(ex))
                    .Handle<SmtpProtocolException>()
                    .Handle<SocketException>()
                    .Handle<IOException>()
                    .Handle<TimeoutException>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(250),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            })
            .AddTimeout(TimeSpan.FromSeconds(15))
            .Build();
}

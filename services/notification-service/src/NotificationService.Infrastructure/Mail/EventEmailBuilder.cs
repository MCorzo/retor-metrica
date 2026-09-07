using System.Text.Json;
using MimeKit;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Mail;

public sealed class EventEmailBuilder(SmtpOptions options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MimeMessage Build(NotificationRecord record)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        message.To.Add(new MailboxAddress("Admin", options.FromAddress));
        message.Subject = $"Event created: {record.EventName}";
        message.Body = new TextPart("plain")
        {
            Text = BuildBody(record),
        };
        return message;
    }

    private static string BuildBody(NotificationRecord record)
    {
        var zones = string.Empty;
        if (!string.IsNullOrWhiteSpace(record.ZoneDetailsJson))
        {
            var zoneList = JsonSerializer.Deserialize<List<EventZoneLine>>(record.ZoneDetailsJson, JsonOptions) ?? [];
            zones = string.Join(Environment.NewLine, zoneList.Select(z => $"  - {z.Name}: ${z.Price:0.00} ({z.Capacity} seats)"));
        }

        return $"A new event was created on the platform:{Environment.NewLine}" +
               $"Name: {record.EventName}{Environment.NewLine}" +
               $"Date: {record.EventDate:O}{Environment.NewLine}" +
               $"Venue: {record.EventVenue}{Environment.NewLine}" +
               $"Correlation id: {record.CorrelationId}{Environment.NewLine}" +
               (string.IsNullOrEmpty(zones) ? string.Empty : $"Zones:{Environment.NewLine}{zones}");
    }

    private sealed class EventZoneLine
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Capacity { get; set; }
    }
}

using System.Net;

namespace TalentPilot.Application.Notifications;

public static class TalentPilotEmailTemplate
{
    public const string TemplateMarker = "talent-pilot-email-template-v1";

    public static string Build(
        string eyebrow,
        string heading,
        string body,
        IReadOnlyList<(string Label, string Value)>? details = null,
        string? actionLabel = null,
        string? actionUrl = null,
        string? preheader = null)
    {
        var detailsHtml = details is { Count: > 0 }
            ? BuildDetails(details)
            : string.Empty;
        var actionHtml = string.IsNullOrWhiteSpace(actionLabel) || string.IsNullOrWhiteSpace(actionUrl)
            ? string.Empty
            : $"""
              <p style="margin:0 0 12px;">
                <a href="{Attr(actionUrl)}" style="background:#0a66c2;border-radius:8px;color:#ffffff;display:inline-block;font-family:Arial,sans-serif;font-size:16px;font-weight:700;line-height:20px;padding:14px 22px;text-decoration:none;">{Html(actionLabel)}</a>
              </p>
              <p style="color:#64748b;font-family:Arial,sans-serif;font-size:12px;line-height:18px;margin:0;">If the button does not open, copy this link: <a href="{Attr(actionUrl)}" style="color:#0a66c2;">{Html(actionUrl)}</a></p>
              """;

        return $"""
          <!doctype html>
          <html lang="en">
            <!-- {TemplateMarker} -->
            <body style="background:#f4f7fb;margin:0;padding:0;">
              <div style="display:none;font-size:1px;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;">{Html(preheader ?? heading)}</div>
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f4f7fb;border-collapse:collapse;margin:0;padding:0;width:100%;">
                <tr>
                  <td style="padding:32px 14px;">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#ffffff;border-collapse:collapse;border-radius:12px;box-shadow:0 18px 45px rgba(15,23,42,0.10);margin:0 auto;max-width:640px;overflow:hidden;width:100%;">
                      <tr>
                        <td style="background:#0a66c2;padding:28px 32px;">
                          {LogoHtml()}
                          <p style="color:#bfdbfe;font-family:Arial,sans-serif;font-size:12px;font-weight:700;letter-spacing:1px;margin:18px 0 10px;text-transform:uppercase;">{Html(eyebrow)}</p>
                          <h1 style="color:#ffffff;font-family:Arial,sans-serif;font-size:26px;line-height:32px;margin:0;">{Html(heading)}</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:30px 32px 34px;">
                          {BuildParagraphs(body)}
                          {detailsHtml}
                          {actionHtml}
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
          </html>
          """;
    }

    public static string Html(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    public static string Attr(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static string LogoHtml()
    {
        return """
          <table role="presentation" cellspacing="0" cellpadding="0" style="border-collapse:collapse;">
            <tr>
              <td style="vertical-align:middle;width:34px;">
                <table role="presentation" cellspacing="0" cellpadding="0" style="border-collapse:separate;border-spacing:4px;width:34px;">
                  <tr>
                    <td style="background:#60a5fa;border-radius:4px;height:10px;width:10px;"></td>
                    <td style="background:#67e8f9;border-radius:4px;height:10px;width:10px;"></td>
                  </tr>
                  <tr>
                    <td style="background:#ffffff;border-radius:4px;height:10px;width:10px;"></td>
                    <td style="background:#1d4ed8;border-radius:4px;height:10px;width:10px;"></td>
                  </tr>
                </table>
              </td>
              <td style="color:#ffffff;font-family:Arial,sans-serif;font-size:16px;font-weight:800;line-height:20px;padding-left:10px;vertical-align:middle;">Talent Pilot</td>
            </tr>
          </table>
          """;
    }

    private static string BuildParagraphs(string body)
    {
        var paragraphs = body
            .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(paragraph => paragraph.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "<br />", StringComparison.Ordinal))
            .Select(paragraph => $"<p style=\"color:#334155;font-family:Arial,sans-serif;font-size:15px;line-height:24px;margin:0 0 18px;\">{HtmlPreservingLineBreaks(paragraph)}</p>");

        return string.Join(Environment.NewLine, paragraphs);
    }

    private static string HtmlPreservingLineBreaks(string value)
    {
        return string.Join("<br />", value.Split("<br />", StringSplitOptions.None).Select(Html));
    }

    private static string BuildDetails(IReadOnlyList<(string Label, string Value)> details)
    {
        var rows = details.Select((detail, index) =>
        {
            var border = index == 0 ? string.Empty : "border-top:1px solid #e2e8f0;";
            var background = index % 2 == 0 ? "background:#f8fafc;" : string.Empty;
            return $"""
              <tr>
                <td style="{background}{border}padding:14px 16px;">
                  <p style="color:#64748b;font-family:Arial,sans-serif;font-size:12px;font-weight:700;letter-spacing:.5px;margin:0 0 4px;text-transform:uppercase;">{Html(detail.Label)}</p>
                  <p style="color:#0f172a;font-family:Arial,sans-serif;font-size:16px;font-weight:700;line-height:22px;margin:0;">{Html(detail.Value)}</p>
                </td>
              </tr>
              """;
        });

        return $"""
          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border-collapse:separate;border-spacing:0;border:1px solid #dbeafe;border-radius:12px;margin:6px 0 24px;overflow:hidden;">
            {string.Join(Environment.NewLine, rows)}
          </table>
          """;
    }
}

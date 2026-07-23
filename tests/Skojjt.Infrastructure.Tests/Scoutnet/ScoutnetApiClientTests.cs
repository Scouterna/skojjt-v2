using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Skojjt.Infrastructure.Scoutnet;

namespace Skojjt.Infrastructure.Tests.Scoutnet;

[TestClass]
public class ScoutnetApiClientTests
{
    private const int TargetMemberNo = 3292644;

    [TestMethod]
    public async Task GetProjectParticipantsAsync_EmptyPrimaryMembershipInfoAcrossBufferBoundary_Parses()
    {
        // Regression test for the camp import failure:
        // Scoutnet returns primary_membership_info as an empty array [] for some participants.
        // When that value straddled an async read-buffer boundary, streaming deserialization
        // (ReadFromJsonAsync) with a Skip-based custom converter threw. The client now buffers
        // the whole response before deserializing. The target member is placed well past the
        // ~16 KB streaming buffer boundary to exercise that path.
        var json = BuildLargeParticipantsJson(TargetMemberNo, paddingParticipants: 400);
        Assert.IsGreaterThan(30000, json.Length, "Payload should exceed a single read buffer.");

        var handler = new StreamingJsonHandler(json);
        var client = CreateClient(handler);

        var result = await client.GetProjectParticipantsAsync(1190, "test-key");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Participants.ContainsKey(TargetMemberNo.ToString()));

        var target = result.Participants[TargetMemberNo.ToString()];
        Assert.AreEqual(TargetMemberNo, target.MemberNo);
        Assert.IsNull(target.PrimaryMembershipInfo);
    }

    private static ScoutnetApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new ScoutnetOptions { BaseUrl = "https://scoutnet.example" });
        var logger = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<ScoutnetApiClient>();
        return new ScoutnetApiClient(httpClient, options, logger);
    }

    private static string BuildLargeParticipantsJson(int targetMemberNo, int paddingParticipants)
    {
        var sb = new StringBuilder();
        sb.Append("{\"participants\":{");

        for (var i = 0; i < paddingParticipants; i++)
        {
            var memberNo = 1000000 + i;
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append('"').Append(memberNo).Append("\":{")
              .Append("\"member_no\":").Append(memberNo).Append(',')
              .Append("\"first_name\":\"Testförnamn").Append(i).Append("\",")
              .Append("\"last_name\":\"Testefternamn").Append(i).Append("\",")
              .Append("\"primary_membership_info\":{")
              .Append("\"group_id\":1137,\"group_name\":\"Tynnereds Scoutkår\",")
              .Append("\"troop_id\":11268,\"troop_name\":\"Ledare\",")
              .Append("\"patrol_id\":null,\"patrol_name\":null}}");
        }

        // The failing member: primary_membership_info is an empty array.
        sb.Append(",\"").Append(targetMemberNo).Append("\":{")
          .Append("\"member_no\":").Append(targetMemberNo).Append(',')
          .Append("\"first_name\":\"Siri\",\"last_name\":\"Dahlqvist\",")
          .Append("\"primary_membership_info\":[]}");

        sb.Append("}}");
        return sb.ToString();
    }

    /// <summary>
    /// Returns the JSON body as a streamed response so that deserialization reads it in
    /// multiple buffer segments, matching real HTTP behavior.
    /// </summary>
    private sealed class StreamingJsonHandler(string json) : HttpMessageHandler
    {
        private readonly byte[] _payload = Encoding.UTF8.GetBytes(json);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new StreamContent(new MemoryStream(_payload));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            return Task.FromResult(response);
        }
    }
}

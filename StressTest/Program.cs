using NBomber.CSharp;
using NBomber.Http.CSharp;
using Microsoft.AspNetCore.SignalR.Client;

// HTTP scenario
var httpScenario = Scenario.Create("http_load", async context =>
{
	using var client = new HttpClient();
	var response = await client.GetAsync("https://localhost:7224/dev-auth/quick-login/ledare?returnUrl=%2F");
	response = await client.GetAsync("https://localhost:7224/sk/1137/t/20251/3365");
	return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
})
.WithLoadSimulations(
	Simulation.Inject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(1))
);

// SignalR scenario (tests your AttendanceHub)
var signalRScenario = Scenario.Create("signalr_load", async context =>
{
	var connection = new HubConnectionBuilder()
		.WithUrl("https://localhost:7224/attendancehub")
		.Build();

	try
	{
		await connection.StartAsync();
		await connection.InvokeAsync("JoinTroopGroup", 3365);
		await connection.InvokeAsync("BroadcastAttendanceChange", 3365, 1, 1, true);
		await connection.StopAsync();
		return Response.Ok();
	}
	catch (Exception)
	{
		return Response.Fail();
	}
})
.WithLoadSimulations(
	Simulation.KeepConstant(copies: 50, during: TimeSpan.FromMinutes(1))
);

NBomberRunner
	.RegisterScenarios(httpScenario, signalRScenario)
	.Run();
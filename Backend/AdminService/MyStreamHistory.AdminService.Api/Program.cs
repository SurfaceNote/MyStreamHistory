using MyStreamHistory.AdminService.Api;

if (args is ["--hash-password"]) { AdminPassword.RunGenerator(); return; }
AdminApplication.Build(WebApplication.CreateBuilder(args)).Run();

public partial class Program { }

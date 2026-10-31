using Poseidon.Api;

public partial class Program
{
    public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

    public static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
        .ConfigureLogging(logging =>
        {
            // Framework request/auth/EF diagnostics can contain callback URLs or database details.
            logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.None);
            logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.None);
            logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
        })
        .ConfigureWebHostDefaults(web => web.UseStartup<Startup>());
}

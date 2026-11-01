using Poseidon.Api;

public partial class Program
{
    public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

    public static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, configuration) =>
        {
            var path = Environment.GetEnvironmentVariable("POSEIDON_LAB_CONFIG");
            if (!string.IsNullOrEmpty(path))
            {
                if (!context.HostingEnvironment.IsDevelopment()) throw new InvalidOperationException("Local lab configuration requires Development.");
                configuration.AddJsonFile(path, optional: false, reloadOnChange: false);
            }
        })
        .ConfigureLogging(logging =>
        {
            // Framework request/auth/EF diagnostics can contain callback URLs or database details.
            logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.None);
            logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.None);
            logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
        })
        .ConfigureWebHostDefaults(web => web.UseStartup<Startup>());
}

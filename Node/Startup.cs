using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Node.Api.Hubs;

namespace Node;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Cat Transfer P2P API v1");
                c.RoutePrefix = "swagger";
            });
        }

        app.UseCors();

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();

            endpoints.MapHub<CatTransferHub>("/catTransferHub");

            endpoints.MapGet("/health", async context =>
            {
                await context.Response.WriteAsync("Cat Transfer P2P Node is running");
            });

            endpoints.MapGet("/api/info", async context =>
            {
                var info = new
                {
                    name = "Cat Transfer P2P API",
                    version = "1.0.0",
                    description = "API REST para controle do sistema Cat Transfer P2P",
                    endpoints = new
                    {
                        swagger = "/swagger",
                        signalr = "/catTransferHub",
                        health = "/health"
                    }
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(info));
            });
        });

        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Startup>>();
            logger.LogInformation("Request: {Method} {Path}", context.Request.Method, context.Request.Path);
            
            await next();
            
            logger.LogInformation("Response: {StatusCode}", context.Response.StatusCode);
        });
    }
}

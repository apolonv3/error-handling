using System.Diagnostics;
using ErrorHandling.Api.Infrastructure;
using ErrorHandling.Api.Middleware;
using ErrorHandling.Api.ProblemDetails;
using ErrorHandling.Domain.Repositories;
using ErrorHandling.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder
    .Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Customize validation problem details
        options.InvalidModelStateResponseFactory = context =>
        {
            var problemDetails = new ValidationProblemDetails(context.ModelState)
            {
                Type = "https://example.com/errors/validation",
                Title = "Validation Failed",
                Status = StatusCodes.Status400BadRequest,
                Instance = context.HttpContext.Request.Path,
            };

            problemDetails.Extensions["traceId"] =
                Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
            problemDetails.Extensions["timestamp"] = DateTime.UtcNow;

            return new BadRequestObjectResult(problemDetails)
            {
                ContentTypes = { "application/problem+json" },
            };
        };
    });

// Configure Problem Details
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = (context) =>
    {
        context.ProblemDetails.Extensions["apiVersion"] = "1.0.0";
        context.ProblemDetails.Extensions["environment"] = builder.Environment.EnvironmentName;
        context.ProblemDetails.Extensions["timestamp"] = DateTime.UtcNow;

        if (builder.Environment.IsDevelopment())
        {
            // Include exception details only in development
            var exceptionFeature =
                context.HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
            if (exceptionFeature?.Error != null)
            {
                context.ProblemDetails.Extensions["exception"] = new
                {
                    message = exceptionFeature.Error.Message,
                    type = exceptionFeature.Error.GetType().Name,
                    stackTrace = exceptionFeature.Error.StackTrace,
                };
            }
        }
    };
});

// Register custom ProblemDetailsFactory
builder.Services.AddSingleton<ProblemDetailsFactory, CustomProblemDetailsFactory>();

// Register repositories
builder.Services.AddSingleton<ICustomerRepository, InMemoryCustomerRepository>();
builder.Services.AddSingleton<IProductRepository, InMemoryProductRepository>();
builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

// Register services
builder.Services.AddScoped<ExceptionOrderService>();
builder.Services.AddScoped<ResultOrderService>();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Error Handling Demo API",
            Version = "v1",
            Description = "Demonstrates different error handling approaches in .NET",
        }
    );
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Error Handling Demo API v1");
        options.RoutePrefix = string.Empty; // Swagger at root
    });
}
else
{
    // Production error handler
    app.UseExceptionHandler("/error");
}

// Add custom middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHttpsRedirection();
app.UseCors();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// Add error endpoint
app.Map(
    "/error",
    (HttpContext context) =>
    {
        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred",
            Type = "https://example.com/errors/internal-server-error",
            Instance = context.Request.Path,
        };

        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        problemDetails.Extensions["timestamp"] = DateTime.UtcNow;

        return Results.Problem(problemDetails);
    }
);

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithOpenApi();

app.Run();

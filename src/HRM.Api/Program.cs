using System.Text;
using FluentValidation;
using HRM.Api.Middleware;
using HRM.Modules.Identity.Api;
using HRM.Modules.Organization.Api;
using HRM.Modules.Personnel.Api;
using HRM.Modules.Scheduler.Jobs;
using HRM.Shared.Kernel.Behaviors;
using HRM.Shared.Kernel.Messaging;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

// Add services from all modules
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddOrganizationModule(builder.Configuration);
builder.Services.AddPersonnelModule(builder.Configuration);

// Register Validators from all assemblies
builder.Services.AddValidatorsFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());

// Register MediatR Pipeline Behaviors
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Add Shared Kernel services
builder.Services.AddScoped<IMessageBroker, InMemoryMessageBroker>();

// Add Middleware
builder.Services.AddScoped<ExceptionHandlingMiddleware>();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Quartz for background jobs
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey(nameof(OutboxProcessorJob));
    q.AddJob<OutboxProcessorJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithSimpleSchedule(schedule =>
            schedule.WithIntervalInSeconds(10).RepeatForever()));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]))
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints from all modules
app.MapIdentityEndpoints();
app.MapOrganizationEndpoints();
app.MapPersonnelEndpoints();


app.Run();

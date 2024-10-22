using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.IO.Converters;
using son;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        options.SerializerSettings.Converters.Add(new GeometryConverter(geometryFactory));
    });

builder.Services.AddDbContext<RepositoryContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("sqlConnection"),
    x => x.UseNetTopologySuite()));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:4200", "http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient();


builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();

app.MapControllers();

app.Run();

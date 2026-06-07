var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
	options.AddPolicy("Phase2BLocalBrowserProof", policy =>
	{
		policy.AllowAnyOrigin();
		policy.AllowAnyHeader();
		policy.AllowAnyMethod();
	});
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// adds middleware for HTTP -> HTTPS
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
	// allow cross domain requests
	app.UseCors("Phase2BLocalBrowserProof");
}
app.UseAuthorization();

// add endpoints
app.MapControllers();

app.Run();

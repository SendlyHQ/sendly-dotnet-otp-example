using Sendly;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

var otpStore = new ConcurrentDictionary<string, string>();

var apiKey = builder.Configuration["Sendly:ApiKey"] 
    ?? throw new InvalidOperationException("Sendly:ApiKey is not configured");

var client = new SendlyClient(apiKey);

app.MapGet("/", () => Results.Redirect("/index.html"));

app.MapPost("/send-otp", async (SendOtpRequest request) =>
{
    try
    {
        var response = await client.Verify.SendAsync(new SendVerificationRequest 
        { 
            To = request.Phone 
        });

        otpStore[request.Phone] = response.Id;

        return Results.Ok(new { success = true, verificationId = response.Id });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, error = ex.Message });
    }
});

app.MapPost("/verify-otp", async (VerifyOtpRequest request) =>
{
    try
    {
        if (!otpStore.TryGetValue(request.Phone, out var verificationId))
        {
            return Results.BadRequest(new { success = false, error = "No verification found for this phone number" });
        }

        var result = await client.Verify.CheckAsync(verificationId, new CheckVerificationRequest 
        { 
            Code = request.Code 
        });

        if (result.Status == "verified")
        {
            otpStore.TryRemove(request.Phone, out _);
            return Results.Ok(new { success = true, message = "Phone number verified successfully!" });
        }
        else
        {
            return Results.BadRequest(new { success = false, error = "Invalid verification code" });
        }
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, error = ex.Message });
    }
});

app.Run();

record SendOtpRequest(string Phone);
record VerifyOtpRequest(string Phone, string Code);

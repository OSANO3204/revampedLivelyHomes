using HousingProject.Architecture.Constants;
using HousingProject.Architecture.CRUDServices.Email;
using HousingProject.Architecture.Data;
using HousingProject.Architecture.HouseRegistration_Services;
using HousingProject.Architecture.IHouseRegistration_Services;
using HousingProject.Architecture.Interfaces.IEmail;
using HousingProject.Architecture.Interfaces.ILandlordModel;
using HousingProject.Architecture.Interfaces.IlogginServices;
using HousingProject.Architecture.Interfaces.IRenteeServices;
using HousingProject.Architecture.IPeopleManagementServvices;
using HousingProject.Architecture.PeopleManagementServices;
using HousingProject.Architecture.Services.Landlord;
using HousingProject.Architecture.Services.Rentee.Services;
using HousingProject.Architecture.Services.User_Login;
using HousingProject.Core.Models.Email;
using HousingProject.Core.Models.People;
using HousingProject.Infrastructure.CRUDServices.HouseRegistration_Services.HouseUnitsServices;
using HousingProject.Infrastructure.CRUDServices.MainPaymentServices;
using HousingProject.Infrastructure.CRUDServices.N_IMages_Services;
using HousingProject.Infrastructure.CRUDServices.Payments.Daraja;
using HousingProject.Infrastructure.CRUDServices.Payments.Rent;
using HousingProject.Infrastructure.CRUDServices.ProfessionalsServices;
using HousingProject.Infrastructure.CRUDServices.UsersExtra;
using HousingProject.Infrastructure.ExtraFunctions;
using HousingProject.Infrastructure.ExtraFunctions.Checkroles.ChcekRoles;
using HousingProject.Infrastructure.ExtraFunctions.Checkroles.IcheckRole;
using HousingProject.Infrastructure.ExtraFunctions.GenerateWorkId;
using HousingProject.Infrastructure.ExtraFunctions.IExtraFunctions;
using HousingProject.Infrastructure.ExtraFunctions.Images;
using HousingProject.Infrastructure.ExtraFunctions.LoggedInUser;
using HousingProject.Infrastructure.ExtraFunctions.RolesDescription;
using HousingProject.Infrastructure.Interfaces.IDarraja;
using HousingProject.Infrastructure.Interfaces.IHouseRegistration_Services;
using HousingProject.Infrastructure.Interfaces.IProfessionalsServices;
using HousingProject.Infrastructure.Interfaces.ITenantStatementServices;
using HousingProject.Infrastructure.Interfaces.IUserExtraServices;
using HousingProject.Infrastructure.JobServices;
using HousingProject.Infrastructure.JobServices.Payment_Receipts;
using HousingProject.Infrastructure.JobServices.tenantjobs;
using HousingProject.Infrastructure.SuperServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Quartz;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var provider = builder.Services.BuildServiceProvider();
var configuration = provider.GetService<IConfiguration>();
builder.Services.Configure<EmailConfiguration>(configuration.GetSection("EmailConfiguration"));
builder.Services.AddIdentity<RegistrationModel, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<HousingProjectContext>();
builder.Services.AddQuartz(q =>
{
    
    var jobkey = new JobKey("Emailjob");
    q.AddJob<Emailjob>(z => z.WithIdentity(jobkey));
    q.AddTrigger(y => y.ForJob(jobkey)
    .WithIdentity("Emailjob-trigger")
    .WithCronSchedule("0/58 * * * * ?"));

 
    var automatedrentpaymentkey = new JobKey("automatedMail");
    q.AddJob<automatedMail>(z => z.WithIdentity(automatedrentpaymentkey));
    q.AddTrigger(y => y.ForJob(automatedrentpaymentkey)
    .WithIdentity("automatedMail-trigger")
    .WithCronSchedule("0 0 12 5 1/1 ? *"));
    //.WithCronSchedule("0 0/5 * 1/1 * ? *"));

    var monthly_rent_Update_key = new JobKey("Monthly_Rent_Update");
    q.AddJob<Monthly_Rent_Update>(z => z.WithIdentity(monthly_rent_Update_key));
    q.AddTrigger(y => y.ForJob(monthly_rent_Update_key)
    .WithIdentity("Monthly_Rent_Update-trigger")
    .WithCronSchedule("0 0 0 1 * ? *"));
    // .WithCronSchedule("0/1 * * * * ?"));

    var send_payment_receipts_key = new JobKey("Payment_Receipt_Job");
    q.AddJob<Payment_Receipt_Job>(z => z.WithIdentity(send_payment_receipts_key));
    q.AddTrigger(y => y.ForJob(send_payment_receipts_key)
    .WithIdentity("Payment_Receipt_Job-trigger")
     //.WithCronSchedule("0 0 0 1 * ? *"));
     .WithCronSchedule("0 0/1 * 1/1 * ? *"));

    //automated rent payday
    var Back_monthly_update_key = new JobKey("Reset_Updated_this_month");
    q.AddJob<Back_monthly_update>(z => z.WithIdentity(Back_monthly_update_key));
    q.AddTrigger(y => y.ForJob(Back_monthly_update_key)
    .WithIdentity("Back_monthly_update-trigger")
     // .WithCronSchedule("0/2 * * * * ?"));
     .WithCronSchedule("0 0/1 * 1/1 * ? *"));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
builder.Services.AddHttpClient("mpesa", m =>
{
    m.BaseAddress =
   new System.Uri("https://sandbox.safaricom.co.ke"

);
});

builder.Services.Configure<FormOptions>(o =>
{
    o.ValueLengthLimit = int.MaxValue;
    o.MultipartBodyLengthLimit = int.MaxValue;
    o.MemoryBufferThreshold = int.MaxValue;
});


builder.Services.AddDbContext<HousingProjectContext>(
               x => x.UseSqlServer(configuration.GetConnectionString("DevConnectiions")));
builder.Services.AddControllers();
// Add services to the container.


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc(
        "v1",
        new OpenApiInfo { Title = "HousingProject.API", Version = "v1" }
    );
    c.AddSecurityDefinition(
      "Bearer",
      new OpenApiSecurityScheme
      {
          In = ParameterLocation.Header,
          Description = "Please Insert token",
          Name = "Authorization",
          Type = SecuritySchemeType.Http,
          BearerFormat = "Jwt",
          Scheme = "bearer"
      }
      );
    c.AddSecurityRequirement(
      new OpenApiSecurityRequirement
     {
            {
            new OpenApiSecurityScheme{
                Reference = new OpenApiReference{Type = ReferenceType.SecurityScheme,Id = "Bearer"}},

            new string[] { }
        }});

});
builder.Services
  .AddAuthentication(opt =>
      {
          opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
          opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
      })
  .AddJwtBearer(opt =>
  {
      opt.RequireHttpsMetadata = true;
      opt.SaveToken = true;
      opt.TokenValidationParameters = new TokenValidationParameters
      {
          ValidateIssuerSigningKey = true,
          IssuerSigningKey = new SymmetricSecurityKey(
              Encoding.ASCII.GetBytes(Constants.JWT_SECURITY_KEY)
          ),
          ValidateIssuer = false,
          ValidateAudience = false
      };
  });
builder.Services.Configure<IdentityOptions>(options =>
options.ClaimsIdentity.UserIdClaimType = ClaimTypes.NameIdentifier
);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRegistrationServices, RegistrationServices>();
builder.Services.AddScoped<IHouse_RegistrationServices, House_RegistrationServices>();
builder.Services.AddScoped<IEmailServices, EmailServices>();
builder.Services.AddScoped<IverificationGenerator, verificationtokenGenerator>();
builder.Services.AddScoped<ILandlordServices, LanlordServices>();
builder.Services.AddScoped<ITenantServices, TenantServices>();

builder.Services.AddScoped<IloggedInServices, UserLoginServices>();
builder.Services.AddScoped<IextraFunctions, AddingCountiesCRUD>();
builder.Services.AddScoped<IImagesServices, ImagesServices>();
builder.Services.AddScoped<IRoles, Roles>();
builder.Services.AddScoped<ILoggedIn, LoggedIn>();
builder.Services.AddScoped<ITenantStatementServices, TenantStatementServices>();
builder.Services.AddScoped<IHouseUnits, HouseUnitsServices>();
builder.Services.AddScoped<IProfessionalsServices, ProfessionalServices>();
builder.Services.AddScoped<IGenerateIdService, GenerateIdService>();
builder.Services.AddScoped<ICheckroles, CheckRoles>();
builder.Services.AddScoped<IAdminServices, AdminService>();
builder.Services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();
builder.Services.AddSingleton<IUrlHelperFactory, UrlHelperFactory>();
builder.Services.AddScoped<In_ImagesServices, n_images_services>();
builder.Services.AddScoped<IDarajaServices, Daraja_Services>();
builder.Services.AddScoped<IpaymentServices, PaymentServices>();
builder.Services.AddScoped<IUserExtraServices, UserExtraServices>();
builder.Services.AddCors();
builder.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddScoped<IUrlHelper>(x =>
    {
        var actionContext = x.GetRequiredService<IActionContextAccessor>().ActionContext;
        var factory = x.GetRequiredService<IUrlHelperFactory>();
        return factory.GetUrlHelper(actionContext);
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
    {
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(
        c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "HousingProject.API v1")
    );
}
app.UseStaticFiles();
app.UseRouting();
app.UseCors(builder =>
{
    builder.AllowAnyOrigin()
           .AllowAnyMethod()
           .AllowAnyHeader();
});


app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

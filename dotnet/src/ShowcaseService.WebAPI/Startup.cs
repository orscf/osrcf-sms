using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MedicalResearch.StudyManagement.Persistence.EF;
using System;
using Microsoft.AspNetCore.Mvc.Formatters;
using System.Threading.Tasks;
using MedicalResearch.StudyManagement.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using System.IO;
using MedicalResearch.StudyManagement.StoreAccess;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Writers;
using MedicalResearch.StudyManagement.Model;
using System.Data.Fuse;
using System.Data.Fuse.Convenience;
using System.Web.UJMW;

namespace MedicalResearch.StudyManagement {

  public class Startup {

    public Startup(IConfiguration configuration) {
      _Configuration = configuration;
      StudyManagementDbContext.ConnectionString = configuration.GetValue<string>("SqlConnectionString");
    }

    private static IConfiguration _Configuration = null;
    public static IConfiguration Configuration { get { return _Configuration; } }

    const string _ApiTitle = "ORSCF StudyManagement";
    Version _ApiVersion = null;

    public void ConfigureServices(IServiceCollection services) {

      services.AddLogging();

      _ApiVersion = typeof(Site).Assembly.GetName().Version;

      //StudyManagementDbContext.Migrate();

      string outDir = AppDomain.CurrentDomain.BaseDirectory;

      services.AddSingleton<IInstituteStore>(new InstituteStore());
      services.AddSingleton<IResearchStudyStore>(new ResearchStudyStore());
      services.AddSingleton<ISiteStore>(new SiteStore());

      services.AddSingleton<ISystemEndpointStore>(new SystemEndpointStore());
      services.AddSingleton<ISystemConnectionStore>(new SystemConnectionStore());

      services.AddSingleton<IInvolvedPersonStore>(new InvolvedPersonStore());
      services.AddSingleton<IInvolvementRoleStore>(new InvolvementRoleStore());

      //services.AddSingleton<IInstituteRelatedSystemAssignmentStore>(new InstituteRelatedSystemAssignmentStore());
      //services.AddSingleton<IStudyRelatedSystemAssignmentStore>(new StudyRelatedSystemAssignmentStore());
      //services.AddSingleton<ISiteRelatedSystemAssignmentStore>(new SiteRelatedSystemAssignmentStore());

      services.AddDynamicUjmwControllers(
        (c) => {

          var opt = new DynamicUjmwControllerOptions() {
            ControllerRoute = "sms/v2/store/[Controller]"
          };

          //c.AddControllerFor<IInstituteStore>(opt);
          c.AddControllerFor<IResearchStudyStore>(opt);
          //c.AddControllerFor<ISiteStore>(opt);

          //c.AddControllerFor<ISystemEndpointStore>(opt);
          //c.AddControllerFor<ISystemConnectionStore>(opt);

          //c.AddControllerFor<IInvolvedPersonStore>(opt);
          //c.AddControllerFor<IInvolvementRoleStore>(opt);

          //c.AddControllerFor<IInstituteRelatedSystemAssignmentStore>(opt);
          //c.AddControllerFor<IStudyRelatedSystemAssignmentStore>(opt);
          //c.AddControllerFor<ISiteRelatedSystemAssignmentStore>(opt);
        }
      );

      services.AddSwaggerGen(c => {

        c.EnableAnnotations(true, true);

        c.IncludeXmlComments(outDir + "Hl7.Fhir.R4.Core.xml", true);
        c.IncludeXmlComments(outDir + "ORSCF.StudyManagement.Contract.xml", true);
        c.IncludeXmlComments(outDir + "ORSCF.StudyManagement.Service.xml", true);
        c.IncludeXmlComments(outDir + "ORSCF.StudyManagement.Service.WebAPI.xml", true);

        #region bearer

        //https://www.thecodebuzz.com/jwt-authorization-token-swagger-open-api-asp-net-core-3-0/
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
          Name = "Authorization",
          Type = SecuritySchemeType.ApiKey,
          Scheme = "Bearer",
          BearerFormat = "JWT",
          In = ParameterLocation.Header,
          Description = "JWT Authorization header using the Bearer scheme."
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
          {
              {
                    new OpenApiSecurityScheme
                      {
                          Reference = new OpenApiReference
                          {
                              Type = ReferenceType.SecurityScheme,
                              Id = "Bearer"
                          }
                      },
                      new string[] {}

              }
          });

        #endregion

        c.UseInlineDefinitionsForEnums();

        //c.SwaggerDoc(
        //  "StoreAccessV1",
        //  new OpenApiInfo {
        //    Title = _ApiTitle + "-StoreAccess",
        //    Version = _ApiVersion.ToString(3),
        //    Description = "NOTE: This is not intended be a 'RESTful' api, as it is NOT located on the persistence layer and is therefore NOT focused on doing CRUD operations! This HTTP-based API uses a 'call-based' approach to known BL operations. IN-, OUT- and return-arguments are transmitted using request-/response- wrappers (see [UJMW](https://github.com/KornSW/UnifiedJsonMessageWrapper)), which are very lightweight and are a compromise for broad support and adaptability in REST-inspired technologies as well as soap-inspired technologies!",
        //    Contact = new OpenApiContact {
        //      Name = "Open Research Study Communication Format",
        //      Email = "info@orscf.org",
        //      Url = new Uri("https://orscf.org")
        //    }
        //  }
        //);

        c.SwaggerDoc(
          "ApiV1",
          new OpenApiInfo {
            Title = _ApiTitle + "-API",
            Version = _ApiVersion.ToString(3),
            Description = "NOTE: This is not intended be a 'RESTful' api, as it is NOT located on the persistence layer and is therefore NOT focused on doing CRUD operations! This HTTP-based API uses a 'call-based' approach to known BL operations. IN-, OUT- and return-arguments are transmitted using request-/response- wrappers (see [UJMW](https://github.com/KornSW/UnifiedJsonMessageWrapper)), which are very lightweight and are a compromise for broad support and adaptability in REST-inspired technologies as well as soap-inspired technologies!",
            Contact = new OpenApiContact {
              Name = "Open Research Study Communication Format",
              Email = "info@orscf.org",
              Url = new Uri("https://orscf.org")
            },
          }
        );

      });

    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerfactory) {

      var logFileFullName = _Configuration.GetValue<string>("LogFileName");
      var logDir = Path.GetFullPath(Path.GetDirectoryName(logFileFullName));
      Directory.CreateDirectory(logDir);
      loggerfactory.AddFile(logFileFullName);

      //required for the www-root
      app.UseStaticFiles();

      if (!_Configuration.GetValue<bool>("ProdMode")) {
        app.UseDeveloperExceptionPage();
      }

      if (_Configuration.GetValue<bool>("EnableSwaggerUi")) {
        var baseUrl = _Configuration.GetValue<string>("BaseUrl");

        app.UseSwagger(o => {
          //warning: needs subfolder! jsons cant be within same dir as swaggerui (below)
          o.RouteTemplate = "docs/schema/{documentName}.{json|yaml}";
          //o.SerializeAsV2 = true;
        });

        app.UseSwaggerUI(c => {

          c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
          c.DefaultModelExpandDepth(2);
          c.DefaultModelsExpandDepth(2);
          //c.ConfigObject.DefaultModelExpandDepth = 2;

          c.DocumentTitle = _ApiTitle + " - OpenAPI Definition(s)";

          //represents the sorting in SwaggerUI combo-box
          c.SwaggerEndpoint("schema/ApiV1.json", _ApiTitle + "-API v" + _ApiVersion.ToString(3));
          //c.SwaggerEndpoint("schema/StoreAccessV1.json", _ApiTitle + "-StoreAccess v" + _ApiVersion.ToString(3));

          c.RoutePrefix = "docs";

          //requires MVC app.UseStaticFiles();
          c.InjectStylesheet(baseUrl + "swagger-ui/custom.css");

        });

      }

      app.UseHttpsRedirection();

      app.UseRouting();

      app.UseAuthorization();

      app.UseEndpoints(endpoints => {
        endpoints.MapControllers();
      });

    }
  }

}

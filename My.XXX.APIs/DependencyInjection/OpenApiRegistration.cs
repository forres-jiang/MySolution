using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace My.XXX.APIs.DependencyInjection;

public static class OpenApiRegistration
{
    public static void AddApiDocumentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSwaggerGen(c =>
        {
            var doc = new OpenApiInfo { Title = "My XXX API", Version = "v1" };
            c.SwaggerDoc("v1", doc);

            c.AddSecurityDefinition("bearerAuth", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme."
            });

            c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("bearerAuth", document),
                    new List<string>()
                }
            });

            // 反射获取xml注释文件
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

            if (File.Exists(xmlPath))
            {
                // 启用xml注释. 该方法第二个参数启用控制器的注释，默认为false. 需要关闭1591警告
                c.IncludeXmlComments(xmlPath, true);
            }

            var serviceXml = Path.Combine(AppContext.BaseDirectory, "My.XXX.Application.xml");
            if (File.Exists(serviceXml))
            {
                c.IncludeXmlComments(serviceXml, true);
            }
        });
    }
}

using My.XXX.APIs.Contracts.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Linq;

namespace My.XXX.APIs.DependencyInjection;

public static class HttpContractRegistration
{
    public static void AddHttpContracts(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        services
            .AddControllers()
            .AddNewtonsoftJson(option =>
            {
                // 接口按服务器本地时区输出时间；部署时应统一服务器时区，内部存储仍使用 UTC。
                option.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Local;
                // 保留现有无时区日期格式，客户端按服务器本地时区解释返回值。
                option.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss.fff";
                //忽略循环引用
                option.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            });

        //重写ModelState 返回类型
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = actionContext =>
            {
                //获取验证失败的模型字段
                var errors = actionContext.ModelState
                .SelectMany(entry => entry.Value?.Errors.Select(error => error.ErrorMessage) ?? Enumerable.Empty<string>())
                .ToList();
                //设置返回内容,根据实际情况可以调整返回httpstatus200或400
                var message = string.Join("|", errors);
                return new BadRequestObjectResult(new APIResponse<object>(0, message, null,
                    "Request.ValidationFailed", actionContext.HttpContext.TraceIdentifier));
            };
        });

    }
}

using System.Net;

namespace Lumen.App.WebAPI.Middlewares {
    public class ApiKeyMiddleware {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private const string ApiKeyHeaderName = "X-Api-Key";

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration) {
            _next = next;
            _configuration = configuration;
        }
        public async Task InvokeAsync(HttpContext context) {
            if (context.Request.Path != "/openapi/v1.json" && context.Request.Path != "/swagger/index.html") {
                if (string.IsNullOrWhiteSpace(context.Request.Headers[ApiKeyHeaderName])) {
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return;
                }
                string? userApiKey = context.Request.Headers[ApiKeyHeaderName];
                if (_configuration.GetValue<string>("ApiKey") != userApiKey!) {
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return;
                }
            }
            await _next(context);
        }
    }
}

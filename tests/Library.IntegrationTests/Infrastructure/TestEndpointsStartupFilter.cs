using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Library.IntegrationTests.Infrastructure;

internal sealed class TestEndpointsStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.UseRouting();
        app.UseEndpoints(TestEndpoints.Map);
    };
}

using Amazon.CDK;
using Microsoft.Extensions.DependencyInjection;

namespace PlatformGenerator;


public interface IServiceConfiguration<out TService, TImplementation> where TImplementation : TService, new() 
{
    void ConfigureServices(ServiceCollection serviceCollection);
    
    Stack? Infrastructure { get; }

    Task CreateInfrastructure();

    Task DeleteInfrastructure();

    TService ServiceInstance { get; }


}


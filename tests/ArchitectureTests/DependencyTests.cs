using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// Architecture tests enforcing Vertical Slice Architecture and DDD dependency boundaries.
/// Run these as part of CI to prevent accidental layer violations.
/// </summary>
public sealed class DependencyTests
{
    private const string ProductCoreAssembly = "Product.Core";
    private const string IdentityCoreAssembly = "Identity.Core";
    private const string SharedKernelAssembly = "NovaStack.SharedKernel";
    private const string InfrastructureBBAssembly = "NovaStack.Infrastructure";

    [Fact]
    public void SharedKernel_Should_Not_DependOn_Any_Service()
    {
        var result = Types.InAssembly(typeof(NovaStack.SharedKernel.Results.Error).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ProductCoreAssembly, IdentityCoreAssembly)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "SharedKernel must not depend on any service-specific assembly.");
    }

    [Fact]
    public void Product_CommandHandlers_Should_BeInternal_And_Sealed()
    {
        var result = Types.InAssembly(typeof(Product.Core.Features.Products.CreateProduct.CreateProductCommand).Assembly)
            .That()
            .HaveNameEndingWith("CommandHandler")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All product command handlers should be sealed to prevent inheritance.");
    }

    [Fact]
    public void Product_QueryHandlers_Should_BeInternal_And_Sealed()
    {
        var result = Types.InAssembly(typeof(Product.Core.Features.Products.CreateProduct.CreateProductCommand).Assembly)
            .That()
            .HaveNameEndingWith("QueryHandler")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All product query handlers should be sealed to prevent inheritance.");
    }

    [Fact]
    public void Identity_CommandHandlers_Should_BeInternal_And_Sealed()
    {
        var result = Types.InAssembly(typeof(Identity.Core.Features.Auth.Login.LoginCommand).Assembly)
            .That()
            .HaveNameEndingWith("CommandHandler")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All identity command handlers should be sealed to prevent inheritance.");
    }

    [Fact]
    public void Identity_QueryHandlers_Should_BeInternal_And_Sealed()
    {
        var result = Types.InAssembly(typeof(Identity.Core.Features.Auth.Login.LoginCommand).Assembly)
            .That()
            .HaveNameEndingWith("QueryHandler")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All identity query handlers should be sealed to prevent inheritance.");
    }
}

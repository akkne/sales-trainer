namespace Sellevate.BuildingBlocks.Tenancy;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class TenantScopedAttribute : Attribute
{
}

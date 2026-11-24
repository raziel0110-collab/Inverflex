using Inverflex.Model;
using Inverflex.Services;

namespace Inverflex.Tests;

public sealed class OrderApprovalPolicyTests
{
    [Fact]
    public void HighValuePendingOrderCanBeApprovedByAdvisor()
    {
        var decision = OrderApprovalPolicy.Evaluate(50_001m, OrderStatus.PendienteAprobacion, UserRole.Asesor);
        Assert.True(decision.RequiresApproval);
        Assert.True(decision.CanApprove);
        Assert.Null(decision.Error);
    }

    [Fact]
    public void OrderAtThresholdRequiresAdvisorApproval()
    {
        var decision = OrderApprovalPolicy.Evaluate(50_000m, OrderStatus.PendienteAprobacion, UserRole.Asesor);
        Assert.True(decision.RequiresApproval);
        Assert.True(decision.CanApprove);
        Assert.Null(decision.Error);
    }

    [Fact]
    public void OrderBelowThresholdDoesNotRequireApproval()
    {
        var decision = OrderApprovalPolicy.Evaluate(49_999.99m, OrderStatus.PendienteAprobacion, UserRole.Asesor);
        Assert.False(decision.RequiresApproval);
        Assert.False(decision.CanApprove);
        Assert.Equal("La orden no requiere aprobación de asesor.", decision.Error);
    }

    [Fact]
    public void ClientCannotApproveHighValueOrder()
    {
        var decision = OrderApprovalPolicy.Evaluate(60_000m, OrderStatus.PendienteAprobacion, UserRole.Cliente);
        Assert.True(decision.RequiresApproval);
        Assert.False(decision.CanApprove);
    }

    [Fact]
    public void CompletedOrderCannotBeApprovedAgain()
    {
        var decision = OrderApprovalPolicy.Evaluate(60_000m, OrderStatus.Ejecutada, UserRole.Asesor);
        Assert.False(decision.CanApprove);
        Assert.Equal("La orden no está pendiente de aprobación.", decision.Error);
    }

    [Fact]
    public void NonPositiveAmountIsRejected()
    {
        var decision = OrderApprovalPolicy.Evaluate(0m, OrderStatus.PendienteAprobacion, UserRole.Asesor);
        Assert.False(decision.CanApprove);
        Assert.Equal("El monto debe ser positivo.", decision.Error);
    }

    [Fact]
    public void AdminCanApproveHighValuePendingOrder()
    {
        var decision = OrderApprovalPolicy.Evaluate(75_000m, OrderStatus.PendienteAprobacion, UserRole.Admin);
        Assert.True(decision.CanApprove);
    }
}

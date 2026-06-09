using System;
using FluentAssertions;
using MyList.Services;
using Xunit;

namespace MyList.Tests.Services;

public class OrbitLayoutServiceTests
{
    [Fact]
    public void Compute_SingleNode_AtTwelveOClock()
    {
        var points = OrbitLayoutService.Compute(1, 100, 100, 50, 50);
        points.Should().HaveCount(1);
        points[0].X.Should().BeApproximately(100, 0.001);
        points[0].Y.Should().BeApproximately(50, 0.001);
    }

    [Fact]
    public void Compute_FourNodes_FormCardinalDirections()
    {
        var points = OrbitLayoutService.Compute(4, 0, 0, 10, 10);
        points[0].X.Should().BeApproximately(0, 0.001);
        points[0].Y.Should().BeApproximately(-10, 0.001);
        points[1].X.Should().BeApproximately(10, 0.001);
        points[1].Y.Should().BeApproximately(0, 0.001);
        points[2].X.Should().BeApproximately(0, 0.001);
        points[2].Y.Should().BeApproximately(10, 0.001);
        points[3].X.Should().BeApproximately(-10, 0.001);
        points[3].Y.Should().BeApproximately(0, 0.001);
    }

    [Fact]
    public void Compute_EllipseRadii_HonorsXYSeparately()
    {
        var points = OrbitLayoutService.Compute(4, 0, 0, 20, 5);
        points[1].X.Should().BeApproximately(20, 0.001);
        points[1].Y.Should().BeApproximately(0, 0.001);
        points[2].X.Should().BeApproximately(0, 0.001);
        points[2].Y.Should().BeApproximately(5, 0.001);
    }

    [Fact]
    public void Compute_ZeroCount_ReturnsSinglePoint()
    {
        var points = OrbitLayoutService.Compute(0, 0, 0, 10, 10);
        points.Should().HaveCount(1);
    }

    [Fact]
    public void ComputePoint_DeterministicAcrossCalls()
    {
        var first = OrbitLayoutService.ComputePoint(2, 8, 50, 50, 30, 30);
        var second = OrbitLayoutService.ComputePoint(2, 8, 50, 50, 30, 30);
        first.Should().Be(second);
    }

    [Fact]
    public void Compute_PointsMatchComputePoint()
    {
        var fleet = OrbitLayoutService.Compute(6, 0, 0, 10, 10);
        for (var i = 0; i < 6; i++)
        {
            var single = OrbitLayoutService.ComputePoint(i, 6, 0, 0, 10, 10);
            fleet[i].X.Should().BeApproximately(single.X, 0.001);
            fleet[i].Y.Should().BeApproximately(single.Y, 0.001);
        }
    }
}

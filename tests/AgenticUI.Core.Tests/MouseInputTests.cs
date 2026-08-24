using System.Text.Json;
using AgenticUI;
using Xunit;

namespace AgenticUI.Core.Tests;

public sealed class MouseInputTests
{
    [Fact]
    public void ClickDefaultsToControlCenterAndLeftButton()
    {
        var input = AgenticMouseInputParser.Parse(new AgenticCommand
        {
            Action = AgenticActions.MouseClick
        });

        Assert.Equal(0.5, input.Start.XRatio);
        Assert.Equal(0.5, input.Start.YRatio);
        Assert.Equal(AgenticMouseButton.Left, input.Button);
    }

    [Fact]
    public void ParserAcceptsJsonProtocolValues()
    {
        using var document = JsonDocument.Parse(
            "{\"xRatio\":0.25,\"yRatio\":0.75,\"button\":\"right\"}");
        var command = new AgenticCommand { Action = AgenticActions.MouseDoubleClick };
        foreach (var property in document.RootElement.EnumerateObject())
        {
            command.Arguments[property.Name] = property.Value.Clone();
        }

        var input = AgenticMouseInputParser.Parse(command);

        Assert.Equal(0.25, input.Start.XRatio);
        Assert.Equal(0.75, input.Start.YRatio);
        Assert.Equal(AgenticMouseButton.Right, input.Button);
    }

    [Fact]
    public void DragRequiresBothEndpointsAndBoundsSteps()
    {
        var command = new AgenticCommand
        {
            Action = AgenticActions.MouseDrag,
            Arguments =
            {
                ["startXRatio"] = 0.1,
                ["startYRatio"] = 0.2,
                ["endXRatio"] = 0.8,
                ["endYRatio"] = 0.9,
                ["steps"] = 20
            }
        };

        var input = AgenticMouseInputParser.Parse(command);

        Assert.Equal(0.1, input.Start.XRatio);
        Assert.Equal(0.9, input.End.YRatio);
        Assert.Equal(20, input.Steps);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void CoordinatesOutsideControlAreRejected(double ratio)
    {
        var command = new AgenticCommand
        {
            Action = AgenticActions.MouseMove,
            Arguments = { ["xRatio"] = ratio }
        };

        Assert.Throws<ArgumentException>(() => AgenticMouseInputParser.Parse(command));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1201)]
    [InlineData(-1201)]
    public void UnsafeWheelDeltaIsRejected(int delta)
    {
        var command = new AgenticCommand
        {
            Action = AgenticActions.MouseWheel,
            Arguments = { ["delta"] = delta }
        };

        Assert.Throws<ArgumentException>(() => AgenticMouseInputParser.Parse(command));
    }
}

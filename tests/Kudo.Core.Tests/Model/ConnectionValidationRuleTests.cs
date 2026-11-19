using Kudo;
using Shouldly;

namespace Kudo.Core.Tests;

public sealed class ConnectionValidationRuleTests
{
    [Fact]
    public void Additional_rule_can_reject_a_structurally_valid_connection()
    {
        var document = CreateDocument();
        var rule = new StubRule(ConnectionValidationResult.Invalid("宿主拒绝", "HOST1001"));

        var validation = document.ValidateConnection(Source, Target, additionalRule: rule);

        validation.IsValid.ShouldBeFalse();
        validation.Message.ShouldBe("宿主拒绝");
        validation.Code.ShouldBe("HOST1001");
        rule.CallCount.ShouldBe(1);
    }

    [Fact]
    public void TryConnect_applies_the_additional_rule_before_mutating_the_document()
    {
        var document = CreateDocument();
        var rule = new StubRule(ConnectionValidationResult.Invalid("宿主拒绝"));

        var connected = document.TryConnect(
            Source,
            Target,
            out var edge,
            out var validation,
            additionalRule: rule);

        connected.ShouldBeFalse();
        validation.IsValid.ShouldBeFalse();
        edge.ShouldBeNull();
        document.Edges.ShouldBeEmpty();
        rule.CallCount.ShouldBe(1);
    }

    [Fact]
    public void Default_rule_cannot_be_bypassed_by_an_additional_rule()
    {
        var document = CreateDocument();
        var rule = new StubRule(ConnectionValidationResult.Valid);

        var validation = document.ValidateConnection(Target, Source, additionalRule: rule);

        validation.IsValid.ShouldBeFalse();
        validation.Code.ShouldBe(GraphValidationCodes.IncompatiblePortKinds);
        rule.CallCount.ShouldBe(0);
    }

    [Fact]
    public void Composite_rule_stops_after_the_first_rejection()
    {
        var document = CreateDocument();
        var first = new StubRule(ConnectionValidationResult.Valid);
        var rejected = new StubRule(ConnectionValidationResult.Invalid("拒绝"));
        var skipped = new StubRule(ConnectionValidationResult.Valid);
        var composite = new CompositeConnectionValidationRule(first, rejected, skipped);
        var request = new ConnectionValidationRequest(document, Source, Target);

        var validation = composite.Validate(in request);

        validation.IsValid.ShouldBeFalse();
        first.CallCount.ShouldBe(1);
        rejected.CallCount.ShouldBe(1);
        skipped.CallCount.ShouldBe(0);
    }

    [Fact]
    public void Default_rule_uses_the_supplied_text_provider()
    {
        var document = CreateDocument();
        var provider = new FixedTextProvider("本地化校验提示");

        var validation = document.ValidateConnection(
            Target,
            Source,
            textProvider: provider);

        validation.Message.ShouldBe("本地化校验提示");
        provider.LastKey.ShouldBe(GraphValidationTextKey.ConnectionPortKindsIncompatible);
    }

    private static readonly PortReference Source = new("source", "out");
    private static readonly PortReference Target = new("target", "in");

    private static GraphDocument CreateDocument() => new()
    {
        Nodes =
        [
            new NodeModel
            {
                Id = "source",
                Ports =
                [
                    new PortModel
                    {
                        Id = "out",
                        Kind = PortKind.Output,
                        Capacity = new PortCapacity { MaxOutgoing = null }
                    }
                ]
            },
            new NodeModel
            {
                Id = "target",
                Ports =
                [
                    new PortModel
                    {
                        Id = "in",
                        Kind = PortKind.Input,
                        Capacity = new PortCapacity { MaxIncoming = null }
                    }
                ]
            }
        ]
    };

    private sealed class StubRule(ConnectionValidationResult result) : IConnectionValidationRule
    {
        public int CallCount { get; private set; }

        public ConnectionValidationResult Validate(in ConnectionValidationRequest request)
        {
            CallCount++;
            return result;
        }
    }

    private sealed class FixedTextProvider(string text) : IGraphValidationTextProvider
    {
        public GraphValidationTextKey? LastKey { get; private set; }

        public string Get(GraphValidationTextKey key)
        {
            LastKey = key;
            return text;
        }
    }
}

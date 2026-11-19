using System.Windows.Input;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    internal static IGraphValidationTextProvider ConnectionValidationTextProvider { get; } =
        new LocalizedConnectionValidationTextProvider();

    public EdgeRoutingStyle NewEdgeRoutingStyle
    {
        get => GetValue(NewEdgeRoutingStyleProperty);
        set => SetValue(NewEdgeRoutingStyleProperty, value);
    }

    public ICommand? ConnectionValidationChangedCommand
    {
        get => GetValue(ConnectionValidationChangedCommandProperty);
        set => SetValue(ConnectionValidationChangedCommandProperty, value);
    }

    public IConnectionValidationRule? ConnectionValidationRule
    {
        get => GetValue(ConnectionValidationRuleProperty);
        set => SetValue(ConnectionValidationRuleProperty, value);
    }

    public PortHitResult? ConnectionCandidatePort => GetConnectionCandidatePort();

    public GraphEdgeRoutingService EdgeRoutingService
    {
        get => _edgeRoutingService;
        set
        {
            _edgeRoutingService = value ?? throw new ArgumentNullException(nameof(value));
            OnEdgeRoutingConfigurationChanged();
        }
    }

    public GraphEdgeRoutingOptions EdgeRoutingOptions
    {
        get => _edgeRoutingOptions;
        set
        {
            _edgeRoutingOptions = value ?? throw new ArgumentNullException(nameof(value));
            OnEdgeRoutingConfigurationChanged();
        }
    }

    public IGraphEdgeObstacleProvider EdgeObstacleProvider
    {
        get => _edgeObstacleProvider;
        set
        {
            _edgeObstacleProvider = value ?? throw new ArgumentNullException(nameof(value));
            OnEdgeRoutingConfigurationChanged();
        }
    }

    public bool IsEdgeRoutingCacheEnabled
    {
        get => _isEdgeRoutingCacheEnabled;
        set
        {
            if (_isEdgeRoutingCacheEnabled == value)
            {
                return;
            }

            _isEdgeRoutingCacheEnabled = value;
            InvalidateEdgeRoutingCache();
        }
    }

    public double ConnectionSnapDistance
    {
        get => _connectionSnapDistance;
        set => _connectionSnapDistance = double.IsFinite(value) && value >= 0
            ? value
            : DefaultConnectionSnapDistance;
    }

    public ConnectionValidationResult? LastConnectionValidation
    {
        get => GetValue(LastConnectionValidationProperty);
        internal set => SetValue(LastConnectionValidationProperty, value);
    }

    public ConnectionValidationResult ValidateConnection(
        PortReference source,
        PortReference target,
        GraphRelationKind relationKind = GraphRelationKind.Connection,
        string? ignoredEdgeId = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        return Document is { } document
            ? document.ValidateConnection(
                source,
                target,
                relationKind,
                ConnectionValidationRule,
                ignoredEdgeId,
                ConnectionValidationTextProvider)
            : ConnectionValidationResult.Invalid(
                GraphValidationText.Get(
                    ConnectionValidationTextProvider,
                    GraphValidationTextKey.MissingConnectionEndpoint),
                GraphValidationCodes.MissingEdgeEndpoint);
    }
}

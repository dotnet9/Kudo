using Kudo;
using Kudo.Core.Algorithms;
using Shouldly;

namespace Kudo.Core.Tests.Algorithms;

public class GraphAlgorithmTests
{
    [Fact]
    public void Graph_traversal_finds_upstream_downstream_and_connected_nodes_in_document_order()
    {
        var document = new GraphDocument();
        foreach (var id in new[] { "a", "b", "c", "d", "isolated" })
        {
            document.AddNode(new NodeModel { Id = id });
        }

        document.AddEdge(new EdgeModel
        {
            Source = new PortReference("a", "out"),
            Target = new PortReference("b", "in")
        });
        document.AddEdge(new EdgeModel
        {
            Source = new PortReference("b", "out"),
            Target = new PortReference("c", "in")
        });
        document.AddEdge(new EdgeModel
        {
            Source = new PortReference("a", "out"),
            Target = new PortReference("d", "in")
        });

        GraphTraversal.GetReachableNodeIds(document, ["b"], GraphTraversalDirection.Upstream)
            .ShouldBe(["a", "b"]);
        GraphTraversal.GetReachableNodeIds(document, ["b"], GraphTraversalDirection.Downstream)
            .ShouldBe(["b", "c"]);
        GraphTraversal.GetReachableNodeIds(document, ["b"], GraphTraversalDirection.Connected)
            .ShouldBe(["a", "b", "c", "d"]);
        GraphTraversal.GetReachableNodeIds(
                document,
                ["b"],
                GraphTraversalDirection.Connected,
                includeStart: false)
            .ShouldBe(["a", "c", "d"]);
    }

    [Fact]
    public void Graph_traversal_finds_only_one_hop_adjacent_nodes()
    {
        var document = new GraphDocument();
        foreach (var id in new[] { "a", "b", "c", "d" })
        {
            document.AddNode(new NodeModel { Id = id });
        }

        document.AddEdge(new EdgeModel
        {
            Source = new PortReference("a", "out"),
            Target = new PortReference("b", "in")
        });
        document.AddEdge(new EdgeModel
        {
            Source = new PortReference("b", "out"),
            Target = new PortReference("c", "in")
        });
        document.AddEdge(new EdgeModel
        {
            Source = new PortReference("d", "out"),
            Target = new PortReference("b", "in")
        });

        GraphTraversal.GetAdjacentNodeIds(document, ["b"])
            .ShouldBe(["a", "b", "c", "d"]);
        GraphTraversal.GetAdjacentNodeIds(
                document,
                ["b"],
                GraphTraversalDirection.Downstream,
                includeStart: false)
            .ShouldBe(["c"]);
    }

    [Fact]
    public void Graph_traversal_finds_shortest_path_with_direction_control()
    {
        var document = new GraphDocument();
        foreach (var id in new[] { "a", "b", "c", "d" })
        {
            document.AddNode(new NodeModel { Id = id });
        }

        document.AddEdge(new EdgeModel
        {
            Source = new PortReference("a", "out"),
            Target = new PortReference("b", "in")
        });
        document.AddEdge(new EdgeModel
        {
            Source = new PortReference("b", "out"),
            Target = new PortReference("c", "in")
        });
        document.AddEdge(new EdgeModel
        {
            Source = new PortReference("a", "out"),
            Target = new PortReference("d", "in")
        });
        document.AddEdge(new EdgeModel
        {
            Source = new PortReference("d", "out"),
            Target = new PortReference("c", "in")
        });

        GraphTraversal.GetShortestPathNodeIds(document, "a", "c", GraphTraversalDirection.Downstream)
            .ShouldBe(["a", "b", "c"]);
        GraphTraversal.GetShortestPathNodeIds(document, "c", "a", GraphTraversalDirection.Downstream)
            .ShouldBeEmpty();
        GraphTraversal.GetShortestPathNodeIds(document, "c", "a", GraphTraversalDirection.Connected)
            .ShouldBe(["c", "b", "a"]);
    }

    [Fact]
    public void Cycle_detector_returns_first_directed_cycle_without_downstream_nodes()
    {
        var document = new GraphDocument();
        foreach (var id in new[] { "a", "b", "c", "downstream" })
        {
            document.AddNode(new NodeModel { Id = id });
        }

        foreach (var (source, target) in new[]
                 {
                     ("a", "b"),
                     ("b", "c"),
                     ("c", "a"),
                     ("c", "downstream")
                 })
        {
            document.AddEdge(new EdgeModel
            {
                Source = new PortReference(source, "out"),
                Target = new PortReference(target, "in")
            });
        }

        CycleDetector.FindCycleNodeIds(document).ShouldBe(["a", "b", "c"]);

        document.Edges.Clear();
        document.AddEdge(new EdgeModel
        {
            Source = new PortReference("b", "out"),
            Target = new PortReference("b", "in")
        });
        CycleDetector.FindCycleNodeIds(document).ShouldBe(["b"]);
    }

    [Fact]
    public void Structure_queries_distinguish_sources_sinks_and_isolated_nodes()
    {
        var document = new GraphDocument();
        foreach (var id in new[] { "source-a", "source-b", "merge", "sink", "isolated" })
        {
            document.AddNode(new NodeModel { Id = id });
        }

        foreach (var source in new[] { "source-a", "source-b" })
        {
            document.AddEdge(new EdgeModel
            {
                Source = new PortReference(source, "out"),
                Target = new PortReference("merge", "in")
            });
        }

        document.AddEdge(new EdgeModel
        {
            Source = new PortReference("merge", "out"),
            Target = new PortReference("sink", "in")
        });

        GraphStructureQueries.GetNodeIds(document, GraphNodeRole.Source)
            .ShouldBe(["source-a", "source-b"]);
        GraphStructureQueries.GetNodeIds(document, GraphNodeRole.Sink)
            .ShouldBe(["sink"]);
        GraphStructureQueries.GetNodeIds(document, GraphNodeRole.Isolated)
            .ShouldBe(["isolated"]);
    }

    [Fact]
    public void Graph_traversal_finds_longest_directed_path_and_rejects_cycles()
    {
        var document = new GraphDocument();
        foreach (var id in new[] { "source-a", "source-b", "merge", "inspect", "sink" })
        {
            document.AddNode(new NodeModel { Id = id });
        }

        foreach (var (source, target) in new[]
                 {
                     ("source-a", "merge"),
                     ("source-b", "merge"),
                     ("merge", "inspect"),
                     ("inspect", "sink"),
                     ("merge", "sink")
                 })
        {
            document.AddEdge(new EdgeModel
            {
                Source = new PortReference(source, "out"),
                Target = new PortReference(target, "in")
            });
        }

        GraphTraversal.GetLongestDirectedPathNodeIds(document)
            .ShouldBe(["source-a", "merge", "inspect", "sink"]);

        document.AddEdge(new EdgeModel
        {
            Source = new PortReference("sink", "out"),
            Target = new PortReference("merge", "in")
        });
        GraphTraversal.GetLongestDirectedPathNodeIds(document).ShouldBeEmpty();
    }

    [Fact]
    public void Edge_path_copies_source_points()
    {
        var source = new[] { GraphPoint.Zero, new GraphPoint(10, 20) };
        var path = new GraphEdgePath(EdgeRoutingStyle.Direct, source);

        source[1] = GraphPoint.Zero;

        path.Points.ShouldBe([GraphPoint.Zero, new GraphPoint(10, 20)]);
    }

    [Fact]
    public void Edge_routing_service_dispatches_to_registered_strategy()
    {
        GraphEdgeRoutingRequest? captured = null;
        var service = new GraphEdgeRoutingService([
            new StubEdgeRouter(EdgeRoutingStyle.Direct, request =>
            {
                captured = request;
                return new GraphEdgePath(request.RoutingStyle, [
                    request.Start,
                    new GraphPoint(50, 75),
                    request.End
                ]);
            })
        ]);
        var request = new GraphEdgeRoutingRequest(
            EdgeRoutingStyle.Direct,
            new GraphPoint(10, 20),
            new GraphPoint(90, 100));

        var path = service.Route(request);

        captured.ShouldBeSameAs(request);
        path.Points.ShouldBe([
            new GraphPoint(10, 20),
            new GraphPoint(50, 75),
            new GraphPoint(90, 100)
        ]);
    }

    [Fact]
    public void Edge_routing_service_replaces_one_strategy_without_losing_defaults()
    {
        var service = GraphEdgeRoutingService.Default.WithRouter(
            new StubEdgeRouter(EdgeRoutingStyle.Direct, request => new GraphEdgePath(
                request.RoutingStyle,
                [request.Start, new GraphPoint(25, 30), request.End])));

        var direct = service.Route(new GraphEdgeRoutingRequest(
            EdgeRoutingStyle.Direct,
            GraphPoint.Zero,
            new GraphPoint(50, 50)));
        var bezier = service.Route(new GraphEdgeRoutingRequest(
            EdgeRoutingStyle.Bezier,
            GraphPoint.Zero,
            new GraphPoint(50, 50)));

        direct.Points.ShouldBe([
            GraphPoint.Zero,
            new GraphPoint(25, 30),
            new GraphPoint(50, 50)
        ]);
        bezier.RoutingStyle.ShouldBe(EdgeRoutingStyle.Bezier);
        bezier.Points.Count.ShouldBeGreaterThan(2);
    }

    [Fact]
    public void Orthogonal_router_uses_explicit_waypoints_without_creating_diagonal_segments()
    {
        var path = GraphEdgeRoutingService.Default.Route(new GraphEdgeRoutingRequest(
            EdgeRoutingStyle.Orthogonal,
            GraphPoint.Zero,
            new GraphPoint(100, 100))
        {
            Waypoints = [new GraphPoint(20, 30), new GraphPoint(80, 30)]
        });

        path.Points.ShouldBe([
            GraphPoint.Zero,
            new GraphPoint(20, 0),
            new GraphPoint(20, 30),
            new GraphPoint(80, 30),
            new GraphPoint(80, 100),
            new GraphPoint(100, 100)
        ]);
        for (var index = 1; index < path.Points.Count; index++)
        {
            var start = path.Points[index - 1];
            var end = path.Points[index];
            (start.X == end.X || start.Y == end.Y).ShouldBeTrue();
        }
    }

    [Fact]
    public void Manual_router_keeps_moved_endpoint_route_orthogonal()
    {
        var path = GraphEdgeRoutingService.Default.Route(new GraphEdgeRoutingRequest(
            EdgeRoutingStyle.Manual,
            new GraphPoint(0, 0),
            new GraphPoint(140, 90))
        {
            Waypoints = [
                new GraphPoint(40, 0),
                new GraphPoint(40, 60),
                new GraphPoint(100, 60)
            ]
        });

        for (var index = 1; index < path.Points.Count; index++)
        {
            var start = path.Points[index - 1];
            var end = path.Points[index];
            (start.X == end.X || start.Y == end.Y).ShouldBeTrue();
        }
    }

    [Fact]
    public void Manual_router_preserves_explicit_collinear_waypoints()
    {
        var path = GraphEdgeRoutingService.Default.Route(new GraphEdgeRoutingRequest(
            EdgeRoutingStyle.Manual,
            new GraphPoint(0, 0),
            new GraphPoint(40, 40))
        {
            Waypoints = [
                new GraphPoint(10, 0),
                new GraphPoint(20, 0),
                new GraphPoint(20, 20)
            ]
        });

        path.Points.ShouldBe([
            new GraphPoint(0, 0),
            new GraphPoint(10, 0),
            new GraphPoint(20, 0),
            new GraphPoint(20, 20),
            new GraphPoint(20, 40),
            new GraphPoint(40, 40)
        ]);
        for (var index = 1; index < path.Points.Count; index++)
        {
            var start = path.Points[index - 1];
            var end = path.Points[index];
            (start.X == end.X || start.Y == end.Y).ShouldBeTrue();
        }
    }

    [Fact]
    public void Orthogonal_router_respects_port_entry_and_exit_directions()
    {
        var path = GraphEdgeRoutingService.Default.Route(new GraphEdgeRoutingRequest(
            EdgeRoutingStyle.Orthogonal,
            GraphPoint.Zero,
            new GraphPoint(100, 80))
        {
            StartDirection = new GraphPoint(1, 0),
            EndDirection = new GraphPoint(0, -1),
            Options = new GraphEdgeRoutingOptions { PortDirectionLeadLength = 20 }
        });

        path.Points[0].ShouldBe(GraphPoint.Zero);
        path.Points[1].Y.ShouldBe(0);
        path.Points[1].X.ShouldBeGreaterThanOrEqualTo(20);
        path.Points[^2].X.ShouldBe(100);
        path.Points[^2].Y.ShouldBeLessThanOrEqualTo(60);
        path.Points[^1].ShouldBe(new GraphPoint(100, 80));
    }

    [Fact]
    public void Default_edge_routing_service_routes_smart_orthogonal_around_obstacles()
    {
        var paddedObstacle = new GraphRect(30, 10, 40, 80);

        var path = GraphEdgeRoutingService.Default.Route(new GraphEdgeRoutingRequest(
            EdgeRoutingStyle.SmartOrthogonal,
            new GraphPoint(0, 50),
            new GraphPoint(100, 50))
        {
            Obstacles = [new GraphRect(40, 20, 20, 60)],
            Options = new GraphEdgeRoutingOptions
            {
                SmartOrthogonal = new OrthogonalRouteOptions { ObstaclePadding = 10 }
            }
        });

        path.RoutingStyle.ShouldBe(EdgeRoutingStyle.SmartOrthogonal);
        path.IsFallback.ShouldBeFalse();
        path.Points.Count.ShouldBeGreaterThan(2);
        for (var index = 1; index < path.Points.Count; index++)
        {
            SegmentCrossesInterior(path.Points[index - 1], path.Points[index], paddedObstacle)
                .ShouldBeFalse();
        }
    }

    [Fact]
    public void Smart_orthogonal_path_preserves_style_when_search_falls_back()
    {
        var path = GraphEdgeRoutingService.Default.Route(new GraphEdgeRoutingRequest(
            EdgeRoutingStyle.SmartOrthogonal,
            new GraphPoint(0, 0),
            new GraphPoint(100, 100))
        {
            Obstacles = [new GraphRect(40, 20, 20, 60)],
            Options = new GraphEdgeRoutingOptions
            {
                SmartOrthogonal = new OrthogonalRouteOptions { MaxSearchStates = 1 }
            }
        });

        path.RoutingStyle.ShouldBe(EdgeRoutingStyle.SmartOrthogonal);
        path.IsFallback.ShouldBeTrue();
        path.Points.ShouldBe([
            new GraphPoint(0, 0),
            new GraphPoint(50, 0),
            new GraphPoint(50, 100),
            new GraphPoint(100, 100)
        ]);
    }

    [Fact]
    public void Orthogonal_router_avoids_padded_obstacle_deterministically()
    {
        var obstacle = new GraphRect(40, 20, 20, 60);
        var options = new OrthogonalRouteOptions { ObstaclePadding = 10 };

        var first = OrthogonalObstacleRouter.Route(
            new GraphPoint(0, 50), new GraphPoint(100, 50), [obstacle], options);
        var second = OrthogonalObstacleRouter.Route(
            new GraphPoint(0, 50), new GraphPoint(100, 50), [obstacle], options);

        first.IsFallback.ShouldBeFalse();
        first.Points.ShouldBe(second.Points);
        first.Points.First().ShouldBe(new GraphPoint(0, 50));
        first.Points.Last().ShouldBe(new GraphPoint(100, 50));
        first.Points.Count.ShouldBeGreaterThan(2);
        for (var index = 1; index < first.Points.Count; index++)
        {
            var start = first.Points[index - 1];
            var end = first.Points[index];
            (start.X == end.X || start.Y == end.Y).ShouldBeTrue();
            SegmentCrossesInterior(start, end, new GraphRect(30, 10, 40, 80)).ShouldBeFalse();
        }
    }

    [Fact]
    public void Orthogonal_router_uses_direct_segment_without_obstacles()
    {
        var result = OrthogonalObstacleRouter.Route(
            new GraphPoint(0, 20), new GraphPoint(100, 20), []);

        result.IsFallback.ShouldBeFalse();
        result.Points.ShouldBe([new GraphPoint(0, 20), new GraphPoint(100, 20)]);
    }

    [Fact]
    public void Orthogonal_router_returns_stable_fallback_when_search_limit_is_exceeded()
    {
        var result = OrthogonalObstacleRouter.Route(
            new GraphPoint(0, 0),
            new GraphPoint(100, 100),
            [new GraphRect(40, 20, 20, 60)],
            new OrthogonalRouteOptions { MaxSearchStates = 1 });

        result.IsFallback.ShouldBeTrue();
        result.Points.ShouldBe([
            new GraphPoint(0, 0),
            new GraphPoint(50, 0),
            new GraphPoint(50, 100),
            new GraphPoint(100, 100)
        ]);
    }

    [Fact]
    public void Orthogonal_router_limits_candidate_grid_before_search()
    {
        var obstacles = Enumerable.Range(0, 20)
            .Select(index => new GraphRect(index * 20 + 10, 20, 8, 60))
            .ToArray();

        var result = OrthogonalObstacleRouter.Route(
            new GraphPoint(0, 0),
            new GraphPoint(500, 100),
            obstacles,
            new OrthogonalRouteOptions { MaxSearchStates = 100 });

        result.IsFallback.ShouldBeTrue();
        result.VisitedStateCount.ShouldBe(0);
    }

    [Fact]
    public void Orthogonal_router_ignores_distant_obstacles_before_building_candidate_grid()
    {
        var localObstacle = new GraphRect(40, 20, 20, 60);
        var distantObstacles = Enumerable.Range(0, 200)
            .Select(index => new GraphRect(
                10_000 + index * 40,
                20_000 + index * 30,
                16,
                16));
        var options = new OrthogonalRouteOptions
        {
            ObstaclePadding = 0,
            MaxSearchStates = 1_000
        };

        var expected = OrthogonalObstacleRouter.Route(
            new GraphPoint(0, 50),
            new GraphPoint(100, 50),
            [localObstacle],
            options);
        var result = OrthogonalObstacleRouter.Route(
            new GraphPoint(0, 50),
            new GraphPoint(100, 50),
            distantObstacles.Prepend(localObstacle),
            options);

        result.IsFallback.ShouldBeFalse();
        result.Points.ShouldBe(expected.Points);
        result.VisitedStateCount.ShouldBe(expected.VisitedStateCount);
    }

    [Fact]
    public void Orthogonal_router_expands_relevant_obstacle_set_to_transitive_neighbors()
    {
        var first = new GraphRect(40, 30, 20, 40);
        var second = new GraphRect(55, 15, 30, 20);
        var obstacles = new[] { second, first };

        var result = OrthogonalObstacleRouter.Route(
            new GraphPoint(0, 50),
            new GraphPoint(100, 50),
            obstacles,
            new OrthogonalRouteOptions { ObstaclePadding = 0 });

        result.IsFallback.ShouldBeFalse();
        for (var index = 1; index < result.Points.Count; index++)
        {
            foreach (var obstacle in obstacles)
            {
                SegmentCrossesInterior(
                    result.Points[index - 1],
                    result.Points[index],
                    obstacle).ShouldBeFalse();
            }
        }
    }

    [Fact]
    public void Orthogonal_router_discovers_disconnected_obstacles_that_block_a_detour()
    {
        var directObstacle = new GraphRect(40, 30, 20, 40);
        var detourObstacle = new GraphRect(20, 20, 10, 20);
        var obstacles = new[] { directObstacle, detourObstacle };

        var result = OrthogonalObstacleRouter.Route(
            new GraphPoint(0, 50),
            new GraphPoint(100, 50),
            obstacles,
            new OrthogonalRouteOptions { ObstaclePadding = 0 });

        result.IsFallback.ShouldBeFalse();
        for (var index = 1; index < result.Points.Count; index++)
        {
            foreach (var obstacle in obstacles)
            {
                SegmentCrossesInterior(
                    result.Points[index - 1],
                    result.Points[index],
                    obstacle).ShouldBeFalse();
            }
        }
    }

    [Fact]
    public void Orthogonal_router_supports_reverse_edges()
    {
        var obstacle = new GraphRect(40, 20, 20, 60);

        var result = OrthogonalObstacleRouter.Route(
            new GraphPoint(100, 50),
            new GraphPoint(0, 50),
            [obstacle],
            new OrthogonalRouteOptions { ObstaclePadding = 10 });

        result.IsFallback.ShouldBeFalse();
        result.Points.First().ShouldBe(new GraphPoint(100, 50));
        result.Points.Last().ShouldBe(new GraphPoint(0, 50));
        for (var index = 1; index < result.Points.Count; index++)
        {
            SegmentCrossesInterior(
                result.Points[index - 1],
                result.Points[index],
                new GraphRect(30, 10, 40, 80)).ShouldBeFalse();
        }
    }

    [Fact]
    public void Orthogonal_router_avoids_overlapping_obstacles()
    {
        var obstacles = new[]
        {
            new GraphRect(30, 20, 30, 60),
            new GraphRect(50, 10, 30, 80)
        };

        var result = OrthogonalObstacleRouter.Route(
            new GraphPoint(0, 50),
            new GraphPoint(100, 50),
            obstacles,
            new OrthogonalRouteOptions { ObstaclePadding = 0 });

        result.IsFallback.ShouldBeFalse();
        for (var index = 1; index < result.Points.Count; index++)
        {
            foreach (var obstacle in obstacles)
            {
                SegmentCrossesInterior(
                    result.Points[index - 1],
                    result.Points[index],
                    obstacle).ShouldBeFalse();
            }
        }
    }

    [Fact]
    public void TopologicalSort_orders_dependencies_before_dependents()
    {
        var document = CreateLinearDocument();

        var order = TopologicalSort.Sort(document);

        order.ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public void LayeredLayout_places_later_nodes_on_later_layers()
    {
        var document = CreateLinearDocument();

        var result = new LayeredLayoutAlgorithm().Layout(new GraphLayoutRequest(document));

        result.NodePositions["b"].X.ShouldBeGreaterThan(result.NodePositions["a"].X);
        result.NodePositions["c"].X.ShouldBeGreaterThan(result.NodePositions["b"].X);
    }

    [Theory]
    [InlineData(NodeAlignment.Left, 10, 10)]
    [InlineData(NodeAlignment.HorizontalCenter, 130, 90)]
    [InlineData(NodeAlignment.Right, 250, 170)]
    public void Horizontal_alignment_uses_the_selected_node_bounds(
        NodeAlignment alignment,
        double expectedFirstX,
        double expectedSecondX)
    {
        var first = new NodeModel
        {
            Id = "first",
            Position = new GraphPoint(10, 20),
            Size = new GraphSize(80, 40)
        };
        var second = new NodeModel
        {
            Id = "second",
            Position = new GraphPoint(170, 100),
            Size = new GraphSize(160, 60)
        };

        var result = GraphNodeArrangement.Align([first, second], alignment);

        result.NodePositions["first"].ShouldBe(new GraphPoint(expectedFirstX, 20));
        result.NodePositions["second"].ShouldBe(new GraphPoint(expectedSecondX, 100));
    }

    [Fact]
    public void Horizontal_distribution_preserves_outer_nodes_and_uses_equal_gaps()
    {
        var first = new NodeModel
        {
            Id = "first",
            Position = new GraphPoint(0, 20),
            Size = new GraphSize(100, 40)
        };
        var middle = new NodeModel
        {
            Id = "middle",
            Position = new GraphPoint(140, 50),
            Size = new GraphSize(60, 40)
        };
        var last = new NodeModel
        {
            Id = "last",
            Position = new GraphPoint(400, 80),
            Size = new GraphSize(100, 40)
        };

        var result = GraphNodeArrangement.Distribute(
            [last, middle, first],
            NodeDistribution.Horizontal);

        result.NodePositions["first"].ShouldBe(first.Position);
        result.NodePositions["middle"].ShouldBe(new GraphPoint(220, 50));
        result.NodePositions["last"].ShouldBe(last.Position);
    }

    [Theory]
    [InlineData(NodeAlignment.Top, 20, 20)]
    [InlineData(NodeAlignment.VerticalCenter, 70, 60)]
    [InlineData(NodeAlignment.Bottom, 120, 100)]
    public void Vertical_alignment_uses_the_selected_node_bounds(
        NodeAlignment alignment,
        double expectedFirstY,
        double expectedSecondY)
    {
        var first = new NodeModel
        {
            Id = "first",
            Position = new GraphPoint(10, 20),
            Size = new GraphSize(80, 40)
        };
        var second = new NodeModel
        {
            Id = "second",
            Position = new GraphPoint(170, 100),
            Size = new GraphSize(160, 60)
        };

        var result = GraphNodeArrangement.Align([first, second], alignment);

        result.NodePositions["first"].ShouldBe(new GraphPoint(10, expectedFirstY));
        result.NodePositions["second"].ShouldBe(new GraphPoint(170, expectedSecondY));
    }

    [Fact]
    public void Vertical_distribution_preserves_outer_nodes_and_uses_equal_gaps()
    {
        var first = new NodeModel
        {
            Id = "first",
            Position = new GraphPoint(20, 0),
            Size = new GraphSize(80, 40)
        };
        var middle = new NodeModel
        {
            Id = "middle",
            Position = new GraphPoint(50, 100),
            Size = new GraphSize(80, 60)
        };
        var last = new NodeModel
        {
            Id = "last",
            Position = new GraphPoint(80, 400),
            Size = new GraphSize(80, 100)
        };

        var result = GraphNodeArrangement.Distribute(
            [last, middle, first],
            NodeDistribution.Vertical);

        result.NodePositions["first"].ShouldBe(first.Position);
        result.NodePositions["middle"].ShouldBe(new GraphPoint(50, 190));
        result.NodePositions["last"].ShouldBe(last.Position);
    }

    [Fact]
    public void Grid_render_metrics_skip_dense_minor_lines_at_low_zoom()
    {
        var settings = new GraphGridSettings
        {
            Spacing = 20,
            MajorLineInterval = 4
        };

        var normal = GraphGridCalculator.GetRenderMetrics(settings, zoom: 1);
        var zoomedOut = GraphGridCalculator.GetRenderMetrics(settings, zoom: 0.2);

        normal.MinorSpacing.ShouldBe(20);
        normal.MajorSpacing.ShouldBe(80);
        zoomedOut.MinorSpacing.ShouldBe(80);
        (zoomedOut.MinorSpacing * 0.2).ShouldBeGreaterThanOrEqualTo(10);
        zoomedOut.MajorSpacing.ShouldBe(320);
    }

    [Fact]
    public void Grid_render_metrics_remain_finite_for_extreme_input()
    {
        var metrics = GraphGridCalculator.GetRenderMetrics(
            new GraphGridSettings { Spacing = double.Epsilon },
            double.Epsilon,
            double.MaxValue);

        double.IsFinite(metrics.MinorSpacing).ShouldBeTrue();
        double.IsFinite(metrics.MajorSpacing).ShouldBeTrue();
        metrics.MinorSpacing.ShouldBeGreaterThan(0);
        metrics.MajorSpacing.ShouldBeGreaterThan(metrics.MinorSpacing);
    }

    [Fact]
    public void Grid_snap_uses_primary_node_and_preserves_multi_node_offset()
    {
        var primary = new NodeModel
        {
            Id = "primary",
            Position = new GraphPoint(13, 17),
            Size = new GraphSize(100, 60)
        };
        var secondary = new NodeModel
        {
            Id = "secondary",
            Position = new GraphPoint(83, 47),
            Size = new GraphSize(80, 40)
        };
        var settings = GraphGridSettings.Default with
        {
            IsSnapEnabled = true,
            IsGuidesEnabled = false,
            Spacing = 20
        };
        var initial = new Dictionary<string, GraphPoint>(StringComparer.Ordinal)
        {
            [primary.Id] = primary.Position,
            [secondary.Id] = secondary.Position
        };

        var result = GraphNodeSnapper.Snap(
            [primary, secondary],
            [],
            initial,
            primary.Id,
            new GraphPoint(6, 4),
            settings);

        result.NodePositions[primary.Id].ShouldBe(new GraphPoint(20, 20));
        result.NodePositions[secondary.Id].ShouldBe(new GraphPoint(90, 50));
        result.IsGridSnapX.ShouldBeTrue();
        result.IsGridSnapY.ShouldBeTrue();
        result.AlignmentGuides.ShouldBeEmpty();
    }

    [Fact]
    public void Alignment_guides_have_priority_over_grid_snap()
    {
        var primary = new NodeModel
        {
            Id = "primary",
            Position = new GraphPoint(10, 20),
            Size = new GraphSize(100, 60)
        };
        var reference = new NodeModel
        {
            Id = "reference",
            Position = new GraphPoint(95, 200),
            Size = new GraphSize(70, 60)
        };
        var settings = GraphGridSettings.Default with
        {
            IsSnapEnabled = true,
            Spacing = 20,
            GuideTolerance = 8,
            SnapTolerance = 8
        };
        var initial = new Dictionary<string, GraphPoint>(StringComparer.Ordinal)
        {
            [primary.Id] = primary.Position
        };

        var result = GraphNodeSnapper.Snap(
            [primary],
            [reference],
            initial,
            primary.Id,
            new GraphPoint(81, 7),
            settings);

        result.NodePositions[primary.Id].ShouldBe(new GraphPoint(95, 20));
        result.IsGuideSnapX.ShouldBeTrue();
        result.IsGridSnapX.ShouldBeFalse();
        result.AlignmentGuides.ShouldContain(
            new GraphAlignmentGuide(GraphGuideOrientation.Vertical, 95));
    }

    [Fact]
    public void Group_bounds_include_members_padding_and_title_header()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel
        {
            Id = "first",
            Position = new GraphPoint(100, 80),
            Size = new GraphSize(120, 60)
        });
        document.AddNode(new NodeModel
        {
            Id = "second",
            Position = new GraphPoint(300, 200),
            Size = new GraphSize(80, 100)
        });
        var group = new GroupModel
        {
            NodeIds = ["first", "second", "missing"],
            Padding = 20
        };

        var bounds = GraphGroupBoundsCalculator.Calculate(document, group);

        bounds.ShouldBe(new GraphRect(80, 32, 320, 288));
    }

    [Fact]
    public void Group_bounds_are_null_without_existing_members()
    {
        var document = new GraphDocument();
        var group = new GroupModel { NodeIds = ["missing"] };

        GraphGroupBoundsCalculator.Calculate(document, group).ShouldBeNull();
    }

    [Fact]
    public void Collapsed_group_uses_compact_bounds_anchored_to_expanded_group()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel
        {
            Id = "first",
            Position = new GraphPoint(100, 80),
            Size = new GraphSize(120, 60)
        });
        document.AddNode(new NodeModel
        {
            Id = "second",
            Position = new GraphPoint(300, 200),
            Size = new GraphSize(80, 100)
        });
        var group = new GroupModel
        {
            NodeIds = ["first", "second"],
            Padding = 20,
            IsCollapsed = true
        };

        GraphGroupBoundsCalculator.CalculateExpanded(document, group)
            .ShouldBe(new GraphRect(80, 32, 320, 288));
        GraphGroupBoundsCalculator.Calculate(document, group)
            .ShouldBe(new GraphRect(80, 32, 180, 44));
    }

    private static GraphDocument CreateLinearDocument()
    {
        var document = new GraphDocument();
        document.AddNode(new NodeModel { Id = "a" });
        document.AddNode(new NodeModel { Id = "b" });
        document.AddNode(new NodeModel { Id = "c" });
        document.AddEdge(new EdgeModel
        {
            Source = new PortReference("a", "out"),
            Target = new PortReference("b", "in")
        });
        document.AddEdge(new EdgeModel
        {
            Source = new PortReference("b", "out"),
            Target = new PortReference("c", "in")
        });

        return document;
    }

    private static bool SegmentCrossesInterior(GraphPoint first, GraphPoint second, GraphRect rect) =>
        first.Y == second.Y
            ? first.Y > rect.Y && first.Y < rect.Y + rect.Height &&
              Math.Max(first.X, second.X) > rect.X && Math.Min(first.X, second.X) < rect.X + rect.Width
            : first.X > rect.X && first.X < rect.X + rect.Width &&
              Math.Max(first.Y, second.Y) > rect.Y && Math.Min(first.Y, second.Y) < rect.Y + rect.Height;

    private sealed class StubEdgeRouter(
        EdgeRoutingStyle routingStyle,
        Func<GraphEdgeRoutingRequest, GraphEdgePath> route) : IGraphEdgeRouter
    {
        public EdgeRoutingStyle RoutingStyle { get; } = routingStyle;

        public GraphEdgePath Route(GraphEdgeRoutingRequest request) => route(request);
    }
}

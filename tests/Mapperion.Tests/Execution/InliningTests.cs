using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public sealed class Leaf
    {
        public int Value { get; set; }

        public int Divisor { get; set; } = 1;
    }

    public sealed class LeafDto
    {
        public int Ratio { get; set; }
    }

    public sealed class Branch
    {
        public string Label { get; set; } = string.Empty;

        public Leaf Leaf { get; set; } = new Leaf();
    }

    public sealed class BranchDto
    {
        public string Label { get; set; } = string.Empty;

        public LeafDto Leaf { get; set; } = new LeafDto();
    }

    public sealed class Trunk
    {
        public Branch Branch { get; set; } = new Branch();

        public List<Branch> Branches { get; set; } = new List<Branch>();
    }

    public sealed class TrunkDto
    {
        public BranchDto Branch { get; set; } = new BranchDto();

        public List<BranchDto> Branches { get; set; } = new List<BranchDto>();
    }

    public sealed class Marked
    {
        public string Text { get; set; } = string.Empty;
    }

    public sealed class MarkedDto
    {
        public string Text { get; set; } = string.Empty;

        public string Trail { get; set; } = string.Empty;
    }

    public sealed class MarkedHolder
    {
        public Marked Inner { get; set; } = new Marked();
    }

    public sealed class MarkedHolderDto
    {
        public MarkedDto Inner { get; set; } = new MarkedDto();
    }

    public sealed class LeafToTextConverter : ITypeConverter<Leaf, LeafDto>
    {
        public LeafDto Convert(Leaf source, LeafDto destination, ResolutionContext context)
        {
            return new LeafDto { Ratio = -1 };
        }
    }

    public abstract class Shape
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class Circle : Shape
    {
        public int Radius { get; set; }
    }

    public class ShapeDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class CircleDto : ShapeDto
    {
        public int Radius { get; set; }
    }

    public sealed class Drawing
    {
        public Shape Shape { get; set; } = new Circle();
    }

    public sealed class DrawingDto
    {
        public ShapeDto Shape { get; set; } = new ShapeDto();
    }

    public sealed class Ping
    {
        public string Name { get; set; } = string.Empty;

        public Pong? Pong { get; set; }
    }

    public sealed class Pong
    {
        public string Name { get; set; } = string.Empty;

        public Ping? Ping { get; set; }
    }

    public sealed class PingDto
    {
        public string Name { get; set; } = string.Empty;

        public PongDto? Pong { get; set; }
    }

    public sealed class PongDto
    {
        public string Name { get; set; } = string.Empty;

        public PingDto? Ping { get; set; }
    }

    /// <summary>
    /// A nested map small enough and plain enough is written into the body that needs it rather
    /// than called. These cover what must survive that: the maps that are not eligible keep their
    /// own plan, and the ones that are absorbed still report where they failed.
    /// </summary>
    public sealed class InliningTests
    {
        private static MapperConfiguration Trees() => new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Trunk, TrunkDto>();
            cfg.CreateMap<Branch, BranchDto>();
            cfg.CreateMap<Leaf, LeafDto>()
               .ForMember(d => d.Ratio, o => o.MapFrom(s => s.Value / s.Divisor));
        });

        [Fact]
        public void A_chain_of_absorbed_maps_produces_the_same_result()
        {
            var trunk = new Trunk
            {
                Branch = new Branch { Label = "one", Leaf = new Leaf { Value = 9, Divisor = 3 } },
            };

            TrunkDto dto = Trees().CreateMapper().Map<Trunk, TrunkDto>(trunk);

            dto.Branch.Label.ShouldBe("one");
            dto.Branch.Leaf.Ratio.ShouldBe(3);
        }

        [Fact]
        public void A_failure_two_levels_down_still_names_every_step()
        {
            var trunk = new Trunk
            {
                Branch = new Branch { Leaf = new Leaf { Value = 1, Divisor = 0 } },
            };

            MappingException error = Should.Throw<MappingException>(
                () => Trees().CreateMapper().Map<Trunk, TrunkDto>(trunk));

            error.MemberPath.ShouldBe("Branch.Leaf.Ratio");
            error.InnerException.ShouldBeOfType<DivideByZeroException>();
        }

        [Fact]
        public void A_failure_inside_a_collection_element_keeps_both_the_index_and_the_member()
        {
            var trunk = new Trunk
            {
                Branches =
                {
                    new Branch { Leaf = new Leaf { Value = 4, Divisor = 2 } },
                    new Branch { Leaf = new Leaf { Value = 1, Divisor = 0 } },
                },
            };

            MappingException error = Should.Throw<MappingException>(
                () => Trees().CreateMapper().Map<Trunk, TrunkDto>(trunk));

            error.MemberPath.ShouldBe("Branches[1].Leaf.Ratio");
        }

        [Fact]
        public void A_null_nested_source_still_yields_a_null_destination()
        {
            var trunk = new Trunk { Branch = null! };

            TrunkDto dto = Trees().CreateMapper().Map<Trunk, TrunkDto>(trunk);

            dto.Branch.ShouldBeNull();
        }

        [Fact]
        public void A_nested_map_with_steps_keeps_its_own_plan_and_runs_them()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<MarkedHolder, MarkedHolderDto>();
                cfg.CreateMap<Marked, MarkedDto>()
                   .ForMember(d => d.Trail, o => o.Ignore())
                   .BeforeMap((source, destination) => destination.Trail = "before")
                   .AfterMap((source, destination) => destination.Trail += "|after");
            });

            var holder = new MarkedHolder { Inner = new Marked { Text = "t" } };

            MarkedHolderDto dto = config.CreateMapper().Map<MarkedHolder, MarkedHolderDto>(holder);

            dto.Inner.Text.ShouldBe("t");
            dto.Inner.Trail.ShouldBe("before|after");
        }

        [Fact]
        public void A_nested_type_converter_keeps_its_own_plan_and_is_used()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Branch, BranchDto>();
                cfg.CreateMap<Leaf, LeafDto>().ConvertUsing<LeafToTextConverter>();
            });

            var branch = new Branch { Label = "l", Leaf = new Leaf { Value = 8, Divisor = 2 } };

            BranchDto dto = config.CreateMapper().Map<Branch, BranchDto>(branch);

            dto.Leaf.Ratio.ShouldBe(-1);
        }

        [Fact]
        public void A_nested_polymorphic_map_keeps_its_own_plan_and_still_dispatches()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Drawing, DrawingDto>();
                cfg.CreateMap<Shape, ShapeDto>().Include<Circle, CircleDto>();
                cfg.CreateMap<Circle, CircleDto>();
            });

            var drawing = new Drawing { Shape = new Circle { Name = "c", Radius = 7 } };

            DrawingDto dto = config.CreateMapper().Map<Drawing, DrawingDto>(drawing);

            dto.Shape.ShouldBeOfType<CircleDto>().Radius.ShouldBe(7);
        }

        [Fact]
        public void A_map_that_reaches_itself_is_not_written_into_itself()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Node, NodeDto>().MaxDepth(3));

            var root = new Node { Name = "a", Next = new Node { Name = "b" } };
            root.Next!.Next = root;

            NodeDto dto = config.CreateMapper().Map<Node, NodeDto>(root);

            dto.Name.ShouldBe("a");
            dto.Next!.Name.ShouldBe("b");
        }

        /// <remarks>
        /// Neither map limits its depth, so neither can be absorbed all the way down. The compiler
        /// has to notice it is already inside one of them and fall back to a call, or it would
        /// write the pair into each other forever.
        /// </remarks>
        [Fact]
        public void Two_maps_that_reach_each_other_still_compile()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Ping, PingDto>();
                cfg.CreateMap<Pong, PongDto>();
            });

            var ping = new Ping { Name = "ping", Pong = new Pong { Name = "pong" } };

            PingDto dto = config.CreateMapper().Map<Ping, PingDto>(ping);

            dto.Name.ShouldBe("ping");
            dto.Pong!.Name.ShouldBe("pong");
            dto.Pong.Ping.ShouldBeNull();
        }
    }
}

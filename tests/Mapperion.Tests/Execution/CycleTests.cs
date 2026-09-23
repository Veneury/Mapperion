using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public sealed class Staffer
    {
        public string Name { get; set; } = string.Empty;

        public Staffer? Manager { get; set; }

        public List<Staffer> Reports { get; set; } = new List<Staffer>();
    }

    public sealed class StafferNodeDto
    {
        public string Name { get; set; } = string.Empty;

        public StafferNodeDto? Manager { get; set; }

        public List<StafferNodeDto> Reports { get; set; } = new List<StafferNodeDto>();
    }

    public struct PointNode
    {
        public int X { get; set; }
    }

    public sealed class CycleTests
    {
        private static Staffer CyclicPair()
        {
            var boss = new Staffer { Name = "boss" };
            var report = new Staffer { Name = "report", Manager = boss };
            boss.Reports.Add(report);

            return report;
        }

        [Fact]
        public void PreserveReferences_terminates_a_real_cycle()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Staffer, StafferNodeDto>().PreserveReferences());

            StafferNodeDto dto = config.CreateMapper().Map<Staffer, StafferNodeDto>(CyclicPair());

            dto.Name.ShouldBe("report");
            dto.Manager!.Name.ShouldBe("boss");
            dto.Manager.Reports.Count.ShouldBe(1);
        }

        [Fact]
        public void PreserveReferences_reuses_the_same_destination_instance()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Staffer, StafferNodeDto>().PreserveReferences());

            StafferNodeDto dto = config.CreateMapper().Map<Staffer, StafferNodeDto>(CyclicPair());

            dto.Manager!.Reports[0].ShouldBeSameAs(dto);
        }

        [Fact]
        public void PreserveReferences_shares_one_destination_for_a_repeated_source()
        {
            var shared = new Staffer { Name = "shared" };
            var root = new Staffer { Name = "root", Reports = { shared, shared } };

            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Staffer, StafferNodeDto>().PreserveReferences());

            StafferNodeDto dto = config.CreateMapper().Map<Staffer, StafferNodeDto>(root);

            dto.Reports[0].ShouldBeSameAs(dto.Reports[1]);
        }

        [Fact]
        public void MaxDepth_truncates_instead_of_recursing()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Staffer, StafferNodeDto>().MaxDepth(2));

            StafferNodeDto dto = config.CreateMapper().Map<Staffer, StafferNodeDto>(CyclicPair());

            dto.Name.ShouldBe("report");
            dto.Manager!.Name.ShouldBe("boss");
            dto.Manager.Reports.Count.ShouldBe(1);
            dto.Manager.Reports[0].ShouldBeNull();
        }

        [Fact]
        public void MaxDepth_of_one_keeps_only_the_root()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Staffer, StafferNodeDto>().MaxDepth(1));

            StafferNodeDto dto = config.CreateMapper().Map<Staffer, StafferNodeDto>(CyclicPair());

            dto.Name.ShouldBe("report");
            dto.Manager.ShouldBeNull();
        }

        [Fact]
        public void The_depth_counter_is_released_between_operations()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Staffer, StafferNodeDto>().MaxDepth(2));

            IMapper mapper = config.CreateMapper();

            for (int i = 0; i < 5; i++)
            {
                mapper.Map<Staffer, StafferNodeDto>(CyclicPair()).Manager.ShouldNotBeNull();
            }
        }

        [Fact]
        public void Two_operations_do_not_share_preserved_references()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Staffer, StafferNodeDto>().PreserveReferences());

            IMapper mapper = config.CreateMapper();
            Staffer source = CyclicPair();

            StafferNodeDto first = mapper.Map<Staffer, StafferNodeDto>(source);
            StafferNodeDto second = mapper.Map<Staffer, StafferNodeDto>(source);

            first.ShouldNotBeSameAs(second);
        }

        /// <remarks>
        /// This is the failure AutoMapper carries as CVE-2026-32933 and will not fix on its MIT
        /// line: a graph that loops recurses until the stack runs out, which cannot be caught and
        /// takes the process with it. Here the map that closes the loop counts its own depth, so
        /// the same graph produces an exception the caller can handle.
        /// </remarks>
        [Fact]
        public void A_looping_graph_fails_with_an_exception_instead_of_killing_the_process()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Staffer, StafferNodeDto>());

            RecursionLimitException error = Should.Throw<RecursionLimitException>(
                () => config.CreateMapper().Map<Staffer, StafferNodeDto>(CyclicPair()));

            error.Limit.ShouldBe(64);
            error.Map!.ShouldContain("Staffer");
        }

        [Fact]
        public void The_recursion_failure_is_still_a_mapping_failure()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Staffer, StafferNodeDto>());

            Should.Throw<MappingException>(
                () => config.CreateMapper().Map<Staffer, StafferNodeDto>(CyclicPair()));
        }

        [Fact]
        public void The_ceiling_is_configurable()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.RecursionLimit = 4;
                cfg.CreateMap<Staffer, StafferNodeDto>();
            });

            RecursionLimitException error = Should.Throw<RecursionLimitException>(
                () => config.CreateMapper().Map<Staffer, StafferNodeDto>(CyclicPair()));

            error.Limit.ShouldBe(4);
        }

        [Fact]
        public void A_deep_graph_that_does_not_loop_is_left_alone()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Staffer, StafferNodeDto>());

            StafferNodeDto dto = config.CreateMapper().Map<Staffer, StafferNodeDto>(Chain(40));

            int depth = 0;

            for (StafferNodeDto? step = dto; step is not null; step = step.Manager)
            {
                depth++;
            }

            depth.ShouldBe(40);
        }

        [Fact]
        public void A_graph_deeper_than_the_ceiling_needs_the_ceiling_raised()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.RecursionLimit = 100;
                cfg.CreateMap<Staffer, StafferNodeDto>();
            });

            Should.NotThrow(() => config.CreateMapper().Map<Staffer, StafferNodeDto>(Chain(80)));
        }

        [Fact]
        public void A_map_that_already_guards_itself_keeps_its_own_answer()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Staffer, StafferNodeDto>().MaxDepth(2));

            StafferNodeDto dto = config.CreateMapper().Map<Staffer, StafferNodeDto>(CyclicPair());

            dto.Name.ShouldBe("report");
            dto.Manager!.Name.ShouldBe("boss");
            dto.Manager.Manager.ShouldBeNull();
        }

        private static Staffer Chain(int length)
        {
            var head = new Staffer { Name = "0" };
            Staffer current = head;

            for (int i = 1; i < length; i++)
            {
                var next = new Staffer { Name = i.ToString(System.Globalization.CultureInfo.InvariantCulture) };
                current.Manager = next;
                current = next;
            }

            return head;
        }

        [Fact]
        public void An_unguarded_cycle_is_reported_by_validation()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Staffer, StafferNodeDto>());

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(config.AssertIsValid);

            error.Errors.ShouldContain(e => e.Contains("form a cycle with nothing to stop it"));
        }

        [Fact]
        public void A_guarded_cycle_passes_validation()
        {
            Should.NotThrow(() => new MapperConfiguration(cfg =>
                cfg.CreateMap<Staffer, StafferNodeDto>().PreserveReferences()).AssertIsValid());

            Should.NotThrow(() => new MapperConfiguration(cfg =>
                cfg.CreateMap<Staffer, StafferNodeDto>().MaxDepth(3)).AssertIsValid());
        }

        [Fact]
        public void PreserveReferences_on_a_value_type_is_reported()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<PointNode, PointNode>().PreserveReferences());

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(config.AssertIsValid);

            error.Errors.ShouldContain(e => e.Contains("needs both sides to be reference types"));
        }

        [Fact]
        public void A_map_without_cycles_allocates_no_state()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Staffer, StafferNodeDto>().MaxDepth(2));

            config.Model.TypeMaps[0].MaxDepth.ShouldBe(2);

            var plain = new MapperConfiguration(cfg => cfg.CreateMap<PointNode, PointNode>());
            plain.Model.TypeMaps[0].MaxDepth.ShouldBeNull();
            plain.Model.TypeMaps[0].PreserveReferences.ShouldBeFalse();
        }
    }
}

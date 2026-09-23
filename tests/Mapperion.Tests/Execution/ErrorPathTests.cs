using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public sealed class Reading
    {
        public int Value { get; set; }

        public int Divisor { get; set; }
    }

    public sealed class ReadingDto
    {
        public int Ratio { get; set; }

        public string Label { get; set; } = string.Empty;
    }

    public sealed class Batch
    {
        public string Name { get; set; } = string.Empty;

        public List<Reading> Readings { get; set; } = new List<Reading>();
    }

    public sealed class BatchDto
    {
        public string Name { get; set; } = string.Empty;

        public List<ReadingDto> Readings { get; set; } = new List<ReadingDto>();
    }

    public sealed class Holder
    {
        public Batch Batch { get; set; } = new Batch();
    }

    public sealed class HolderDto
    {
        public BatchDto Batch { get; set; } = new BatchDto();
    }

    public sealed class NeedsAName
    {
        public NeedsAName(string missing)
        {
            Missing = missing;
        }

        public string Missing { get; }
    }

    public sealed class NamedBatchDto
    {
        public List<NeedsAName> Readings { get; set; } = new List<NeedsAName>();
    }

    public sealed class ExplodingConverter : ITypeConverter<Reading, ReadingDto>
    {
        public ReadingDto Convert(Reading source, ReadingDto destination, ResolutionContext context)
        {
            throw new InvalidOperationException("converter blew up");
        }
    }

    public sealed class ErrorPathTests
    {
        private static MapperConfiguration Ratios() => new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Batch, BatchDto>();
            cfg.CreateMap<Reading, ReadingDto>()
               .ForMember(d => d.Ratio, o => o.MapFrom(s => s.Value / s.Divisor))
               .ForMember(d => d.Label, o => o.Ignore());
        });

        [Fact]
        public void A_failing_member_is_named()
        {
            IMapper mapper = Ratios().CreateMapper();

            MappingException error = Should.Throw<MappingException>(
                () => mapper.Map<Reading, ReadingDto>(new Reading { Value = 1, Divisor = 0 }));

            error.MemberPath.ShouldBe("Ratio");
            error.Message.ShouldContain("Reading -> ReadingDto");
            error.Message.ShouldContain("'Ratio'");
        }

        [Fact]
        public void The_original_exception_is_kept_as_the_inner_one()
        {
            IMapper mapper = Ratios().CreateMapper();

            MappingException error = Should.Throw<MappingException>(
                () => mapper.Map<Reading, ReadingDto>(new Reading { Value = 1, Divisor = 0 }));

            error.InnerException.ShouldBeOfType<DivideByZeroException>();
        }

        [Fact]
        public void A_failing_element_is_reported_with_its_index()
        {
            IMapper mapper = Ratios().CreateMapper();

            var batch = new Batch
            {
                Name = "b",
                Readings =
                {
                    new Reading { Value = 4, Divisor = 2 },
                    new Reading { Value = 9, Divisor = 3 },
                    new Reading { Value = 1, Divisor = 0 },
                },
            };

            MappingException error = Should.Throw<MappingException>(
                () => mapper.Map<Batch, BatchDto>(batch));

            error.MemberPath.ShouldBe("Readings[2].Ratio");
        }

        [Fact]
        public void The_path_spans_nested_maps()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Holder, HolderDto>();
                cfg.CreateMap<Batch, BatchDto>();
                cfg.CreateMap<Reading, ReadingDto>()
                   .ForMember(d => d.Ratio, o => o.MapFrom(s => s.Value / s.Divisor))
                   .ForMember(d => d.Label, o => o.Ignore());
            });

            var holder = new Holder
            {
                Batch = new Batch { Readings = { new Reading { Value = 1, Divisor = 0 } } },
            };

            MappingException error = Should.Throw<MappingException>(
                () => config.CreateMapper().Map<Holder, HolderDto>(holder));

            error.MemberPath.ShouldBe("Batch.Readings[0].Ratio");
        }

        [Fact]
        public void A_failing_type_converter_is_reported_against_the_member_that_used_it()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Batch, BatchDto>();
                cfg.CreateMap<Reading, ReadingDto>().ConvertUsing<ExplodingConverter>();
            });

            var batch = new Batch { Readings = { new Reading() } };

            MappingException error = Should.Throw<MappingException>(
                () => config.CreateMapper().Map<Batch, BatchDto>(batch));

            error.MemberPath.ShouldBe("Readings[0]");
            error.Message.ShouldContain("Batch -> BatchDto");
        }

        [Fact]
        public void A_failing_after_step_is_named_as_such()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Reading, ReadingDto>()
                   .ForMember(d => d.Ratio, o => o.Ignore())
                   .ForMember(d => d.Label, o => o.Ignore())
                   .AfterMap((source, destination) => throw new InvalidOperationException("step")));

            MappingException error = Should.Throw<MappingException>(
                () => config.CreateMapper().Map<Reading, ReadingDto>(new Reading()));

            error.MemberPath.ShouldBe("(after step)");
        }

        [Fact]
        public void A_configuration_problem_found_while_mapping_stays_a_configuration_problem()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Batch, NamedBatchDto>();
                cfg.CreateMap<Reading, NeedsAName>();
            });

            var batch = new Batch { Readings = { new Reading() } };

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => config.CreateMapper().Map<Batch, NamedBatchDto>(batch));

            error.Message.ShouldContain("'missing'");
        }

        [Fact]
        public void A_successful_map_is_unaffected()
        {
            ReadingDto dto = Ratios().CreateMapper().Map<Reading, ReadingDto>(new Reading { Value = 9, Divisor = 3 });

            dto.Ratio.ShouldBe(3);
        }
    }
}

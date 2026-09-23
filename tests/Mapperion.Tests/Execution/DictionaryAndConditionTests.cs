using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public sealed class Score
    {
        public int Points { get; set; }
    }

    public sealed class ScoreDto
    {
        public long Points { get; set; }
    }

    public sealed class Scoreboard
    {
        public Dictionary<string, Score> ByPlayer { get; set; } = new Dictionary<string, Score>();

        public Dictionary<int, string> Labels { get; set; } = new Dictionary<int, string>();
    }

    public sealed class ScoreboardDto
    {
        public Dictionary<string, ScoreDto> ByPlayer { get; set; } = new Dictionary<string, ScoreDto>();

        public IReadOnlyDictionary<long, string> Labels { get; set; } = new Dictionary<long, string>();
    }

    public sealed class Probe
    {
        public int Id { get; set; }

        public bool Enabled { get; set; }

        public int Reads { get; private set; }

        public string Payload
        {
            get
            {
                Reads++;
                return "payload " + Id;
            }
        }
    }

    public sealed class ProbeDto
    {
        public string Payload { get; set; } = string.Empty;
    }

    public sealed class DictionaryAndConditionTests
    {
        private static Scoreboard SampleBoard() => new Scoreboard
        {
            ByPlayer =
            {
                ["ada"] = new Score { Points = 10 },
                ["grace"] = new Score { Points = 20 },
            },
            Labels =
            {
                [1] = "gold",
                [2] = "silver",
            },
        };

        private static IMapper BoardMapper() => new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Scoreboard, ScoreboardDto>();
            cfg.CreateMap<Score, ScoreDto>();
        }).CreateMapper();

        [Fact]
        public void A_dictionary_of_complex_values_uses_the_nested_map()
        {
            ScoreboardDto dto = BoardMapper().Map<Scoreboard, ScoreboardDto>(SampleBoard());

            dto.ByPlayer.Count.ShouldBe(2);
            dto.ByPlayer["ada"].Points.ShouldBe(10L);
            dto.ByPlayer["grace"].Points.ShouldBe(20L);
        }

        [Fact]
        public void A_dictionary_converts_its_keys_as_well_as_its_values()
        {
            ScoreboardDto dto = BoardMapper().Map<Scoreboard, ScoreboardDto>(SampleBoard());

            dto.Labels.Count.ShouldBe(2);
            dto.Labels[1L].ShouldBe("gold");
            dto.Labels.Keys.ShouldAllBe(k => k.GetType() == typeof(long));
        }

        [Fact]
        public void A_dictionary_is_not_mistaken_for_a_sequence_of_pairs()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Scoreboard, ScoreboardDto>();
                cfg.CreateMap<Score, ScoreDto>();
            });

            config.AssertIsValid();
            config.Model.TypeMaps[0].FindMember(nameof(ScoreboardDto.ByPlayer)).ShouldNotBeNull();
        }

        [Fact]
        public void A_null_dictionary_becomes_an_empty_one_by_default()
        {
            var board = SampleBoard();
            board.ByPlayer = null!;

            BoardMapper().Map<Scoreboard, ScoreboardDto>(board).ByPlayer.ShouldBeEmpty();
        }

        [Fact]
        public void A_null_dictionary_stays_null_when_allowed()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AllowNullCollections = true;
                cfg.CreateMap<Scoreboard, ScoreboardDto>();
                cfg.CreateMap<Score, ScoreDto>();
            });

            var board = SampleBoard();
            board.ByPlayer = null!;

            config.CreateMapper().Map<Scoreboard, ScoreboardDto>(board).ByPlayer.ShouldBeNull();
        }

        [Fact]
        public void A_dictionary_cannot_be_projected()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Scoreboard, ScoreboardDto>();
                cfg.CreateMap<Score, ScoreDto>();
            });

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => new[] { SampleBoard() }.AsQueryable().ProjectTo<ScoreboardDto>(config).ToList());

            error.Message.ShouldContain("cannot build a dictionary");
        }

        [Fact]
        public void A_condition_reads_the_source_even_when_it_does_not_hold()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Probe, ProbeDto>()
                   .ForMember(d => d.Payload, o => o.Condition(s => s.Enabled)));

            var probe = new Probe { Id = 1, Enabled = false };
            ProbeDto dto = config.CreateMapper().Map<Probe, ProbeDto>(probe);

            dto.Payload.ShouldBe(string.Empty);
            probe.Reads.ShouldBe(1);
        }

        [Fact]
        public void A_precondition_skips_the_read_entirely()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Probe, ProbeDto>()
                   .ForMember(d => d.Payload, o => o.PreCondition(s => s.Enabled)));

            var probe = new Probe { Id = 1, Enabled = false };
            ProbeDto dto = config.CreateMapper().Map<Probe, ProbeDto>(probe);

            dto.Payload.ShouldBe(string.Empty);
            probe.Reads.ShouldBe(0);
        }

        [Fact]
        public void A_precondition_that_holds_lets_the_member_through()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Probe, ProbeDto>()
                   .ForMember(d => d.Payload, o => o.PreCondition(s => s.Enabled)));

            var probe = new Probe { Id = 3, Enabled = true };

            config.CreateMapper().Map<Probe, ProbeDto>(probe).Payload.ShouldBe("payload 3");
        }

        [Fact]
        public void A_precondition_runs_before_a_condition()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Probe, ProbeDto>()
                   .ForMember(d => d.Payload, o =>
                   {
                       o.PreCondition(s => s.Enabled);
                       o.Condition(s => s.Id > 10);
                   }));

            var probe = new Probe { Id = 1, Enabled = false };
            config.CreateMapper().Map<Probe, ProbeDto>(probe).Payload.ShouldBe(string.Empty);

            probe.Reads.ShouldBe(0);
        }

        [Fact]
        public void A_precondition_is_honoured_in_a_projection()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Probe, ProbeDto>()
                   .ForMember(d => d.Payload, o => o.PreCondition(s => s.Enabled)));

            List<ProbeDto> probes = new[] { new Probe { Id = 1, Enabled = false }, new Probe { Id = 2, Enabled = true } }
                .AsQueryable()
                .ProjectTo<ProbeDto>(config)
                .ToList();

            probes[0].Payload.ShouldBeNull();
            probes[1].Payload.ShouldBe("payload 2");
        }

        [Fact]
        public void Null_preconditions_are_rejected()
        {
            Should.Throw<ArgumentNullException>(() => new MapperConfiguration(cfg =>
                cfg.CreateMap<Probe, ProbeDto>()
                   .ForMember(d => d.Payload, o => o.PreCondition(null!))));
        }
    }
}

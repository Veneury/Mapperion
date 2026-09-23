using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Projection
{
    public sealed class Tag
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class TagDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class Note
    {
        public int Id { get; set; }

        public string Body { get; set; } = string.Empty;

        public Tag? Tag { get; set; }

        public Note? Parent { get; set; }
    }

    public sealed class NoteDto
    {
        public long Id { get; set; }

        public string Body { get; set; } = string.Empty;

        public TagDto? Tag { get; set; }

        public string Unmapped { get; set; } = string.Empty;
    }

    public sealed class RecursiveNoteDto
    {
        public string Body { get; set; } = string.Empty;

        public RecursiveNoteDto? Parent { get; set; }
    }

    public sealed class UpperConverter : IValueConverter<string, string>
    {
        public string Convert(string sourceMember, ResolutionContext context) => sourceMember.ToUpperInvariant();
    }

    public sealed class NoteConverter : ITypeConverter<Note, NoteDto>
    {
        public NoteDto Convert(Note source, NoteDto destination, ResolutionContext context) => new NoteDto();
    }

    public sealed class ProjectionTests
    {
        private static IQueryable<Note> Notes() => new[]
        {
            new Note { Id = 1, Body = "first", Tag = new Tag { Name = "red" } },
            new Note { Id = 2, Body = "second" },
        }.AsQueryable();

        [Fact]
        public void A_nested_object_is_expanded_and_a_null_one_stays_null()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Note, NoteDto>().ForMember(d => d.Unmapped, o => o.Ignore());
                cfg.CreateMap<Tag, TagDto>();
            });

            List<NoteDto> notes = Notes().ProjectTo<NoteDto>(config).ToList();

            notes[0].Tag!.Name.ShouldBe("red");
            notes[1].Tag.ShouldBeNull();
        }

        [Fact]
        public void A_widening_conversion_is_applied()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Note, NoteDto>().ForMember(d => d.Unmapped, o => o.Ignore());
                cfg.CreateMap<Tag, TagDto>();
            });

            Notes().ProjectTo<NoteDto>(config).First().Id.ShouldBe(1L);
        }

        [Fact]
        public void A_member_with_no_source_is_left_alone()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Note, NoteDto>();
                cfg.CreateMap<Tag, TagDto>();
            });

            Notes().ProjectTo<NoteDto>(config).First().Unmapped.ShouldBe(string.Empty);
        }

        [Fact]
        public void A_condition_becomes_a_conditional_value()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Note, NoteDto>()
                   .ForMember(d => d.Unmapped, o => o.Ignore())
                   .ForMember(d => d.Body, o => o.Condition(s => s.Id > 1));
                cfg.CreateMap<Tag, TagDto>();
            });

            List<NoteDto> notes = Notes().ProjectTo<NoteDto>(config).ToList();

            notes[0].Body.ShouldBeNull();
            notes[1].Body.ShouldBe("second");
        }

        [Fact]
        public void A_self_referencing_map_is_reported_instead_of_looping()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Note, RecursiveNoteDto>());

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => Notes().ProjectTo<RecursiveNoteDto>(config).ToList());

            error.Message.ShouldContain("refers to itself");
        }

        [Fact]
        public void A_self_referencing_map_projects_once_the_cycle_is_ignored()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Note, RecursiveNoteDto>().ForMember(d => d.Parent, o => o.Ignore()));

            Notes().ProjectTo<RecursiveNoteDto>(config).First().Body.ShouldBe("first");
        }

        [Fact]
        public void A_type_converter_is_reported()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Note, NoteDto>().ConvertUsing<NoteConverter>());

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => Notes().ProjectTo<NoteDto>(config).ToList());

            error.Message.ShouldContain("type converter");
        }

        [Fact]
        public void A_value_converter_is_reported()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Note, NoteDto>()
                   .ForMember(d => d.Unmapped, o => o.Ignore())
                   .ForMember(d => d.Body, o => o.ConvertUsing<UpperConverter, string>());
                cfg.CreateMap<Tag, TagDto>();
            });

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => Notes().ProjectTo<NoteDto>(config).ToList());

            error.Message.ShouldContain("value converter");
        }

        [Fact]
        public void A_missing_nested_map_is_reported()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Note, NoteDto>().ForMember(d => d.Unmapped, o => o.Ignore()));

            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => Notes().ProjectTo<NoteDto>(config).ToList());

            error.Message.ShouldContain("cannot convert");
        }

        [Fact]
        public void An_unconfigured_pair_is_reported()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Tag, TagDto>());

            Should.Throw<MappingException>(() => Notes().ProjectTo<NoteDto>(config).ToList());
        }

        [Fact]
        public void The_projection_is_built_once_and_reused()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Note, NoteDto>().ForMember(d => d.Unmapped, o => o.Ignore());
                cfg.CreateMap<Tag, TagDto>();
            });

            IMapper mapper = config.CreateMapper();

            for (int i = 0; i < 20; i++)
            {
                mapper.ProjectTo<NoteDto>(Notes()).Count().ShouldBe(2);
            }
        }

        [Fact]
        public void Null_arguments_are_rejected()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Tag, TagDto>());

            Should.Throw<ArgumentNullException>(() => ((IQueryable)null!).ProjectTo<TagDto>(config));
            Should.Throw<ArgumentNullException>(() => Notes().ProjectTo<TagDto>(null!));
            Should.Throw<ArgumentNullException>(() => config.CreateMapper().ProjectTo<TagDto>(null!));
        }
    }
}

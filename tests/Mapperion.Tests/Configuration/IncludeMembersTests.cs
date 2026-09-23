using Mapperion.Model;
using System.Linq;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Configuration
{
    public sealed class Application
    {
        public string Reference { get; set; } = string.Empty;

        public Applicant Applicant { get; set; } = new Applicant();

        public Employment? Employment { get; set; }
    }

    public sealed class Applicant
    {
        public string FullName { get; set; } = string.Empty;

        public int Age { get; set; }

        public string Notes { get; set; } = string.Empty;
    }

    public sealed class Employment
    {
        public string Employer { get; set; } = string.Empty;

        public decimal Salary { get; set; }
    }

    public sealed class ApplicationDto
    {
        public string Reference { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public int Age { get; set; }

        public string Employer { get; set; } = string.Empty;

        public decimal Salary { get; set; }
    }

    public sealed class ShoutingConverter : IValueConverter<string, string>
    {
        public string Convert(string source, ResolutionContext context) => source.ToUpperInvariant();
    }

    public sealed class AgeResolver : IValueResolver<Applicant, ApplicationDto, int>
    {
        public int Resolve(Applicant source, ApplicationDto destination, int member, ResolutionContext context)
        {
            return source.Age + 1;
        }
    }

    public sealed class IncludeMembersTests
    {
        private static MapperConfiguration Applications() => new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Application, ApplicationDto>()
               .IncludeMembers(s => s.Applicant, s => s.Employment!);

            cfg.CreateMap<Applicant, ApplicationDto>()
               .ValidateMemberList(MemberListValidation.None);

            cfg.CreateMap<Employment, ApplicationDto>()
               .ValidateMemberList(MemberListValidation.None);
        });

        [Fact]
        public void Members_come_from_the_included_objects()
        {
            ApplicationDto dto = Applications().CreateMapper().Map<Application, ApplicationDto>(
                new Application
                {
                    Reference = "A-1",
                    Applicant = new Applicant { FullName = "Ana", Age = 30 },
                    Employment = new Employment { Employer = "Acme", Salary = 1000m },
                });

            dto.Reference.ShouldBe("A-1");
            dto.FullName.ShouldBe("Ana");
            dto.Age.ShouldBe(30);
            dto.Employer.ShouldBe("Acme");
            dto.Salary.ShouldBe(1000m);
        }

        /// <remarks>
        /// The default is written, not skipped, which is the same thing a flattened path does when
        /// something along it is null. Consistency with the rest of the library matters more here
        /// than leaving whatever the destination happened to arrive with.
        /// </remarks>
        [Fact]
        public void A_null_included_member_yields_the_default_for_what_it_would_have_filled()
        {
            ApplicationDto dto = Applications().CreateMapper().Map<Application, ApplicationDto>(
                new Application
                {
                    Reference = "A-2",
                    Applicant = new Applicant { FullName = "Leo", Age = 20 },
                    Employment = null,
                });

            dto.FullName.ShouldBe("Leo");
            dto.Employer.ShouldBeNull();
            dto.Salary.ShouldBe(0m);
        }

        [Fact]
        public void An_included_map_is_not_needed_when_the_names_line_up()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Application, ApplicationDto>()
                   .IncludeMembers(s => s.Applicant, s => s.Employment!));

            ApplicationDto dto = config.CreateMapper().Map<Application, ApplicationDto>(
                new Application
                {
                    Applicant = new Applicant { FullName = "Ana", Age = 30 },
                    Employment = new Employment { Employer = "Acme", Salary = 1000m },
                });

            dto.FullName.ShouldBe("Ana");
            dto.Employer.ShouldBe("Acme");
        }

        [Fact]
        public void What_the_outer_map_resolves_itself_wins()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Application, ApplicationDto>()
                   .ForMember(d => d.FullName, o => o.MapFrom(s => "outer"))
                   .IncludeMembers(s => s.Applicant)
                   .ForMember(d => d.Employer, o => o.Ignore())
                   .ForMember(d => d.Salary, o => o.Ignore());

                cfg.CreateMap<Applicant, ApplicationDto>()
                   .ValidateMemberList(MemberListValidation.None);
            });

            ApplicationDto dto = config.CreateMapper().Map<Application, ApplicationDto>(
                new Application { Applicant = new Applicant { FullName = "Ana" } });

            dto.FullName.ShouldBe("outer");
        }

        [Fact]
        public void The_first_included_member_that_has_something_to_say_provides_it()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Application, ApplicationDto>()
                   .IncludeMembers(s => s.Employment!, s => s.Applicant)
                   .ForMember(d => d.FullName, o => o.Ignore())
                   .ForMember(d => d.Age, o => o.Ignore());

                cfg.CreateMap<Employment, ApplicationDto>()
                   .ForMember(d => d.Employer, o => o.MapFrom(s => "from employment"))
                   .ValidateMemberList(MemberListValidation.None);

                cfg.CreateMap<Applicant, ApplicationDto>()
                   .ForMember(d => d.Employer, o => o.MapFrom(s => "from applicant"))
                   .ValidateMemberList(MemberListValidation.None);
            });

            ApplicationDto dto = config.CreateMapper().Map<Application, ApplicationDto>(
                new Application { Employment = new Employment() });

            dto.Employer.ShouldBe("from employment");
        }

        [Fact]
        public void A_member_the_included_map_ignores_is_offered_to_the_next_one()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Application, ApplicationDto>()
                   .IncludeMembers(s => s.Employment!, s => s.Applicant)
                   .ForMember(d => d.Age, o => o.Ignore())
                   .ForMember(d => d.Salary, o => o.Ignore());

                cfg.CreateMap<Employment, ApplicationDto>()
                   .ForMember(d => d.FullName, o => o.Ignore())
                   .ValidateMemberList(MemberListValidation.None);

                cfg.CreateMap<Applicant, ApplicationDto>()
                   .ForMember(d => d.FullName, o => o.MapFrom(s => s.FullName))
                   .ValidateMemberList(MemberListValidation.None);
            });

            ApplicationDto dto = config.CreateMapper().Map<Application, ApplicationDto>(
                new Application
                {
                    Applicant = new Applicant { FullName = "Ana" },
                    Employment = new Employment(),
                });

            dto.FullName.ShouldBe("Ana");
        }

        [Fact]
        public void A_rename_on_the_included_map_carries_over()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Application, ApplicationDto>()
                   .IncludeMembers(s => s.Applicant)
                   .ForMember(d => d.Employer, o => o.Ignore())
                   .ForMember(d => d.Salary, o => o.Ignore());

                cfg.CreateMap<Applicant, ApplicationDto>()
                   .ForMember(d => d.FullName, o => o.MapFrom(s => s.Notes))
                   .ValidateMemberList(MemberListValidation.None);
            });

            ApplicationDto dto = config.CreateMapper().Map<Application, ApplicationDto>(
                new Application { Applicant = new Applicant { FullName = "Ana", Notes = "renamed" } });

            dto.FullName.ShouldBe("renamed");
        }

        [Fact]
        public void A_converter_on_the_included_map_carries_over()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Application, ApplicationDto>()
                   .IncludeMembers(s => s.Applicant)
                   .ForMember(d => d.Employer, o => o.Ignore())
                   .ForMember(d => d.Salary, o => o.Ignore());

                cfg.CreateMap<Applicant, ApplicationDto>()
                   .ForMember(d => d.FullName, o => o.ConvertUsing<ShoutingConverter, string>())
                   .ValidateMemberList(MemberListValidation.None);
            });

            ApplicationDto dto = config.CreateMapper().Map<Application, ApplicationDto>(
                new Application { Applicant = new Applicant { FullName = "ana" } });

            dto.FullName.ShouldBe("ANA");
        }

        [Fact]
        public void A_resolver_on_the_included_map_is_given_the_included_instance()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Application, ApplicationDto>()
                   .IncludeMembers(s => s.Applicant)
                   .ForMember(d => d.Employer, o => o.Ignore())
                   .ForMember(d => d.Salary, o => o.Ignore());

                cfg.CreateMap<Applicant, ApplicationDto>()
                   .ForMember(d => d.Age, o => o.MapFrom<AgeResolver>())
                   .ValidateMemberList(MemberListValidation.None);
            });

            ApplicationDto dto = config.CreateMapper().Map<Application, ApplicationDto>(
                new Application { Applicant = new Applicant { FullName = "Ana", Age = 30 } });

            dto.Age.ShouldBe(31);
        }

        [Fact]
        public void A_condition_on_the_included_map_is_reported_rather_than_dropped()
        {
            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => new MapperConfiguration(cfg =>
                {
                    cfg.CreateMap<Application, ApplicationDto>()
                       .IncludeMembers(s => s.Applicant)
                       .ForMember(d => d.Employer, o => o.Ignore())
                       .ForMember(d => d.Salary, o => o.Ignore());

                    cfg.CreateMap<Applicant, ApplicationDto>()
                       .ForMember(d => d.FullName, o =>
                       {
                           o.MapFrom(s => s.FullName);
                           o.Condition(s => s.Age > 18);
                       })
                       .ValidateMemberList(MemberListValidation.None);
                }));

            error.Message.ShouldContain("condition");
        }

        [Fact]
        public void An_included_member_that_is_not_a_member_access_is_refused()
        {
            Should.Throw<MapperConfigurationException>(
                () => new MapperConfiguration(cfg =>
                    cfg.CreateMap<Application, ApplicationDto>()
                       .IncludeMembers(s => s.Reference + "x")));
        }

        [Fact]
        public void The_configuration_validates()
        {
            Should.NotThrow(Applications().AssertIsValid);
        }

        [Fact]
        public void A_projection_reaches_through_the_included_members()
        {
            var applications = new[]
            {
                new Application
                {
                    Reference = "A-1",
                    Applicant = new Applicant { FullName = "Ana", Age = 30 },
                    Employment = new Employment { Employer = "Acme", Salary = 1000m },
                },
            };

            ApplicationDto dto = applications
                .AsQueryable()
                .ProjectTo<ApplicationDto>(Applications())
                .Single();

            dto.FullName.ShouldBe("Ana");
            dto.Employer.ShouldBe("Acme");
        }
    }
}

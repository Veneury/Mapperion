using System;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Configuration
{
    public sealed class Ledger
    {
        public string Owner { get; set; } = string.Empty;

        public int Balance { get; set; }

        public Office Office { get; set; } = new Office();

        public string Unread { get; set; } = string.Empty;
    }

    public sealed class Office
    {
        public string Code { get; set; } = string.Empty;
    }

    public sealed class LedgerDto
    {
        public string Owner { get; set; } = string.Empty;

        public int Balance { get; set; }

        public string OfficeCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// The overloads that name a member as text, and the one that speaks about the source. They
    /// exist because the library this one is a drop-in for has them, and a configuration that
    /// chooses a member somewhere a lambda cannot reach had to be rewritten by hand without them.
    /// </summary>
    public sealed class NamedMemberTests
    {
        [Fact]
        public void ForMember_takes_the_destination_member_by_name()
        {
            var config = new MapperConfiguration(cfg => cfg
                .CreateMap<Ledger, LedgerDto>()
                .ForMember("Owner", o => o.MapFrom(s => s.Owner.ToUpperInvariant())));

            LedgerDto result = config.CreateMapper()
                .Map<Ledger, LedgerDto>(new Ledger { Owner = "ada" });

            result.Owner.ShouldBe("ADA");
        }

        [Fact]
        public void MapFrom_takes_the_source_member_by_name()
        {
            var config = new MapperConfiguration(cfg => cfg
                .CreateMap<Ledger, LedgerDto>()
                .ForMember(d => d.Owner, o => o.MapFrom("Unread")));

            LedgerDto result = config.CreateMapper()
                .Map<Ledger, LedgerDto>(new Ledger { Owner = "ada", Unread = "borrowed" });

            result.Owner.ShouldBe("borrowed");
        }

        /// <remarks>A dotted name walks a path, the same shape the expression overloads accept.</remarks>
        [Fact]
        public void A_dotted_name_walks_a_path()
        {
            var config = new MapperConfiguration(cfg => cfg
                .CreateMap<Ledger, LedgerDto>()
                .ForMember("OfficeCode", o => o.MapFrom("Office.Code")));

            LedgerDto result = config.CreateMapper()
                .Map<Ledger, LedgerDto>(new Ledger { Office = new Office { Code = "B7" } });

            result.OfficeCode.ShouldBe("B7");
        }

        /// <remarks>
        /// The name is resolved while the configuration is built, so a misspelling is a
        /// configuration error rather than a member that quietly maps to nothing. That is the
        /// whole reason to prefer the expression overloads where there is a choice.
        /// </remarks>
        [Fact]
        public void A_name_that_matches_nothing_is_a_configuration_error()
        {
            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => new MapperConfiguration(cfg => cfg
                    .CreateMap<Ledger, LedgerDto>()
                    .ForMember("Nonexistent", o => o.Ignore())));

            error.Message.ShouldContain("Nonexistent");
        }

        [Fact]
        public void A_source_name_that_matches_nothing_is_a_configuration_error()
        {
            MapperConfigurationException error = Should.Throw<MapperConfigurationException>(
                () => new MapperConfiguration(cfg => cfg
                    .CreateMap<Ledger, LedgerDto>()
                    .ForMember(d => d.Owner, o => o.MapFrom("Nonexistent"))));

            error.Message.ShouldContain("Nonexistent");
        }

        [Fact]
        public void ForAllMembers_reaches_every_destination_member()
        {
            var config = new MapperConfiguration(cfg => cfg
                .CreateMap<Ledger, LedgerDto>()
                .ForAllMembers(o => o.Ignore()));

            LedgerDto result = config.CreateMapper()
                .Map<Ledger, LedgerDto>(new Ledger { Owner = "ada", Balance = 5 });

            result.Owner.ShouldBe(string.Empty);
            result.Balance.ShouldBe(0);
            result.OfficeCode.ShouldBe(string.Empty);
        }

        /// <remarks>
        /// Validation against the source list reports every readable member no destination member
        /// reads. This is how one of them is excused, and it is the only thing there is to say
        /// about a source member.
        /// </remarks>
        [Fact]
        public void ForSourceMember_excuses_a_member_from_source_validation()
        {
            var config = new MapperConfiguration(cfg => cfg
                .CreateMap<Ledger, LedgerDto>()
                .ValidateMemberList(Mapperion.Model.MemberListValidation.Source)
                .ForMember(d => d.OfficeCode, o => o.MapFrom(s => s.Office.Code))
                .ForSourceMember(s => s.Unread, o => o.DoNotValidate()));

            Should.NotThrow(() => config.AssertConfigurationIsValid());
        }

        [Fact]
        public void An_unread_source_member_is_still_reported_without_it()
        {
            var config = new MapperConfiguration(cfg => cfg
                .CreateMap<Ledger, LedgerDto>()
                .ValidateMemberList(Mapperion.Model.MemberListValidation.Source)
                .ForMember(d => d.OfficeCode, o => o.MapFrom(s => s.Office.Code)));

            MapperConfigurationException error =
                Should.Throw<MapperConfigurationException>(() => config.AssertConfigurationIsValid());

            error.Message.ShouldContain("Unread");
        }
    }
}

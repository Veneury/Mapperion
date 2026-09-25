using System;
using Mapperion.Model;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Execution
{
    public enum Grade
    {
        Low = 1,
        Mid = 2,
        High = 3,
    }

    /// <summary>The same names with the numbers turned round, so the two policies disagree.</summary>
    public enum GradeDto
    {
        High = 1,
        Mid = 2,
        Low = 3,
    }

    public enum Tier
    {
        Low = 1,
        Odd = 7,
    }

    public enum TierDto
    {
        Low = 5,
    }

    [Flags]
    public enum Access
    {
        None = 0,
        Read = 1,
        Write = 2,
    }

    [Flags]
    public enum AccessDto
    {
        None = 0,
        Read = 4,
        Write = 8,
    }

    public enum Twin
    {
        Segundo = 9,
    }

    /// <remarks>
    /// Two members on one number. Declared with the analyser told to allow it, because the point
    /// is what happens when someone else's enum is shaped like this.
    /// </remarks>
#pragma warning disable CA1069
    public enum TwinDto
    {
        First = 1,
        Segundo = 1,
    }
#pragma warning restore CA1069

    public sealed class TwinHolder
    {
        public Twin Twin { get; set; }
    }

    public sealed class TwinHolderDto
    {
        public TwinDto Twin { get; set; }
    }

    public sealed class Report
    {
        public Grade Grade { get; set; }

        public Tier Tier { get; set; }

        public Access Access { get; set; }

        public Grade? Optional { get; set; }
    }

    public sealed class ReportDto
    {
        public GradeDto Grade { get; set; }

        public TierDto Tier { get; set; }

        public AccessDto Access { get; set; }

        public GradeDto? Optional { get; set; }
    }

    /// <summary>
    /// The name correspondence is settled while the plan is compiled rather than on every call, so
    /// these pin the answers it has to keep giving, including the awkward ones.
    /// </summary>
    public sealed class EnumMappingTests
    {
        private static IMapper Mapper(EnumMappingPolicy policy) => new MapperConfiguration(cfg =>
        {
            cfg.EnumMapping = policy;
            cfg.CreateMap<Report, ReportDto>();
        }).CreateMapper();

        private static ReportDto Map(EnumMappingPolicy policy, Report source) =>
            Mapper(policy).Map<Report, ReportDto>(source);

        /// <summary>
        /// Every member set to something that crosses, so a test about one of them is not answered
        /// by another. Under <see cref="EnumMappingPolicy.ByName"/> a zero that nobody declared is
        /// itself an error, and the default of an enum is a zero.
        /// </summary>
        private static Report Valid() => new Report
        {
            Grade = Grade.High,
            Tier = Tier.Low,
            Access = Access.Read,
        };

        [Fact]
        public void A_name_on_both_sides_wins_over_the_number()
        {
            ReportDto dto = Map(EnumMappingPolicy.ByNameThenValue, new Report { Grade = Grade.High });

            dto.Grade.ShouldBe(GradeDto.High);
            ((int)dto.Grade).ShouldBe(1);
        }

        [Fact]
        public void By_value_ignores_the_name()
        {
            ReportDto dto = Map(EnumMappingPolicy.ByValue, new Report { Grade = Grade.High });

            ((int)dto.Grade).ShouldBe(3);
            dto.Grade.ShouldBe(GradeDto.Low);
        }

        /// <remarks>
        /// <c>Odd</c> has no counterpart, so the number is what is left to go on.
        /// </remarks>
        [Fact]
        public void A_name_the_destination_lacks_falls_back_to_the_number()
        {
            ReportDto dto = Map(EnumMappingPolicy.ByNameThenValue, new Report { Tier = Tier.Odd });

            ((int)dto.Tier).ShouldBe(7);
        }

        [Fact]
        public void The_same_name_missing_under_by_name_is_an_error_that_says_which()
        {
            MappingException error = Should.Throw<MappingException>(
                () =>
                {
                    Report source = Valid();
                    source.Tier = Tier.Odd;
                    Map(EnumMappingPolicy.ByName, source);
                });

            error.ToString().ShouldContain("Odd");
            error.ToString().ShouldContain("TierDto");
        }

        /// <remarks>
        /// A value nobody declared is not a name, so it can only be carried across as a number.
        /// </remarks>
        [Fact]
        public void A_value_outside_the_declared_ones_is_carried_as_a_number()
        {
            ReportDto dto = Map(EnumMappingPolicy.ByNameThenValue, new Report { Grade = (Grade)99 });

            ((int)dto.Grade).ShouldBe(99);
        }

        [Fact]
        public void A_value_outside_the_declared_ones_under_by_name_is_an_error()
        {
            Should.Throw<MappingException>(
                () =>
                {
                    Report source = Valid();
                    source.Grade = (Grade)99;
                    Map(EnumMappingPolicy.ByName, source);
                });
        }

        /// <remarks>
        /// Two flags together are not one of the declared values, and they still cross by name:
        /// the pair prints as two names and reads back as two names.
        /// </remarks>
        [Fact]
        public void A_combination_of_flags_crosses_by_name()
        {
            ReportDto dto = Map(
                EnumMappingPolicy.ByNameThenValue,
                new Report { Access = Access.Read | Access.Write });

            dto.Access.ShouldBe(AccessDto.Read | AccessDto.Write);
            ((int)dto.Access).ShouldBe(12);
        }

        [Fact]
        public void A_single_flag_crosses_by_name_too()
        {
            Map(EnumMappingPolicy.ByNameThenValue, new Report { Access = Access.Write })
                .Access.ShouldBe(AccessDto.Write);
        }

        /// <remarks>
        /// <c>TwinDto.Segundo</c> shares its number with <c>First</c>, and only one of the two is
        /// what that number prints as. The one that does not is no more a name match than a
        /// stranger, so the number is used instead.
        /// </remarks>
        [Fact]
        public void A_destination_name_that_is_not_what_its_number_prints_as_is_not_a_match()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.EnumMapping = EnumMappingPolicy.ByNameThenValue;
                cfg.CreateMap<TwinHolder, TwinHolderDto>();
            });

            TwinHolderDto dto = configuration.CreateMapper()
                .Map<TwinHolder, TwinHolderDto>(new TwinHolder { Twin = Twin.Segundo });

            ((int)dto.Twin).ShouldBe(9);
        }

        [Fact]
        public void A_nullable_enum_carries_its_value_across()
        {
            Map(EnumMappingPolicy.ByNameThenValue, new Report { Optional = Grade.High })
                .Optional.ShouldBe(GradeDto.High);
        }

        [Fact]
        public void A_null_nullable_enum_stays_null()
        {
            Map(EnumMappingPolicy.ByNameThenValue, new Report { Optional = null })
                .Optional.ShouldBeNull();
        }
    }
}

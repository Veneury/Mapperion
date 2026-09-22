using Mapperion.Model;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Configuration
{
    public sealed class Account
    {
        public string Holder { get; set; } = string.Empty;

        public decimal Balance { get; set; }

        public Branch? Branch { get; set; }

        public string Computed => Holder + "!";
    }

    public sealed class Branch
    {
        public string Code { get; set; } = string.Empty;
    }

    public sealed class AccountDto
    {
        public string Owner { get; set; } = string.Empty;

        public decimal Balance { get; set; }

        public string BranchCode { get; set; } = string.Empty;

        public string Note { get; set; } = string.Empty;
    }

    public sealed class AccountProfile : Profile
    {
        public AccountProfile()
        {
            CreateMap<Account, AccountDto>()
                .ForMember(d => d.Owner, o => o.MapFrom(s => s.Holder))
                .ForMember(d => d.Note, o => o.Ignore())
                .ReverseMap();
        }
    }

    public sealed class ReverseMapTests
    {
        private static MapperConfiguration Configured() => new MapperConfiguration(cfg =>
            cfg.CreateMap<Account, AccountDto>()
               .ForMember(d => d.Owner, o => o.MapFrom(s => s.Holder))
               .ForMember(d => d.Note, o => o.Ignore())
               .ReverseMap());

        private static string? PathOf(TypeMapDefinition map, string destinationMember)
        {
            return (map.FindMember(destinationMember)?.Source as MemberPathSource)?.Path.ToString();
        }

        [Fact]
        public void The_reverse_pair_is_declared()
        {
            MapperModel model = Configured().Model;

            model.Count.ShouldBe(2);
            model.Contains(new TypeMapKey(typeof(AccountDto), typeof(Account))).ShouldBeTrue();
        }

        [Fact]
        public void The_reverse_map_is_marked_as_such()
        {
            Configured().Model.TypeMaps[0].IsReverse.ShouldBeFalse();
            Configured().Model.TypeMaps[1].IsReverse.ShouldBeTrue();
        }

        [Fact]
        public void A_renamed_member_is_inverted()
        {
            TypeMapDefinition reverse = Configured().Model.TypeMaps[1];

            PathOf(reverse, nameof(Account.Holder)).ShouldBe("Owner");
            reverse.FindMember(nameof(Account.Holder))!.IsExplicit.ShouldBeTrue();
        }

        [Fact]
        public void Members_that_were_not_configured_resolve_by_convention()
        {
            PathOf(Configured().Model.TypeMaps[1], nameof(Account.Balance)).ShouldBe("Balance");
        }

        [Fact]
        public void A_flattened_path_is_not_inverted()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Account, AccountDto>()
                   .ForMember(d => d.BranchCode, o => o.MapFrom(s => s.Branch!.Code))
                   .ReverseMap());

            config.Model.TypeMaps[1].FindMember(nameof(Account.Branch))!.Source.ShouldBeNull();
        }

        [Fact]
        public void An_ignored_member_is_not_inverted()
        {
            TypeMapDefinition reverse = Configured().Model.TypeMaps[1];

            reverse.FindMember(nameof(Account.Holder))!.IsIgnored.ShouldBeFalse();
        }

        [Fact]
        public void A_read_only_source_member_is_not_written_back()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Account, AccountDto>()
                   .ForMember(d => d.Owner, o => o.MapFrom(s => s.Computed))
                   .ReverseMap());

            config.Model.TypeMaps[1].FindMember("Computed").ShouldBeNull();
        }

        [Fact]
        public void The_returned_expression_configures_the_reverse()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Account, AccountDto>()
                   .ForMember(d => d.Owner, o => o.MapFrom(s => s.Holder))
                   .ReverseMap()
                   .ForMember(d => d.Balance, o => o.Ignore()));

            config.Model.TypeMaps[1].FindMember(nameof(Account.Balance))!.IsIgnored.ShouldBeTrue();
        }

        [Fact]
        public void ReverseMap_works_inside_a_profile()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<AccountProfile>());

            config.Model.Count.ShouldBe(2);
            PathOf(config.Model.TypeMaps[1], nameof(Account.Holder)).ShouldBe("Owner");
        }

        [Fact]
        public void Declaring_the_reverse_pair_twice_is_rejected()
        {
            Should.Throw<MapperConfigurationException>(() => new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<AccountDto, Account>();
                cfg.CreateMap<Account, AccountDto>().ReverseMap();
            }));
        }

        [Fact]
        public void A_round_trip_keeps_the_renamed_member()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.CreateMap<Account, AccountDto>()
                   .ForMember(d => d.Owner, o => o.MapFrom(s => s.Holder))
                   .ForMember(d => d.Note, o => o.Ignore())
                   .ForMember(d => d.BranchCode, o => o.MapFrom(s => s.Branch!.Code))
                   .ReverseMap()
                   .ForMember(d => d.Branch, o => o.Ignore()));

            IMapper mapper = config.CreateMapper();

            var account = new Account
            {
                Holder = "Ada",
                Balance = 10m,
                Branch = new Branch { Code = "B1" },
            };

            AccountDto dto = mapper.Map<Account, AccountDto>(account);
            dto.Owner.ShouldBe("Ada");
            dto.BranchCode.ShouldBe("B1");

            Account back = mapper.Map<AccountDto, Account>(dto);
            back.Holder.ShouldBe("Ada");
            back.Balance.ShouldBe(10m);
        }
    }
}

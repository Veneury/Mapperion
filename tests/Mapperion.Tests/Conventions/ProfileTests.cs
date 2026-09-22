using System.Reflection;
using Mapperion.Model;
using Shouldly;
using Xunit;

namespace Mapperion.Tests.Conventions
{
    public sealed class ProfileTests
    {
        [Fact]
        public void A_profile_can_be_registered_by_type()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<PersonProfile>());

            config.Model.Contains(new TypeMapKey(typeof(Person), typeof(PersonDto))).ShouldBeTrue();
        }

        [Fact]
        public void A_profile_can_be_registered_by_instance()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile(new BadgeProfile()));

            config.Model.Contains(new TypeMapKey(typeof(Person), typeof(BadgeDto))).ShouldBeTrue();
        }

        [Fact]
        public void Maps_declared_in_a_profile_go_through_the_conventions()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<PersonProfile>());

            TypeMapDefinition map = config.Model.TypeMaps[0];
            var source = map.FindMember(nameof(PersonDto.ContactEmail))!.Source.ShouldBeOfType<MemberPathSource>();
            source.Path.ToString().ShouldBe("Contact.Email");
            map.FindMember(nameof(PersonDto.Unmatched))!.IsIgnored.ShouldBeTrue();
        }

        [Fact]
        public void Scanning_an_assembly_registers_every_profile_in_it()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfiles(Assembly.GetExecutingAssembly()));

            config.Model.Contains(new TypeMapKey(typeof(Person), typeof(PersonDto))).ShouldBeTrue();
            config.Model.Contains(new TypeMapKey(typeof(Person), typeof(BadgeDto))).ShouldBeTrue();
        }

        [Fact]
        public void A_pair_declared_twice_across_sources_is_rejected()
        {
            Should.Throw<MapperConfigurationException>(() => new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Person, PersonDto>();
                cfg.AddProfile<PersonProfile>();
            }));
        }

        [Fact]
        public void Null_profiles_are_rejected()
        {
            Should.Throw<System.ArgumentNullException>(() =>
                new MapperConfiguration(cfg => cfg.AddProfile(null!)));
        }
    }
}
